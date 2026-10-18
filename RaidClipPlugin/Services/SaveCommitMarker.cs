using System.Security.Cryptography;
using System.Text.Json;

namespace RaidClipPlugin.Services;

// Hash-only write-ahead metadata. viewer-points.json remains the sole player authority.
internal sealed class SaveCommitMarker(string source)
{
    private sealed record State(int Version, string? Committed, string? Prepared);
    private readonly string path=source+".commit.json";
    private State state=new(1,null,null);
    private static async Task<string?> HashAsync(string path, CancellationToken ct) => File.Exists(path)
        ? Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path,ct))) : null;
    public async Task RecoverAsync(CancellationToken ct)
    {
        await using var lease=await AcquireAsync(source,ct);
        var actual=await HashAsync(source,ct);
        if(!File.Exists(path))
        {
            state=new(1,actual,null); return; // Reading a legacy file does not migrate or write it.
        }
        state=JsonSerializer.Deserialize<State>(await File.ReadAllTextAsync(path,ct)) ?? throw new InvalidDataException("Invalid commit marker.");
        if(state.Version!=1 || (actual!=state.Committed && (state.Prepared is null || actual!=state.Prepared)))
            throw new InvalidDataException("Player save differs from committed/prepared hash; explicit recovery required.");
        state=new(1,actual,null); await WriteAsync(ct);
    }
    internal static async Task<FileStream> AcquireAsync(string source, CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(3));
        while(true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            try {return new FileStream(source+".writer.lock",FileMode.OpenOrCreate,FileAccess.Write,FileShare.None);}
            catch(IOException){await Task.Delay(20,timeout.Token);}
        }
    }
    public async Task PrepareAsync(string temporary, CancellationToken ct)
    {
        if(await HashAsync(source,ct)!=state.Committed) throw new InvalidDataException("Player save changed outside this instance.");
        state=state with {Prepared=await HashAsync(temporary,ct)}; await WriteAsync(ct);
    }
    public async Task CompleteAsync(CancellationToken ct)
    {state=new(1,state.Prepared,null); await WriteAsync(ct);}
    private async Task WriteAsync(CancellationToken ct)
    {
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            await using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                await JsonSerializer.SerializeAsync(output,state,cancellationToken:ct);
                await output.FlushAsync(ct); output.Flush(true);
            }
            File.Move(temp,path,true);
        }
        finally {if(File.Exists(temp))File.Delete(temp);}
    }
}
