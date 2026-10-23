using RaidClipPlugin.Services;
namespace RaidClipPlugin;
public sealed partial class MainForm
{
    private GroqRuntime? _groqRuntime;
    private void StartGroqRuntime(TwitchService twitch,TwitchSession session,TwitchUser broadcaster,CancellationToken ct)
    {
        _groqRuntime?.Stop();var epoch=_streamEpoch;
        var runtime=new GroqRuntime(_viewerPoints,_groqDaily,session.UserId,session.Login,
            ()=>!ct.IsCancellationRequested&&epoch==_streamEpoch&&_chatModeration?.Diagnostics.WebSocketConnected==true,
            (text,token)=>twitch.SendChatMessageAsync(broadcaster.Id,session.UserId,text,token),ct);
        _groqRuntime=runtime;
        _chatModeration!.MessageReceived+=message=>{if(epoch==_streamEpoch)runtime.EnqueueChat(message);return Task.CompletedTask;};
    }
}
