using Ee4v.Core.Testing;

namespace Ee4v.UI.Tests
{
    public sealed class UiTestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "UI",
                "UI",
                "Ee4v.UI.Tests.Editor",
                "enum 管理された Unity 内蔵アイコンの解決可否を確認します。",
                order: 200);
        }
    }
}
