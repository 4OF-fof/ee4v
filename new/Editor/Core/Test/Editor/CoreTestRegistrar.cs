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
                "Core の I18N スコープ解決と localization 監査ルールを確認します。",
                order: -100);
        }
    }
}
