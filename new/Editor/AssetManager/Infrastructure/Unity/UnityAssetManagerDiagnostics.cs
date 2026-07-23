using System;
using Ee4v.AssetManager.Application.Ports;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure.Unity
{
    internal sealed class UnityAssetManagerDiagnostics
        : IAssetManagerDiagnostics
    {
        public void ReportChangeSubscriberFailure(Exception exception)
        {
            if (exception != null)
            {
                Debug.LogException(exception);
            }
        }
    }
}
