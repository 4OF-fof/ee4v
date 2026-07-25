using Ee4v.Core.Injector;
using Ee4v.Core.Settings;

namespace Ee4v.FolderStyle
{
    internal static class FolderStyleFeature
    {
        internal const string RegistrationId =
            "editor-enhancements.folder-style";

        public static ItemInjectionRegistration CreateRegistration(
            ISettingsService settings,
            FolderStyleRenderer renderer)
        {
            return new ItemInjectionRegistration(
                RegistrationId,
                InjectionChannel.ProjectItem,
                renderer.Draw,
                priority: 0,
                isEnabled: () => settings.Get(
                    FolderStyleDefinitions.Enabled));
        }
    }
}
