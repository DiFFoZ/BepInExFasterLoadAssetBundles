using System;
using System.Threading.Tasks;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal static class AsyncHelper
{
    public static void Schedule(Func<Task> func)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await func();
            }

            catch (Exception ex)
            {
                Patcher.Logger.LogError(ex);
            }
        });
    }
}
