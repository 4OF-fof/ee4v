using Ee4v.Testing.Infrastructure.Unity;
using Ee4v.Testing.UI;
using UnityEditor;

namespace Ee4v.Testing.Composition
{
    [InitializeOnLoad]
    internal static class TestingBootstrap
    {
        private static bool _initialized;

        static TestingBootstrap()
        {
            EnsureInitialized();
        }

        internal static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            TestingUiDependencies.Configure(
                new UnityFeatureTestCatalog(),
                new FeatureTestRunnerService(
                    new UnityFeatureTestRunnerGateway()));
        }
    }
}
