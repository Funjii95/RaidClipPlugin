using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public enum RpgRewardSource { Activity, Fishing, DiveNetWin, Boss, MinigameNetWin, Administrative, Salvage, Transfer, StakeRefund, PerkRefund, AlreadyBoosted }
public sealed record RpgReward(long Amount,RpgRewardSource Source,bool HotTimeApplied=false);
public sealed record RpgHotTimeState(string EventId,string BossId,DateTimeOffset Start,DateTimeOffset End,bool Active,bool StartAnnounced=false,bool EndAnnounced=false);
public sealed record RpgHotTimeStatus(RpgHotTimeState? Event,int RemainingSeconds,bool Active,string Error="");
public sealed class RpgHotTimeService
{
    private readonly string _path;
    private readonly Func<DateTimeOffset> _now;
    private readonly object _gate=new();
    private RpgHotTimeState? _state;
    private string _error="";
    public RpgHotTimeService(string directory,Func<DateTimeOffset>? now=null)
    {
        _path=Path.Combine(directory,"hot-time.json");_now=now??(()=>DateTimeOffset.UtcNow);
        if(File.Exists(_path))
        {
            try { _state=RpgAtomicFile.Load<RpgHotTimeState>(_path,Validate); if(_state.End<=_now()) _state=_state with { Active=false,StartAnnounced=true,EndAnnounced=true }; }
            catch(Exception ex) { _error="Hot-Time-Status konnte nicht geladen werden: "+ex.GetType().Name; }
        }
    }
    private static void Validate(RpgHotTimeState state)
    { if(string.IsNullOrWhiteSpace(state.EventId)||state.End<=state.Start||state.End-state.Start>TimeSpan.FromHours(1)) throw new InvalidDataException("Ungültiger Hot-Time-Status."); }
    public RpgHotTimeStatus Status
    {
        get { lock(_gate) { var active=_error.Length==0 && _state is {Active:true} && _state.End>_now();return new(_state is null?null:_state with {Active=active},active?Math.Max(0,(int)Math.Ceiling((_state!.End-_now()).TotalSeconds)):0,active,_error); } }
    }
    public bool StartAfterBoss(string bossId,RpgHotTimeConfig config,bool realCommunityVictory)
    {
        config.Validate();
        lock(_gate)
        {
            if(!realCommunityVictory||!config.Enabled||!config.AutoAfterBoss||Status.Active||_state?.BossId==bossId)return false;
            if(_error.Length>0)throw new InvalidDataException(_error);
            var start=_now();
            var state=new RpgHotTimeState(Guid.NewGuid().ToString("N"),bossId,start,start.AddMinutes(config.DurationMinutes),true);
            RpgAtomicFile.Save(_path,state,Validate);_state=state;return true;
        }
    }
    public void End()
    {
        lock(_gate) { if(_state is not {Active:true})return;var state=_state with { Active=false };RpgAtomicFile.Save(_path,state,Validate);_state=state; }
    }
    internal bool StartAdministrative(string requestId,RpgHotTimeConfig config)
    {
        config.Validate();
        lock(_gate)
        {
            if(!config.Enabled||Status.Active||_state?.BossId==requestId)return false;
            if(_error.Length>0)throw new InvalidDataException(_error);
            var now=_now();var state=new RpgHotTimeState(Guid.NewGuid().ToString("N"),requestId,now,now.AddMinutes(config.DurationMinutes),true);
            RpgAtomicFile.Save(_path,state,Validate);_state=state;return true;
        }
    }
    // Durable at-most-once message claim; no offline replay after an expired restart.
    public IReadOnlyList<string> PollMessages(RpgHotTimeConfig config)
    {
        lock(_gate)
        {
            if(_state is null||_error.Length>0)return Array.Empty<string>();
            var state=_state;var messages=new List<string>();
            if(state.Active&&(!config.Enabled||state.End<=_now()))state=state with {Active=false};
            if(state.Active&&!state.StartAnnounced)
            {
                var template=state.BossId.StartsWith("admin:",StringComparison.Ordinal)?"🔥 HOT TIME! Vom Kanalinhaber gestartet: {minutes} Minuten +{drop}% Item-Dropchance und {reward}!":config.StartMessage;
                messages.Add(template.Replace("{minutes}",RpgChatPresentation.Number(decimal.Round((decimal)(state.End-state.Start).TotalMinutes,2))).Replace("{drop}",RpgChatPresentation.Number(config.DropBonusPercent)).Replace("{pearls}",RpgChatPresentation.Number(config.PearlBonusPercent)).Replace("{reward}",config.PearlBonusPercent==100?"doppelte Perlenbelohnungen":"+"+RpgChatPresentation.Number(config.PearlBonusPercent)+"% Perlenbelohnungen"));
                state=state with {StartAnnounced=true};
            }
            if(!state.Active&&!state.EndAnnounced) { if(state.StartAnnounced)messages.Add(config.EndMessage);state=state with {EndAnnounced=true,StartAnnounced=true}; }
            if(state!=_state){RpgAtomicFile.Save(_path,state,Validate);_state=state;}return messages;
        }
    }
    public RpgReward Apply(RpgReward reward,RpgHotTimeConfig config)
        =>ApplyAt(reward,config,Status.Active);
    public static RpgReward ApplyAt(RpgReward reward,RpgHotTimeConfig config,bool active)
    {
        if(reward.HotTimeApplied||reward.Amount<=0||!config.Enabled||!active||reward.Source is not (RpgRewardSource.Activity or RpgRewardSource.Fishing or RpgRewardSource.DiveNetWin or RpgRewardSource.Boss or RpgRewardSource.MinigameNetWin))return reward;
        return reward with {Amount=(long)Math.Min(long.MaxValue,decimal.Floor(reward.Amount*(1+Math.Clamp(config.PearlBonusPercent,0,200)/100))),HotTimeApplied=true};
    }
    public decimal DropBonus(RpgHotTimeConfig config)=>config.Enabled&&Status.Active?config.DropBonusPercent:0;
}
