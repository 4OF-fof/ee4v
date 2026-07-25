using System;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.FolderStyle
{
    [InitializeOnLoad]
    internal static class FolderStyleBootstrap
    {
        private static bool _initialized;
        private static ISettingsService _settings;
        private static IDisposable _registration;
        private static FolderStyleService _service;

        static FolderStyleBootstrap()
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

            FeatureBootstrapContract.Initialize(
                "FolderStyle",
                typeof(FolderStyleDefinitions),
                () => FolderStyleDefinitions.RegisterAll(settings),
                () => RegisterFeature(settings));

            settings.Changed += OnSettingChanged;
        }

        private static void RegisterFeature(
            ISettingsService settings)
        {
            _service = new FolderStyleService(
                FolderStyleStore.instance);
            var renderer = new FolderStyleRenderer(
                _service,
                new FolderStyleIconCache(),
                new FolderStyleAltTrigger(),
                (folderGuids, position) =>
                    FolderStyleWindow.ShowAt(
                        folderGuids,
                        position,
                        _service));
            _registration = InjectorApi.Register(
                FolderStyleFeature.CreateRegistration(
                    settings,
                    renderer));
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    FolderStyleDefinitions.Enabled))
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
