using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Ee4v.Mcp
{
    [InitializeOnLoad]
    internal static class UnityMainThreadDispatcher
    {
        private static readonly Queue<Action> Queue = new Queue<Action>();

        static UnityMainThreadDispatcher()
        {
            EditorApplication.update -= Drain;
            EditorApplication.update += Drain;
        }

        internal static Task<T> Run<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var gate = new object();
            var started = false;
            var registration = cancellationToken.Register(() =>
            {
                lock (gate) if (!started) completion.TrySetCanceled();
            });
            _ = completion.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
            lock (Queue)
            {
                Queue.Enqueue(async () =>
                {
                    try
                    {
                        lock (gate)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            started = true;
                        }
                        completion.TrySetResult(await action());
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        completion.TrySetCanceled();
                    }
                    catch (Exception exception)
                    {
                        completion.TrySetException(exception);
                    }
                });
            }

            return completion.Task;
        }

        private static void Drain()
        {
            Action[] pending;
            lock (Queue)
            {
                if (Queue.Count == 0)
                {
                    return;
                }

                pending = Queue.ToArray();
                Queue.Clear();
            }

            for (var index = 0; index < pending.Length; index++)
            {
                pending[index]();
            }
        }
    }
}
