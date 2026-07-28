using System;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using UnityEditor;

namespace Ee4v.AssetManager.Composition
{
    internal sealed class AssetManagerUiSchedulerAdapter : IAssetManagerUiScheduler
    {
        private readonly int _mainThreadId;
        private readonly SynchronizationContext _mainThreadContext;

        public AssetManagerUiSchedulerAdapter()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _mainThreadContext = SynchronizationContext.Current;
        }

        public void RunOnMainThread(Action operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (Thread.CurrentThread.ManagedThreadId ==
                _mainThreadId)
            {
                operation();
                return;
            }

            if (_mainThreadContext != null)
            {
                _mainThreadContext.Post(
                    _ => operation(),
                    null);
                return;
            }

            EditorApplication.delayCall += () => operation();
        }

        public void RunInBackground<T>(
            Func<CancellationToken, T> operation,
            CancellationToken cancellationToken,
            Action<AssetManagerBackgroundResult<T>> completed)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            Task.Run(() => operation(cancellationToken), cancellationToken).ContinueWith(task =>
            {
                var result = new AssetManagerBackgroundResult<T>(
                    task.Status == TaskStatus.RanToCompletion ? task.Result : default(T),
                    task.IsFaulted && task.Exception != null
                        ? task.Exception.GetBaseException()
                        : null,
                    task.IsCanceled);
                RunOnMainThread(() => completed(result));
            });
        }
    }
}
