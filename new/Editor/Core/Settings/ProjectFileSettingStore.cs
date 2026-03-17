using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Ee4v.Core.Settings
{
    internal sealed class ProjectFileSettingStore : ISettingStore
    {
        private readonly string _filePath;
        private readonly IFileSystem _fileSystem;

        public ProjectFileSettingStore()
            : this(null, null)
        {
        }

        internal ProjectFileSettingStore(string filePath, IFileSystem fileSystem)
        {
            _filePath = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ee4v.settings.json")
                : filePath;
            _fileSystem = fileSystem ?? new DefaultFileSystem();
        }

        public Dictionary<string, string> LoadAll()
        {
            if (!_fileSystem.FileExists(_filePath))
            {
                return new Dictionary<string, string>();
            }

            var raw = _fileSystem.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(raw) ?? new Dictionary<string, string>();
        }

        public void SaveAll(Dictionary<string, string> values)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !_fileSystem.DirectoryExists(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }

            var raw = JsonConvert.SerializeObject(values, Formatting.Indented);
            _fileSystem.WriteAllText(_filePath, raw);
        }
    }
}
