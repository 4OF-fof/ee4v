using Ee4v.Core.Testing;

namespace Ee4v.Phase1.Tests
{
    public sealed class Phase1TestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "Phase1",
                "Phase1",
                "Ee4v.Phase1.Tests.Editor",
                "Phase1 の定義登録と bootstrap 復元を確認します。",
                order: 100);
        }
    }
}
