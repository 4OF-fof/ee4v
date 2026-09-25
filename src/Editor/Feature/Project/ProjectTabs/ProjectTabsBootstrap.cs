using System;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.ProjectTabs
{
    [InitializeOnLoad]
    internal static class ProjectTabsBootstrap
    {
        private static readonly SettingDefinition<bool> Enabled =
            new SettingDefinition<bool>(
                "projectTabs.enabled",
                SettingScope.User,
                "ProjectTabs",
                "settings.section.project",
                "settings.enabled.label",
                "settings.enabled.tooltip",
                true,
                order: 0,
                keywords: new[]
                {
                    "project",
                    "tab",
                    "history",
                    "navigation"
                });

        private static bool _initialized;
        private static ISettingsService _settings;
        private static IDisposable _registration;
        private static ProjectTabsSession _session;
        private static UnityProjectFavoriteFolderStore _favoriteStore;
        private static ProjectTabsFavoriteSynchronizer
            _favoriteSynchronizer;

        internal static event Action Changed;

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

            settings.Register(Enabled);
            RegisterFeature(settings);

            settings.Changed += OnSettingChanged;
        }

        internal static ProjectTabsSession Session
        {
            get
            {
                EnsureInitialized();
                return _session;
            }
        }

        private static void RegisterFeature(ISettingsService settings)
        {
            _favoriteSynchronizer?.Dispose();
            _favoriteStore?.Dispose();
            if (_session != null)
            {
                _session.Changed -= OnSessionChanged;
            }

            _favoriteStore = new UnityProjectFavoriteFolderStore();
            _session = new ProjectTabsSession(
                ProjectTabsStateStore.instance,
                UnityProjectBrowserNavigator.CreateDefaultLocation(),
                _favoriteStore);
            _session.Changed += OnSessionChanged;
            _favoriteSynchronizer =
                new ProjectTabsFavoriteSynchronizer(
                    _session,
                    _favoriteStore);
            _registration = InjectorApi.Register(
                new VisualElementInjectionRegistration(
                    "editor-enhancements.project-tabs",
                    InjectionChannel.ProjectToolbar,
                    context => new ProjectTabsHost(
                        context.Window,
                        _session),
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
                InjectorApi.Repaint(InjectionChannel.ProjectToolbar);
            }
        }

        private static void OnSessionChanged()
        {
            Changed?.Invoke();
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
