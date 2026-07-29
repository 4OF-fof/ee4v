using Ee4v.Core.Injector;
using Ee4v.Core.Settings;

namespace Ee4v.HierarchyDecoration
{
    internal static class HierarchyDecorationFeature
    {
        internal const string RegistrationId =
            "editor-enhancements.hierarchy-decoration";

        public static ItemInjectionRegistration CreateRegistration(
            ISettingsService settings,
            HierarchyDecorationRenderer renderer)
        {
            return new ItemInjectionRegistration(
                RegistrationId,
                InjectionChannel.HierarchyItem,
                renderer.Draw,
                priority: 100,
                isEnabled: () => settings.Get(
                    HierarchyDecorationDefinitions.Enabled));
        }
    }
}
