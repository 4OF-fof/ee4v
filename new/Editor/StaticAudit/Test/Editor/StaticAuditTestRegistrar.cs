using Ee4v.Core.Testing;

namespace Ee4v.StaticAudit.Tests
{
    public sealed class StaticAuditTestRegistrar : IFeatureTestRegistrar
    {
        public FeatureTestDescriptor CreateDescriptor()
        {
            return new FeatureTestDescriptor(
                "StaticAudit",
                "Static Audit",
                "Ee4v.StaticAudit.Tests.Editor",
                "静的監査ルールの成立性を確認します。",
                order: 300,
                category: FeatureTestCategory.StaticAudit);
        }
    }
}
