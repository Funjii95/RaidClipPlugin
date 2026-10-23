using System.Security.Cryptography;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public enum HeistState { Inactive, Joining, Evaluating, Successful, Failed, Cancelled }
public sealed record HeistParticipant(string UserId,string Login,string DisplayName);
public sealed record HeistStatus(HeistState State,string Creator,int ParticipantCount,int SecondsRemaining,
    long Jackpot,int SuccessChancePercent,IReadOnlyList<string> Participants,bool TestMode=false);

public interface IHeistTwitchClient
{
    Task SendChatMessageAsync(string broadcasterId, string senderId, string message, CancellationToken cancellationToken);
    Task<bool> IsFollowerAsync(string broadcasterId, string userId, CancellationToken cancellationToken);
}

public interface IHeistRandom
{
    int NextInclusive(int minimum,int maximum);
}

public sealed class CryptoHeistRandom : IHeistRandom
{
    public int NextInclusive(int minimum,int maximum) => RandomNumberGenerator.GetInt32(minimum,checked(maximum+1));
}

public static class HeistRules
{
    public static bool IsSuccess(int chancePercent,int roll) => roll >= 1 && roll <= 100 && roll <= Math.Clamp(chancePercent,0,100);
    public static long[] CalculatePayouts(long jackpot,int participantCount,IReadOnlyCollection<int> extraRecipients)
    {
        if(participantCount<1)throw new ArgumentOutOfRangeException(nameof(participantCount));
        if(jackpot<0)throw new ArgumentOutOfRangeException(nameof(jackpot));
        var remainder=jackpot%participantCount; var extras=extraRecipients.Distinct().ToHashSet();
        if(extras.Count!=remainder||extras.Any(x=>x<0||x>=participantCount))throw new ArgumentException("Ungültige Restpunktverteilung.",nameof(extraRecipients));
        var share=jackpot/participantCount; return Enumerable.Range(0,participantCount).Select(i=>share+(extras.Contains(i)?1L:0L)).ToArray();
    }
}

public sealed partial class HeistService : IAsyncDisposable
{
    private readonly string _broadcasterId;
    private readonly string _chatUserId;
    private readonly IHeistTwitchClient _twitch;
    private readonly ViewerPointStore _points;
    private readonly IHeistRandom _random;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly Dictionary<string,DateTimeOffset> _creatorCooldowns=new(StringComparer.Ordinal);
    private HeistConfig _config;
    private MinigameConfig _minigame;
    private Dictionary<string,HeistParticipant>? _participants;
    private CancellationTokenSource? _sessionCts;
    private Task? _sessionTask;
    private DateTimeOffset _lastGlobalCompletion=DateTimeOffset.MinValue;
    private string _creator="";
    private int _remaining;
    private bool _disposed;
    private string _eventId="";
    private DateTimeOffset _startedAt,_joinEndsAt;
    private HeistEventSnapshot? _snapshot;
    public HeistEventSnapshot? Snapshot => Volatile.Read(ref _snapshot);
    public event Action<HeistEventSnapshot>? EventChanged;

    public event Action<HeistStatus>? StatusChanged;
    public HeistState State { get; private set; }=HeistState.Inactive;

    public HeistService(string broadcasterId,string chatUserId,HeistConfig config,
        MinigameConfig minigame,IHeistTwitchClient twitch,ViewerPointStore points,IHeistRandom? random=null)
    {
        _broadcasterId=broadcasterId; _chatUserId=chatUserId; _config=config;
        _minigame=minigame; _twitch=twitch; _points=points; _random=random??new CryptoHeistRandom();
    }

    public void UpdateConfig(HeistConfig config,MinigameConfig minigame)
    { _config=config; _minigame=minigame; }

    public static string FormatCooldown(TimeSpan remaining)
    {
        var totalSeconds=Math.Max(0,(int)Math.Ceiling(remaining.TotalSeconds));
        var minutes=totalSeconds/60;
        var seconds=totalSeconds%60;
        return minutes>0 ? $"{minutes} Min. {seconds} Sek." : $"{seconds} Sek.";
    }

    public bool Recognizes(string command) =>
        CommandRegistry.Normalize(command)==CommandRegistry.Normalize(_config.StartCommand) ||
        CommandRegistry.Normalize(command)==CommandRegistry.Normalize(_config.JoinCommand);

    public bool Matches(string command) => _config.Enabled && Recognizes(command);

