using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal class HashingHelper
{
    private const int c_BufferSize = 16 * (int)FileHelper.c_MBToBytes;

    public static int HashFile(Span<char> destination, string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, c_BufferSize, FileOptions.SequentialScan);
        return WriteHash(destination, fileStream);
    }

    public static unsafe int WriteHash(Span<char> destination, Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);

        var buffer = UnsafeUtility.Malloc(4096, 16, Allocator.Temp);
        var span = new Span<byte>(buffer, 4096);

        var hash = new Hash128();

        int readBytes;
        while ((readBytes = stream.Read(span)) > 0)
        {
            hash.Append(buffer, (ulong)readBytes);
        }

        UnsafeUtility.Free(buffer, Allocator.Temp);

        Span<byte> hashSpan = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(hashSpan, hash.u64_0);
        BinaryPrimitives.WriteUInt64LittleEndian(hashSpan.Slice(8), hash.u64_1);

        return HashToString(destination, hashSpan);
    }

    /// <summary>
    /// Writes user readable hash from bytes
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="hash"></param>
    /// <returns>Written <see langword="char"/> count</returns>
    private static int HashToString(Span<char> destination, ReadOnlySpan<byte> hash)
    {
        for (var i = 0; i < hash.Length; i++)
        {
            hash[i].TryFormat(destination.Slice(i * 2), out _, "X2", CultureInfo.InvariantCulture);
        }

        return hash.Length * 2;
    }
}
