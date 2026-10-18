namespace SnapshotProtocol;

public interface ISnapshotSink
{
    Task<SnapshotDecision> ApplyAsync(SnapshotMessage message, CancellationToken ct = default);
}

public static class SnapshotRules
{
    // Shared by file and Firestore stores. The caller must hold its account lock/transaction.
    public static SnapshotDecision Decide(StoredSnapshot? current, ValidatedSnapshot incoming)
    {
        if (current is not null)
        {
            if (current.ChannelId != incoming.ChannelId || current.UserId != incoming.UserId ||
                current.Revision <= 0 || current.SnapshotId == Guid.Empty || current.CapturedAt == default ||
                current.ContentHash is not { Length: 64 } || current.Deleted != (current.AccountJson is null))
                throw new InvalidDataException("Invalid stored snapshot metadata.");
            if (incoming.Revision < current.Revision) return SnapshotDecision.Stale;
            if (incoming.Revision == current.Revision)
                return incoming.ContentHash == current.ContentHash && incoming.SnapshotId == current.SnapshotId
                    ? SnapshotDecision.Duplicate : SnapshotDecision.Conflict;
            if (incoming.SnapshotId == current.SnapshotId) return SnapshotDecision.Conflict;
            if (current.Deleted && incoming.Kind == SnapshotKind.Upsert) return SnapshotDecision.RestoreRequired;
        }
        if (incoming.Kind == SnapshotKind.Restore &&
            (current is not { Deleted: true } || current.Revision != incoming.ExpectedDeletedRevision))
            return SnapshotDecision.Conflict;
        return SnapshotDecision.Applied;
    }
}
