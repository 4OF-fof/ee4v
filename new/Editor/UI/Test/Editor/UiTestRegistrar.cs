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
                "UI 基盤の内蔵アイコン解決と direct Label 監査を確認します。",
                order: 200);
        }
    }
}
