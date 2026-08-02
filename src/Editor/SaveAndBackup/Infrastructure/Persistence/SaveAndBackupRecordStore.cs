using System;
using System.Collections.Generic;
using System.IO;
using Ee4v.SaveAndBackup.Application;
using Newtonsoft.Json;

namespace Ee4v.SaveAndBackup.Infrastructure.Persistence
{
    internal sealed class SaveAndBackupRecordStore : ISaveAndBackupRecordStore
    {
        private readonly string _path;

        internal SaveAndBackupRecordStore(string projectRoot)
        {
            _path = Path.Combine(
                projectRoot ?? throw new ArgumentNullException(nameof(projectRoot)),
                "ProjectSettings",
                "ee4v.save-and-backup.json");
        }

        public IReadOnlyList<SaveAndBackupRecord> LoadAll()
        {
            if (!File.Exists(_path))
            {
                return Array.Empty<SaveAndBackupRecord>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<SaveAndBackupRecord>>(
                           File.ReadAllText(_path)) ??
                       new List<SaveAndBackupRecord>();
            }
            catch (JsonException)
            {
                return Array.Empty<SaveAndBackupRecord>();
            }
        }

        public void SaveAll(IReadOnlyList<SaveAndBackupRecord> records)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(
                _path,
                JsonConvert.SerializeObject(
                    records ?? Array.Empty<SaveAndBackupRecord>(),
                    Formatting.Indented));
        }
    }
}
