using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.SaveAndBackup.Domain;

namespace Ee4v.SaveAndBackup.Application
{
    public sealed class SaveAndBackupService
    {
        private readonly ISaveAndBackupRecordStore _records;
        private readonly ISaveAndBackupGateway _backup;
        private readonly ISaveAndBackupEnvironment _environment;

        public SaveAndBackupService(
            ISaveAndBackupRecordStore records,
            ISaveAndBackupGateway backup,
            ISaveAndBackupEnvironment environment)
        {
            _records = records ??
                throw new ArgumentNullException(nameof(records));
            _backup = backup ??
                throw new ArgumentNullException(nameof(backup));
            _environment = environment ??
                throw new ArgumentNullException(nameof(environment));
        }

        public SaveAndBackupRecord RegisterTarget(
            string targetPrefabGuid,
            string targetPath,
            string displayName,
            SaveAndBackupTrigger trigger)
        {
            if (string.IsNullOrWhiteSpace(targetPrefabGuid))
            {
                throw new ArgumentException(
                    "Target prefab GUID is required.",
                    nameof(targetPrefabGuid));
            }

            var records = _records.LoadAll().ToList();
            var record = records.FirstOrDefault(item =>
                string.Equals(
                    item.TargetPrefabGuid,
                    targetPrefabGuid,
                    StringComparison.OrdinalIgnoreCase));
            if (record == null)
            {
                record = new SaveAndBackupRecord
                {
                    Id = _environment.CreateId(),
                    TargetPrefabGuid = targetPrefabGuid
                };
                records.Add(record);
            }

            record.TargetPath = targetPath ?? string.Empty;
            record.DisplayName = displayName ?? string.Empty;
            record.Trigger = trigger;
            _records.SaveAll(records);
            return record;
        }

        public IReadOnlyList<SaveAndBackupRecord> GetRecords()
        {
            return _records.LoadAll();
        }

        public BackupOperationResult BackupNow(string recordId)
        {
            var record = FindRecord(recordId);
            if (record == null)
            {
                return new BackupOperationResult
                {
                    Error = "Save target was not found."
                };
            }

            if (!string.IsNullOrWhiteSpace(
                    record.PendingSnapshotId))
            {
                return Commit(
                    record,
                    record.PendingSnapshotId,
                    "Retry backup");
            }

            var snapshot = _backup.CreateSnapshot(
                CreateSnapshotRequest(record, string.Empty));
            return snapshot.Succeeded
                ? Commit(
                    record,
                    snapshot.SnapshotId,
                    "Manual backup")
                : snapshot;
        }

        internal SaveAndBackupRecord FindByTargetPrefab(
            string guid)
        {
            return _records.LoadAll().FirstOrDefault(record =>
                string.Equals(
                    record.TargetPrefabGuid,
                    guid,
                    StringComparison.OrdinalIgnoreCase));
        }

        internal BackupSnapshotRequest CreateSnapshotRequest(
            SaveAndBackupRecord record,
            string buildOutputPath)
        {
            return new BackupSnapshotRequest
            {
                Record = record,
                BuildOutputPath = buildOutputPath ?? string.Empty,
                Platform = string.Empty,
                CreatedAtUtc = _environment.UtcNow
            };
        }

        internal BackupOperationResult Commit(
            SaveAndBackupRecord record,
            string snapshotId,
            string reason)
        {
            var result = _backup.Commit(
                snapshotId,
                "SaveAndBackup: " +
                (record.DisplayName ?? record.Id) +
                " - " + reason);
            if (!result.Succeeded)
            {
                SetPendingSnapshot(
                    record.Id,
                    result.SnapshotId);
                return result;
            }

            var records = _records.LoadAll().ToList();
            var stored = records.FirstOrDefault(
                item => item.Id == record.Id);
            if (stored != null)
            {
                stored.PendingSnapshotId = string.Empty;
                if (!result.Skipped)
                {
                    stored.LastBackupCommit = result.CommitId;
                    stored.LastBackupAtUtc =
                        _environment.UtcNow;
                }
                _records.SaveAll(records);
            }

            return result;
        }

        internal void SetExternalId(
            SaveAndBackupRecord record,
            string externalId)
        {
            if (record == null ||
                string.IsNullOrWhiteSpace(externalId))
            {
                return;
            }

            var records = _records.LoadAll().ToList();
            var stored = records.FirstOrDefault(
                item => item.Id == record.Id);
            if (stored != null)
            {
                stored.ExternalId = externalId;
                _records.SaveAll(records);
            }
        }

        private SaveAndBackupRecord FindRecord(string recordId)
        {
            return _records.LoadAll().FirstOrDefault(
                record => record.Id == recordId);
        }

        private void SetPendingSnapshot(
            string recordId,
            string snapshotId)
        {
            if (string.IsNullOrWhiteSpace(snapshotId))
            {
                return;
            }

            var records = _records.LoadAll().ToList();
            var stored = records.FirstOrDefault(
                item => item.Id == recordId);
            if (stored != null)
            {
                stored.PendingSnapshotId = snapshotId;
                _records.SaveAll(records);
            }
        }
    }
}
