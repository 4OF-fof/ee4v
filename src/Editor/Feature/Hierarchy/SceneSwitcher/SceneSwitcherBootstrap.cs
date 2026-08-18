using System;
using System.Linq;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.SceneSwitcher
{
    [InitializeOnLoad]
    internal static class SceneSwitcherBootstrap
    {
        private static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "sceneSwitcher.enabled",
                SettingScope.User,
                "SceneSwitcher",
                "settings.section.hierarchy",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 0,
                keywords: new[]
                {
                    "hierarchy",
                    "scene",
                    "switcher"
                });

        internal static readonly SettingDefinition<string> CreateFolder =
            new SettingDefinition<string>(
                "sceneSwitcher.createFolder",
                SettingScope.User,
                "SceneSwitcher",
                "settings.section.creation",
                "settings.createFolder.label",
                "settings.createFolder.tooltip",
                "Assets/Scene",
                order: 0,
                keywords: new[]
                {
                    "scene",
                    "create",
                    "folder",
                    "template"
                });

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
            return _settings.Get(CreateFolder);
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

            settings.Register(Enabled);
            settings.Register(CreateFolder);
            SceneSwitcherSettingDrawers.Register();
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
                        Enabled)));

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
                    Enabled))
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

    internal sealed class SceneSwitcherAssetPostprocessor
        : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsScene(importedAssets) ||
                ContainsScene(deletedAssets) ||
                ContainsScene(movedAssets) ||
                ContainsScene(movedFromAssetPaths))
            {
                SceneSwitcherBootstrap.RefreshCatalog();
            }
        }

        private static bool ContainsScene(string[] paths)
        {
            return paths != null &&
                   paths.Any(path =>
                       path.EndsWith(
                           ".unity",
                           StringComparison.OrdinalIgnoreCase));
        }
    }
}
