using System;
using System.Collections.Generic;

namespace Ee4v.SaveAndBackup.Application
{
    public interface ISaveAndBackupRecordStore
    {
        IReadOnlyList<SaveAndBackupRecord> LoadAll();
        void SaveAll(IReadOnlyList<SaveAndBackupRecord> records);
    }

    public interface ISaveAndBackupGateway
    {
        BackupOperationResult CreateSnapshot(
            BackupSnapshotRequest request);
        BackupOperationResult Commit(
            string snapshotId,
            string message);
        void Discard(string snapshotId);
    }

    public interface ISaveAndBackupEnvironment
    {
        string CreateId();
        DateTime UtcNow { get; }
    }
}
