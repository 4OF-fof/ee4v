using Ee4v.Testing.Contracts;

namespace Ee4v.ProjectTabs.Tests
{
    public sealed class ProjectTabsTestRegistrar
        : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "ProjectTabs",
                "ProjectTabs",
                "Ee4v.ProjectTabs.Tests.Editor",
                "Project ウィンドウのタブと履歴管理を確認します。",
                order: 320);
        }
    }
}
