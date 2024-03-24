using System;

namespace BepInExFasterLoadAssetBundles.Models;
internal class Metadata
{
    public string? UncompressedAssetBundleName { get; set; }

    public string OriginalAssetBundleHash { get; set; } = null!;

    public bool ShouldNotDecompress { get; set; }

    public DateTime LastAccessTime { get; set; }
}
