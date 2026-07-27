using System.Collections.Generic;
using Ee4v.Testing.Application;
using Ee4v.Testing.Contracts;

namespace Ee4v.Testing.Infrastructure.Unity
{
    internal static class UnityTestingInfrastructure
    {
        internal static IFeatureTestCatalog CreateCatalog()
        {
            return new UnityFeatureTestCatalog();
        }

        internal static IFeatureTestRunner CreateRunner()
        {
            return new FeatureTestRunnerService();
        }
    }

    internal sealed class UnityFeatureTestCatalog : IFeatureTestCatalog
    {
        public IReadOnlyList<FeatureTestDescriptor> Refresh()
        {
            return FeatureTestRegistry.Refresh();
        }
    }
}
