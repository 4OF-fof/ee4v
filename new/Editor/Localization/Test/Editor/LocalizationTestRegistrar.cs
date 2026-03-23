using Ee4v.Core.Testing;

namespace Ee4v.Localization.Tests
{
    public sealed class LocalizationTestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "Localization",
                "Localization",
                "Ee4v.Localization.Tests.Editor",
                "ローカライズ監査ルールの成立性を確認します。",
                order: 300,
                category: FeatureTestCategory.StaticAudit);
        }
    }
}
