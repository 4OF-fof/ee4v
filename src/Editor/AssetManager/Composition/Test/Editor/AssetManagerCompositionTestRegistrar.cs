using Ee4v.Testing.Contracts;

namespace Ee4v.AssetManager.Composition.Tests
{
    public sealed class AssetManagerCompositionTestRegistrar :
        IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "AssetManager Composition",
                "AssetManager",
                "Ee4v.AssetManager.Composition.Tests.Editor",
                "AssetManager の Unity adapter と main thread 境界を確認します。",
                order: 390);
        }
    }
}
