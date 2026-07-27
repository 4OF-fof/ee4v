using Ee4v.Core.Settings;

namespace Ee4v.HiddenObjects
{
    internal static class HiddenObjectsSettingDrawers
    {
        private static bool _registered;

        public static void Register()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            CommaSeparatedListSettingDrawer.Register(
                HiddenObjectsDefinitions.ExcludedScenePatterns);
            CommaSeparatedListSettingDrawer.Register(
                HiddenObjectsDefinitions.ExcludedObjectPatterns);
        }
    }
}
