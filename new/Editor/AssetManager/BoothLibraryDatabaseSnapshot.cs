using System;
using System.IO;

namespace Ee4v.AssetManager
{
    internal sealed class BoothLibraryDatabaseSnapshot : IDisposable
    {
        private readonly string _snapshotDirectoryPath;

        private BoothLibraryDatabaseSnapshot(string snapshotDirectoryPath, string databasePath)
        {
            _snapshotDirectoryPath = snapshotDirectoryPath;
            DatabasePath = databasePath;
        }

        public string DatabasePath { get; private set; }

        public static BoothLibraryDatabaseSnapshot Create(string sourceDatabasePath)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabasePath))
            {
                throw new ArgumentException("Database path is required.", nameof(sourceDatabasePath));
            }

            if (!File.Exists(sourceDatabasePath))
            {
                throw new FileNotFoundException("SQLite database file was not found.", sourceDatabasePath);
            }

            var snapshotDirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "ee4v",
                "blm-db-snapshots",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(snapshotDirectoryPath);

            var fileName = Path.GetFileName(sourceDatabasePath);
            var snapshotDatabasePath = Path.Combine(snapshotDirectoryPath, fileName);
            File.Copy(sourceDatabasePath, snapshotDatabasePath, true);

            CopySidecarFile(sourceDatabasePath + "-wal", snapshotDatabasePath + "-wal");
            CopySidecarFile(sourceDatabasePath + "-shm", snapshotDatabasePath + "-shm");

            return new BoothLibraryDatabaseSnapshot(snapshotDirectoryPath, snapshotDatabasePath);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_snapshotDirectoryPath))
                {
                    Directory.Delete(_snapshotDirectoryPath, true);
                }
            }
            catch
            {
                // Best effort cleanup for temporary SQLite snapshots.
            }
        }

        private static void CopySidecarFile(string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            File.Copy(sourcePath, destinationPath, true);
        }
    }
}
