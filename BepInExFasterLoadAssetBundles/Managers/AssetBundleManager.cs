using System;
using System.IO;
using System.Threading.Tasks;
using BepInExFasterLoadAssetBundles.Helpers;
using BepInExFasterLoadAssetBundles.Models;
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
        return TryRecompressAssetBundleInternal(ref path, null);
    }

    public FileStream? TryRecompressAssetBundle(FileStream stream)
    {
        var path = string.Copy(stream.Name);

        if (TryRecompressAssetBundleInternal(ref path, HashingHelper.HashStream(stream)))
        {
            return File.OpenRead(path);
        }

        return null;
    }

    public bool TryRecompressAssetBundleInternal(ref string path, byte[]? hash)
    {
        hash ??= HashingHelper.HashFile(path);

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
                Patcher.Logger.LogDebug($"Loading uncompressed bundle \"{metadata.UncompressedAssetBundleName}\"");
                path = newPath;

                metadata.LastAccessTime = DateTime.Now;
                Patcher.MetadataManager.SaveMetadata(metadata);

                return true;
            }

            Patcher.Logger.LogWarning($"Failed to find decompressed assetbundle at \"{newPath}\". Probably it was deleted?");
        }

        var nonRefPath = path;
        AsyncHelper.Schedule(() => DecompressAssetBundleAsync(nonRefPath, hash));

        return false;
    }

    public void DeleteCachedAssetBundle(string path)
    {
        FileHelper.TryDeleteFile(path, out var fileException);
        if (fileException != null)
        {
            Patcher.Logger.LogError($"Failed to delete uncompressed assetbundle\n{fileException}");
        }
    }

    private async Task DecompressAssetBundleAsync(string path, byte[] hash)
    {
        var metadata = new Metadata()
        {
            OriginalAssetBundleHash = HashingHelper.HashToString(hash),
            LastAccessTime = DateTime.Now,
        };
        var originalFileName = Path.GetFileNameWithoutExtension(path);
        var outputName = originalFileName + '_' + metadata.GetHashCode() + ".assetbundle";
        var outputPath = Path.Combine(CachePath, outputName);

        // when loading assetbundle async via stream, the file can be still in use. Wait a bit for that
        var tries = 5;
        while (--tries > 0)
        {
            try
            {
                using var tempStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                await Task.Delay(100);
            }
        }

        var op = AssetBundle.RecompressAssetBundleAsync(path, outputPath,
            BuildCompression.UncompressedRuntime, 0, ThreadPriority.High);

        await op.WaitCompletionAsync();

        if (op.result is not AssetBundleLoadResult.Success)
        {
            Patcher.Logger.LogWarning($"Failed to decompress a assetbundle at \"{path}\"\n{op.humanReadableResult}");
            return;
        }

        // check if unity returned the same assetbundle (means that assetbundle is already decompressed)
        if (hash.AsSpan().SequenceEqual(HashingHelper.HashFile(outputPath)))
        {
            Patcher.Logger.LogDebug($"Assetbundle \"{originalFileName}\" is already uncompressed, adding to ignore list");

            metadata.ShouldNotDecompress = true;
            Patcher.MetadataManager.SaveMetadata(metadata);

            DeleteCachedAssetBundle(outputPath);
            return;
        }

        metadata.UncompressedAssetBundleName = outputName;
        Patcher.MetadataManager.SaveMetadata(metadata);
    }
}
