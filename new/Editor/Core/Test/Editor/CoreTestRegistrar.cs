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
                "Core 基盤とテスト管理機能の成立性を確認します。",
                order: -100);
        }
    }
}
