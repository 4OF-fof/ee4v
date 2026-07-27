using System;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.SceneSwitcher
{
    [InitializeOnLoad]
    internal static class SceneSwitcherBootstrap
    {
        private static bool _initialized;
        private static ISettingsService _settings;
        private static IDisposable _registration;
        private static SceneSwitcherController _controller;

        static SceneSwitcherBootstrap()
        {
            EnsureInitialized();
        }

        internal static SceneSwitcherController Controller
        {
            get
            {
                EnsureInitialized();
                return _controller;
            }
        }

        internal static string GetCreateFolder()
        {
            EnsureInitialized();
            return _settings.Get(
                SceneSwitcherDefinitions.CreateFolder);
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
            _settings = settings;

            FeatureBootstrapContract.Initialize(
                "SceneSwitcher",
                typeof(SceneSwitcherDefinitions),
                () =>
                {
                    SceneSwitcherDefinitions.RegisterAll(settings);
                    SceneSwitcherSettingDrawers.Register();
                },
                () =>
                {
                    _controller = new SceneSwitcherController(
                        SceneSwitcherStateStore.instance,
                        new UnitySceneSwitcherGateway());
                    _registration = InjectorApi.Register(
                        new ItemInjectionRegistration(
                            "editor-enhancements.scene-switcher",
                            InjectionChannel.HierarchyItem,
                            SceneSwitcherHierarchyTrigger.Draw,
                            priority: 0,
                            isEnabled: () => settings.Get(
                                SceneSwitcherDefinitions.Enabled)));
                });

            settings.Changed += OnSettingChanged;
        }

        internal static void RefreshCatalog()
        {
            Controller.RefreshCatalog();
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    SceneSwitcherDefinitions.Enabled))
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
