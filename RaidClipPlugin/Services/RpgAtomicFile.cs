using System.Text;
using System.Text.Json;

namespace RaidClipPlugin.Services;

public static class RpgAtomicFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented=true, PropertyNameCaseInsensitive=true };
    public static T Load<T>(string path,Action<T> validate)
    {
        var value=JsonSerializer.Deserialize<T>(File.ReadAllText(path,Encoding.UTF8),Options) ?? throw new InvalidDataException("Konfigurationsdatei ist leer.");
        validate(value);return value;
    }
    public static void Save<T>(string path,T value,Action<T> validate)
    {
        validate(value);
        if(File.Exists(path)) _=Load(path,validate); // Never replace the last valid backup with corrupt input.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
            { var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value,Options));stream.Write(bytes);stream.Flush(true); }
            _=Load(temp,validate);
            if(File.Exists(path)) File.Replace(temp,path,path+".bak");else File.Move(temp,path);
        }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    public static void Restore<T>(string path,Action<T> validate)
    {
        var value=Load(path+".bak",validate);
        var temp=path+".restore-"+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temp,JsonSerializer.Serialize(value,Options),Encoding.UTF8);_=Load(temp,validate);
            if(File.Exists(path)) File.Replace(temp,path,path+".before-restore-"+Guid.NewGuid().ToString("N")+".bak");else File.Move(temp,path);
        }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
}
