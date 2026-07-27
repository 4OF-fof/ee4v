using Ee4v.Core.Injector;
using Ee4v.Core.Settings;

namespace Ee4v.HierarchyStyle
{
    internal static class HierarchyStyleFeature
    {
        internal const string RegistrationId =
            "editor-enhancements.hierarchy-style";

        public static ItemInjectionRegistration CreateRegistration(
            ISettingsService settings,
            HierarchyStyleRenderer renderer)
        {
            return new ItemInjectionRegistration(
                RegistrationId,
                InjectionChannel.HierarchyItem,
                renderer.Draw,
                priority: -100,
                isEnabled: () => settings.Get(
                    HierarchyStyleDefinitions.Enabled));
        }
    }
}
