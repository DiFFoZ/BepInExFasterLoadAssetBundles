using System.IO;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal static class DriveHelper
{
    private const long c_GBToBytes = 1024 * 1024 * 1024;

    public static bool HasDriveSpaceOnPath(string path, long expectedSpaceGB)
    {
        var driveLetter = Path.GetPathRoot(Path.GetFullPath(path));
        var driveInfo = new DriveInfo(driveLetter);

        return driveInfo.AvailableFreeSpace > (expectedSpaceGB * c_GBToBytes);
    }
}
