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

    public static byte[] HashFile(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, c_BufferSize, FileOptions.SequentialScan);
        return HashStream(fileStream);
    }

    public static unsafe byte[] HashStream(Stream stream)
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

        var hashArray = new byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(hashArray, hash.u64_0);
        BinaryPrimitives.WriteUInt64LittleEndian(hashArray.AsSpan(8), hash.u64_1);

        return hashArray;
    }

    public static string HashToString(Span<byte> hash)
    {
        Span<char> chars = stackalloc char[hash.Length * 2];

        for (var i = 0; i < hash.Length; i++)
        {
            var b = hash[i];
            b.TryFormat(chars[(i * 2)..], out _, "X2", CultureInfo.InvariantCulture);
        }

        return chars.ToString();
    }

    public static int WriteHash(Span<byte> destination, string hash)
    {
        if ((hash.Length / 2) > destination.Length)
        {
            throw new ArgumentOutOfRangeException("Destination is small to write hash", nameof(destination));
        }

        for (var i = 0; i < hash.Length; i += 2)
        {
            var s = hash.AsSpan(i, 2);
            destination[i / 2] = byte.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return hash.Length / 2;
    }
}
