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

    public bool TryRecompressAssetBundle(Stream stream, out string path)
    {
        var hash = HashingHelper.HashStream(stream);

        path = null!;
        if (FindCachedBundleByHash(hash, out var newPath))
        {
            if (newPath != null)
            {
                path = newPath;
                return true;
            }

            Patcher.Logger.LogDebug("Found assetbundle metadata, but path was null. Probably bundle is already uncompressed!");
            return false;
        }

        if (stream is FileStream fileStream)
        {
            path = string.Copy(fileStream.Name);
            RecompressAssetBundleInternal(new(path, hash, false));
            return false;
        }

        // copy stream to temp file
        var tempDirectory = Path.Combine(CachePath, "temp");
        if (!Directory.Exists(tempDirectory))
        {
            Directory.CreateDirectory(tempDirectory);
        }

        var name = Guid.NewGuid().ToString("N") + ".assetbundle";
        var tempFile = Path.Combine(tempDirectory, name);

        using (var fs = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write))
        {
            stream.Seek(0, SeekOrigin.Begin);
            stream.CopyTo(fs);
        }

        RecompressAssetBundleInternal(new(tempFile, hash, true));
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

    private bool FindCachedBundleByHash(byte[] hash, out string? path)
    {
        path = null!;

        var metadata = Patcher.MetadataManager.FindMetadataByHash(hash);
        if (metadata == null)
        {
            return false;
        }

        if (metadata.ShouldNotDecompress)
        {
            // note: returning null path
            return true;
        }

        if (metadata.UncompressedAssetBundleName == null)
        {
            return false;
        }

        var newPath = Path.Combine(CachePath, metadata.UncompressedAssetBundleName);
        if (!File.Exists(newPath))
        {
            Patcher.Logger.LogWarning($"Failed to find decompressed assetbundle at \"{newPath}\". Probably it was deleted?");
            return false;
        }

        Patcher.Logger.LogDebug($"Loading uncompressed bundle \"{metadata.UncompressedAssetBundleName}\"");
        path = newPath;

        metadata.LastAccessTime = DateTime.Now;
        Patcher.MetadataManager.SaveMetadata(metadata);

        return true;
    }

    private void RecompressAssetBundleInternal(WorkAsset workAsset)
    {
        if (!File.Exists(workAsset.Path))
        {
            return;
        }

        if (DriveHelper.HasDriveSpaceOnPath(CachePath, 10))
        {
            Patcher.Logger.LogDebug($"Queued recompress of \"{Path.GetFileName(workAsset.Path)}\" assetbundle");

            m_WorkAssets.Enqueue(workAsset);
            StartRunner();
            return;
        }

        Patcher.Logger.LogWarning($"Ignoring request of decompressing, because the free drive space is less than 10GB");
        return;
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
                await DecompressAssetBundleAsync(work);
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

    private async Task DecompressAssetBundleAsync(WorkAsset workAsset)
    {
        var metadata = new Metadata()
        {
            OriginalAssetBundleHash = HashingHelper.HashToString(workAsset.Hash),
            LastAccessTime = DateTime.Now,
        };
        var originalFileName = Path.GetFileNameWithoutExtension(workAsset.Path);
        var outputName = originalFileName + '_' + metadata.GetHashCode() + ".assetbundle";
        var outputPath = Path.Combine(CachePath, outputName);

        // when loading assetbundle async via stream, the file can be still in use. Wait a bit for that
        await FileHelper.RetryUntilFileIsClosedAsync(workAsset.Path, 5);
        await AsyncHelper.SwitchToMainThread();

        var op = AssetBundle.RecompressAssetBundleAsync(workAsset.Path, outputPath,
            BuildCompression.UncompressedRuntime, 0, ThreadPriority.Normal);

        await op.WaitCompletionAsync();

        // we are in main thread, load results locally to make unity happy
        var result = op.result;
        var humanReadableResult = op.humanReadableResult;
        var success = op.success;

        await AsyncHelper.SwitchToThreadPool();

        // delete temp bundle if needed
        if (workAsset.DeleteBundleAfterOperation)
        {
            FileHelper.TryDeleteFile(workAsset.Path, out _);
        }

        if (result is not AssetBundleLoadResult.Success || !success)
        {
            Patcher.Logger.LogWarning($"Failed to decompress a assetbundle at \"{workAsset.Path}\"\nResult: {result}, {humanReadableResult}");
            return;
        }

        // check if unity returned the same assetbundle (means that assetbundle is already decompressed)
        if (workAsset.Hash.AsSpan().SequenceEqual(HashingHelper.HashFile(outputPath)))
        {
            Patcher.Logger.LogDebug($"Assetbundle \"{originalFileName}\" is already uncompressed, adding to ignore list");

            metadata.ShouldNotDecompress = true;
            Patcher.MetadataManager.SaveMetadata(metadata);

            DeleteCachedAssetBundle(outputPath);
            return;
        }

        Patcher.Logger.LogDebug($"Assetbundle \"{originalFileName}\" is now uncompressed!");

        metadata.UncompressedAssetBundleName = outputName;
        Patcher.MetadataManager.SaveMetadata(metadata);
    }

    private readonly struct WorkAsset
    {
        public WorkAsset(string path, byte[] hash, bool deleteBundleAfterOperation)
        {
            Path = path;
            Hash = hash;
            DeleteBundleAfterOperation = deleteBundleAfterOperation;
        }

        public string Path { get; }
        public byte[] Hash { get; }
        public bool DeleteBundleAfterOperation { get; }
    }
}
