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

    public void TryRecompressAssetBundle(ref string path)
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
                return;
            }

            Console.WriteLine("Unable to find decompressed assetbundle");
        }

        if (metadata?.ShouldNotDecompress == true)
        {
            Console.WriteLine("Ignored decompress");
            return;
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
            Console.WriteLine(op.humanReadableResult);
            return;
        }

        var shouldDelete = false;
        using (var fileStream = File.OpenRead(outputPath))
        {
            if (hash.AsSpan().SequenceEqual(HashingHelper.HashStream(fileStream)))
            {
                Console.WriteLine("SEQ EQUAL");

                metadata.ShouldNotDecompress = true;
                MetadataManager.Instance.AddMetadata(metadata);

                shouldDelete = true;
            }
        }

        if (shouldDelete)
        {
            File.Delete(outputPath);
            return;
        }

        path = outputPath;

        metadata.UncompressedAssetBundleName = outputName;
        MetadataManager.Instance.AddMetadata(metadata);
    }
}
