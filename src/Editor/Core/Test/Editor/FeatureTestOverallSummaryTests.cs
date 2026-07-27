using System.Collections.Generic;
using Ee4v.Testing.Application;
using Ee4v.Testing.Contracts;
using Ee4v.Testing.UI;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class FeatureTestOverallSummaryTests
    {
        [Test]
        [FeatureTestCase(
            "Test Listの全体サマリを集計する",
            "全suiteの状態、case結果件数、実行時間を検索表示とは独立して集計できることを確認します。",
            order: 83,
            category: FeatureTestCategory.Ui)]
        public void Build_AggregatesSuiteStatusesAndCaseResults()
        {
            var descriptors = new[]
            {
                CreateDescriptor("Passed"),
                CreateDescriptor("Failed"),
                CreateDescriptor("NotRun")
            };
            var records = new Dictionary<string, FeatureTestRunRecord>
            {
                ["Passed"] = new FeatureTestRunRecord
                {
                    Status = FeatureTestRunStatus.Passed,
                    PassCount = 3,
                    SkipCount = 1,
                    DurationSeconds = 1.25d
                },
                ["Failed"] = new FeatureTestRunRecord
                {
                    Status = FeatureTestRunStatus.Failed,
                    PassCount = 2,
                    FailCount = 1,
                    InconclusiveCount = 1,
                    DurationSeconds = 2.75d
                }
            };

            var summary = FeatureTestOverallSummary.Build(
                descriptors,
                featureScope => records.TryGetValue(featureScope, out var record)
                    ? record
                    : null);

            Assert.That(summary.SuiteCount, Is.EqualTo(3));
            Assert.That(summary.PassedSuiteCount, Is.EqualTo(1));
            Assert.That(summary.FailedSuiteCount, Is.EqualTo(1));
            Assert.That(summary.NotRunSuiteCount, Is.EqualTo(1));
            Assert.That(summary.PassCount, Is.EqualTo(5));
            Assert.That(summary.FailCount, Is.EqualTo(1));
            Assert.That(summary.SkipCount, Is.EqualTo(1));
            Assert.That(summary.InconclusiveCount, Is.EqualTo(1));
            Assert.That(summary.DurationSeconds, Is.EqualTo(4d));
            Assert.That(summary.Status, Is.EqualTo(FeatureTestRunStatus.Failed));
        }

        [Test]
        public void Status_PrioritizesActiveRun()
        {
            var descriptors = new[]
            {
                CreateDescriptor("Failed"),
                CreateDescriptor("Running")
            };
            var records = new Dictionary<string, FeatureTestRunRecord>
            {
                ["Failed"] = new FeatureTestRunRecord
                {
                    Status = FeatureTestRunStatus.Failed
                },
                ["Running"] = new FeatureTestRunRecord
                {
                    Status = FeatureTestRunStatus.Running
                }
            };

            var summary = FeatureTestOverallSummary.Build(
                descriptors,
                featureScope => records[featureScope]);

            Assert.That(summary.Status, Is.EqualTo(FeatureTestRunStatus.Running));
        }

        private static FeatureTestDescriptor CreateDescriptor(string scope)
        {
            return new FeatureTestDescriptor(
                scope,
                scope,
                "Ee4v." + scope + ".Tests.Editor");
        }
    }
}
