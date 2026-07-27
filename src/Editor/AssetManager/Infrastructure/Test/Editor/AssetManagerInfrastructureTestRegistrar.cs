using Ee4v.AssetManager.Contracts;
using Ee4v.Testing.Contracts;

namespace Ee4v.AssetManager.Infrastructure.Tests
{
    public sealed class AssetManagerInfrastructureTestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "AssetManager Infrastructure",
                "AssetManager",
                "Ee4v.AssetManager.Infrastructure.Tests.Editor",
                "AssetManager Infrastructure の DB schema、file adapter、datasource sync を確認します。",
                order: 300);
        }
    }
}
