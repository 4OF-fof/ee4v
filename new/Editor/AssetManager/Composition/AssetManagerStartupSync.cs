using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Application;
using Ee4v.Core.Background;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.Composition
{
    internal static class AssetManagerStartupSync
    {
        private const string SessionKey = "ee4v.assetManager.startupSync.started";
        private static bool _scheduled;
        private static AssetManagerService _assetManager;

        internal static event Action<IReadOnlyList<AssetSyncConflict>, Action<bool>> ConfirmationRequested;

        internal static void EnsureInitialized(AssetManagerService assetManager)
        {
            _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
            if (_scheduled)
            {
                return;
            }

            _scheduled = true;
            EditorApplication.delayCall -= TryStart;
            EditorApplication.delayCall += TryStart;
        }

        private static void TryStart()
        {
            EditorApplication.delayCall -= TryStart;
            if (UnityEngine.Application.isBatchMode || SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            SettingApi.Preload(SettingScope.User);

            var blmPath = ResolvePath(SettingApi.Get(AssetManagerDefinitions.BlmDatabasePath));
            var eaglePath = ResolvePath(SettingApi.Get(AssetManagerDefinitions.EagleLibraryPath));
            var checkBlm = SettingApi.Get(AssetManagerDefinitions.AutoSyncBlmOnStartup) && File.Exists(blmPath);
            var checkEagle = SettingApi.Get(AssetManagerDefinitions.AutoSyncEagleOnStartup) && Directory.Exists(eaglePath);
            if (!checkBlm && !checkEagle)
            {
                return;
            }

            var activity = BackgroundActivityApi.Begin(I18N.Get("assetManager.background.datasourceCheck"));
            Task.Run(() => Prepare(checkBlm, blmPath, checkEagle, eaglePath)).ContinueWith(task =>
            {
                EditorApplication.delayCall += () => HandlePrepared(task, activity);
            });
        }

        private static PreparedStartupSync Prepare(bool checkBlm, string blmPath, bool checkEagle, string eaglePath)
        {
            return new PreparedStartupSync(
                checkBlm ? _assetManager.PrepareBlmSync(new BlmSyncRequest(blmPath)) : null,
                checkEagle ? _assetManager.PrepareEagleSync(new EagleSyncRequest(eaglePath)) : null);
        }

        private static void HandlePrepared(Task<PreparedStartupSync> task, IDisposable activity)
        {
            if (task.IsFaulted)
            {
                activity.Dispose();
                if (task.Exception != null)
                {
                    Debug.LogException(task.Exception.GetBaseException());
                }

                return;
            }

            if (task.IsCanceled || task.Result == null || !task.Result.HasChanges)
            {
                activity.Dispose();
                return;
            }

            var prepared = task.Result;
            var conflicts = prepared.Conflicts;
            if (conflicts.Count == 0)
            {
                Apply(prepared, activity);
                return;
            }

            var handler = ConfirmationRequested;
            if (handler == null)
            {
                activity.Dispose();
                return;
            }

            activity.Dispose();
            var resolved = 0;
            handler(conflicts, overwrite =>
            {
                if (Interlocked.Exchange(ref resolved, 1) != 0)
                {
                    return;
                }

                if (!overwrite)
                {
                    activity.Dispose();
                    return;
                }

                Apply(prepared, activity);
            });
        }

        private static void Apply(PreparedStartupSync prepared, IDisposable activity)
        {
            activity.Dispose();
            var syncActivity = BackgroundActivityApi.Begin(I18N.Get("assetManager.background.datasourceSync"));
            Task.Run(() =>
            {
                var results = new List<AssetSyncResult>();
                if (prepared.Blm != null && prepared.Blm.Preview.HasChanges)
                {
                    results.Add(_assetManager.ApplyPreparedSync(prepared.Blm, true));
                }

                if (prepared.Eagle != null && prepared.Eagle.Preview.HasChanges)
                {
                    results.Add(_assetManager.ApplyPreparedSync(prepared.Eagle, true));
                }

                return results;
            }).ContinueWith(task =>
            {
                EditorApplication.delayCall += () =>
                {
                    syncActivity.Dispose();
                    if (task.IsFaulted)
                    {
                        if (task.Exception != null)
                        {
                            Debug.LogException(task.Exception.GetBaseException());
                        }

                        return;
                    }

                    if (!task.IsCanceled && task.Result.Any(result => result.State != AssetSyncState.Failed))
                    {
                        _assetManager.NotifyCatalogChanged();
                    }
                };
            });
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private sealed class PreparedStartupSync
        {
            internal PreparedStartupSync(PreparedAssetSync blm, PreparedAssetSync eagle)
            {
                Blm = blm;
                Eagle = eagle;
            }

            internal PreparedAssetSync Blm { get; }

            internal PreparedAssetSync Eagle { get; }

            internal bool HasChanges =>
                (Blm != null && Blm.Preview.HasChanges) ||
                (Eagle != null && Eagle.Preview.HasChanges);

            internal IReadOnlyList<AssetSyncConflict> Conflicts =>
                (Blm != null ? Blm.Preview.Conflicts : Array.Empty<AssetSyncConflict>())
                .Concat(Eagle != null ? Eagle.Preview.Conflicts : Array.Empty<AssetSyncConflict>())
                .ToArray();
        }
    }
}
