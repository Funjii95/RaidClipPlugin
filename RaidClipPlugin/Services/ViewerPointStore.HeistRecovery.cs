using System.Text.Json;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed partial class ViewerPointStore
{
    private sealed record HeistCommit(string Marker,string[] Users,long JackpotAfter);
    private string HeistCommitPath=>Path.Combine(Path.GetDirectoryName(_filePath)!,"heist-commit.json");
    private async Task PrepareHeistCommitAsync(HeistCommit commit,CancellationToken ct)
    {
        await using var file=new FileStream(HeistCommitPath,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        await JsonSerializer.SerializeAsync(file,commit,cancellationToken:ct);await file.FlushAsync(ct);file.Flush(true);
    }
    private async Task ReconcileHeistCommitAsync(CancellationToken ct)
    {
        if(!File.Exists(HeistCommitPath))return;
        var commit=JsonSerializer.Deserialize<HeistCommit>(await File.ReadAllTextAsync(HeistCommitPath,ct))??throw new InvalidDataException("Heist-Beleg fehlt.");
        if(!commit.Marker.StartsWith("heist-paid:",StringComparison.Ordinal)||commit.Users.Length==0||commit.JackpotAfter<0)throw new InvalidDataException("Heist-Beleg ungültig.");
        var disk=File.Exists(_filePath)?JsonSerializer.Deserialize<Dictionary<string,ViewerPointEntry>>(await File.ReadAllTextAsync(_filePath,ct),JsonOptions)!:new();
        var paid=commit.Users.Count(id=>disk.TryGetValue(id,out var a)&&a.Rpg.CompletedActions.Contains(commit.Marker));
        if(paid!=0 && paid!=commit.Users.Length)throw new InvalidDataException("Heist-Auszahlung widersprüchlich. Keine weitere Buchung zulässig.");
        if(paid==commit.Users.Length){
            _jackpot=commit.JackpotAfter;
            RpgAtomicFile.Save(_statePath,_jackpot,_=>{});
        }
        else _jackpot=File.Exists(_statePath)?JsonSerializer.Deserialize<long>(await File.ReadAllTextAsync(_statePath,ct),JsonOptions):0;
        // Restore memory from the committed player file after a failed write, never from a draft.
        _entries=new(disk,StringComparer.Ordinal);
        File.Delete(HeistCommitPath);
    }
}
