using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal class HashingHelper
{
    public static byte[] HashStream(Stream stream)
    {
        using var sha1 = SHA1.Create();
        return sha1.ComputeHash(stream);
    }

    public static string HashToString(byte[] hash)
    {
        Span<char> chars = stackalloc char[40];

        Span<char> hexs = stackalloc char[2];
        for (var i = 0; i < hash.Length; i++)
        {
            var b = hash[i];

            b.TryFormat(hexs, out var charsWritten, "X2");
            hexs.CopyTo(chars[(i * 2)..]);
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
