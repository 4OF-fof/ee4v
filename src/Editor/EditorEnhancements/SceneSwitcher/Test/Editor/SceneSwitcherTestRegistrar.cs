using Ee4v.Testing.Contracts;

namespace Ee4v.SceneSwitcher.Tests
{
    public sealed class SceneSwitcherTestRegistrar
        : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "SceneSwitcher",
                "Scene Switcher",
                "Ee4v.SceneSwitcher.Tests.Editor",
                "Scene一覧の同期、優先表示、検索、並べ替え、新規作成の判断を確認します。",
                order: 330);
        }
    }
}
