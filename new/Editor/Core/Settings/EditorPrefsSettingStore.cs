using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;

namespace Ee4v.Core.Settings
{
    internal sealed class EditorPrefsSettingStore : ISettingStore
    {
        private const string StorageKey = "dev.4of.ee4v.settings.user";

        public Dictionary<string, string> LoadAll()
        {
            if (!EditorPrefs.HasKey(StorageKey))
            {
                return new Dictionary<string, string>();
            }

            var raw = EditorPrefs.GetString(StorageKey, "{}");
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(raw) ?? new Dictionary<string, string>();
        }

        public void SaveAll(Dictionary<string, string> values)
        {
            var raw = JsonConvert.SerializeObject(values, Formatting.Indented);
            EditorPrefs.SetString(StorageKey, raw);
        }
    }
}
