using System;
using Ee4v.SaveAndBackup.Domain;

namespace Ee4v.SaveAndBackup.Application
{
    public sealed class SaveAndBackupRecord
    {
        public string Id { get; set; }
        public string TargetPrefabGuid { get; set; }
        public string TargetPath { get; set; }
        public string DisplayName { get; set; }
        public string ExternalId { get; set; }
        public SaveAndBackupTrigger Trigger { get; set; }
        public string LastBackupCommit { get; set; }
        public DateTime? LastBackupAtUtc { get; set; }
        public string PendingSnapshotId { get; set; }
    }

    public sealed class BackupSnapshotRequest
    {
        public SaveAndBackupRecord Record { get; set; }
        public string BuildOutputPath { get; set; }
        public string Platform { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class BackupOperationResult
    {
        public bool Succeeded { get; set; }
        public bool Skipped { get; set; }
        public string SnapshotId { get; set; }
        public string CommitId { get; set; }
        public string Error { get; set; }
    }
}
