using System.Security.Cryptography;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public enum HeistState { Inactive, Joining, Evaluating, Successful, Failed, Cancelled }
public sealed record HeistParticipant(string UserId,string Login,string DisplayName);
public sealed record HeistStatus(HeistState State,string Creator,int ParticipantCount,int SecondsRemaining,
    int Jackpot,int SuccessChancePercent,IReadOnlyList<string> Participants,bool TestMode=false);

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
    public static int[] CalculatePayouts(int jackpot,int participantCount,IReadOnlyCollection<int> extraRecipients)
    {
        if(participantCount<1)throw new ArgumentOutOfRangeException(nameof(participantCount));
        if(jackpot<0)throw new ArgumentOutOfRangeException(nameof(jackpot));
        var remainder=jackpot%participantCount; var extras=extraRecipients.Distinct().ToHashSet();
        if(extras.Count!=remainder||extras.Any(x=>x<0||x>=participantCount))throw new ArgumentException("Ungültige Restpunktverteilung.",nameof(extraRecipients));
        var share=jackpot/participantCount; return Enumerable.Range(0,participantCount).Select(i=>share+(extras.Contains(i)?1:0)).ToArray();
    }
}

public sealed class HeistService : IAsyncDisposable
{
    private readonly string _broadcasterId;
    private readonly string _chatUserId;
    private readonly TwitchService _twitch;
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

    public event Action<HeistStatus>? StatusChanged;
    public HeistState State { get; private set; }=HeistState.Inactive;

    public HeistService(string broadcasterId,string chatUserId,HeistConfig config,
        MinigameConfig minigame,TwitchService twitch,ViewerPointStore points,IHeistRandom? random=null)
    {
        _broadcasterId=broadcasterId; _chatUserId=chatUserId; _config=config;
        _minigame=minigame; _twitch=twitch; _points=points; _random=random??new CryptoHeistRandom();
    }

    public void UpdateConfig(HeistConfig config,MinigameConfig minigame)
    { _config=config; _minigame=minigame; }

    public bool Matches(string command) => _config.Enabled &&
        (CommandRegistry.Normalize(command)==CommandRegistry.Normalize(_config.StartCommand) ||
         CommandRegistry.Normalize(command)==CommandRegistry.Normalize(_config.JoinCommand));

    public async Task ProcessAsync(ChatMessage message,string command,CancellationToken cancellationToken)
    {
        if(CommandRegistry.Normalize(command)==CommandRegistry.Normalize(_config.StartCommand))
            await StartAsync(message,cancellationToken);
        else await JoinAsync(message,cancellationToken);
    }

