using Ee4v.Testing.Contracts;

namespace Ee4v.HierarchyStyle.Tests
{
    public sealed class HierarchyStyleTestRegistrar
        : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "HierarchyStyle",
                "HierarchyStyle",
                "Ee4v.HierarchyStyle.Tests.Editor",
                "Hierarchyの背景色、アイコン、Alt操作を確認します。",
                order: 310);
        }
    }
}
