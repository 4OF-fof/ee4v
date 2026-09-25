using System;
using System.Collections.Generic;
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

        internal static Task<T> Run<T>(Func<Task<T>> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var completion = new TaskCompletionSource<T>();
            lock (Queue)
            {
                Queue.Enqueue(async () =>
                {
                    try
                    {
                        completion.TrySetResult(await action());
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
