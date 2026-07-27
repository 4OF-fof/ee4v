using Ee4v.Testing.Contracts;

namespace Ee4v.HiddenObjects.Tests
{
    public sealed class HiddenObjectsTestRegistrar
        : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "HiddenObjects",
                "Hidden Objects",
                "Ee4v.HiddenObjects.Tests.Editor",
                "Hierarchy の非表示オブジェクト列挙と復帰判断を確認します。",
                order: 320);
        }
    }
}
