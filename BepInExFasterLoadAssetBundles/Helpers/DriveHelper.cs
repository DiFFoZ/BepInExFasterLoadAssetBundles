using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BepInExFasterLoadAssetBundles.Helpers;

internal static class DriveHelper
{
    private static readonly bool s_IgnoreDriveSpace;

    static DriveHelper()
    {
        s_IgnoreDriveSpace = Environment.GetCommandLineArgs()
            .Contains("--ignore-space-check", StringComparer.OrdinalIgnoreCase);
    }

    public static unsafe bool HasDriveSpaceOnPath(string path, long expectedSpaceGB)
    {
        if (s_IgnoreDriveSpace)
        {
            return true;
        }

        path = Path.GetFullPath(path);

        var driveLetter = Path.GetPathRoot(path);
        var driveInfo = new DriveInfo(driveLetter);
        var driveTotalFreeSpace = driveInfo.TotalFreeSpace;

        Patcher.Logger.LogInfo($"Received {driveTotalFreeSpace} bytes from drive info");

        // Check for free space from windows API, as drive info may not work on some linux distros
        if (Application.platform == RuntimePlatform.WindowsPlayer)
        {
            var result = WindowsAPI.GetDiskFreeSpaceEx(path,
             out var lpFreeBytesAvailableToCaller, out var lpTotalNumberOfBytes, out var lpTotalNumberOfFreeBytes);

            if (!result)
            {
                var error = Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
                Patcher.Logger.LogError(error.ToString() ?? "Unknown error :(");
            }
            else
            {
                Patcher.Logger.LogInfo($"lpFreeBytesAvailableToCaller: {lpFreeBytesAvailableToCaller}\nlpTotalNumberOfBytes: {lpTotalNumberOfBytes}\nlpTotalNumberOfFreeBytes: {lpTotalNumberOfFreeBytes}");
                driveTotalFreeSpace = unchecked((long)lpFreeBytesAvailableToCaller);
            }
        }

        return driveTotalFreeSpace > (expectedSpaceGB * FileHelper.c_GBToBytes);
    }

    private static class WindowsAPI
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static unsafe extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
         out ulong lpFreeBytesAvailableToCaller,
         out ulong lpTotalNumberOfBytes,
         out ulong lpTotalNumberOfFreeBytes);
    }
}
