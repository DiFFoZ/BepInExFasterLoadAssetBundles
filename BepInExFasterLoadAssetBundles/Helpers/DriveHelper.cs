using System;
using System.IO;
using System.Linq;

namespace BepInExFasterLoadAssetBundles.Helpers;

internal static class DriveHelper
{
    private static readonly bool s_IgnoreDriveSpace;

    static DriveHelper()
    {
        s_IgnoreDriveSpace = Environment.GetCommandLineArgs()
            .Contains("--ignore-space-check", StringComparer.OrdinalIgnoreCase);
    }

    public static bool HasDriveSpaceOnPath(string path, long expectedSpaceGB)
    {
        if (s_IgnoreDriveSpace)
        {
            return true;
        }

        var driveLetter = Path.GetPathRoot(Path.GetFullPath(path));
        var driveInfo = new DriveInfo(driveLetter);

        // Switched to TotalFreeSpace for potential fix for Wine users.
        // AvailableFreeSpace uses user disk quota to get accurate free space
        // and probably because of that Wine reports invalid available free space.

        return driveInfo.TotalFreeSpace > (expectedSpaceGB * FileHelper.c_GBToBytes);
    }
}
