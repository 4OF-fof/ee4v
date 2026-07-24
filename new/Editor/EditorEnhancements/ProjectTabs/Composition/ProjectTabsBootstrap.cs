using System;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.ProjectTabs
{
    [InitializeOnLoad]
    internal static class ProjectTabsBootstrap
    {
        private static bool _initialized;
        private static ISettingsService _settings;
        private static IDisposable _registration;
        private static ProjectTabsSession _session;

        static ProjectTabsBootstrap()
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
                "ProjectTabs",
                typeof(ProjectTabsDefinitions),
                () => ProjectTabsDefinitions.RegisterAll(settings),
                () => RegisterFeature(settings));

            settings.Changed += OnSettingChanged;
        }

        private static void RegisterFeature(ISettingsService settings)
        {
            _session = new ProjectTabsSession(
                ProjectTabsStateStore.instance,
                UnityProjectBrowserNavigator.CreateDefaultLocation());
            _registration = InjectorApi.Register(
                new VisualElementInjectionRegistration(
                    "editor-enhancements.project-tabs",
                    InjectionChannel.ProjectToolbar,
                    context => new ProjectTabsHost(
                        context.Window,
                        _session,
                        new UnityProjectTabFolderDropResolver()),
                    priority: 0,
                    isEnabled: () => settings.Get(
                        ProjectTabsDefinitions.Enabled)));
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    ProjectTabsDefinitions.Enabled))
            {
                InjectorApi.Repaint(InjectionChannel.ProjectToolbar);
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
