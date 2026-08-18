using System;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using Ee4v.ItemStyle;
using UnityEditor;

namespace Ee4v.ProjectStyle
{
    [InitializeOnLoad]
    internal static class ProjectStyleBootstrap
    {
        private static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "projectStyle.enabled",
                SettingScope.User,
                "ProjectStyle",
                "settings.section.project",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 0,
                keywords: new[]
                {
                    "project",
                    "folder",
                    "color",
                    "icon",
                    "style"
                });

        private static bool _initialized;
        private static ISettingsService _settings;
        private static IDisposable _registration;
        private static ItemStyleService _service;

        static ProjectStyleBootstrap()
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
            _settings = settings;

            settings.Register(Enabled);
            RegisterFeature(settings);

            settings.Changed += OnSettingChanged;
        }

        internal static ItemStyleService Service
        {
            get
            {
                EnsureInitialized();
                return _service;
            }
        }

        private static void RegisterFeature(
            ISettingsService settings)
        {
            _service = ItemStyleService.Create("project");
            var renderer = new ProjectStyleRenderer(
                _service,
                new ItemStyleIconCache(),
                new ItemStyleAltTrigger<string>(),
                (folderGuids, position) =>
                    ProjectStyleWindow.ShowAt(
                        folderGuids,
                        position,
                        _service));
            _registration = InjectorApi.Register(
                new ItemInjectionRegistration(
                    "editor-enhancements.project-style",
                    InjectionChannel.ProjectItem,
                    renderer.Draw,
                    priority: 0,
                    isEnabled: () => settings.Get(
                        Enabled)));
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
                    InjectionChannel.ProjectItem);
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