    public async Task StartAsync(ChatMessage creator,CancellationToken cancellationToken)
    {
        if(!_config.Enabled)return;
        if(IsBlocked(creator)) { Console.WriteLine($"Heist-Start von {creator.UserName} abgelehnt: Bot/Blacklist."); return; }
        if(!await IsAllowedAsync(creator,cancellationToken)) { Console.WriteLine($"Heist-Start von {creator.UserName} abgelehnt: Berechtigung."); return; }
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now=DateTimeOffset.UtcNow;
            if(State is HeistState.Joining or HeistState.Evaluating)
            { await SendAsync($"@{creator.UserName}, es läuft bereits ein Heist.",cancellationToken); return; }
            if(now-_lastGlobalCompletion<TimeSpan.FromMinutes(_config.GlobalCooldownMinutes))
            { await SendAsync($"@{creator.UserName}, der globale Heist-Cooldown ist noch aktiv.",cancellationToken); return; }
            if(_creatorCooldowns.TryGetValue(creator.UserId,out var last) && now-last<TimeSpan.FromMinutes(_config.UserCooldownMinutes))
            { await SendAsync($"@{creator.UserName}, dein Heist-Cooldown ist noch aktiv.",cancellationToken); return; }
            _sessionCts?.Dispose();
            _sessionCts=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _participants=new Dictionary<string,HeistParticipant>(StringComparer.Ordinal);
            _participants.Add(creator.UserId,new HeistParticipant(creator.UserId,creator.UserLogin,creator.UserName));
            _creator=creator.UserName; _remaining=_config.JoinDurationSeconds; State=HeistState.Joining;
            _creatorCooldowns[creator.UserId]=now;
            Console.WriteLine($"Heist gestartet. Ersteller: {creator.UserName}; Beitrittsphase: {_config.JoinDurationSeconds}s; Erfolgschance: {_config.SuccessChancePercent}%.");
            await PublishStatusAsync(_sessionCts.Token);
            await SendAsync(Format(_config.StartMessage,creator.UserName,1,0,0),_sessionCts.Token);
            _sessionTask=RunSessionAsync(_sessionCts.Token);
        }
        finally { _gate.Release(); }
    }

    public async Task JoinAsync(ChatMessage user,CancellationToken cancellationToken)
    {
        if(!_config.Enabled)return;
        if(IsBlocked(user)) { Console.WriteLine($"Heist-Beitritt von {user.UserName} abgelehnt: Bot/Blacklist."); return; }
        if(!await IsAllowedAsync(user,cancellationToken)) { Console.WriteLine($"Heist-Beitritt von {user.UserName} abgelehnt: Berechtigung."); return; }
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if(State!=HeistState.Joining||_participants is null)
            { await SendAsync(Format(_config.NoActiveHeistMessage,user.UserName,0,0,0),cancellationToken); return; }
            if(_participants.ContainsKey(user.UserId))
            { Console.WriteLine($"Doppelter Heist-Beitritt abgelehnt: {user.UserName}."); await SendAsync(Format(_config.AlreadyJoinedMessage,user.UserName,_participants.Count,0,0),cancellationToken); return; }
            if(_participants.Count>=_config.MaximumParticipants)
            { Console.WriteLine("Heist-Maximalteilnehmerzahl erreicht."); await SendAsync(Format(_config.MaximumParticipantsMessage,user.UserName,_participants.Count,0,0),cancellationToken); return; }
            _participants.Add(user.UserId,new HeistParticipant(user.UserId,user.UserLogin,user.UserName));
            Console.WriteLine($"Heist-Beitritt: {user.UserName}; Teilnehmer: {_participants.Count}.");
            if(_config.SendParticipantJoinMessages)
                await SendAsync(Format(_config.JoinMessage,user.UserName,_participants.Count,0,0),cancellationToken);
            await PublishStatusAsync(cancellationToken);
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
                _remaining--;
                if(_config.SendCountdownMessages && (_remaining is 30 or 10 or 5 or 3 or 2 or 1) && announced.Add(_remaining))
                    await SendAsync($"Heist startet in {_remaining} Sekunden – Beitritt mit {_config.JoinCommand}.",cancellationToken);
                StatusChanged?.Invoke(CurrentStatus(0));
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
                await SendAsync(Format(_config.NotEnoughParticipantsMessage,"",participants.Length,0,0),cancellationToken);
                Console.WriteLine("Heist automatisch abgebrochen: zu wenige Teilnehmer.");
                StatusChanged?.Invoke(CurrentStatus(0)); CleanupSession(); return;
            }
            State=HeistState.Evaluating;
            var jackpot=await _points.GetJackpotAsync(_minigame.JackpotStartValue,cancellationToken);
            StatusChanged?.Invoke(CurrentStatus(jackpot));
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
                StatusChanged?.Invoke(CurrentStatus(jackpot)); CleanupSession(); return;
            }
            var remainder=jackpot%participants.Length;
            var candidates=Enumerable.Range(0,participants.Length).ToList();
            for(var i=candidates.Count-1;i>0;i--){var j=_random.NextInclusive(0,i);(candidates[i],candidates[j])=(candidates[j],candidates[i]);}
            var extras=candidates.Take(remainder).ToArray();
            var payout=await _points.PayoutHeistJackpotAsync(participants.Select(x=>(x.UserId,x.DisplayName)).ToArray(),extras,
                _minigame.JackpotStartValue,_config.ResetJackpotAfterSuccess,_minigame.HistoryLimit,cancellationToken);
            foreach(var item in payout.Payouts)Console.WriteLine($"Heist-Auszahlung: {item.DisplayName} +{item.Payout}; Stand {item.NewBalance}.");
            if(extras.Length>0)Console.WriteLine("Heist-Restpunkte an Teilnehmerindizes: "+string.Join(",",extras));
            Console.WriteLine($"Heist-Jackpot nach Reset: {payout.JackpotAfter}.");
            State=HeistState.Successful;
            if(_config.SendResultMessages)await SendAsync(Format(_config.SuccessMessage,"",participants.Length,payout.JackpotBefore,payout.JackpotBefore/participants.Length),cancellationToken);
            StatusChanged?.Invoke(CurrentStatus(payout.JackpotAfter)); CleanupSession();
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
            StatusChanged?.Invoke(CurrentStatus(0)); CleanupSession();
        }
        finally { _gate.Release(); }
    }

    public async Task RunTestAsync(CancellationToken cancellationToken)
    {
        var jackpot=await _points.GetJackpotAsync(_minigame.JackpotStartValue,cancellationToken);
        var count=Math.Max(3,_config.MinimumParticipants); var roll=_random.NextInclusive(1,100);
        Console.WriteLine("TESTMODUS – keine echte Auszahlung");
        Console.WriteLine(Format(_config.StartMessage,"TestStreamer",1,0,0));
        for(var i=2;i<=count;i++)Console.WriteLine(Format(_config.JoinMessage,"TestUser"+i,i,0,0));
        Console.WriteLine(Format(_config.EvaluationMessage,"",count,jackpot,jackpot/count));
        Console.WriteLine(Format(roll<=_config.SuccessChancePercent?_config.SuccessMessage:_config.FailureMessage,"",count,jackpot,jackpot/count));
        Console.WriteLine($"Test-Heist: Chance {_config.SuccessChancePercent}%, Zufallswert {roll}; Jackpot und Punkte unverändert.");
    }

    private bool IsBlocked(ChatMessage user)=>user.UserId==_chatUserId || _minigame.PointsBlacklist.Any(x=>
        x.Equals(user.UserLogin,StringComparison.OrdinalIgnoreCase)||x.Equals(user.UserName,StringComparison.OrdinalIgnoreCase));
    private async Task<bool> IsAllowedAsync(ChatMessage user,CancellationToken token)
    {
        if(user.IsBroadcaster||user.UserId==_broadcasterId)return true;
        if(_config.AllowEveryone)return true;
        if(_config.AllowModerators&&user.IsModerator||_config.AllowVips&&user.IsVip||_config.AllowSubscribers&&user.IsSubscriber)return true;
        return _config.AllowFollowers&&await _twitch.IsFollowerAsync(_broadcasterId,user.UserId,token);
    }
    private string Format(string template,string user,int current,int jackpot,int share)=>template
        .Replace("{user}",user).Replace("{seconds}",_config.JoinDurationSeconds.ToString())
        .Replace("{minimum}",_config.MinimumParticipants.ToString()).Replace("{maximum}",_config.MaximumParticipants.ToString())
        .Replace("{current}",current.ToString()).Replace("{count}",current.ToString())
        .Replace("{jackpot}",jackpot.ToString("N0")).Replace("{share}",share.ToString("N0"))
        .Replace("{currencyName}",_minigame.CurrencyPlural).Replace("{startCommand}",_config.StartCommand)
        .Replace("{joinCommand}",_config.JoinCommand).Replace("{successChance}",_config.SuccessChancePercent.ToString());
    private async Task SendAsync(string message,CancellationToken token)
    { try{await _twitch.SendChatMessageAsync(_broadcasterId,_chatUserId,message,token);}catch(OperationCanceledException)when(token.IsCancellationRequested){}catch(Exception ex){Console.WriteLine("Heist-Chatantwort fehlgeschlagen: "+ex.Message);} }
    private HeistStatus CurrentStatus(int jackpot)=>new(State,_creator,_participants?.Count??0,_remaining,jackpot,
        _config.SuccessChancePercent,_participants?.Values.Select(x=>x.DisplayName).ToArray()??Array.Empty<string>());
    private async Task PublishStatusAsync(CancellationToken token)=>StatusChanged?.Invoke(CurrentStatus(await _points.GetJackpotAsync(_minigame.JackpotStartValue,token)));
    private void CleanupSession(){_participants=null;_creator="";_remaining=0;_sessionCts?.Dispose();_sessionCts=null;}
    public async ValueTask DisposeAsync(){if(_disposed)return;_disposed=true;await CancelAsync(false);if(_sessionTask is not null)try{await _sessionTask;}catch(OperationCanceledException){} _gate.Dispose();}
}
