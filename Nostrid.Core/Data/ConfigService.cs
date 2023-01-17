using Nostrid.Model;

namespace Nostrid.Data;

public class ConfigService
{
    private readonly EventDatabase eventDatabase;

    public ConfigService(EventDatabase eventDatabase)
    {
        this.eventDatabase = eventDatabase;
    }

    private Config mainConfig;
    public Config MainConfig
    {
        get
        {
            return mainConfig ?? (mainConfig = eventDatabase.GetConfig());
        }
        set
        {
            mainConfig = value;
            eventDatabase.SaveConfig(mainConfig);
        }
    }

}

