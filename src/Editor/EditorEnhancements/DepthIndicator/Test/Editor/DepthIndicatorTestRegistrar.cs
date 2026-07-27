using Ee4v.Testing.Contracts;

namespace Ee4v.DepthIndicator.Tests
{
    public sealed class DepthIndicatorTestRegistrar
        : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "DepthIndicator",
                "DepthIndicator",
                "Ee4v.DepthIndicator.Tests.Editor",
                "Hierarchyの深度インジケーターを確認します。",
                order: 300);
        }
    }
}
