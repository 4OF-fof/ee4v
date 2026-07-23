namespace Ee4v.Core.Settings
{
    internal interface IEditorPrefsFacade
    {
        bool HasKey(string key);

        string GetString(string key, string defaultValue);

        void SetString(string key, string value);
    }
}
