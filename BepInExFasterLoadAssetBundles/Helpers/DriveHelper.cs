using System.IO;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal static class DriveHelper
{
    public static bool HasDriveSpaceOnPath(string path, long expectedSpaceGB)
    {
        var driveLetter = Path.GetPathRoot(Path.GetFullPath(path));
        var driveInfo = new DriveInfo(driveLetter);

        return driveInfo.AvailableFreeSpace > (expectedSpaceGB * FileHelper.c_GBToBytes);
    }
}
