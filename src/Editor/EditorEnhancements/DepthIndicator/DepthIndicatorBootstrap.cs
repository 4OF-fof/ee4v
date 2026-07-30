using System;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.DepthIndicator
{
    [InitializeOnLoad]
    internal static class DepthIndicatorBootstrap
    {
        private static bool _initialized;
        private static ISettingsService _settings;
        private static IDisposable _registration;

        static DepthIndicatorBootstrap()
        {
            EnsureInitialized();
        }

        internal static void EnsureInitialized()
        {
            var settings = CoreSettings.Current;
            if (_initialized && ReferenceEquals(_settings, settings))
            {
                return;
            }

            _initialized = true;
            DetachSettings();
            _registration?.Dispose();
            _settings = settings;

            FeatureBootstrapContract.Initialize(
                "DepthIndicator",
                typeof(DepthIndicatorDefinitions),
                () => DepthIndicatorDefinitions.RegisterAll(settings),
                () => _registration = InjectorApi.Register(
                    new ItemInjectionRegistration(
                        "editor-enhancements.depth-indicator",
                        InjectionChannel.HierarchyItem,
                        DepthIndicatorRenderer.Draw,
                        priority: 0,
                        isEnabled: () => settings.Get(
                            DepthIndicatorDefinitions.Enabled))));

            settings.Changed += OnSettingChanged;
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    DepthIndicatorDefinitions.Enabled))
            {
                InjectorApi.Repaint(InjectionChannel.HierarchyItem);
            }
        }

        private static void DetachSettings()
        {
            if (_settings != null)
            {
                _settings.Changed -= OnSettingChanged;
            }
        }
    }
}
