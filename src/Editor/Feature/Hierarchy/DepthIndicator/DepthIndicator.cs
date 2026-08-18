using System.Runtime.CompilerServices;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using UnityEditor;

[assembly: InternalsVisibleTo(
    "Ee4v.DepthIndicator.Tests.Editor")]

namespace Ee4v.DepthIndicator
{
    [InitializeOnLoad]
    internal static class DepthIndicator
    {
        private static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "depthIndicator.enabled",
                SettingScope.User,
                "DepthIndicator",
                "settings.section.hierarchy",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 0,
                keywords: new[]
                {
                    "hierarchy",
                    "depth",
                    "indicator"
                });

        static DepthIndicator()
        {
            var settings = CoreSettings.Current;
            settings.Register(Enabled);
            InjectorApi.Register(
                new ItemInjectionRegistration(
                    "depth-indicator.renderer",
                    InjectionChannel.HierarchyItem,
                    DepthIndicatorRenderer.Draw,
                    priority: 0,
                    isEnabled: () => settings.Get(Enabled)));
            settings.Changed += OnSettingChanged;
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(args.Definition, Enabled))
            {
                InjectorApi.Repaint(InjectionChannel.HierarchyItem);
            }
        }
    }
}
