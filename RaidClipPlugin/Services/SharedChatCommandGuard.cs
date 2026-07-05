using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed record SharedChatCommandDecision(
    bool Allowed,
    bool IsCommand,
    string Command,
    string SourceRoomId,
    string CurrentRoomId,
    string ConfiguredBroadcasterId);

public sealed class SharedChatCommandGuard
{
    private readonly string _configuredBroadcasterId;
    private readonly bool _ignoreSharedChatOrigins;

    public SharedChatCommandGuard(
        string configuredBroadcasterId,
        bool ignoreSharedChatOrigins = true)
    {
        _configuredBroadcasterId = configuredBroadcasterId?.Trim() ?? "";
        _ignoreSharedChatOrigins = ignoreSharedChatOrigins;
    }

    public SharedChatCommandDecision Evaluate(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var parsed = ChatCommandParser.Parse(message.Text);
        var command = parsed.IsCommand
            ? parsed.Prefix + parsed.Command
            : "";
        var sourceRoomId = FirstNotEmpty(
            message.SourceBroadcasterUserId,
            message.SourceRoomId);
        var currentRoomId = FirstNotEmpty(
            message.BroadcasterUserId,
            message.RoomId,
            _configuredBroadcasterId);

        if (!parsed.IsCommand || !_ignoreSharedChatOrigins ||
            string.IsNullOrWhiteSpace(sourceRoomId))
        {
            return new SharedChatCommandDecision(
                true, parsed.IsCommand, command, sourceRoomId,
                currentRoomId, _configuredBroadcasterId);
        }

        var allowed = sourceRoomId.Equals(
            _configuredBroadcasterId,
            StringComparison.Ordinal);
        return new SharedChatCommandDecision(
            allowed, true, command, sourceRoomId,
            currentRoomId, _configuredBroadcasterId);
    }

    public bool IsCommandAllowedForCurrentChannel(ChatMessage message) =>
        Evaluate(message).Allowed;

    private static string FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value =>
            !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
}
