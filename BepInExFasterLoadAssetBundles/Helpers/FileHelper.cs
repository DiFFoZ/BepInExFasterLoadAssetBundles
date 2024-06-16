using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal static class FileHelper
{
    public const long c_GBToBytes = 1024 * 1024 * 1024;
    public const long c_MBToBytes = 1024 * 1024;

    public static bool TryDeleteFile(string path, [NotNullWhen(false)] out Exception? exception)
    {
        try
        {
            File.Delete(path);
            exception = null;
            return true;
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
        }
    }

    public static async Task RetryUntilFileIsClosedAsync(string path, int maxTries = 5)
    {
        var tries = maxTries;
        while (--tries > 0)
        {
            try
            {
                using var tempStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException)
            {
                await Task.Delay(1000);
            }
        }
    }
}
