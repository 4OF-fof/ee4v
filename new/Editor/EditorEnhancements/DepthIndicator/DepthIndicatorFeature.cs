using Ee4v.Core.Injector;
using Ee4v.Core.Settings;

namespace Ee4v.DepthIndicator
{
    internal static class DepthIndicatorFeature
    {
        internal const string RegistrationId =
            "editor-enhancements.depth-indicator";

        public static ItemInjectionRegistration CreateRegistration(
            ISettingsService settings)
        {
            return new ItemInjectionRegistration(
                RegistrationId,
                InjectionChannel.HierarchyItem,
                DepthIndicatorRenderer.Draw,
                priority: 0,
                isEnabled: () => settings.Get(
                    DepthIndicatorDefinitions.Enabled));
        }
    }
}
