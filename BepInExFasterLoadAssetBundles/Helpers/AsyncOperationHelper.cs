using System.Threading;
using UnityEngine;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal static class AsyncOperationHelper
{
    public static void WaitUntilOperationComplete<T>(T op) where T : AsyncOperation
    {
        while (!op.isDone)
        {
            Thread.Sleep(100);
        }
    }
}
