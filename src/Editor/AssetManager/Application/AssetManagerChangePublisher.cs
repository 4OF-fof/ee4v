using System;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application
{
    internal sealed class AssetManagerChangePublisher
    {
        internal event Action<AssetManagerChange> Changed;

        internal void Publish(AssetManagerChange change)
        {
            var handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<AssetManagerChange> handler in
                     handlers.GetInvocationList())
            {
                try
                {
                    handler(change);
                }
                catch
                {
                }
            }
        }
    }
}
