using System;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.HiddenObjects
{
    [InitializeOnLoad]
    internal static class HiddenObjectsBootstrap
    {
        private static bool _initialized;
        private static ISettingsService _settings;
        private static IDisposable _registration;

        static HiddenObjectsBootstrap()
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
                "HiddenObjects",
                typeof(HiddenObjectsDefinitions),
                () =>
                {
                    HiddenObjectsDefinitions.RegisterAll(settings);
                    HiddenObjectsSettingDrawers.Register();
                },
                () => _registration = InjectorApi.Register(
                    new ItemInjectionRegistration(
                        "editor-enhancements.hidden-objects",
                        InjectionChannel.HierarchyItem,
                        HiddenObjectHierarchyButtonRenderer.Draw,
                        priority: 100,
                        isEnabled: () => settings.Get(
                            HiddenObjectsDefinitions
                                .HierarchyButtonEnabled))));

            settings.Changed += OnSettingChanged;
        }

        internal static HiddenObjectsController CreateController()
        {
            EnsureInitialized();
            return new HiddenObjectsController(
                new UnityHiddenObjectRepository(),
                new UnityHiddenObjectNavigator(),
                new SettingsHiddenObjectExclusionSource(_settings));
        }

        internal static UnityHiddenObjectIconProvider CreateIconProvider()
        {
            return new UnityHiddenObjectIconProvider();
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    HiddenObjectsDefinitions.HierarchyButtonEnabled))
            {
                InjectorApi.Repaint(InjectionChannel.HierarchyItem);
            }

            if (ReferenceEquals(
                    args.Definition,
                    HiddenObjectsDefinitions.ExcludedScenePatterns) ||
                ReferenceEquals(
                    args.Definition,
                    HiddenObjectsDefinitions.ExcludedObjectPatterns))
            {
                HiddenObjectsWindow.RefreshAll();
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
