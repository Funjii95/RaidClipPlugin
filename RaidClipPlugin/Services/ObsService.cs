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
