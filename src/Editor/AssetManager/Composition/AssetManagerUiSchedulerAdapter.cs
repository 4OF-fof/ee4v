using System;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using UnityEditor;

namespace Ee4v.AssetManager.Composition
{
    internal sealed class AssetManagerUiSchedulerAdapter : IAssetManagerUiScheduler
    {
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
                EditorApplication.delayCall += () => completed(result);
            });
        }
    }
}
