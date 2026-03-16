using UnityEditor;

namespace Ee4v.Core.Settings
{
    internal sealed class DefaultEditorPrefsFacade : IEditorPrefsFacade
    {
        public bool HasKey(string key)
        {
            return EditorPrefs.HasKey(key);
        }

        public string GetString(string key, string defaultValue)
        {
            return EditorPrefs.GetString(key, defaultValue);
        }

        public void SetString(string key, string value)
        {
            EditorPrefs.SetString(key, value);
        }
    }
}
