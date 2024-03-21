using System;
using System.IO;
using BepInExFasterLoadAssetBundles.Helpers;
using UnityEngine;

namespace BepInExFasterLoadAssetBundles.Managers;
internal class AssetBundleManager
{
    private string CachePath { get; }

    public AssetBundleManager(string cachePath)
    {
        CachePath = cachePath;

        if (!Directory.Exists(CachePath))
        {
            Directory.CreateDirectory(CachePath);
            return;
        }
    }

    public bool TryRecompressAssetBundle(ref string path)
    {
        try
        {
            byte[] hash;
            using (var fileStream = File.OpenRead(path))
            {
                hash = HashingHelper.HashStream(fileStream);
            }

            var metadata = MetadataManager.Instance.FindMetadataByHash(hash);
            if (metadata != null && metadata.UncompressedAssetBundleName != null)
            {
                var newPath = Path.Combine(CachePath, metadata.UncompressedAssetBundleName);
                if (File.Exists(newPath))
                {
                    path = newPath;
                    return true;
                }

                BepInExFasterLoadAssetBundlesPatcher.Logger.LogWarning($"Failed to find decompressed assetbundle at {newPath}. Probably it was deleted?");
            }

            if (metadata?.ShouldNotDecompress == true)
            {
                return false;
            }

            metadata = new()
            {
                OriginalAssetBundleHash = HashingHelper.HashToString(hash)
            };
            var outputName = Path.GetFileNameWithoutExtension(path) + '_' + metadata.GetHashCode() + ".assetbundle";
            var outputPath = Path.Combine(CachePath, outputName);

            var op = AssetBundle.RecompressAssetBundleAsync(path, outputPath, BuildCompression.UncompressedRuntime, 0, ThreadPriority.High);
            AsyncOperationHelper.WaitUntilOperationComplete(op);

            if (op.result is not AssetBundleLoadResult.Success)
            {
                BepInExFasterLoadAssetBundlesPatcher.Logger.LogWarning($"Failed to decompress a assetbundle at {path}\n{op.humanReadableResult}");
                return false;
            }

            // check if unity returned the same assetbundle (means that assetbundle is already decompressed)
            var shouldDelete = false;
            using (var fileStream = File.OpenRead(outputPath))
            {
                if (hash.AsSpan().SequenceEqual(HashingHelper.HashStream(fileStream)))
                {
                    metadata.ShouldNotDecompress = true;
                    MetadataManager.Instance.AddMetadata(metadata);

                    shouldDelete = true;
                }
            }

            if (shouldDelete)
            {
                File.Delete(outputPath);
                return false;
            }

            path = outputPath;

            metadata.UncompressedAssetBundleName = outputName;
            MetadataManager.Instance.AddMetadata(metadata);

            return true;
        }
        catch (Exception ex)
        {
            BepInExFasterLoadAssetBundlesPatcher.Logger.LogError($"Failed to decompress a assetbundle at {path}\n{ex}");
        }

        return false;
    }
}
