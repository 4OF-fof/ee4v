using System;
using System.Collections.Generic;

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
            DetailedResult = string.Empty;
            RunId = string.Empty;
            CaseStatuses = new Dictionary<string, FeatureTestRunStatus>(StringComparer.OrdinalIgnoreCase);
            CaseDetails = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public FeatureTestRunStatus Status { get; set; }

        public string RunId { get; set; }

        public string Message { get; set; }

        public string DetailedResult { get; set; }

        public int PassCount { get; set; }

        public int FailCount { get; set; }

        public int SkipCount { get; set; }

        public int InconclusiveCount { get; set; }

        public double DurationSeconds { get; set; }

        public DateTime? FinishedAtUtc { get; set; }

        public Dictionary<string, FeatureTestRunStatus> CaseStatuses { get; }

        public Dictionary<string, string> CaseDetails { get; }
    }
}
