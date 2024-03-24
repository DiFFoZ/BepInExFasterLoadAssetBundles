using System;
using System.Collections.Generic;
using System.IO;
using BepInExFasterLoadAssetBundles.Helpers;
using BepInExFasterLoadAssetBundles.Models;
using Newtonsoft.Json;

namespace BepInExFasterLoadAssetBundles.Managers;
internal class MetadataManager
{
    private readonly string m_MetadataFile;
    private readonly object m_Lock = new();

    private List<Metadata> m_Metadata = null!;

    public MetadataManager(string metadataFile)
    {
        m_MetadataFile = metadataFile;
        LoadFile();
    }

    public Metadata? FindMetadataByHash(byte[] hash)
    {
        Span<byte> tempHash = stackalloc byte[20];

        lock (m_Lock)
        {
            foreach (var metadata in m_Metadata)
            {
                HashingHelper.StringToHash(metadata.OriginalAssetBundleHash, tempHash);
                if (tempHash.SequenceEqual(hash))
                {
                    return metadata;
                }
            }
        }

        return null;
    }

    public void SaveMetadata(Metadata metadata)
    {
        var index = m_Metadata.FindIndex(m => m.OriginalAssetBundleHash == metadata.OriginalAssetBundleHash);

        if (index == -1)
        {
            m_Metadata.Add(metadata);
        }
        else
        {
            m_Metadata[index] = metadata;
        }

        SaveFile();
    }

    private void LoadFile()
    {
        if (!File.Exists(m_MetadataFile))
        {
            m_Metadata = [];
            return;
        }

        try
        {
            m_Metadata = JsonConvert.DeserializeObject<List<Metadata>>(File.ReadAllText(m_MetadataFile)) ?? [];
        }
        catch (Exception ex)
        {
            Patcher.Logger.LogError($"Failed to deserialize metadata.json file\n{ex}");
            m_Metadata = [];
            return;
        }

        if (UpgradeMetadata())
        {
            return;
        }

        DeleteOldBundles();
    }

    /// <summary>
    /// Upgrades metadata file if some changes happened in <see cref="Metadata"/>.
    /// </summary>
    /// <returns><see langword="true"/> when metadata was upgraded, otherwise <see langword="false"/></returns>
    private bool UpgradeMetadata()
    {
        var shouldSave = false;
        foreach (var metadata in m_Metadata)
        {
            var isTimeDefault = metadata.LastAccessTime == default;
            if (isTimeDefault)
            {
                metadata.LastAccessTime = DateTime.Now;
            }

            shouldSave |= isTimeDefault;
        }

        if (shouldSave)
        {
            SaveFile();
        }

        return shouldSave;
    }

    private void SaveFile()
    {
        lock (m_Lock)
        {
            File.WriteAllText(m_MetadataFile, JsonConvert.SerializeObject(m_Metadata));
        }
    }

    private void DeleteOldBundles()
    {
        const int c_DeleteAfterDays = 3;

        for (var i = m_Metadata.Count - 1; i >= 0; i--)
        {
            var metadata = m_Metadata[i];
            if ((DateTime.Now - metadata.LastAccessTime).TotalDays < c_DeleteAfterDays)
            {
                continue;
            }

            m_Metadata.RemoveAt(i);
            if (metadata.UncompressedAssetBundleName == null)
            {
                continue;
            }

            Patcher.Logger.LogInfo($"Deleting unused asset bundle cache {metadata.UncompressedAssetBundleName}");
            Patcher.AssetBundleManager.DeleteCachedAssetBundle(Path.Combine(Patcher.AssetBundleManager.CachePath, metadata.UncompressedAssetBundleName));
        }
    }
}
