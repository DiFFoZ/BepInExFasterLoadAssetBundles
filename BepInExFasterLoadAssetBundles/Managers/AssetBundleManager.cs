using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using BepInExFasterLoadAssetBundles.Helpers;
using BepInExFasterLoadAssetBundles.Models;
using UnityEngine;

namespace BepInExFasterLoadAssetBundles.Managers;
internal class AssetBundleManager
{
    private readonly ConcurrentQueue<WorkAsset> m_WorkAssets = new();
    private readonly object m_Lock = new();
    private bool m_IsProcessingQueue;

    public string CachePath { get; }

    public AssetBundleManager(string cachePath)
    {
        CachePath = cachePath;

        if (!Directory.Exists(CachePath))
        {
            Directory.CreateDirectory(CachePath);
            return;
        }

        DeleteTempFiles();
    }

    private void DeleteTempFiles()
    {
        // unity creates tmp files when decompress
        var count = 0;
        try
        {
            foreach (var tempFile in Directory.EnumerateFiles(CachePath, "*.tmp"))
            {
                File.Delete(tempFile);
                count++;
            }
        }
        catch (Exception ex)
        {
            Patcher.Logger.LogError($"Failed to delete temp files\n{ex}");
        }

        if (count > 0)
        {
            Patcher.Logger.LogWarning($"Deleted {count} temp files");
        }
    }

    public bool TryRecompressAssetBundle(ref string path)
    {
        return TryRecompressAssetBundleInternal(ref path, HashingHelper.HashFile(path));
    }

    public bool TryRecompressAssetBundle(FileStream stream, out string path)
    {
        path = string.Copy(stream.Name);
        return TryRecompressAssetBundleInternal(ref path, HashingHelper.HashStream(stream));
    }

    public bool TryRecompressAssetBundleInternal(ref string path, byte[] hash)
    {
        if (!File.Exists(path))
        {
            return false;
        }

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

        if (DriveHelper.HasDriveSpaceOnPath(CachePath, 10))
        {
            m_WorkAssets.Enqueue(new(path, hash));
            StartRunner();
        }
        else
        {
            Patcher.Logger.LogWarning($"Ignoring request of decompressing, because the drive space is less than 10GB");
        }
       
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

    private void StartRunner()
    {
        if (m_IsProcessingQueue)
        {
            return;
        }

        lock (m_Lock)
        {
            if (m_IsProcessingQueue)
            {
                return;
            }

            m_IsProcessingQueue = true;
        }

        AsyncHelper.Schedule(ProcessQueue);
    }

    private async Task ProcessQueue()
    {
        try
        {
            while (m_WorkAssets.TryDequeue(out var work))
            {
                await DecompressAssetBundleAsync(work.Path, work.Hash);
            }
        }
        finally
        {
            lock (m_Lock)
            {
                if (m_IsProcessingQueue)
                {
                    m_IsProcessingQueue = false;
                }
            }
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
        await FileHelper.RetryUntilFileIsClosedAsync(path, 5);
        await AsyncHelper.SwitchToMainThread();

        var op = AssetBundle.RecompressAssetBundleAsync(path, outputPath,
            BuildCompression.UncompressedRuntime, 0, UnityEngine.ThreadPriority.Normal);

        await op.WaitCompletionAsync();
        await AsyncHelper.SwitchToThreadPool();

        if (op.result is not AssetBundleLoadResult.Success || !op.success)
        {
            Patcher.Logger.LogWarning($"Failed to decompress a assetbundle at \"{path}\"\nResult: {op.result}, {op.humanReadableResult}");
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

    private struct WorkAsset
    {
        public WorkAsset(string path, byte[] hash)
        {
            Path = path;
            Hash = hash;
        }

        public string Path { get; set; }
        public byte[] Hash { get; set; }
    }
}
