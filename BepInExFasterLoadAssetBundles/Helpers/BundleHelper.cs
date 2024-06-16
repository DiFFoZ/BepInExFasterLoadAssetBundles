using System;
using System.Buffers.Binary;
using System.IO;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal static class BundleHelper
{
    public static bool CheckBundleIsAlreadyDecompressed(Stream stream)
    {
        // special thanks to AssetRipper for providing info how bundle header are serialized
        // https://github.com/AssetRipper/AssetRipper/blob/master/Source/AssetRipper.IO.Files/BundleFiles/CompressionType.cs
        stream.Seek(0x2d, SeekOrigin.Begin);

        Span<byte> buffer = stackalloc byte[4];
        stream.Read(buffer);

        var flags = BinaryPrimitives.ReadInt32BigEndian(buffer);
        var compressionType = flags & 0x3f;

        // 0 - none (uncompressed)
        // 1 - LZMA
        // 2 - LZ4
        // 3 - LZ4HC
        return compressionType is 0 or 2;
    }
}
