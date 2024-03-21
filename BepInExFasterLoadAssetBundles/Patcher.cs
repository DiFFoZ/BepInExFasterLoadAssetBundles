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
    private static string OutputFolder { get; } = Path.Combine(Paths.CachePath, "AssetBundles");
    private static AssetBundleManager AssetBundleManager { get; set; } = null!;

    [HarmonyPatch(typeof(Chainloader), nameof(Chainloader.Initialize))]
    [HarmonyPostfix]
    public static void ChainloaderInit()
    {
        if (!Directory.Exists(OutputFolder))
        {
            Directory.CreateDirectory(OutputFolder);
        }

        new MetadataManager(Path.Combine(OutputFolder, "metadata.json"));
        AssetBundleManager = new(OutputFolder);

        var thisType = typeof(Patcher);
        var harmony = BepInExFasterLoadAssetBundlesPatcher.Harmony;

        harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromFile_Internal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromFileFast))));

        // todo
        /*harmony.Patch(AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadFromStreamInternal)),
            prefix: new(thisType.GetMethod(nameof(LoadAssetBundleFromStreamFast))));*/
    }

    /*public static void LoadAssetBundleFromStreamFast(ref Stream stream)
    {
        var tempPath = Path.GetTempPath();
        using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);

        stream.CopyTo(fileStream);
        fileStream.Position = 0;

        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(fileStream);
        fileStream.Position = 0;

        var outputPath = Path.Combine(OutputFolder, Path.GetFileName(HashToString(hash)));
        if (File.Exists(outputPath))
        {
            return AssetBundle.LoadFromFile(outputPath);
        }
    }*/

    public static void LoadAssetBundleFromFileFast(ref string path)
    {
        // mod trying to load assetbundle at null path, buh
        if (path == null)
        {
            return;
        }

        var tempPath = string.Copy(path);
        var success = AssetBundleManager.TryRecompressAssetBundle(ref tempPath);

        if (success)
        {
            path = tempPath;
        }
    }
}
