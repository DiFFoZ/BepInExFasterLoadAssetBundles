using System;
using System.IO;
using BepInExFasterLoadAssetBundles.Helpers;
using UnityEngine;

namespace BepInExFasterLoadAssetBundles.Managers;
internal class AssetBundleManager
{
    public string CachePath { get; }

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
        var originalFileName = Path.GetFileNameWithoutExtension(path);
        byte[] hash = HashingHelper.HashFile(path);

        var metadata = Patcher.MetadataManager.FindMetadataByHash(hash);
        if (metadata != null)
        {
            if (metadata.ShouldNotDecompress || metadata.UncompressedAssetBundleName == null)
            {
                return false;
            }

            var newPath = Path.Combine(CachePath, metadata.UncompressedAssetBundleName);
            if (File.Exists(newPath))
            {
                Patcher.Logger.LogDebug(
                    $"Found uncompressed bundle {metadata.UncompressedAssetBundleName}, loading it instead of {originalFileName}");
                path = newPath;

                metadata.LastAccessTime = DateTime.Now;
                Patcher.MetadataManager.SaveMetadata(metadata);

                return true;
            }

            Patcher.Logger.LogWarning($"Failed to find decompressed assetbundle at {newPath}. Probably it was deleted?");
        }

        metadata = new()
        {
            OriginalAssetBundleHash = HashingHelper.HashToString(hash),
            LastAccessTime = DateTime.Now,
        };
        var outputName = originalFileName + '_' + metadata.GetHashCode() + ".assetbundle";
        var outputPath = Path.Combine(CachePath, outputName);

        var op = AssetBundle.RecompressAssetBundleAsync(path, outputPath,
            BuildCompression.UncompressedRuntime, 0, ThreadPriority.High);
        AsyncOperationHelper.WaitUntilOperationComplete(op);

        if (op.result is not AssetBundleLoadResult.Success)
        {
            Patcher.Logger.LogWarning($"Failed to decompress a assetbundle at {path}\n{op.humanReadableResult}");
            return false;
        }

        // check if unity returned the same assetbundle (means that assetbundle is already decompressed)
        if (hash.AsSpan().SequenceEqual(HashingHelper.HashFile(outputPath)))
        {
            Patcher.Logger.LogDebug($"Assetbundle {originalFileName} is already uncompressed, adding to ignore list");

            metadata.ShouldNotDecompress = true;
            Patcher.MetadataManager.SaveMetadata(metadata);

            DeleteCachedAssetBundle(outputPath);
            return false;
        }

        path = outputPath;

        metadata.UncompressedAssetBundleName = outputName;
        Patcher.MetadataManager.SaveMetadata(metadata);

        Patcher.Logger.LogDebug($"Loading uncompressed bundle {outputName} instead of {originalFileName}");

        return true;
    }

    public void DeleteCachedAssetBundle(string path)
    {
        FileHelper.TryDeleteFile(path, out var fileException);
        if (fileException != null)
        {
            Patcher.Logger.LogError($"Failed to delete uncompressed assetbundle\n{fileException}");
        }
    }
}
