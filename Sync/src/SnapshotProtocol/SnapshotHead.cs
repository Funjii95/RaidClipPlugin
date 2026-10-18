namespace SnapshotProtocol;

// Metadata only. No profile, history or credentials are returned to the sender.
public sealed record SnapshotHead(int ContractVersion, string ChannelId, string UserId, long Revision,
    Guid SnapshotId, DateTimeOffset CapturedAt, bool Deleted, string ContentHash);
