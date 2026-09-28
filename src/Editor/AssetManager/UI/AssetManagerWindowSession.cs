using System;
using System.IO;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.AssetProtection;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    [InitializeOnLoad]
    internal static class AssetManagerWindowSession
    {
        private const string StartupSyncSessionKey =
            "ee4v.assetManager.startupSync.started";
        private static AssetManagerViewState _viewState =
            new AssetManagerViewState();
        private static IAssetManager _manager;
        private static IAssetVariantManager _variantManager;

        internal static event Action ManagerInvalidated;

        static AssetManagerWindowSession()
        {
            GlobalDataSettings.PathChanged -=
                InvalidateManager;
            GlobalDataSettings.PathChanged +=
                InvalidateManager;
            EditorApplication.delayCall -= SyncSourcesOnStartup;
            EditorApplication.delayCall += SyncSourcesOnStartup;
            EditorApplication.delayCall += InitializeProtection;
        }

        public static AssetManagerView CreateView(
            AssetManagerViewMode mode,
            Action<string> createDerivedAsset = null)
        {
            return new AssetManagerView(
                GetManager(),
                _viewState,
                mode,
                createDerivedAsset: createDerivedAsset);
        }

        public static void PrepareRoot(VisualElement root)
        {
            UiComposition.Prepare(
                root,
                "Editor/AssetManager/UI/asset-manager.uss",
                "Editor/AssetManager/UI/searchable-file-tree.uss",
                "Editor/AssetManager/UI/asset-detail.uss");
        }

        internal static void PrepareWorkflowRoot(VisualElement root)
        {
            UiComposition.Prepare(
                root,
                "Editor/AssetManager/UI/asset-manager.uss",
                "Editor/AssetManager/UI/searchable-file-tree.uss",
                "Editor/AssetManager/UI/asset-detail.uss",
                "Editor/AssetManager/UI/asset-modification-workflow.uss");
        }

        internal static IAssetManager GetManager()
        {
            if (_manager != null)
            {
                return _manager;
            }

            _manager = AssetManagerFactory.OpenSession(Path.Combine(
                Environment.ExpandEnvironmentVariables(
                    AssetManagerSettings.Ee4vLibraryPath),
                "asset-manager-v1.db"));
            AssetProtectionModule.Configure(_manager);
            return _manager;
        }

        private static void InvalidateManager()
        {
            AssetProtectionModule.Configure(null);
            _manager = null;
            _variantManager = null;
            _viewState = new AssetManagerViewState();
            EditorApplication.delayCall -= InitializeProtection;
            EditorApplication.delayCall += InitializeProtection;
            var handlers = ManagerInvalidated;
            if (handlers == null)
            {
                return;
            }

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        internal static IAssetVariantManager TryGetVariantManager(IAssetManager manager)
        {
            if (manager == null || !ReferenceEquals(manager, _manager)) { return null; }
            if (_variantManager == null)
            {
                _variantManager = AssetManagerFactory.OpenVariantSession(Path.Combine(
                    Environment.ExpandEnvironmentVariables(AssetManagerSettings.Ee4vLibraryPath),
                    "asset-manager-v1.db"), manager);
            }
            return _variantManager;
        }

        private static void InitializeProtection()
        {
            EditorApplication.delayCall -= InitializeProtection;
            try
            {
                GetManager();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void SyncSourcesOnStartup()
        {
            EditorApplication.delayCall -= SyncSourcesOnStartup;
            if (Application.isBatchMode ||
                SessionState.GetBool(StartupSyncSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(StartupSyncSessionKey, true);
            var eaglePath = ExistingDirectory(
                AssetManagerSettings.EagleLibraryPath);
            var ee4vPath = ExistingDirectory(
                AssetManagerSettings.Ee4vLibraryPath);
            var syncEagle =
                AssetManagerSettings.AutoSyncEagleOnStartup &&
                eaglePath != null;
            var syncEe4v =
                AssetManagerSettings.AutoSyncEe4vOnStartup &&
                ee4vPath != null;
            if (!syncEagle && !syncEe4v)
            {
                return;
            }

            try
            {
                var manager = GetManager();
                if (syncEagle)
                {
                    ReportSyncErrors(
                        "Eagle",
                        manager.SyncEagle(new EagleSyncRequest(
                            eaglePath,
                            AssetManagerSettings.EagleTargetRoot)));
                }

                if (syncEe4v)
                {
                    ReportSyncErrors(
                        "ee4v",
                        manager.SyncEe4v(new Ee4vSyncRequest(ee4vPath)));
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static string ExistingDirectory(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                var path = Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(value));
                return Directory.Exists(path) ? path : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ReportSyncErrors(
            string source,
            AssetSyncResult result)
        {
            if (result.ErrorCount == 0)
            {
                return;
            }

            Debug.LogWarning(
                "AssetManager startup " + source + " sync: " +
                string.Join(Environment.NewLine, result.ErrorMessages));
        }
    }

    [InitializeOnLoad]
    internal static class DerivedAssetHierarchyVisibility
    {
        private static bool _syncScheduled;

        static DerivedAssetHierarchyVisibility()
        {
            EditorApplication.hierarchyChanged += ScheduleSync;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            PrefabUtility.prefabInstanceUpdated += OnPrefabInstanceUpdated;
            Undo.undoRedoPerformed += ScheduleSync;
            ScheduleSync();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            ScheduleSync();
        }

        private static void OnPrefabInstanceUpdated(GameObject instance)
        {
            ScheduleSync();
        }

        private static void ScheduleSync()
        {
            if (_syncScheduled)
            {
                return;
            }

            _syncScheduled = true;
            EditorApplication.delayCall += SyncOpenScenes;
        }

        private static void SyncOpenScenes()
        {
            EditorApplication.delayCall -= SyncOpenScenes;
            _syncScheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() || !scene.isLoaded ||
                    EditorSceneManager.IsPreviewScene(scene))
                {
                    continue;
                }

                var wasDirty = scene.isDirty;
                var changed = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    changed |= SyncSceneTree(root.transform);
                }

                if (changed && !wasDirty)
                {
                    EditorSceneApi.TryClearDirtiness(scene);
                }
            }
        }

        private static bool SyncSceneTree(Transform current)
        {
            var gameObject = current.gameObject;
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject) &&
                IsDerivedAssetInstance(gameObject))
            {
                return SyncDerivedAssetInstance(gameObject);
            }

            var changed = false;
            for (var index = 0; index < current.childCount; index++)
            {
                changed |= SyncSceneTree(current.GetChild(index));
            }
            return changed;
        }

        private static bool IsDerivedAssetInstance(GameObject root)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            var path = AssetDatabase.GetAssetPath(source);
            return !string.IsNullOrEmpty(path) && path.StartsWith(
                DerivedAssetCreator.VariantRoot + "/",
                StringComparison.Ordinal);
        }

        private static bool SyncDerivedAssetInstance(GameObject root)
        {
            var changed = false;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var gameObject = transform.gameObject;
                var hidden = (gameObject.hideFlags &
                    HideFlags.HideInHierarchy) != 0;
                var shouldHide = string.Equals(gameObject.tag,
                    "EditorOnly", StringComparison.Ordinal);
                if (hidden == shouldHide)
                {
                    continue;
                }

                gameObject.hideFlags = shouldHide
                    ? gameObject.hideFlags | HideFlags.HideInHierarchy
                    : gameObject.hideFlags & ~HideFlags.HideInHierarchy;
                changed = true;
            }

            if (changed)
            {
                EditorApplication.RepaintHierarchyWindow();
            }
            return changed;
        }
    }
}
