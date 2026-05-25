using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace BepInExFasterLoadAssetBundles.Helpers;

internal static class DriveHelper
{
    private static readonly bool s_IgnoreDriveSpace;

    private delegate void GetDiskFreeSpace(string path, out ulong availableFreeSpace, out ulong totalSize, out ulong totalFreeSpace);
    private static readonly GetDiskFreeSpace s_GetDiskFreeSpace;

    static DriveHelper()
    {
        s_IgnoreDriveSpace = Environment.GetCommandLineArgs()
            .Contains("--ignore-space-check", StringComparer.OrdinalIgnoreCase);

        var method = typeof(DriveInfo).GetMethod("GetDiskFreeSpace", BindingFlags.NonPublic | BindingFlags.Static);
        s_GetDiskFreeSpace = AccessTools.MethodDelegate<GetDiskFreeSpace>(method);
    }

    public static bool HasDriveSpaceOnPath(string path, long expectedSpaceGB)
    {
        if (s_IgnoreDriveSpace)
        {
            return true;
        }

        path = Path.GetFullPath(path);

        s_GetDiskFreeSpace(path, out var availableFreeSpace, out var totalSize, out var totalFreeSpace);

        return availableFreeSpace > (ulong)(expectedSpaceGB * FileHelper.c_GBToBytes);
    }
}
