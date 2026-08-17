using System.Collections.Generic;

namespace Ee4v.Core.Settings
{
    public static class CoreSettings
    {
        private static ISettingsService _current = CreateDefault();

        public static ISettingsService Current
        {
            get { return _current; }
        }

        internal static void ResetForTests(ISettingsService replacement = null)
        {
            _current = replacement ?? CreateDefault();
        }

        private static ISettingsService CreateDefault()
        {
            return new SettingsService(
                new Dictionary<SettingScope, ISettingStore>
                {
                    { SettingScope.User, new EditorPrefsSettingStore() },
                    { SettingScope.Project, new ProjectFileSettingStore() }
                },
                new NewtonsoftSettingValueSerializer());
        }
    }
}
