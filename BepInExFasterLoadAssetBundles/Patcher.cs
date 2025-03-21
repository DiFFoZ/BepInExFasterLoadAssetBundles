using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
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

        // file
        var patchMethod = new HarmonyMethod(thisType.GetMethod(nameof(LoadAssetBundleFromFileFast), allBinding));
        var assetBundleType = typeof(AssetBundle);

        string[] loadNames = [nameof(AssetBundle.LoadFromFile), nameof(AssetBundle.LoadFromFileAsync)];
        foreach (var loadName in loadNames)
        {
            harmony.Patch(AccessTools.Method(assetBundleType, loadName, [typeof(string)]),
                prefix: patchMethod);

            harmony.Patch(AccessTools.Method(assetBundleType, loadName, [typeof(string), typeof(uint)]),
               prefix: patchMethod);

            harmony.Patch(AccessTools.Method(assetBundleType, loadName, [typeof(string), typeof(uint), typeof(ulong)]),
               prefix: patchMethod);
        }

        // streams
        harmony.Patch(AccessTools.Method(assetBundleType, nameof(AssetBundle.LoadFromStreamInternal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromStreamFast), allBinding)));

        harmony.Patch(AccessTools.Method(assetBundleType, nameof(AssetBundle.LoadFromStreamAsyncInternal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromStreamAsyncFast), allBinding)));

        // bytes
        harmony.Patch(AccessTools.Method(assetBundleType, nameof(AssetBundle.LoadFromMemory_Internal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromMemoryFast), allBinding)));
    }

    private static void LoadAssetBundleFromFileFast(ref string path)
    {
        // mod trying to load assetbundle at null path, buh
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            using var bundleFileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 16 * 1024 * 1024, FileOptions.SequentialScan);

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
            __result = AssetBundle.LoadFromFile_Internal(path, 0, 0);
            return false;
        }

        return true;
    }

    private static bool LoadAssetBundleFromStreamAsyncFast(Stream stream, ref AssetBundleCreateRequest? __result)
    {
        if (HandleStreamBundle(stream, out var path))
        {
            __result = AssetBundle.LoadFromFileAsync_Internal(path, 0, 0);
            return false;
        }
        
        return true;
    }

    private static bool LoadAssetBundleFromMemoryFast(byte[] binary, ref AssetBundle? __result)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(binary.Length);
        binary.CopyTo(buffer, 0);

        using var memoryStream = new MemoryStream(buffer, 0, binary.Length);

        var result = HandleStreamBundle(memoryStream, out var path);

        ArrayPool<byte>.Shared.Return(buffer);

        if (result)
        {
            __result = AssetBundle.LoadFromFile_Internal(path, 0, 0);
            return false;
        }

        return true;
    }

    private static bool HandleStreamBundle(Stream stream, [NotNullWhen(true)] out string? path)
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
}
