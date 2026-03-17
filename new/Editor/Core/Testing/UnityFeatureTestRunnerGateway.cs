using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Ee4v.Core.Testing
{
    internal sealed class UnityFeatureTestRunnerGateway : IFeatureTestRunnerGateway
    {
        private readonly TestRunnerApi _api;

        public UnityFeatureTestRunnerGateway()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
        }

        public void RegisterCallbacks(ICallbacks callbacks)
        {
            _api.RegisterCallbacks(callbacks);
        }

        public void UnregisterCallbacks(ICallbacks callbacks)
        {
            _api.UnregisterCallbacks(callbacks);
        }

        public string Execute(ExecutionSettings executionSettings)
        {
            return _api.Execute(executionSettings);
        }
    }
}
