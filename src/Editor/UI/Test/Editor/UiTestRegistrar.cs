using Ee4v.Testing.Contracts;

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
                "UI 基盤の layout、design token、内蔵アイコン解決、direct Label 監査を確認します。",
                order: 200);
        }
    }
}
