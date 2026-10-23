namespace RaidClipPlugin.Services;
public enum GroqScene { Daily,Dive,Fishing,Mimic,Boss,LevelUp,Conversation }
public sealed record GroqStoryEvent(GroqScene Scene,string Id,string Outcome,string DisplayName="",int Level=0);
public sealed partial class ViewerPointStore
{
    public event Action<GroqStoryEvent>? StoryCommitted;
    internal void PublishStory(GroqStoryEvent story)
    {
        if(StoryCommitted is null)return;
        foreach(Action<GroqStoryEvent> listener in StoryCommitted.GetInvocationList())try{listener(story);}catch{Console.WriteLine("RPG-Kommentar konnte nicht vorgemerkt werden.");}
    }
}
