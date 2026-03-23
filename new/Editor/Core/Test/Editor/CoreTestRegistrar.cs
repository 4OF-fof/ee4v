using Ee4v.Core.Testing;

namespace Ee4v.Core.Tests
{
    public sealed class CoreTestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "Core",
                "Core",
                "Ee4v.Core.Tests.Editor",
                "Core の I18N スコープ解決を確認します。",
                order: -100);
        }
    }
}
