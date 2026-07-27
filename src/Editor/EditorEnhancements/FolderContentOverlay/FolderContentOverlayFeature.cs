using Ee4v.Core.Injector;
using Ee4v.Core.Settings;

namespace Ee4v.FolderContentOverlay
{
    internal static class FolderContentOverlayFeature
    {
        internal const string RegistrationId =
            "editor-enhancements.folder-content-overlay";

        public static ItemInjectionRegistration CreateRegistration(
            ISettingsService settings,
            FolderContentOverlayRenderer renderer)
        {
            return new ItemInjectionRegistration(
                RegistrationId,
                InjectionChannel.ProjectItem,
                renderer.Draw,
                priority: 10,
                isEnabled: () => settings.Get(
                    FolderContentOverlayDefinitions.Enabled));
        }
    }
}
