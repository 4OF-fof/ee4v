using UnityEditor.TestTools.TestRunner.Api;

namespace Ee4v.Testing.Infrastructure.Unity
{
    internal interface IFeatureTestRunnerGateway
    {
        void RegisterCallbacks(ICallbacks callbacks);

        void UnregisterCallbacks(ICallbacks callbacks);

        string Execute(ExecutionSettings executionSettings);
    }
}
