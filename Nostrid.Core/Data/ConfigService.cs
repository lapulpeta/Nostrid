using Nostrid.Misc;
using Nostrid.Model;
using System.Net;

namespace Nostrid.Data;

public class ConfigService
{
    private readonly EventDatabase eventDatabase;

    private readonly IWebProxy DefaultProxy;

    public ConfigService(EventDatabase eventDatabase)
    {
        this.eventDatabase = eventDatabase;
        DefaultProxy = HttpClient.DefaultProxy;
        ConfigureProxy();
    }

    private Config mainConfig;
    public Config MainConfig
    {
        get
        {
            return mainConfig ??= eventDatabase.GetConfig();
        }
        set
        {
            mainConfig = value;
            eventDatabase.SaveConfig(mainConfig);
            ConfigureProxy();
        }
    }

    public void Save()
    {
        eventDatabase.SaveConfig(mainConfig);
    }

    private void ConfigureProxy()
    {
        if (Utils.IsWasm() || MainConfig.ProxyUri.IsNullOrEmpty())
        {
            HttpClient.DefaultProxy = DefaultProxy;
            return;
        }

        try
        {
            NetworkCredential? credentials = null;
            if (MainConfig.ProxyUser.IsNotNullOrEmpty())
            {
                credentials = new NetworkCredential(MainConfig.ProxyUser, MainConfig.ProxyPassword);
            }
            var webProxy = new WebProxy(new Uri(MainConfig.ProxyUri.Trim()))
            {
                Credentials = credentials
            };

            HttpClient.DefaultProxy = webProxy;
        }
        catch
        {
            HttpClient.DefaultProxy = DefaultProxy;
        }
    }
}

