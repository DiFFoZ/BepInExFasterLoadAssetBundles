using System;
using System.Collections.Generic;
using System.IO;
using BepInExFasterLoadAssetBundles.Helpers;
using BepInExFasterLoadAssetBundles.Models;
using Newtonsoft.Json;

namespace BepInExFasterLoadAssetBundles.Managers;
internal class MetadataManager
{
    public static MetadataManager Instance { get; private set; } = null!;

    private readonly string m_MetadataFile;

    private List<Metadata> m_Metadata = null!;

    public MetadataManager(string metadataFile)
    {
        Instance = this;

        m_MetadataFile = metadataFile;
        LoadFile();
    }

    public Metadata? FindMetadataByHash(byte[] hash)
    {
        Span<byte> tempHash = stackalloc byte[20];

        foreach (var metadata in m_Metadata)
        {
            HashingHelper.StringToHash(metadata.OriginalAssetBundleHash, tempHash);
            if (tempHash.SequenceEqual(hash))
            {
                return metadata;
            }
        }

        return null;
    }

    public void AddMetadata(Metadata metadata)
    {
        m_Metadata.RemoveAll(m => m.OriginalAssetBundleHash == metadata.OriginalAssetBundleHash);

        m_Metadata.Add(metadata);
        File.WriteAllText(m_MetadataFile, JsonConvert.SerializeObject(m_Metadata));
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
            BepInExFasterLoadAssetBundlesPatcher.Logger.LogError($"Failed to deserialize metadata.json file\n{ex}");
            m_Metadata = [];
        }
    }
}
