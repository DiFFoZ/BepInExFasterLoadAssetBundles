using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BepInExFasterLoadAssetBundles.Helpers;
using BepInExFasterLoadAssetBundles.Models;
using UnityEngine;

namespace BepInExFasterLoadAssetBundles.Managers;
internal class AssetBundleManager
{
    private readonly ConcurrentQueue<WorkAsset> m_WorkAssets = new();
    private readonly object m_Lock = new();
    private readonly string m_PathForTemp;
    private bool m_IsProcessingQueue;

    public string CachePath { get; }

    public AssetBundleManager(string cachePath)
    {
        CachePath = cachePath;

        if (!Directory.Exists(CachePath))
        {
            Directory.CreateDirectory(CachePath);
        }

        m_PathForTemp = Path.Combine(CachePath, "temp");
        if (!Directory.Exists(m_PathForTemp))
        {
            Directory.CreateDirectory(m_PathForTemp);
        }

        DeleteTempFiles();
    }

    private void DeleteTempFiles()
    {
        var count = 0;
        try
        {
            // unity creates tmp files when decompress (.tmp)
            // and our .assetbundle temp file when getting bundle from embedded resource
            foreach (var tempFile in Directory.EnumerateFiles(CachePath, "*.tmp")
                .Concat(Directory.EnumerateFiles(m_PathForTemp, "*.assetbundle")))
            {
                DeleteFileSafely(ref count, tempFile);
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

        static void DeleteFileSafely(ref int count, string tempFile)
        {
            if (!FileHelper.TryDeleteFile(tempFile, out var exception))
            {
                Patcher.Logger.LogError($"Failed to delete temp file\n{exception}");
                return;
            }

            count++;
        }
    }

    public unsafe bool TryRecompressAssetBundle(Stream stream, [NotNullWhen(true)] out string? path)
    {
        if (BundleHelper.CheckBundleIsAlreadyDecompressed(stream))
        {
            Patcher.Logger.LogInfo("Original bundle is already uncompressed, using it instead");
            path = null;
            return false;
        }

        Span<char> hash = stackalloc char[32];
        HashingHelper.WriteHash(hash, stream);

        path = null;
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
            RecompressAssetBundleInternal(new(path, hash.ToString(), false));
            return false;
        }

        var tempFileName = Guid.NewGuid().ToString("N") + ".assetbundle";
        var tempFile = Path.Combine(m_PathForTemp, tempFileName);

        using (var fs = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.SequentialScan))
        {
            stream.Seek(0, SeekOrigin.Begin);

            if (stream is UnmanagedMemoryStream unmanagedMemoryStream && unmanagedMemoryStream.Length < int.MaxValue)
            {
                var span = new ReadOnlySpan<byte>(unmanagedMemoryStream.PositionPointer, (int)unmanagedMemoryStream.Length);
                fs.Write(span);
            }
            else
            {
                stream.CopyTo(fs);
            }
        }

        RecompressAssetBundleInternal(new(tempFile, hash.ToString(), true));
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

    private bool FindCachedBundleByHash(ReadOnlySpan<char> hash, out string? path)
    {
        path = null!;

        var metadata = Patcher.MetadataManager.FindMetadataByHash(hash);
        if (metadata == null)
        {
            return false;
        }

        if (metadata.ShouldNotDecompress)
        {
            ModifyAccessTimeAndSave(metadata);

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
            Patcher.MetadataManager.DeleteMetadata(metadata);
            return false;
        }

        Patcher.Logger.LogDebug($"Loading uncompressed bundle \"{metadata.UncompressedAssetBundleName}\"");
        path = newPath;

        ModifyAccessTimeAndSave(metadata);

        return true;

        static void ModifyAccessTimeAndSave(Metadata metadata)
        {
            metadata.LastAccessTime = DateTime.Now;
            Patcher.MetadataManager.SaveMetadata(metadata);
        }
    }

    private void RecompressAssetBundleInternal(WorkAsset workAsset)
    {
        if (!DriveHelper.HasDriveSpaceOnPath(CachePath, 10))
        {
            Patcher.Logger.LogWarning($"Ignoring request of decompressing, because the free drive space is less than 10GB");
            return;
        }

        Patcher.Logger.LogDebug($"Queued recompress of \"{Path.GetFileName(workAsset.Path)}\" assetbundle");

        m_WorkAssets.Enqueue(workAsset);
        StartRunner();
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
            OriginalAssetBundleHash = workAsset.Hash,
            LastAccessTime = DateTime.Now,
        };
        var originalFileName = Path.GetFileNameWithoutExtension(workAsset.Path);
        var outputName = originalFileName + '_' + metadata.GetHashCode() + ".assetbundle";
        var outputPath = Path.Combine(CachePath, outputName);
        var buildCompression = BuildCompression.LZ4Runtime;

        Patcher.Logger.LogDebug($"Decompressing \"{originalFileName}\"");

        // when loading assetbundle async via stream, the file can be still in use. Wait a bit for that
        await FileHelper.RetryUntilFileIsClosedAsync(workAsset.Path, 5);
        await AsyncHelper.SwitchToMainThread();

        var op = AssetBundle.RecompressAssetBundleAsync(workAsset.Path, outputPath,
            buildCompression, 0, ThreadPriority.Normal);

        await op.WaitCompletionAsync();

        // we are in main thread, load results locally to make unity happy
        var result = op.result;
        var humanReadableResult = op.humanReadableResult;
        var success = op.success;

        var newHash = GetHashOfFile(outputPath);

        await AsyncHelper.SwitchToThreadPool();

        // delete temp bundle if needed
        if (workAsset.DeleteBundleAfterOperation)
        {
            FileHelper.TryDeleteFile(workAsset.Path, out _);
        }

        Patcher.Logger.LogDebug($"Result of decompression \"{originalFileName}\": {result} ({success}), {humanReadableResult}");
        if (result is not AssetBundleLoadResult.Success || !success)
        {
            Patcher.Logger.LogWarning($"Failed to decompress a assetbundle at \"{workAsset.Path}\"\nResult: {result}, {humanReadableResult}");
            return;
        }

        // check if unity returned the same assetbundle (means that assetbundle is already decompressed)
        if (newHash.Equals(workAsset.Hash, StringComparison.InvariantCultureIgnoreCase))
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

        static string GetHashOfFile(string filePath)
        {
            Span<char> hash = stackalloc char[32];
            HashingHelper.HashFile(hash, filePath);

            return hash.ToString();
        }
    }

    private readonly struct WorkAsset
    {
        public WorkAsset(string path, string hash, bool deleteBundleAfterOperation)
        {
            Path = path;
            Hash = hash;
            DeleteBundleAfterOperation = deleteBundleAfterOperation;
        }

        public string Path { get; }
        public string Hash { get; }
        public bool DeleteBundleAfterOperation { get; }
    }
}
