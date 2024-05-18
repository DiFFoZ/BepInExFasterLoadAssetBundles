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

    private static bool s_IsLoadingBundle;

    [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
    [HarmonyPostfix]
    public static void ChainloaderInitialized()
    {
        // BepInEx is ready to load plugins, patching Unity assetbundles
        AsyncHelper.InitUnitySynchronizationContext();
        Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(BepInExFasterLoadAssetBundlesPatcher));

        DeleteOldCache();

        var dataPath = new DirectoryInfo(Application.dataPath).Parent.FullName;
        var outputFolder = Path.Combine(dataPath, "Cache", "AssetBundles");
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        AssetBundleManager = new(outputFolder);
        MetadataManager = new MetadataManager(Path.Combine(outputFolder, "metadata.json"));

        Patch();
    }

    private static void Patch()
    {
        var thisType = typeof(Patcher);
        var harmony = BepInExFasterLoadAssetBundlesPatcher.Harmony;
        var allBinding = AccessTools.all;
        var assetBundleType = typeof(AssetBundle);

        // file
        var filePatchMethod = new HarmonyMethod(thisType.GetMethod(nameof(LoadAssetBundleFromFileFast), allBinding));

        harmony.Patch(AccessTools.Method(assetBundleType, nameof(AssetBundle.LoadFromFile_Internal)),
            prefix: filePatchMethod);

        harmony.Patch(AccessTools.Method(assetBundleType, nameof(AssetBundle.LoadFromFileAsync_Internal)),
            prefix: filePatchMethod);

        // streams
        harmony.Patch(AccessTools.Method(assetBundleType, nameof(AssetBundle.LoadFromStreamInternal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromStreamFast), allBinding)));

        harmony.Patch(AccessTools.Method(assetBundleType, nameof(AssetBundle.LoadFromStreamAsyncInternal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromStreamAsyncFast), allBinding)));
    }

    // Added 2024-04-04, can be removed 2024-05-04
    private static void DeleteOldCache()
    {
        try
        {
            var persistentDataPath = Application.persistentDataPath;
            var outputFolder = Path.Combine(persistentDataPath, "Cache", "AssetBundles");
            if (Directory.Exists(outputFolder))
            {
                Directory.Delete(outputFolder, true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to delete old cache\n{ex}");
        }
    }

    private static void LoadAssetBundleFromFileFast(ref string path)
    {
        // ignore bundle load request if we calling it
        if (s_IsLoadingBundle)
        {
            return;
        }

        try
        {
            using var bundleFileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);

            if (HandleStreamBundle(bundleFileStream, out var newPath))
            {
                path = newPath;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to decompress assetbundle\n{ex}");
        }
    }

    private static bool LoadAssetBundleFromStreamFast(Stream stream, ref AssetBundle? __result)
    {
        if (HandleStreamBundle(stream, out var path))
        {
            using var _ = new SetLoadingFlagTemp();
            __result = AssetBundle.LoadFromFile_Internal(path, 0, 0);
            return false;
        }
        
        return true;
    }

    private static bool LoadAssetBundleFromStreamAsyncFast(Stream stream, ref AssetBundleCreateRequest? __result)
    {
        if (HandleStreamBundle(stream, out var path))
        {
            using var _ = new SetLoadingFlagTemp();
            __result = AssetBundle.LoadFromFileAsync_Internal(path, 0, 0);
            return false;
        }
        
        return true;
    }

    private static bool HandleStreamBundle(Stream stream, out string path)
    {
        var previousPosition = stream.Position;

        try
        {
            return AssetBundleManager.TryRecompressAssetBundle(stream, out path);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to decompress assetbundle\n{ex}");
        }

        stream.Position = previousPosition;
        path = null!;
        return false;
    }

    // hack to not write try/finally in every method
    private readonly struct SetLoadingFlagTemp : IDisposable
    {
        public SetLoadingFlagTemp()
        {
            s_IsLoadingBundle = true;
        }

        public void Dispose()
        {
            s_IsLoadingBundle = false;
        }
    }
}
