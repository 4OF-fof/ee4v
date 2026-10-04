using System.Collections.Generic;

namespace Ee4v.Core.Settings
{
    public static class CoreSettings
    {
        private static readonly ISettingsService _current = CreateDefault();

        public static ISettingsService Current
        {
            get { return _current; }
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
