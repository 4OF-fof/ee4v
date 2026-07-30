using System.Collections.Generic;
using Ee4v.Testing.Application;
using Ee4v.Testing.Contracts;

namespace Ee4v.Testing.Infrastructure.Unity
{
    internal sealed class UnityFeatureTestCatalog : IFeatureTestCatalog
    {
        public IReadOnlyList<FeatureTestDescriptor> Refresh()
        {
            return FeatureTestRegistry.Refresh();
        }
    }
}
