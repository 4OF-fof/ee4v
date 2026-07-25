using System;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;
using UnityEditor;

namespace Ee4v.HierarchyStyle
{
    [InitializeOnLoad]
    internal static class HierarchyStyleBootstrap
    {
        private static bool _initialized;
        private static ISettingsService _settings;
        private static IDisposable _registration;
        private static HierarchyObjectIdentity _identity;
        private static HierarchyStyleIconApplier _iconApplier;
        private static HierarchyStyleService _service;
        private static IHierarchyObjectVisibility _visibility;

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

            FeatureBootstrapContract.Initialize(
                "HierarchyStyle",
                typeof(HierarchyStyleDefinitions),
                () =>
                    HierarchyStyleDefinitions
                        .RegisterAll(settings),
                () => RegisterFeature(settings));

            settings.Changed += OnSettingChanged;
            EditorApplication.hierarchyChanged +=
                OnHierarchyChanged;
        }

        private static void RegisterFeature(
            ISettingsService settings)
        {
            _service = new HierarchyStyleService(
                HierarchyStyleStore.instance);
            _identity = new HierarchyObjectIdentity();
            var iconCache =
                new HierarchyStyleIconCache();
            _iconApplier =
                new HierarchyStyleIconApplier(
                    iconCache);
            _visibility =
                new UnityHierarchyObjectVisibility();
            var renderer = new HierarchyStyleRenderer(
                _service,
                _identity,
                _iconApplier,
                new HierarchyStyleAltTrigger(),
                (targets, position) =>
                    HierarchyStyleWindow.ShowAt(
                        targets,
                        position,
                        _service,
                        _identity,
                        _iconApplier,
                        _visibility));
            _registration = InjectorApi.Register(
                HierarchyStyleFeature
                    .CreateRegistration(
                        settings,
                        renderer));
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (!ReferenceEquals(
                    args.Definition,
                    HierarchyStyleDefinitions.Enabled))
            {
                return;
            }

            if (!_settings.Get(
                    HierarchyStyleDefinitions.Enabled))
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
