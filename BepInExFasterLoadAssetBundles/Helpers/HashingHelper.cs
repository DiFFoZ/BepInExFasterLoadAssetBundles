using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal class HashingHelper
{
    public static byte[] HashFile(string path)
    {
        using var fileStream = File.OpenRead(path);
        using var sha1 = new SHA1Managed();
        return sha1.ComputeHash(fileStream);
    }

    public static string HashToString(byte[] hash)
    {
        Span<char> chars = stackalloc char[40];

        for (var i = 0; i < hash.Length; i++)
        {
            var b = hash[i];
            b.TryFormat(chars[(i * 2)..], out var charsWritten, "X2");
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
