using System;
using Ee4v.SaveAndBackup.Domain;

namespace Ee4v.SaveAndBackup.Application
{
    public sealed class SaveAndBackupCoordinator
    {
        private readonly SaveAndBackupService _service;
        private readonly ISaveAndBackupGateway _backup;
        private SaveAndBackupRecord _record;
        private SaveAndBackupSession _session;

        public SaveAndBackupCoordinator(
            SaveAndBackupService service,
            ISaveAndBackupGateway backup)
        {
            _service = service ??
                throw new ArgumentNullException(nameof(service));
            _backup = backup ??
                throw new ArgumentNullException(nameof(backup));
        }

        public string Begin(
            string targetPrefabGuid,
            bool hasUnappliedOverrides)
        {
            if (_session != null && !_session.IsFinished)
            {
                return string.Empty;
            }

            _record = _service.FindByTargetPrefab(
                targetPrefabGuid);
            if (_record == null ||
                _record.Trigger ==
                SaveAndBackupTrigger.ManualOnly)
            {
                return string.Empty;
            }

            if (hasUnappliedOverrides)
            {
                return "unapplied-overrides";
            }

            var snapshot = _backup.CreateSnapshot(
                _service.CreateSnapshotRequest(
                    _record,
                    string.Empty));
            if (!snapshot.Succeeded)
            {
                _record = null;
                return snapshot.Error ?? "snapshot-failed";
            }

            _session = new SaveAndBackupSession(
                _record.Id,
                snapshot.SnapshotId,
                _record.Trigger);
            return string.Empty;
        }

        public BackupOperationResult BuildSucceeded(
            string buildOutputPath)
        {
            if (_session == null ||
                !_session.OnBuildSucceeded())
            {
                return new BackupOperationResult
                {
                    Skipped = true
                };
            }

            return FinishCommit(
                "Build succeeded",
                true);
        }

        public BackupOperationResult UploadSucceeded(
            string externalId)
        {
            if (_record != null &&
                !string.IsNullOrWhiteSpace(externalId))
            {
                _service.SetExternalId(
                    _record,
                    externalId);
            }

            if (_session == null ||
                !_session.OnUploadSucceeded())
            {
                return new BackupOperationResult
                {
                    Skipped = true
                };
            }

            return FinishCommit(
                "Upload succeeded",
                false);
        }

        public void Failed()
        {
            if (_session != null && _session.OnFailed())
            {
                _backup.Discard(_session.SnapshotId);
            }

            Clear();
        }

        private BackupOperationResult FinishCommit(
            string reason,
            bool preserveRecordForUpload)
        {
            var result = _service.Commit(
                _record,
                _session.SnapshotId,
                reason);
            if (preserveRecordForUpload)
            {
                _session = null;
            }
            else
            {
                Clear();
            }

            return result;
        }

        private void Clear()
        {
            _record = null;
            _session = null;
        }
    }
}
