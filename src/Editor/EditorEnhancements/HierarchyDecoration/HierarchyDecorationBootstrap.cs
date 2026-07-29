using System;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.HierarchyDecoration
{
    [InitializeOnLoad]
    internal static class HierarchyDecorationBootstrap
    {
        private static bool _initialized;
        private static ISettingsService _settings;
        private static IDisposable _registration;

        static HierarchyDecorationBootstrap()
        {
            EnsureInitialized();
        }

        internal static void EnsureInitialized()
        {
            var settings = CoreSettings.Current;
            if (_initialized &&
                ReferenceEquals(_settings, settings))
            {
                return;
            }

            _initialized = true;
            DetachSettings();
            _registration?.Dispose();
            ObjectChangeEvents.changesPublished -=
                HierarchyDecorationCreationMonitor
                    .HandleChanges;
            _settings = settings;

            FeatureBootstrapContract.Initialize(
                "HierarchyDecoration",
                typeof(HierarchyDecorationDefinitions),
                () =>
                    HierarchyDecorationDefinitions
                        .RegisterAll(settings),
                () => _registration = InjectorApi.Register(
                    HierarchyDecorationFeature
                        .CreateRegistration(
                            settings,
                            new HierarchyDecorationRenderer())));

            settings.Changed += OnSettingChanged;
            ObjectChangeEvents.changesPublished +=
                HierarchyDecorationCreationMonitor
                    .HandleChanges;
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    HierarchyDecorationDefinitions.Enabled))
            {
                InjectorApi.Repaint(
                    InjectionChannel.HierarchyItem);
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
