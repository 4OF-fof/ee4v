using System;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using Ee4v.HiddenObjects;
using Ee4v.ItemStyle;
using UnityEditor;

namespace Ee4v.HierarchyStyle
{
    [InitializeOnLoad]
    internal static class HierarchyStyleBootstrap
    {
        private static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "hierarchyStyle.enabled",
                SettingScope.User,
                "HierarchyStyle",
                "settings.section.hierarchy",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 10,
                keywords: new[]
                {
                    "hierarchy",
                    "color",
                    "background",
                    "icon",
                    "hide",
                    "alt"
                });

        private static bool _initialized;
        private static ISettingsService _settings;
        private static IDisposable _registration;
        private static HierarchyObjectIdentity _identity;
        private static HierarchyStyleIconApplier _iconApplier;
        private static ItemStyleService _service;

        static HierarchyStyleBootstrap()
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
            EditorApplication.hierarchyChanged -=
                OnHierarchyChanged;
            _settings = settings;

            settings.Register(Enabled);
            RegisterFeature(settings);
            HiddenObjectsFeature.EnsureInitialized();

            settings.Changed += OnSettingChanged;
            EditorApplication.hierarchyChanged +=
                OnHierarchyChanged;
        }

        internal static ItemStyleService Service
        {
            get
            {
                EnsureInitialized();
                return _service;
            }
        }

        internal static HierarchyObjectIdentity Identity
        {
            get
            {
                EnsureInitialized();
                return _identity;
            }
        }

        private static void RegisterFeature(
            ISettingsService settings)
        {
            _service = ItemStyleService.Create("hierarchy");
            _identity = new HierarchyObjectIdentity();
            var iconCache = new ItemStyleIconCache();
            _iconApplier =
                new HierarchyStyleIconApplier(
                    iconCache);
            var renderer = new HierarchyStyleRenderer(
                _service,
                _identity,
                _iconApplier,
                new ItemStyleAltTrigger<int>(),
                (targets, position) =>
                    HierarchyStyleWindow.ShowAt(
                        targets,
                        position,
                        _service,
                        _identity,
                        _iconApplier));
            _registration = InjectorApi.Register(
                new ItemInjectionRegistration(
                    "editor-enhancements.hierarchy-style",
                    InjectionChannel.HierarchyItem,
                    renderer.Draw,
                    priority: -100,
                    isEnabled: () => settings.Get(
                        Enabled)));
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (!ReferenceEquals(
                    args.Definition,
                    Enabled))
            {
                return;
            }

            if (!_settings.Get(
                    Enabled))
            {
                _iconApplier?.RemoveAll();
            }

            InjectorApi.Repaint(
                InjectionChannel.HierarchyItem);
        }

        private static void OnHierarchyChanged()
        {
            _identity?.Clear();
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
