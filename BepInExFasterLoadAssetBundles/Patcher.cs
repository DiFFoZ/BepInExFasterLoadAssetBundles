using System;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using BepInExFasterLoadAssetBundles.Managers;
using HarmonyLib;
using UnityEngine;

namespace BepInExFasterLoadAssetBundles;
[HarmonyPatch]
internal static class Patcher
{
    internal static ManualLogSource Logger { get; private set; } = null!;
    internal static AssetBundleManager AssetBundleManager { get; private set; } = null!;
    internal static MetadataManager MetadataManager { get; private set; } = null!;

    [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
    [HarmonyPostfix]
    public static void ChainloaderInitialized()
    {
        // BepInEx is ready to load plugins, patching Unity assetbundles

        Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(BepInExFasterLoadAssetBundlesPatcher));

        var outputFolder = Path.Combine(Paths.CachePath, "AssetBundles");
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        AssetBundleManager = new(outputFolder);
        MetadataManager = new MetadataManager(Path.Combine(outputFolder, "metadata.json"));

        var thisType = typeof(Patcher);
        var harmony = BepInExFasterLoadAssetBundlesPatcher.Harmony;

        // file
        harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromFile_Internal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromFileFast))));

        harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromFileAsync_Internal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromFileFast))));


        // streams
        harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromStreamInternal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromStreamFast))));

        harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromStreamAsyncInternal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromStreamFast))));
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
            Logger.LogError($"Failed to decompress assetbundle\n{ex}");
        }

        if (success)
        {
            path = tempPath;
        }
    }

    public static void LoadAssetBundleFromStreamFast(ref Stream stream)
    {
        if (stream is not FileStream fileStream)
        {
            return;
        }

        var previousPosition = fileStream.Position;

        try
        {
            var decompressedStream = AssetBundleManager.TryRecompressAssetBundle(fileStream);
            if (decompressedStream != null)
            {
                stream = decompressedStream;
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to decompress assetbundle\n{ex}");
        }

        fileStream.Position = previousPosition;
    }
}
