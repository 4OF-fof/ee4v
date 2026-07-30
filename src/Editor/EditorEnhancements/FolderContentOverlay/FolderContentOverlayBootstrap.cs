using System;
using System.Collections.Generic;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.FolderContentOverlay
{
    [InitializeOnLoad]
    internal static class FolderContentOverlayBootstrap
    {
        private static bool _initialized;
        private static ISettingsService _settings;
        private static IDisposable _registration;
        private static FolderContentIconCache _iconCache;
        private static FolderContentOverlayRenderer _renderer;

        static FolderContentOverlayBootstrap()
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
            DetachAssetChanges();
            _registration?.Dispose();
            _settings = settings;

            FeatureBootstrapContract.Initialize(
                "FolderContentOverlay",
                typeof(FolderContentOverlayDefinitions),
                () => FolderContentOverlayDefinitions.RegisterAll(settings),
                () => RegisterFeature(settings));

            settings.Changed += OnSettingChanged;
            FolderContentOverlayAssetPostprocessor.FoldersChanged +=
                OnFoldersChanged;
        }

        private static void RegisterFeature(ISettingsService settings)
        {
            _iconCache = new FolderContentIconCache(
                new FolderAssetIconResolver());
            _renderer = new FolderContentOverlayRenderer(_iconCache);
            _registration = InjectorApi.Register(
                new ItemInjectionRegistration(
                    "editor-enhancements.folder-content-overlay",
                    InjectionChannel.ProjectItem,
                    _renderer.Draw,
                    priority: 10,
                    isEnabled: () => settings.Get(
                        FolderContentOverlayDefinitions.Enabled)));
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    FolderContentOverlayDefinitions.Enabled))
            {
                InjectorApi.Repaint(InjectionChannel.ProjectItem);
            }
        }

        private static void DetachSettings()
        {
            if (_settings != null)
            {
                _settings.Changed -= OnSettingChanged;
            }
        }

        private static void OnFoldersChanged(
            IReadOnlyCollection<string> folderPaths)
        {
            if (_iconCache == null || folderPaths == null)
            {
                return;
            }

            var invalidated = false;
            foreach (var folderPath in folderPaths)
            {
                invalidated |= _iconCache.Invalidate(folderPath);
            }

            if (invalidated)
            {
                InjectorApi.Repaint(InjectionChannel.ProjectItem);
            }
        }

        private static void DetachAssetChanges()
        {
            FolderContentOverlayAssetPostprocessor.FoldersChanged -=
                OnFoldersChanged;
        }
    }
}
