using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class PlaceholderChatProvider : IChatProvider
{
    public bool IsConnected { get; private set; }
    public event Action<ChatMessage>? MessageReceived;
    public event Action<string>? Error;

    public Task ConnectAsync(string channelName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(channelName))
        {
            const string message = "Kein Twitch-Kanal konfiguriert.";
            Error?.Invoke(message);
            throw new InvalidOperationException(message);
        }
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    internal void PublishForFutureProvider(ChatMessage message) =>
        MessageReceived?.Invoke(message);
}
