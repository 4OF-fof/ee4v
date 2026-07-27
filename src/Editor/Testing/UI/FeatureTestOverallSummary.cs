using System;
using System.Collections.Generic;
using Ee4v.Testing.Application;
using Ee4v.Testing.Contracts;

namespace Ee4v.Testing.UI
{
    internal sealed class FeatureTestOverallSummary
    {
        private FeatureTestOverallSummary()
        {
        }

        public int SuiteCount { get; private set; }

        public int PassedSuiteCount { get; private set; }

        public int FailedSuiteCount { get; private set; }

        public int SkippedSuiteCount { get; private set; }

        public int InconclusiveSuiteCount { get; private set; }

        public int RunningSuiteCount { get; private set; }

        public int NotRunSuiteCount { get; private set; }

        public int PassCount { get; private set; }

        public int FailCount { get; private set; }

        public int SkipCount { get; private set; }

        public int InconclusiveCount { get; private set; }

        public double DurationSeconds { get; private set; }

        public FeatureTestRunStatus Status
        {
            get
            {
                if (RunningSuiteCount > 0)
                {
                    return FeatureTestRunStatus.Running;
                }

                if (FailedSuiteCount > 0)
                {
                    return FeatureTestRunStatus.Failed;
                }

                if (InconclusiveSuiteCount > 0)
                {
                    return FeatureTestRunStatus.Inconclusive;
                }

                if (SkippedSuiteCount > 0)
                {
                    return FeatureTestRunStatus.Skipped;
                }

                return SuiteCount > 0 && PassedSuiteCount == SuiteCount
                    ? FeatureTestRunStatus.Passed
                    : FeatureTestRunStatus.NotRun;
            }
        }

        public static FeatureTestOverallSummary Build(
            IReadOnlyList<FeatureTestDescriptor> descriptors,
            Func<string, FeatureTestRunRecord> getRecord)
        {
            var summary = new FeatureTestOverallSummary();
            if (descriptors == null)
            {
                return summary;
            }

            for (var i = 0; i < descriptors.Count; i++)
            {
                var descriptor = descriptors[i];
                if (descriptor == null)
                {
                    continue;
                }

                summary.SuiteCount++;
                var record = getRecord != null
                    ? getRecord(descriptor.FeatureScope)
                    : null;
                summary.Add(record ?? new FeatureTestRunRecord());
            }

            return summary;
        }

        private void Add(FeatureTestRunRecord record)
        {
            switch (record.Status)
            {
                case FeatureTestRunStatus.Running:
                    RunningSuiteCount++;
                    break;
                case FeatureTestRunStatus.Passed:
                    PassedSuiteCount++;
                    break;
                case FeatureTestRunStatus.Failed:
                    FailedSuiteCount++;
                    break;
                case FeatureTestRunStatus.Skipped:
                    SkippedSuiteCount++;
                    break;
                case FeatureTestRunStatus.Inconclusive:
                    InconclusiveSuiteCount++;
                    break;
                case FeatureTestRunStatus.NotRun:
                default:
                    NotRunSuiteCount++;
                    break;
            }

            PassCount += record.PassCount;
            FailCount += record.FailCount;
            SkipCount += record.SkipCount;
            InconclusiveCount += record.InconclusiveCount;
            DurationSeconds += record.DurationSeconds;
        }
    }
}
