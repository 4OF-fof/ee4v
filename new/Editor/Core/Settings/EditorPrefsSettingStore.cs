using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ee4v.Core.Settings
{
    internal sealed class EditorPrefsSettingStore : ISettingStore
    {
        private const string StorageKey = "dev.4of.ee4v.settings.user";
        private readonly IEditorPrefsFacade _editorPrefs;
        private readonly string _storageKey;

        public EditorPrefsSettingStore()
            : this(null, null)
        {
        }

        internal EditorPrefsSettingStore(string storageKey, IEditorPrefsFacade editorPrefs)
        {
            _storageKey = string.IsNullOrWhiteSpace(storageKey) ? StorageKey : storageKey;
            _editorPrefs = editorPrefs ?? new DefaultEditorPrefsFacade();
        }

        public Dictionary<string, string> LoadAll()
        {
            if (!_editorPrefs.HasKey(_storageKey))
            {
                return new Dictionary<string, string>();
            }

            var raw = _editorPrefs.GetString(_storageKey, "{}");
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(raw) ?? new Dictionary<string, string>();
        }

        public void SaveAll(Dictionary<string, string> values)
        {
            var raw = JsonConvert.SerializeObject(values, Formatting.Indented);
            _editorPrefs.SetString(_storageKey, raw);
        }
    }
}
