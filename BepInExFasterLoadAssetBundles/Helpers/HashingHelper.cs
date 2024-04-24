using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal class HashingHelper
{
    private const int c_BufferSize = 81920;

    public static byte[] HashFile(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, c_BufferSize, FileOptions.SequentialScan);
        return HashStream(fileStream);
    }

    public static byte[] HashStream(Stream stream)
    {
        using var sha1 = new SHA1Managed();

        var array = ArrayPool<byte>.Shared.Rent(c_BufferSize);
        int readBytes;
        while ((readBytes = stream.Read(array, 0, c_BufferSize)) > 0)
        {
            sha1.TransformBlock(array, 0, readBytes, array, 0);
        }

        sha1.TransformFinalBlock([], 0, 0);

        ArrayPool<byte>.Shared.Return(array);

        return sha1.Hash;
    }

    public static string HashToString(byte[] hash)
    {
        Span<char> chars = stackalloc char[40];

        for (var i = 0; i < hash.Length; i++)
        {
            var b = hash[i];
            b.TryFormat(chars[(i * 2)..], out _, "X2");
        }

        return chars.ToString();
    }

    public static void StringToHash(string str, Span<byte> buffer)
    {
        const int length = 40;

        if (str.Length != length)
        {
            throw new ArgumentException("String length is not equals to 40", nameof(str));
        }

        for (var i = 0; i < 40; i += 2)
        {
            var s = str.AsSpan(i, 2);
            buffer[i / 2] = byte.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }
}
