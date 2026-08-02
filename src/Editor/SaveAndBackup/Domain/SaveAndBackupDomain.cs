using System;

namespace Ee4v.SaveAndBackup.Domain
{
    public enum SaveAndBackupTrigger
    {
        BuildSuccess,
        UploadSuccessOnly,
        ManualOnly
    }

    public sealed class SaveAndBackupSession
    {
        private bool _finished;

        public SaveAndBackupSession(
            string recordId,
            string snapshotId,
            SaveAndBackupTrigger trigger)
        {
            if (string.IsNullOrWhiteSpace(recordId))
            {
                throw new ArgumentException(
                    "Record ID is required.",
                    nameof(recordId));
            }

            if (string.IsNullOrWhiteSpace(snapshotId))
            {
                throw new ArgumentException(
                    "Snapshot ID is required.",
                    nameof(snapshotId));
            }

            RecordId = recordId;
            SnapshotId = snapshotId;
            Trigger = trigger;
        }

        public string RecordId { get; }
        public string SnapshotId { get; }
        public SaveAndBackupTrigger Trigger { get; }
        public bool IsFinished => _finished;

        public bool OnBuildSucceeded()
        {
            if (_finished ||
                Trigger != SaveAndBackupTrigger.BuildSuccess)
            {
                return false;
            }

            _finished = true;
            return true;
        }

        public bool OnUploadSucceeded()
        {
            if (_finished ||
                Trigger != SaveAndBackupTrigger.UploadSuccessOnly)
            {
                return false;
            }

            _finished = true;
            return true;
        }

        public bool OnFailed()
        {
            if (_finished)
            {
                return false;
            }

            _finished = true;
            return true;
        }
    }
}
