using System;
using System.IO;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using BepInExFasterLoadAssetBundles.Helpers;
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
        AsyncHelper.InitUnitySynchronizationContext();
        Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(BepInExFasterLoadAssetBundlesPatcher));

        var persistentDataPath = Application.persistentDataPath;
        var outputFolder = Path.Combine(persistentDataPath, "Cache", "AssetBundles");
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        AssetBundleManager = new(outputFolder);
        MetadataManager = new MetadataManager(Path.Combine(outputFolder, "metadata.json"));

        var thisType = typeof(Patcher);
        var harmony = BepInExFasterLoadAssetBundlesPatcher.Harmony;
        var binding = AccessTools.all;

        // file
        var patchMethod = new HarmonyMethod(thisType.GetMethod(nameof(LoadAssetBundleFromFileFast), binding));
        harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromFile_Internal)),
            prefix: patchMethod);

        harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromFileAsync_Internal)),
            prefix: patchMethod);

        // streams
        harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromStreamInternal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromStreamFast), binding)));

        harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromStreamAsyncInternal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromStreamAsyncFast), binding)));
    }

    private static void LoadAssetBundleFromFileFast(ref string path)
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

    private static void LoadAssetBundleFromStreamFast(ref Stream stream)
    {
        if (stream is not FileStream fileStream)
        {
            return;
        }

        var previousPosition = fileStream.Position;

        try
        {
            if (AssetBundleManager.TryRecompressAssetBundle(fileStream, out var path))
            {
                stream = File.OpenRead(path);
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to decompress assetbundle\n{ex}");
        }

        fileStream.Position = previousPosition;
    }

    private static bool LoadAssetBundleFromStreamAsyncFast(Stream stream, out AssetBundleCreateRequest? __result)
    {
        __result = null;
        if (stream is not FileStream fileStream)
        {
            return true;
        }

        var previousPosition = fileStream.Position;

        try
        {
            if (AssetBundleManager.TryRecompressAssetBundle(fileStream, out var path))
            {
                __result = AssetBundle.LoadFromFileAsync(path);
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to decompress assetbundle\n{ex}");
        }

        fileStream.Position = previousPosition;
        return true;
    }
}
