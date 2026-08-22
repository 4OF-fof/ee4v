using System;
using Ee4v.Core.Injector;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Ee4v.HiddenObjects
{
    internal static class HiddenObjectsFeature
    {
        private static readonly SettingDefinition<bool>
            HierarchyButtonEnabled =
                new SettingDefinition<bool>(
                    "hiddenObjects.hierarchyButton.enabled",
                    SettingScope.User,
                    "HiddenObjects",
                    "settings.section.hierarchy",
                    "settings.hierarchyButton.label",
                    "settings.hierarchyButton.tooltip",
                    true,
                    order: 0,
                    keywords: new[]
                    {
                        "hierarchy",
                        "hidden",
                        "visibility"
                    });

        internal static readonly SettingDefinition<string>
            ExcludedScenePatterns =
                new SettingDefinition<string>(
                    "hiddenObjects.exclusions.scenePatterns",
                    SettingScope.User,
                    "HiddenObjects",
                    "settings.section.exclusions",
                    "settings.exclusions.scenes.label",
                    "settings.exclusions.scenes.tooltip",
                    "*NDMF*",
                    order: 0,
                    keywords: new[]
                    {
                        "hidden",
                        "exclude",
                        "scene",
                        "ndmf"
                    });

        internal static readonly SettingDefinition<string>
            ExcludedObjectPatterns =
                new SettingDefinition<string>(
                    "hiddenObjects.exclusions.objectPatterns",
                    SettingScope.User,
                    "HiddenObjects",
                    "settings.section.exclusions",
                    "settings.exclusions.objects.label",
                    "settings.exclusions.objects.tooltip",
                    "nadena.dev.ndmf*Activator",
                    order: 1,
                    keywords: new[]
                    {
                        "hidden",
                        "exclude",
                        "object",
                        "ndmf"
                    });

        private static bool _initialized;
        private static bool _drawersRegistered;
        private static ISettingsService _settings;
        private static IDisposable _registration;
        private static UnityHiddenObjectVisibilityService _visibility;

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
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorApplication.delayCall -=
                RestorePersistedVisibility;
            Undo.undoRedoPerformed -= OnUndoRedo;
            _settings = settings;
            _visibility =
                new UnityHiddenObjectVisibilityService(
                    HiddenObjectRestoreStateStore.instance);

            settings.Register(HierarchyButtonEnabled);
            settings.Register(ExcludedScenePatterns);
            settings.Register(ExcludedObjectPatterns);
            RegisterSettingDrawers();
            _registration = InjectorApi.Register(
                new ItemInjectionRegistration(
                    "editor-enhancements.hidden-objects",
                    InjectionChannel.HierarchyItem,
                    HiddenObjectHierarchyButtonRenderer.Draw,
                    priority: 100,
                    isEnabled: () => settings.Get(
                        HierarchyButtonEnabled)));

            settings.Changed += OnSettingChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall +=
                RestorePersistedVisibility;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        internal static HiddenObjectsController CreateController()
        {
            EnsureInitialized();
            return new HiddenObjectsController(
                new UnityHiddenObjectRepository(_visibility),
                new SettingsHiddenObjectExclusionSource(_settings));
        }

        internal static UnityHiddenObjectVisibilityService Visibility
        {
            get
            {
                EnsureInitialized();
                return _visibility;
            }
        }

        private static void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    HierarchyButtonEnabled))
            {
                InjectorApi.Repaint(InjectionChannel.HierarchyItem);
            }

            if (ReferenceEquals(
                    args.Definition,
                    ExcludedScenePatterns) ||
                ReferenceEquals(
                    args.Definition,
                    ExcludedObjectPatterns))
            {
                HiddenObjectsWindow.RefreshAll();
            }
        }

        private static void OnSceneOpened(
            Scene scene,
            OpenSceneMode mode)
        {
            RestorePersistedVisibility();
        }

        private static void RestorePersistedVisibility()
        {
            _visibility?.RestorePersistedVisibility();
        }

        private static void OnUndoRedo()
        {
            _visibility?.SynchronizePersistedVisibility();
        }

        private static void DetachSettings()
        {
            if (_settings != null)
            {
                _settings.Changed -= OnSettingChanged;
            }
        }

        private static void RegisterSettingDrawers()
        {
            if (_drawersRegistered)
            {
                return;
            }

            _drawersRegistered = true;
            CommaSeparatedListSettingDrawer.Register(
                ExcludedScenePatterns);
            CommaSeparatedListSettingDrawer.Register(
                ExcludedObjectPatterns);
        }
    }
}
