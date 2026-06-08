using System.Linq;
using Ee4v.Core.Testing;
using Ee4v.Core.Testing.StaticAnalysis;
using NUnit.Framework;

namespace Ee4v.UI.Tests
{
    public sealed class UiLabelAuditTests
    {
        [Test]
        [FeatureTestCase(
            "direct Label 利用が許可対象だけに限定される",
            "package 全体を走査し、UiTextFactory 実装以外に direct Label / Label 継承が存在しないことを確認します。",
            order: 210,
            category: FeatureTestCategory.Ui)]
        public void SourceLabelAuditService_Analyze_ReturnsNoViolations()
        {
            var report = SourceLabelAuditService.Analyze();

            Assert.That(
                report.Violations,
                Is.Empty,
                string.Join("\n", report.Violations.Select(violation => violation.RelativePath)));
        }
    }
}
