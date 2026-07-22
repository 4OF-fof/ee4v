using Ee4v.Core.Testing;

namespace Ee4v.AssetManager.Tests
{
    public sealed class AssetManagerUiTestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "AssetManager UI",
                "AssetManager",
                "Ee4v.AssetManager.UI.Tests.Editor",
                "AssetManager UI の File Tree 画像 preview と表示用データ変換を確認します。",
                order: 400);
        }
    }
}
