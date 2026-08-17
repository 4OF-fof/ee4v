using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Ee4v.Core.Settings
{
    internal sealed class ProjectFileSettingStore : ISettingStore
    {
        private readonly string _filePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "ProjectSettings",
            "ee4v.settings.json");

        public Dictionary<string, string> LoadAll()
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, string>();
            }

            var raw = File.ReadAllText(_filePath);
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(raw) ??
                       new Dictionary<string, string>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, string>();
            }
        }

        public void SaveAll(Dictionary<string, string> values)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                _filePath,
                JsonConvert.SerializeObject(values, Formatting.Indented));
        }
    }
}
