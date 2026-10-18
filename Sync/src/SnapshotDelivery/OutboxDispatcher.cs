using SnapshotProtocol;

namespace SnapshotDelivery;

public sealed class OutboxDispatcher(LocalOutboxStore store, HttpSnapshotSender sender)
{
    // One bounded attempt. Scheduling/backoff is the host's responsibility; no busy retry loop.
    public async Task<DeliveryResult> SendNextAsync(string userId, CancellationToken ct = default)
    {
        await using var lease = await store.AcquireDispatchLeaseAsync(userId, ct);
        var state = await store.ReadAsync(userId, ct);
        if (state?.Pending.FirstOrDefault() is not { } message) return new(DeliveryStatus.Empty);
        var result = await sender.SendAsync(message, ct);
        if (result is { Status: DeliveryStatus.Delivered, Decision: { } decision })
            await store.AcknowledgeAsync(userId, message.SnapshotId, message.Revision, decision, ct);
        return result;
    }
}
