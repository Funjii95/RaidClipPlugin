using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public sealed class ObsService : IDisposable
{
    private readonly OBSWebsocket _obs = new();
    private readonly AppConfig _config;

    public ObsService(AppConfig config) => _config = config;

    public bool IsConnected => _obs.IsConnected;

    public void Connect()
    {
        var address = $"ws://{_config.OBS.Host}:{_config.OBS.Port}";
        Console.WriteLine($"Verbinde mit OBS: {address}");
#pragma warning disable CS0618
        _obs.Connect(address, _config.OBS.Password);
#pragma warning restore CS0618
        var timeout = 50;
        while (!_obs.IsConnected && timeout-- > 0) Thread.Sleep(100);
        if (!_obs.IsConnected)
            throw new InvalidOperationException("OBS konnte nicht verbunden werden. Prüfe WebSocket-Server, Port und Passwort.");
        Console.WriteLine("OBS verbunden.");
    }

    public ObsSourceSetupResult EnsureBrowserSourceInCurrentScene(
        string idleUrl)
    {
        if (!_obs.IsConnected)
        {
            throw new InvalidOperationException("OBS ist nicht verbunden.");
        }

        var sceneName = _obs.GetCurrentProgramScene();
        var sourceName = _config.Player.BrowserSource;
        var sourceSettings = new JObject
        {
            ["url"] = idleUrl,
            ["width"] = 1920,
            ["height"] = 1080,
            ["fps"] = 30,
            ["shutdown"] = false,
            ["restart_when_active"] = false,
            ["reroute_audio"] = false
        };

        var inputExists = false;

        try
        {
            _obs.GetInputSettings(sourceName);
            inputExists = true;
        }
        catch
        {
        }

        int sceneItemId;
        var addedToScene = false;
        var createdInput = false;

        if (!inputExists)
        {
            sceneItemId = _obs.CreateInput(
                sceneName,
                sourceName,
                "browser_source",
                sourceSettings,
                true);
            createdInput = true;
            addedToScene = true;
        }
        else
        {
            _obs.SetInputSettings(sourceName, sourceSettings, true);

            try
            {
                sceneItemId = _obs.GetSceneItemId(
                    sceneName,
                    sourceName,
                    0);
            }
            catch
            {
                sceneItemId = _obs.CreateSceneItem(
                    sceneName,
                    sourceName,
                    true);
                addedToScene = true;
            }
        }

        _obs.SetSceneItemEnabled(sceneName, sceneItemId, true);

        return new ObsSourceSetupResult(
            sceneName,
            sourceName,
            createdInput,
            addedToScene);
    }

    public bool SceneExists(string sceneName)
    {
        EnsureConnected();
        return _obs.GetSceneList().Scenes.Any(scene =>
            scene.Name.Equals(sceneName, StringComparison.OrdinalIgnoreCase));
    }

    public bool InputExists(string inputName)
    {
        EnsureConnected();
        return _obs.GetInputList(null).Any(input =>
            input.InputName.Equals(inputName, StringComparison.OrdinalIgnoreCase));
    }

    public string[] GetBrowserSourceNames()
    {EnsureConnected();return _obs.GetInputList("browser_source").Select(i=>i.InputName).ToArray();}
    internal static bool IsCommunityOverlayUrl(string? url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri) &&
        uri.Scheme=="http" && (uri.Host=="127.0.0.1" || uri.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase)) && uri.Port==18769 && uri.AbsolutePath=="/";
    public int RefreshCommunityOverlay(HeistOverlayConfig config)
    {
        EnsureConnected();
        var available=GetBrowserSourceNames();
        var names=string.IsNullOrWhiteSpace(config.BrowserSource)
            ? available.Where(name=>!name.Equals(_config.Player.BrowserSource,StringComparison.OrdinalIgnoreCase) &&
                IsCommunityOverlayUrl(_obs.GetInputSettings(name).Settings["url"]?.ToString())).ToArray()
            : new[]{config.BrowserSource.Trim()};
        foreach(var name in names)
        {
            if(name.Equals(_config.Player.BrowserSource,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Die Clip-Player-Quelle wird nicht als Eventquelle neu geladen.");
            if(!available.Contains(name,StringComparer.Ordinal))throw new InvalidOperationException($"Die ausgewählte Event-Browserquelle '{name}' fehlt in OBS.");
            _obs.PressInputPropertiesButton(name,"refreshnocache");
        }
        return names.Length;
    }
    public string[] GetSceneNames()
    {EnsureConnected();return _obs.GetSceneList().Scenes.Select(s=>s.Name).ToArray();}
    public void EnsureCommunityOverlay(HeistOverlayConfig config)
    {
        EnsureConnected();config.Validate();
        if(string.IsNullOrWhiteSpace(config.BrowserSource))throw new InvalidOperationException("Bitte eine eigene Browserquelle auswählen oder benennen.");
        if(config.BrowserSource.Equals(_config.Player.BrowserSource,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Die Clip-Player-Quelle darf nicht als Community-Overlay überschrieben werden.");
        var scene=string.IsNullOrWhiteSpace(config.Scene)?_obs.GetCurrentProgramScene():config.Scene;
        var settings=new JObject{["url"]=AdventureOverlayServer.Url,["width"]=900,["height"]=600,["fps"]=30,["shutdown"]=false,["restart_when_active"]=false};
        if(InputExists(config.BrowserSource))
        {
            if(!GetBrowserSourceNames().Contains(config.BrowserSource,StringComparer.Ordinal))throw new InvalidOperationException("Die ausgewählte Quelle ist keine Browserquelle.");
            _obs.SetInputSettings(config.BrowserSource,settings,true);
            if(!SourceExistsInScene(scene,config.BrowserSource))_obs.CreateSceneItem(scene,config.BrowserSource,true);
        }
        else _obs.CreateInput(scene,config.BrowserSource,"browser_source",settings,true);
        _obs.SetSceneItemEnabled(scene,_obs.GetSceneItemId(scene,config.BrowserSource,0),true);
    }
    public bool SourceExistsInScene(string sceneName, string sourceName)
    {
        EnsureConnected();
        try { _obs.GetSceneItemId(sceneName, sourceName, 0); return true; }
        catch { return false; }
    }

    public bool IsInputMuted(string inputName)
    {
        EnsureConnected();
        return _obs.GetInputMute(inputName);
    }

    public string GetRecordingDirectory()
    {
        EnsureConnected();
        return _obs.GetRecordDirectory();
    }

    public void SetCurrentScene(string sceneName)
    {
        EnsureConnected();
        _obs.SetCurrentProgramScene(sceneName);
    }

    public bool IsStreaming
    {
        get { EnsureConnected(); return _obs.GetStreamStatus().IsActive; }
    }

    public void StartStreaming()
    {
        EnsureConnected();
        if (!IsStreaming) _obs.StartStream();
    }

    private void EnsureConnected()
    {
        if (!_obs.IsConnected)
            throw new InvalidOperationException("OBS ist nicht verbunden.");
    }

    public void SetBrowserUrl(string url)
    {
        if (!_obs.IsConnected) throw new InvalidOperationException("OBS ist nicht verbunden.");
        _obs.SetInputSettings(_config.Player.BrowserSource, new JObject { ["url"] = url }, true);
    }

    public void Dispose()
    {
        if (_obs.IsConnected) _obs.Disconnect();
    }
}

public sealed record ObsSourceSetupResult(
    string SceneName,
    string SourceName,
    bool CreatedInput,
    bool AddedToScene);
