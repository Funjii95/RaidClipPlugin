using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public class ObsService
{
    private readonly OBSWebsocket _obs;
    private readonly AppConfig _config;

    public ObsService(AppConfig config)
    {
        _config = config;
        _obs = new OBSWebsocket();
    }

    public void Connect()
    {
        string address = $"ws://{_config.OBS.Host}:{_config.OBS.Port}";

        Console.WriteLine($"Verbinde mit OBS: {address}");

#pragma warning disable CS0618
        _obs.Connect(address, _config.OBS.Password);
#pragma warning restore CS0618

        Console.WriteLine("✅ Mit OBS verbunden.");
    }

    public void SetBrowserUrl(string url)
    {
        JObject settings = new JObject
        {
            ["url"] = url
        };

        _obs.SetInputSettings(
            _config.Player.BrowserSource,
            settings,
            true);

        Console.WriteLine("✅ Browserquelle aktualisiert.");
    }

    public void Disconnect()
    {
        if (_obs.IsConnected)
        {
            _obs.Disconnect();
            Console.WriteLine("🔌 Von OBS getrennt.");
        }
    }

    public OBSWebsocket Client => _obs;
}