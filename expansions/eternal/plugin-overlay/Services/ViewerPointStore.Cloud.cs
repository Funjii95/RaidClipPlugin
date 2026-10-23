namespace RaidClipPlugin.Services;

public sealed partial class ViewerPointStore
{
    internal async Task<byte[]?> ReadCommittedFileAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync(ct);
            if(!File.Exists(_filePath))return null;
            // Event metadata shares the atomic save but is never a player account.
            var bytes=await File.ReadAllBytesAsync(_filePath,ct);
            var node=System.Text.Json.Nodes.JsonNode.Parse(bytes)!.AsObject();
            node.Remove(GlobalBossKey);return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(node,JsonOptions);
        }
        finally{_lock.Release();}
    }
}
