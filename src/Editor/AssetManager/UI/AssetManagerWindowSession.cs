using System;
using System.IO;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.AssetProtection;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
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
            AssetManagerViewMode mode)
        {
            return new AssetManagerView(
                GetManager(),
                _viewState,
                mode);
        }

        public static void PrepareRoot(VisualElement root)
        {
            UiComposition.Prepare(
                root,
                "Editor/AssetManager/UI/asset-manager.uss",
                "Editor/AssetManager/UI/searchable-file-tree.uss",
                "Editor/AssetManager/UI/asset-detail.uss");
        }

        internal static IAssetManager GetManager()
        {
            if (_manager != null)
            {
                return _manager;
            }

            _manager = AssetManagerFactory.Open(Path.Combine(
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
}
