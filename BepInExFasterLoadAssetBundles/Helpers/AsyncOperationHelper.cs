using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BepInExFasterLoadAssetBundles.Helpers;
internal static class AsyncOperationHelper
{
    public static AsyncOperationAwaiter WaitCompletionAsync<T>(this T op) where T : AsyncOperation
    {
        return new AsyncOperationAwaiter(op);
    }

    public struct AsyncOperationAwaiter : ICriticalNotifyCompletion
    {
        private AsyncOperation? m_AsyncOperation;
        private Action? m_ContinuationAction;

        public AsyncOperationAwaiter(AsyncOperation asyncOperation)
        {
            m_AsyncOperation = asyncOperation;
            m_ContinuationAction = null;
        }

        public readonly AsyncOperationAwaiter GetAwaiter() => this;
        public readonly bool IsCompleted => m_AsyncOperation!.isDone;

        public void GetResult()
        {
            if (m_AsyncOperation != null)
                m_AsyncOperation.completed -= OnCompleted;

            m_AsyncOperation = null;
            m_ContinuationAction = null;
        }

        public void OnCompleted(Action continuation)
        {
            UnsafeOnCompleted(continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            m_ContinuationAction = continuation;
            m_AsyncOperation!.completed += OnCompleted;
        }

        private readonly void OnCompleted(AsyncOperation _)
        {
            m_ContinuationAction?.Invoke();
        }
    }
}
