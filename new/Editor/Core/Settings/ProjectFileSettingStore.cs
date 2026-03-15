using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Ee4v.Settings
{
    internal sealed class ProjectFileSettingStore : ISettingStore
    {
        private readonly string _filePath = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ee4v.settings.json");

        public Dictionary<string, string> LoadAll()
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, string>();
            }

            var raw = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(raw) ?? new Dictionary<string, string>();
        }

        public void SaveAll(Dictionary<string, string> values)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var raw = JsonConvert.SerializeObject(values, Formatting.Indented);
            File.WriteAllText(_filePath, raw);
        }
    }
}
