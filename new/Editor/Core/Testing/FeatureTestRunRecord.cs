using System;

namespace Ee4v.Core.Testing
{
    internal enum FeatureTestRunStatus
    {
        NotRun,
        Running,
        Passed,
        Failed,
        Skipped,
        Inconclusive
    }

    internal sealed class FeatureTestRunRecord
    {
        public FeatureTestRunRecord()
        {
            Status = FeatureTestRunStatus.NotRun;
            Message = string.Empty;
            RunId = string.Empty;
        }

        public FeatureTestRunStatus Status { get; set; }

        public string RunId { get; set; }

        public string Message { get; set; }

        public int PassCount { get; set; }

        public int FailCount { get; set; }

        public int SkipCount { get; set; }

        public int InconclusiveCount { get; set; }

        public double DurationSeconds { get; set; }

        public DateTime? FinishedAtUtc { get; set; }
    }
}
