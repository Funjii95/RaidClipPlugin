using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public enum CommunityEventType { Boss, Race, Heist, HotTime, Notification }
public sealed record OverlayPlayer(string Name,long Payout,long Lost);
public sealed record HeistOverlayView(string EventId,string Name,string Phase,DateTimeOffset StartedAt,DateTimeOffset JoinEndsAt,
    string JoinCommand,string Currency,long Jackpot,long EntryStake,int ParticipantCount,IReadOnlyList<OverlayPlayer> Players,
    long TotalPayout,long TotalLost,DateTimeOffset? PresentationStartedAt,int AnimationSeconds,int ResultSeconds,bool SuccessAnimation,bool FailureAnimation);
public sealed record CommunityOverlayEvent(string EventId,CommunityEventType Type,int Priority,DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,string State,bool Visible=false);
public sealed record CommunityOverlayState(DateTimeOffset ServerNow,CommunityOverlayEvent? Current,HeistOverlayView? Heist,
    int QueuedCount,bool HotTimeActive=false,DateTimeOffset? HotTimeEndsAt=null,decimal HotTimeDrop=0,decimal HotTimePearls=0);

/// <summary>Presentation only. Never changes a game clock, participants, points or outcomes.</summary>
public sealed class CommunityOverlayEventManager
{
    private sealed record Pending(HeistEventSnapshot Snapshot,DateTimeOffset? PresentedAt=null);
    private readonly object _gate=new();
    private readonly Dictionary<string,Pending> _pending=new();
    private readonly Dictionary<string,DateTimeOffset> _completed=new();
    private HeistOverlayConfig _config;
    public CommunityOverlayEventManager(HeistOverlayConfig? config=null){_config=config??new();_config.Validate();}
    public void Configure(HeistOverlayConfig config){config.Validate();lock(_gate){_config=config;if(!config.Enabled)_pending.Clear();}}
    public void Update(HeistEventSnapshot snapshot)
    {
        lock(_gate)
        {
            if(!_config.Enabled || string.IsNullOrEmpty(snapshot.EventId) || _completed.ContainsKey(snapshot.EventId))return;
            if(_pending.TryGetValue(snapshot.EventId,out var old))
            {
                if(old.Snapshot.CompletedAt is not null)return; // Never rewind a completed event on delayed status delivery.
                _pending[snapshot.EventId]=old with {Snapshot=snapshot};
            }
            else
            {
                if(_pending.Count>=16)_pending.Remove(_pending.MinBy(p=>p.Value.Snapshot.StartedAt).Key);
                _pending[snapshot.EventId]=new(snapshot);
            }
        }
    }
    public void Hide(string eventId,DateTimeOffset now)
    {lock(_gate){_pending.Remove(eventId);_completed[eventId]=now;}}
    public CommunityOverlayState Read(AdventureStatus adventure,DateTimeOffset now,RpgHotTimeStatus? hot=null,RpgHotTimeConfig? hotConfig=null)
    {
        lock(_gate)
        {
            foreach(var id in _completed.Where(p=>now-p.Value>TimeSpan.FromMinutes(15)).Select(p=>p.Key).ToArray())_completed.Remove(id);
            foreach(var pair in _pending.ToArray())
            {
                var p=pair.Value;var snapshot=p.Snapshot;
                var stale=snapshot.CompletedAt is {} end ? now-end>TimeSpan.FromSeconds(_config.MaximumQueuedSeconds) && p.PresentedAt is null : now>snapshot.JoinEndsAt.AddSeconds(30);
                var finished=p.PresentedAt is {} start && now>=start.AddSeconds(_config.AnimationSeconds+_config.ResultSeconds+1);
                if(stale||finished){_pending.Remove(pair.Key);_completed[pair.Key]=now;}
            }
            var candidates=new List<CommunityOverlayEvent>();
            if(adventure.BossActive && adventure.BossEndsAt>now)candidates.Add(new(adventure.BossEventId.Length>0?adventure.BossEventId:"boss:"+adventure.BossEndsAt!.Value.Ticks,CommunityEventType.Boss,_config.BossPriority,adventure.BossStartedAt??now,adventure.BossEndsAt!.Value,"active"));
            if(adventure.RaceActive && adventure.RaceEndsAt>now)candidates.Add(new(adventure.RaceEventId.Length>0?adventure.RaceEventId:"race:"+adventure.RaceEndsAt!.Value.Ticks,CommunityEventType.Race,_config.RacePriority,adventure.RaceStartedAt??now,adventure.RaceEndsAt!.Value,"active"));
            if(_config.Enabled)foreach(var p in _pending.Values)
                candidates.Add(new(p.Snapshot.EventId,CommunityEventType.Heist,p.Snapshot.CompletedAt.HasValue?_config.ResultPriority:_config.HeistPriority,p.Snapshot.StartedAt,
                    p.PresentedAt?.AddSeconds(_config.AnimationSeconds+_config.ResultSeconds+1)??p.Snapshot.CompletedAt?.AddSeconds(_config.MaximumQueuedSeconds)??p.Snapshot.JoinEndsAt,p.Snapshot.State.ToString()));
            var current=candidates.OrderByDescending(c=>c.Priority).ThenBy(c=>c.StartedAt).ThenBy(c=>c.EventId,StringComparer.Ordinal).FirstOrDefault();
            HeistOverlayView? view=null;
            if(current?.Type==CommunityEventType.Heist && _pending.TryGetValue(current.EventId,out var pending))
            {
                if(pending.Snapshot.CompletedAt.HasValue && pending.PresentedAt is null)
                    _pending[current.EventId]=pending=pending with {PresentedAt=now};
                var s=pending.Snapshot;
                var phase=s.State.ToString();
                if(pending.PresentedAt is {} shown)
                {
                    var elapsed=(now-shown).TotalSeconds;
                    phase=elapsed<_config.AnimationSeconds && s.State!=HeistState.Cancelled?"Raiding":elapsed>=_config.AnimationSeconds+_config.ResultSeconds?"Hiding":s.State.ToString();
                }
                view=new(s.EventId,s.Name,phase,s.StartedAt,s.JoinEndsAt,s.JoinCommand,s.Currency,s.Jackpot,s.EntryStake,s.Players.Count,
                    _config.ShowParticipants?s.Players.Take(_config.MaximumVisibleParticipants).Select(p=>new OverlayPlayer(p.DisplayName,p.Payout,p.Lost)).ToArray():Array.Empty<OverlayPlayer>(),
                    s.TotalPayout,s.TotalLost,pending.PresentedAt,_config.AnimationSeconds,_config.ResultSeconds,_config.SuccessAnimation,_config.FailureAnimation);
                current=current with {Visible=true};
            }
            else if(current is not null)current=current with {Visible=true};
            var showHot=_config.ShowHotTime && hotConfig?.Enabled==true && hot?.Active==true && hot.Event?.End>now && current?.Type!=CommunityEventType.Boss;
            return new(now,current,view,Math.Max(0,candidates.Count-1),showHot,showHot?hot!.Event?.End:null,hotConfig?.DropBonusPercent??0,hotConfig?.PearlBonusPercent??0);
        }
    }
}
