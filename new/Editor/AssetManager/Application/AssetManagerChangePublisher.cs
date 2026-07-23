using System;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application
{
    internal sealed class AssetManagerChangePublisher
    {
        private readonly IAssetManagerDiagnostics _diagnostics;

        internal AssetManagerChangePublisher(
            IAssetManagerDiagnostics diagnostics)
        {
            _diagnostics = diagnostics ??
                           throw new ArgumentNullException(
                               nameof(diagnostics));
        }

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
                catch (Exception exception)
                {
                    _diagnostics.ReportChangeSubscriberFailure(exception);
                }
            }
        }
    }
}
