using System;
using System.IO;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal static class FileHelper
{
    public static bool TryDeleteFile(string path, out Exception? exception)
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
}
