using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static BepInExFasterLoadAssetBundles.Helpers.AsyncOperationHelper;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal static class AsyncHelper
{
    private static UnitySynchronizationContext s_SynchronizationContext = null!;
    private static int s_MainThreadId = -1;

    public static void InitUnitySynchronizationContext()
    {
        s_SynchronizationContext = (UnitySynchronizationContext)SynchronizationContext.Current;
        s_MainThreadId = Thread.CurrentThread.ManagedThreadId;
    }

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

    public static SwitchToMainThreadAwaiter SwitchToMainThread() => new();

    public readonly struct SwitchToMainThreadAwaiter : ICriticalNotifyCompletion
    {
        private static readonly SendOrPostCallback s_OnPostAction = OnPost;

        public readonly SwitchToMainThreadAwaiter GetAwaiter() => this;
        public readonly bool IsCompleted => Thread.CurrentThread.ManagedThreadId == s_MainThreadId;

        public void GetResult()
        { }

        public void OnCompleted(Action continuation)
        {
            UnsafeOnCompleted(continuation);
        }

        public readonly void UnsafeOnCompleted(Action continuation)
        {
            s_SynchronizationContext.Post(s_OnPostAction, continuation);
        }

        private static void OnPost(object state)
        {
            var action = state as Action;
            action?.Invoke();
        }
    }
}
