namespace SnapshotProtocol;

public sealed record SnapshotAcknowledgement(int ContractVersion, string ChannelId, string UserId,
    long Revision, Guid SnapshotId, string Decision);
