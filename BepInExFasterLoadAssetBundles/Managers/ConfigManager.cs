using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace BepInExFasterLoadAssetBundles.Managers;

internal class ConfigManager
{
    internal ConfigFile Config = null!;
    internal ConfigEntry<int> CacheRetentionDays = null!;

    public ConfigManager()
    {
        Config = new ConfigFile(Path.Combine(
            [
                Application.persistentDataPath,
                nameof(BepInExFasterLoadAssetBundlesPatcher) + ".cfg"
            ]), false);

        CacheRetentionDays = Config.Bind("Cache", "Cache retention (days)", 7,
            new ConfigDescription("Deletes cached bundles after the specified number of days", new AcceptableValueRange<int>(1, 99)));
    }
}
