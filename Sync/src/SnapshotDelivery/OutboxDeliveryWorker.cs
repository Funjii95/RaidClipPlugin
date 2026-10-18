namespace SnapshotDelivery;

public sealed class OutboxDeliveryWorker
{
    private readonly OutboxDispatcher dispatcher;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    public OutboxDeliveryWorker(OutboxDispatcher dispatcher) : this(dispatcher, Task.Delay) { }
    internal OutboxDeliveryWorker(OutboxDispatcher dispatcher, Func<TimeSpan, CancellationToken, Task> delay)
    {
        this.dispatcher = dispatcher;
        this.delay = delay;
    }

    // Host explicitly selects a user and owns lifetime/cancellation. No plugin files are scanned.
    public async Task<DeliveryStatus> RunAsync(string userId, CancellationToken ct)
    {
        var failures = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var result = await dispatcher.SendNextAsync(userId, ct);
            if (result.Status == DeliveryStatus.Blocked) return DeliveryStatus.Blocked;
            if (result.Status == DeliveryStatus.Delivered) { failures = 0; continue; }
            if (result.Status == DeliveryStatus.Empty)
            {
                failures = 0;
                await delay(TimeSpan.FromSeconds(5), ct);
            }
            else
            {
                failures = Math.Min(failures + 1, 6);
                await delay(TimeSpan.FromSeconds(Math.Min(60, 1 << failures)), ct);
            }
        }
    }
}
