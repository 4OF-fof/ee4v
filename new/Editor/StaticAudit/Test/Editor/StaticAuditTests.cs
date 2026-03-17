using System.Linq;
using Ee4v.Core.Testing;
using Ee4v.Core.Testing.StaticAnalysis;
using NUnit.Framework;

namespace Ee4v.StaticAudit.Tests
{
    public sealed class StaticAuditTests
    {
        [Test]
        [FeatureTestCase(
            "direct Label 利用が許可対象だけに限定される",
            "package 全体を走査し、UiTextFactory 実装以外に direct Label / Label 継承が存在しないことを確認します。",
            order: 10,
            category: FeatureTestCategory.StaticAudit)]
        public void SourceLabelAuditService_Analyze_ReturnsNoViolations()
        {
            var report = SourceLabelAuditService.Analyze();

            Assert.That(
                report.Violations,
                Is.Empty,
                string.Join("\n", report.Violations.Select(violation => violation.RelativePath)));
        }

        [Test]
        [FeatureTestCase(
            "ローカライズに重複キーがない",
            "locale / scope ごとにキー重複がなく、一意に定義されていることを確認します。",
            order: 20,
            category: FeatureTestCategory.StaticAudit)]
        public void LocalizationStaticAuditService_Analyze_ReturnsNoDuplicateKeys()
        {
            var report = LocalizationStaticAuditService.Analyze();

            Assert.That(
                report.DuplicateKeys,
                Is.Empty,
                string.Join(
                    "\n",
                    report.DuplicateKeys.Select(issue =>
                        issue.Locale + "/" + issue.Scope + ": " + issue.Key +
                        " (" + issue.OriginalFilePath + " -> " + issue.DuplicateFilePath + ")")));
        }

        [Test]
        [FeatureTestCase(
            "ローカライズ不足キーがない",
            "コード参照される i18n key が各 locale / scope に不足なく定義されていることを確認します。",
            order: 21,
            category: FeatureTestCategory.StaticAudit)]
        public void LocalizationStaticAuditService_Analyze_ReturnsNoMissingKeys()
        {
            var report = LocalizationStaticAuditService.Analyze();

            Assert.That(
                report.MissingKeys,
                Is.Empty,
                string.Join(
                    "\n",
                    report.MissingKeys.Select(issue =>
                        issue.Locale + "/" + issue.Scope + ": " + issue.Key +
                        " (" + issue.FilePath + ":" + issue.LineNumber + ")")));
        }

        [Test]
        [FeatureTestCase(
            "ローカライズ未使用キーがない",
            "どの locale / scope にもコードから参照されない余剰 key が残っていないことを確認します。",
            order: 22,
            category: FeatureTestCategory.StaticAudit)]
        public void LocalizationStaticAuditService_Analyze_ReturnsNoUnusedKeys()
        {
            var report = LocalizationStaticAuditService.Analyze();

            Assert.That(
                report.UnusedKeys,
                Is.Empty,
                string.Join(
                    "\n",
                    report.UnusedKeys.Select(issue =>
                        issue.Locale + "/" + issue.Scope + ": " + issue.Key +
                        " (" + issue.FilePath + ")")));
        }
    }
}
