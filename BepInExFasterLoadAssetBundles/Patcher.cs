using System;
using System.IO;
using System.Threading;
using BepInEx;
using BepInEx.Bootstrap;
using BepInExFasterLoadAssetBundles.Helpers;
using BepInExFasterLoadAssetBundles.Managers;
using HarmonyLib;
using UnityEngine;

namespace BepInExFasterLoadAssetBundles;
[HarmonyPatch]
internal static class Patcher
{
    internal static AssetBundleManager AssetBundleManager { get; private set; } = null!;
    internal static MetadataManager MetadataManager { get; private set; } = null!;

    [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
    [HarmonyPostfix]
    public static void ChainloaderInitialized()
    {
        // BepInEx is ready to load plugins, patching Unity assetbundles

        var outputFolder = Path.Combine(Paths.CachePath, "AssetBundles");
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        AssetBundleManager = new(outputFolder);
        MetadataManager = new MetadataManager(Path.Combine(outputFolder, "metadata.json"));

        var thisType = typeof(Patcher);
        var harmony = BepInExFasterLoadAssetBundlesPatcher.Harmony;

        harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromFile_Internal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromFileFast))));

        // todo
        /*harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromStreamInternal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromStreamFast))));*/
    }

    public static void LoadAssetBundleFromFileFast(ref string path)
    {
        // mod trying to load assetbundle at null path, buh
        if (path == null)
        {
            return;
        }

        var tempPath = string.Copy(path);
        var success = false;
        try
        {
            success = AssetBundleManager.TryRecompressAssetBundle(ref tempPath);
        }
        catch (Exception ex)
        {
            BepInExFasterLoadAssetBundlesPatcher.Logger.LogError($"Failed to decompress assetbundle\n{ex}");
        }

        if (success)
        {
            path = tempPath;
        }
    }
}
