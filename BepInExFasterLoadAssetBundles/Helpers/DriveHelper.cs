using System.IO;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal static class DriveHelper
{
    public static bool HasDriveSpaceOnPath(string path, long expectedSpaceGB)
    {
        var driveLetter = Path.GetPathRoot(Path.GetFullPath(path));
        var driveInfo = new DriveInfo(driveLetter);

        // Switched to TotalFreeSpace for potential fix for Wine users.
        // AvailableFreeSpace uses user disk quota to get accurate free space
        // and probably because of that Wine reports invalid available free space.

        return driveInfo.TotalFreeSpace > (expectedSpaceGB * FileHelper.c_GBToBytes);
    }
}
