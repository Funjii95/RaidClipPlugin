using Microsoft.Extensions.Configuration;
using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

public class ConfigurationService
{
    public AppConfig Load()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("Config/config.json", optional: false)
            .Build();

        var appConfig = new AppConfig();
        config.Bind(appConfig);

        return appConfig;
    }
}