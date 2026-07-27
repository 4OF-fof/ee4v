using Ee4v.Testing.Contracts;

namespace Ee4v.AssetManager.UI.Tests
{
    public sealed class AssetManagerUiTestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "AssetManager UI",
                "AssetManager",
                "Ee4v.AssetManager.UI.Tests.Editor",
                "AssetManager UI の window state、File Tree 画像 preview、詳細表示、表示用データ変換を確認します。",
                order: 400);
        }
    }
}
