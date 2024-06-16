using System;
using System.Buffers.Binary;
using System.IO;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal static class BundleHelper
{
    public static bool CheckBundleIsAlreadyDecompressed(Stream stream)
    {
        // special thanks to AssetRipper for providing info how bundle header are serialized
        // https://github.com/AssetRipper/AssetRipper/blob/master/Source/AssetRipper.IO.Files/BundleFiles/BundleHeader.cs
        // https://github.com/AssetRipper/AssetRipper/blob/master/Source/AssetRipper.IO.Files/BundleFiles/FileStream/FileStreamBundleHeader.cs
        stream.Seek(0x0, SeekOrigin.Begin);

        // skip magic string
        SkipString(stream);
        stream.Position += 4; // skip version
        SkipString(stream); // skip web version
        SkipString(stream); // skip web min rev
        stream.Position += 8 + 4 + 4; // skip size, compressed block size, uncompressed block size

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

    private static void SkipString(Stream stream)
    {
        // "C" string type (zero-term and the end of string)
        while (stream.ReadByte() != 0)
        {
            continue;
        }
    }
}
