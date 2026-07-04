using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

// Vorbereitung für einen späteren nativen Chat mit Twitch IRC/EventSub sowie
// Twitch-, BTTV-, 7TV- und FFZ-Emotes. Der WebView2-Popout bleibt Standard.
public interface IChatProvider
{
    bool IsConnected { get; }
    event Action<ChatMessage>? MessageReceived;
    event Action<string>? Error;
    Task ConnectAsync(string channelName, CancellationToken cancellationToken);
    Task DisconnectAsync();
}
