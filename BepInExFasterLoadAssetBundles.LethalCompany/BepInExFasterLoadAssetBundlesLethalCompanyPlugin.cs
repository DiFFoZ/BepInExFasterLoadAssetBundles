using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BepInExFasterLoadAssetBundles.LethalCompany;
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("Lethal Company.exe")]
public class BepInExFasterLoadAssetBundlesLethalCompanyPlugin : BaseUnityPlugin
{
    public static BepInExFasterLoadAssetBundlesLethalCompanyPlugin Instance { get; private set; } = null!;
    internal new ManualLogSource Logger { get; private set; } = null!;
    internal Harmony? Harmony { get; set; }

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;
    }
}
