using Ee4v.Core.Testing;

namespace Ee4v.AssetManager.Api.Tests
{
    public sealed class AssetManagerApiTestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "AssetManager API",
                "AssetManager",
                "Ee4v.AssetManager.Api.Tests.Editor",
                "AssetManager API の DB schema、file 管理、datasource sync を確認します。",
                order: 300);
        }
    }
}
