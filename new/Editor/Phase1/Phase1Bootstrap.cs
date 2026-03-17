using Ee4v.Core.Internal;
using UnityEditor;

namespace Ee4v.Phase1
{
    [InitializeOnLoad]
    internal static class Phase1Bootstrap
    {
        private static bool _initialized;

        static Phase1Bootstrap()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            FeatureBootstrapContract.Initialize(
                "Phase1",
                typeof(Phase1Definitions),
                Phase1Definitions.RegisterAll,
                Phase1StubBootstrap.RegisterAll);
        }
    }
}