    public async Task ProcessAsync(ChatMessage message,string command,CancellationToken cancellationToken)
    {
        if(!Recognizes(command)) return;
        if(!_config.Enabled)
        {
            await SendAsync($"@{message.UserName}, das Heist-Modul ist aktuell deaktiviert.",cancellationToken);
            return;
        }
        if(CommandRegistry.Normalize(command)==CommandRegistry.Normalize(_config.StartCommand))
            await StartAsync(message,cancellationToken);
        else await JoinAsync(message,cancellationToken);
    }

    public async Task StartAsync(ChatMessage creator,CancellationToken cancellationToken)
    {
        if(!_config.Enabled)return;
        if(IsBlocked(creator))
        {
            Console.WriteLine($"Heist-Start von {creator.UserName} abgelehnt: Bot/Blacklist.");
            await SendAsync($"@{creator.UserName}, du darfst keinen Heist starten.",cancellationToken);
            return;
        }
        if(!await IsAllowedAsync(creator,cancellationToken,starting:true))
        {
            Console.WriteLine($"Heist-Start von {creator.UserName} abgelehnt: Berechtigung.");
            await SendAsync($"@{creator.UserName}, du hast keine Berechtigung, einen Heist zu starten.",cancellationToken);
            return;
        }
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now=DateTimeOffset.UtcNow;
            if(State is HeistState.Joining or HeistState.Evaluating)
            { await SendAsync($"@{creator.UserName}, es läuft bereits ein Heist.",cancellationToken); return; }
            var globalCooldown=TimeSpan.FromMinutes(_config.GlobalCooldownMinutes);
            if(now-_lastGlobalCompletion<globalCooldown)
            {
                var remaining=globalCooldown-(now-_lastGlobalCompletion);
                await SendAsync($"@{creator.UserName}, globaler Heist-Cooldown: noch {FormatCooldown(remaining)}.",cancellationToken);
                return;
            }
            if(_creatorCooldowns.TryGetValue(creator.UserId,out var last))
            {
                var userCooldown=TimeSpan.FromMinutes(_config.UserCooldownMinutes);
                if(now-last<userCooldown)
                {
                    var remaining=userCooldown-(now-last);
                    await SendAsync($"@{creator.UserName}, dein Heist-Cooldown: noch {FormatCooldown(remaining)}.",cancellationToken);
                    return;
                }
            }
            _sessionCts?.Dispose();
            _sessionCts=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _participants=new Dictionary<string,HeistParticipant>(StringComparer.Ordinal);
            _participants.Add(creator.UserId,new HeistParticipant(creator.UserId,creator.UserLogin,creator.UserName));
            _creator=creator.UserName; _remaining=_config.JoinDurationSeconds; State=HeistState.Joining;
            _eventId=Guid.NewGuid().ToString("N");_startedAt=now;_joinEndsAt=now.AddSeconds(_config.JoinDurationSeconds);
            _creatorCooldowns[creator.UserId]=now;
            Console.WriteLine($"Heist gestartet. Ersteller: {creator.UserName}; Beitrittsphase: {_config.JoinDurationSeconds}s; Erfolgschance: {_config.SuccessChancePercent}%.");
            var jackpot=await _points.GetJackpotAsync(_minigame.JackpotStartValue,_sessionCts.Token);
            Publish(jackpot);
            if(_config.SendStartMessages)await SendAsync(Format(_config.StartMessage,creator.UserName,1,jackpot,1),_sessionCts.Token);
            _sessionTask=RunSessionAsync(_sessionCts.Token);
        }
        finally { _gate.Release(); }
    }

    public async Task JoinAsync(ChatMessage user,CancellationToken cancellationToken, string? expectedEvent=null)
    {
        if(!_config.Enabled)return;
        if(IsBlocked(user))
        {
            Console.WriteLine($"Heist-Beitritt von {user.UserName} abgelehnt: Bot/Blacklist.");
            await SendAsync($"@{user.UserName}, du darfst diesem Heist nicht beitreten.",cancellationToken);
            return;
        }
        if(!await IsAllowedAsync(user,cancellationToken))
        {
            Console.WriteLine($"Heist-Beitritt von {user.UserName} abgelehnt: Berechtigung.");
            await SendAsync($"@{user.UserName}, du hast keine Berechtigung, diesem Heist beizutreten.",cancellationToken);
            return;
        }
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if(expectedEvent is not null && expectedEvent!=_eventId)return;
            var jackpot=await _points.GetJackpotAsync(_minigame.JackpotStartValue,cancellationToken);
            if(State!=HeistState.Joining||_participants is null||DateTimeOffset.UtcNow>=_joinEndsAt)
            { await SendAsync(Format(_config.NoActiveHeistMessage,user.UserName,0,jackpot,0),cancellationToken); return; }
            if(_participants.ContainsKey(user.UserId))
            { Console.WriteLine($"Doppelter Heist-Beitritt abgelehnt: {user.UserName}."); await SendAsync(Format(_config.AlreadyJoinedMessage,user.UserName,_participants.Count,jackpot,CalculateShare(jackpot,_participants.Count)),cancellationToken); return; }
            if(_participants.Count>=_config.MaximumParticipants)
            { Console.WriteLine("Heist-Maximalteilnehmerzahl erreicht."); await SendAsync(Format(_config.MaximumParticipantsMessage,user.UserName,_participants.Count,jackpot,CalculateShare(jackpot,_participants.Count)),cancellationToken); return; }
            void AddParticipant()=>_participants.Add(user.UserId,new HeistParticipant(user.UserId,user.UserLogin,user.UserName));
            if(expectedEvent is not null && _points.Admission is {} admission)admission.Commit(AddParticipant);else AddParticipant();
            Console.WriteLine($"Heist-Beitritt: {user.UserName}; Teilnehmer: {_participants.Count}.");
            if(_config.SendParticipantJoinMessages)
                await SendAsync(Format(_config.JoinMessage,user.UserName,_participants.Count,jackpot,CalculateShare(jackpot,_participants.Count)),cancellationToken);
            Publish(jackpot);
        }
        finally { _gate.Release(); }
    }

    private async Task RunSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var announced=new HashSet<int>();
            while(_remaining>0)
            {
                await Task.Delay(TimeSpan.FromSeconds(1),cancellationToken);
                _remaining=Math.Max(0,(int)Math.Ceiling((_joinEndsAt-DateTimeOffset.UtcNow).TotalSeconds));
                if(_config.SendCountdownMessages && (_remaining is 30 or 10 or 5 or 3 or 2 or 1) && announced.Add(_remaining))
                    await SendAsync($"Heist startet in {_remaining} Sekunden – Beitritt mit {_config.JoinCommand}.",cancellationToken);
                await PublishStatusAsync(cancellationToken);
            }
            await ResolveAsync(cancellationToken);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
        { Console.WriteLine("Heist-Beitrittsphase wurde abgebrochen."); }
        catch(Exception exception)
        { Console.WriteLine("Heist-Fehler: "+exception.Message); await CancelAsync(false,CancellationToken.None); }
    }

    private async Task ResolveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if(State!=HeistState.Joining||_participants is null)return;
            var participants=_participants.Values.ToArray();
            Console.WriteLine($"Heist-Beitrittsphase beendet. Teilnehmer: {participants.Length}.");
            if(participants.Length<_config.MinimumParticipants)
            {
                State=HeistState.Cancelled;
                if(_config.ApplyGlobalCooldownOnCancelledHeist)_lastGlobalCompletion=DateTimeOffset.UtcNow;
                var cancelledJackpot=await _points.GetJackpotAsync(_minigame.JackpotStartValue,cancellationToken);
                await SendAsync(Format(_config.NotEnoughParticipantsMessage,"",participants.Length,cancelledJackpot,CalculateShare(cancelledJackpot,participants.Length)),cancellationToken);
                Console.WriteLine("Heist automatisch abgebrochen: zu wenige Teilnehmer.");
                Publish(cancelledJackpot); CleanupSession(); return;
            }
            State=HeistState.Evaluating;
            var jackpot=await _points.GetJackpotAsync(_minigame.JackpotStartValue,cancellationToken);
            Publish(jackpot);
            if(_config.SendResultMessages)await SendAsync(Format(_config.EvaluationMessage,"",participants.Length,jackpot,jackpot/participants.Length),cancellationToken);
            var roll=_random.NextInclusive(1,100);
            var success=HeistRules.IsSuccess(_config.SuccessChancePercent,roll);
            Console.WriteLine($"Heist-Auswertung: Chance {_config.SuccessChancePercent}%, Zufallswert {roll}, Ergebnis {(success?"Erfolg":"Fehlschlag")}, Teilnehmer {participants.Length}, Jackpot vorher {jackpot}.");
            _lastGlobalCompletion=DateTimeOffset.UtcNow;
            if(!success)
            {
                State=HeistState.Failed;
                if(_config.SendResultMessages)await SendAsync(Format(_config.FailureMessage,"",participants.Length,jackpot,0),cancellationToken);
                Console.WriteLine($"Heist fehlgeschlagen. Jackpot unverändert: {jackpot}.");
                Publish(jackpot); CleanupSession(); return;
            }
            var remainder=jackpot%participants.Length;
            var candidates=Enumerable.Range(0,participants.Length).ToList();
            for(var i=candidates.Count-1;i>0;i--){var j=_random.NextInclusive(0,i);(candidates[i],candidates[j])=(candidates[j],candidates[i]);}
            var extras=candidates.Take((int)remainder).ToArray();
            var payout=await _points.PayoutHeistJackpotAsync(participants.Select(x=>(x.UserId,x.DisplayName)).ToArray(),extras,
                _minigame.JackpotStartValue,_config.ResetJackpotAfterSuccess,_minigame.HistoryLimit,cancellationToken,_eventId);
            foreach(var item in payout.Payouts)Console.WriteLine($"Heist-Auszahlung: {item.DisplayName} +{item.Payout}; Stand {item.NewBalance}.");
            if(extras.Length>0)Console.WriteLine("Heist-Restpunkte an Teilnehmerindizes: "+string.Join(",",extras));
            Console.WriteLine($"Heist-Jackpot nach Reset: {payout.JackpotAfter}.");
            State=HeistState.Successful;
            if(_config.SendResultMessages)await SendAsync(Format(_config.SuccessMessage,"",participants.Length,payout.JackpotBefore,payout.JackpotBefore/participants.Length),cancellationToken);
            Publish(payout.JackpotBefore,payout.Payouts.Select(p=>new HeistPlayerResult(p.UserId,p.DisplayName,p.Payout)).ToArray()); CleanupSession();
        }
        finally { _gate.Release(); }
    }

    public async Task CancelAsync(bool announce=true,CancellationToken cancellationToken=default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if(State is not (HeistState.Joining or HeistState.Evaluating))return;
            _sessionCts?.Cancel(); State=HeistState.Cancelled;
            Console.WriteLine("Heist manuell abgebrochen; keine Auszahlung und kein Jackpot-Reset.");
            if(announce)await SendAsync("Der laufende Heist wurde abgebrochen.",cancellationToken);
            var jackpot=await _points.GetJackpotAsync(_minigame.JackpotStartValue,cancellationToken);
            Publish(jackpot); CleanupSession();
        }
        finally { _gate.Release(); }
    }

    public async Task RunTestAsync(CancellationToken cancellationToken)
    {
        var jackpot=await _points.GetJackpotAsync(_minigame.JackpotStartValue,cancellationToken);
        var count=Math.Max(3,_config.MinimumParticipants); var roll=_random.NextInclusive(1,100);
        var startText="[TEST] "+Format(_config.StartMessage,"TestStreamer",1,jackpot,jackpot);
        var evaluationText="[TEST] "+Format(_config.EvaluationMessage,"",count,jackpot,jackpot/count);
        var resultText="[TEST] "+Format(roll<=_config.SuccessChancePercent?_config.SuccessMessage:_config.FailureMessage,"",count,jackpot,jackpot/count);
        Console.WriteLine("TESTMODUS – keine echte Auszahlung");
        Console.WriteLine(startText); Console.WriteLine(evaluationText); Console.WriteLine(resultText);
        Console.WriteLine($"Test-Heist: Chance {_config.SuccessChancePercent}%, Zufallswert {roll}; Jackpot und Punkte unverändert.");
        await SendAsync(startText,cancellationToken);
        await SendAsync(evaluationText,cancellationToken);
        await SendAsync(resultText+" Keine echte Auszahlung.",cancellationToken);
    }

    private bool IsBlocked(ChatMessage user)=>(user.UserId==_chatUserId && user.UserId!=_broadcasterId) || _minigame.PointsBlacklist.Any(x=>
        x.Equals(user.UserLogin,StringComparison.OrdinalIgnoreCase)||x.Equals(user.UserName,StringComparison.OrdinalIgnoreCase));
    private async Task<bool> IsAllowedAsync(ChatMessage user,CancellationToken token,bool starting=false)
    {
        if (starting) return EventStartCommand.IsAllowed(user, _broadcasterId);
        if (user.CommandAuthorization == CommandAuthorization.Allowed) return true;
        if (user.CommandAuthorization == CommandAuthorization.Denied) return false;
        if(user.IsBroadcaster||user.UserId==_broadcasterId)return true;
        if(starting&&user.IsModerator)return true;
        if(_config.AllowEveryone)return true;
        if(_config.AllowModerators&&user.IsModerator||_config.AllowVips&&user.IsVip||_config.AllowSubscribers&&user.IsSubscriber)return true;
        return _config.AllowFollowers&&await _twitch.IsFollowerAsync(_broadcasterId,user.UserId,token);
    }
    private static long CalculateShare(long jackpot,int participantCount)=>participantCount<=0?0:jackpot/participantCount;
    private string Format(string template,string user,int current,long jackpot,long share)=>template
        .Replace("{user}",user).Replace("{seconds}",_config.JoinDurationSeconds.ToString())
        .Replace("{minimum}",_config.MinimumParticipants.ToString()).Replace("{maximum}",_config.MaximumParticipants.ToString())
        .Replace("{current}",current.ToString()).Replace("{count}",current.ToString())
        .Replace("{jackpot}",jackpot.ToString("N0")).Replace("{share}",share.ToString("N0"))
        .Replace("{currencyName}",_minigame.CurrencyPlural).Replace("{startCommand}",_config.StartCommand)
        .Replace("{joinCommand}",_config.JoinCommand).Replace("{successChance}",_config.SuccessChancePercent.ToString());
    private async Task SendAsync(string message,CancellationToken token)
    { try{await _twitch.SendChatMessageAsync(_broadcasterId,_chatUserId,message,token);}catch(OperationCanceledException)when(token.IsCancellationRequested){}catch(Exception ex){Console.WriteLine("Heist-Chatantwort fehlgeschlagen: "+ex.Message);} }
    private HeistStatus CurrentStatus(long jackpot)=>new(State,_creator,_participants?.Count??0,_remaining,jackpot,
        _config.SuccessChancePercent,_participants?.Values.Select(x=>x.DisplayName).ToArray()??Array.Empty<string>());
    private void Publish(long jackpot,IReadOnlyList<HeistPlayerResult>? results=null)
    {
        var terminal=State is HeistState.Successful or HeistState.Failed or HeistState.Cancelled;
        var players=results??_participants?.Values.Select(p=>new HeistPlayerResult(p.UserId,p.DisplayName,0)).ToArray()??Array.Empty<HeistPlayerResult>();
        var snapshot=new HeistEventSnapshot(_eventId,_config.Overlay.Title,State,_startedAt,_joinEndsAt,terminal?DateTimeOffset.UtcNow:null,
            _config.MinimumParticipants,_config.MaximumParticipants,_config.JoinCommand,_minigame.CurrencyPlural,jackpot,0,Array.AsReadOnly(players.ToArray()),players.Sum(p=>p.Payout),0);
        Volatile.Write(ref _snapshot,snapshot);
        // A broken display subscriber must never cancel or repeat a committed payout.
        if(EventChanged is not null)foreach(Action<HeistEventSnapshot> handler in EventChanged.GetInvocationList())
            try{handler(snapshot);}catch(Exception ex){Console.WriteLine("Heist-Anzeige: "+ex.Message);}
        if(StatusChanged is not null)foreach(Action<HeistStatus> handler in StatusChanged.GetInvocationList())
            try{handler(CurrentStatus(jackpot));}catch(Exception ex){Console.WriteLine("Heist-Statusanzeige: "+ex.Message);}
    }
    private async Task PublishStatusAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try{if(State==HeistState.Joining)Publish(await _points.GetJackpotAsync(_minigame.JackpotStartValue,token));}
        finally{_gate.Release();}
    }
    internal async Task FinishAdministrativeAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token);try{if(State!=HeistState.Joining)throw new InvalidOperationException("Kein offener Heist.");_remaining=0;_joinEndsAt=DateTimeOffset.UtcNow;}finally{_gate.Release();}
        await ResolveAsync(token);
    }
    // Debug only: the caller must own an isolated service/store, never a live frontend.
    internal async Task FinishJoiningForTestAsync(CancellationToken token=default)
    {
        await _gate.WaitAsync(token);try{_remaining=0;_joinEndsAt=DateTimeOffset.UtcNow;}finally{_gate.Release();}
        await ResolveAsync(token);
    }
    private void CleanupSession(){_participants=null;_creator="";_remaining=0;_sessionCts?.Cancel();_sessionCts?.Dispose();_sessionCts=null;}
    public async ValueTask DisposeAsync(){if(_disposed)return;_disposed=true;await CancelAsync(false);if(_sessionTask is not null)try{await _sessionTask;}catch(OperationCanceledException){} _gate.Dispose();}
}
