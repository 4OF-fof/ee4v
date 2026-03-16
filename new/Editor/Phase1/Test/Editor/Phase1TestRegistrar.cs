using Ee4v.Core.Testing;

namespace Ee4v.Phase1.Tests
{
    public sealed class Phase1TestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "Phase1",
                "Phase1",
                "Ee4v.Phase1.Tests.Editor",
                "Phase1 definitions and bootstrap coverage.",
                order: 100);
        }
    }
}
