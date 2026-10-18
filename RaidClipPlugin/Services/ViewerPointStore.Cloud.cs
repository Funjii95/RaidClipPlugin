namespace RaidClipPlugin.Services;

public sealed partial class ViewerPointStore
{
    internal async Task<byte[]?> ReadCommittedFileAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync(ct);
            return File.Exists(_filePath) ? await File.ReadAllBytesAsync(_filePath,ct) : null;
        }
        finally{_lock.Release();}
    }
}
