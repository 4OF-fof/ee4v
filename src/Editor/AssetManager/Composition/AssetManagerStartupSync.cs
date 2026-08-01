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
        private static ISettingsService _settings;
        private static int _running;
        private static int _manualReloadRequested;
        private static readonly object CompletionLock = new object();
        private static readonly HashSet<Action> CompletionCallbacks =
            new HashSet<Action>();

        internal static event Action<IReadOnlyList<AssetSyncConflict>, Action<bool>> ConfirmationRequested;

        internal static void EnsureInitialized(
            AssetManagerService assetManager,
            ISettingsService settings)
        {
            _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
            _settings.Preload(SettingScope.User);

            var blmPath = ResolvePath(_settings.Get(AssetManagerDefinitions.BlmDatabasePath));
            var eaglePath = ResolvePath(_settings.Get(AssetManagerDefinitions.EagleLibraryPath));
            var checkBlm = _settings.Get(AssetManagerDefinitions.AutoSyncBlmOnStartup) && File.Exists(blmPath);
            var checkEagle = _settings.Get(AssetManagerDefinitions.AutoSyncEagleOnStartup) && Directory.Exists(eaglePath);
            if (!checkBlm && !checkEagle)
            {
                return;
            }

            StartSync(checkBlm, blmPath, checkEagle, eaglePath);
        }

        internal static void RequestManualSync(Action completed)
        {
            if (_assetManager == null || _settings == null)
            {
                InvokeCompletion(completed);
                return;
            }

            _settings.Preload(SettingScope.User);
            var blmPath = ResolvePath(
                _settings.Get(AssetManagerDefinitions.BlmDatabasePath));
            var eaglePath = ResolvePath(
                _settings.Get(AssetManagerDefinitions.EagleLibraryPath));
            var checkBlm = File.Exists(blmPath);
            var checkEagle = Directory.Exists(eaglePath);
            if (!checkBlm && !checkEagle)
            {
                Debug.LogWarning(I18N.Get(
                    "assetManager.background.noDatasource"));
            }

            if (completed != null)
            {
                lock (CompletionLock)
                {
                    CompletionCallbacks.Add(completed);
                }
            }

            Interlocked.Exchange(
                ref _manualReloadRequested,
                1);
            StartSync(checkBlm, blmPath, checkEagle, eaglePath);
        }

        private static void StartSync(
            bool checkBlm,
            string blmPath,
            bool checkEagle,
            string eaglePath)
        {
            if (Interlocked.CompareExchange(
                    ref _running,
                    1,
                    0) != 0)
            {
                return;
            }

            var activity = CoreBackgroundActivities.Current.Begin(
                I18N.Get("assetManager.background.datasourceCheck"));
            Task.Run(() => Prepare(checkBlm, blmPath, checkEagle, eaglePath)).ContinueWith(task =>
            {
                EditorApplication.delayCall += () => HandlePrepared(task, activity);
            });
        }

        private static PreparedStartupSync Prepare(bool checkBlm, string blmPath, bool checkEagle, string eaglePath)
        {
            // A manual reload must also recreate an absent database before
            // the open views are asked to read it again.
            _assetManager.GetCollections();
            return new PreparedStartupSync(
                checkBlm ? _assetManager.PrepareBlmSync(new BlmSyncRequest(blmPath)) : null,
                checkEagle ? _assetManager.PrepareEagleSync(new EagleSyncRequest(eaglePath)) : null);
        }

        private static void HandlePrepared(Task<PreparedStartupSync> task, IDisposable activity)
        {
            activity.Dispose();
            if (task.IsFaulted)
            {
                if (task.Exception != null)
                {
                    Debug.LogException(task.Exception.GetBaseException());
                }

                CompleteSync();
                return;
            }

            if (task.IsCanceled || task.Result == null || !task.Result.HasChanges)
            {
                CompleteSync();
                return;
            }

            var prepared = task.Result;
            var conflicts = prepared.Conflicts;
            if (conflicts.Count == 0)
            {
                Apply(prepared);
                return;
            }

            var handler = ConfirmationRequested;
            if (handler == null)
            {
                CompleteSync();
                return;
            }

            var resolved = 0;
            try
            {
                handler(conflicts, overwrite =>
                {
                    if (Interlocked.Exchange(ref resolved, 1) != 0)
                    {
                        return;
                    }

                    if (!overwrite)
                    {
                        CompleteSync();
                        return;
                    }

                    Apply(prepared);
                });
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                CompleteSync();
            }
        }

        private static void Apply(PreparedStartupSync prepared)
        {
            var syncActivity = CoreBackgroundActivities.Current.Begin(
                I18N.Get("assetManager.background.datasourceSync"));
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

                        CompleteSync();
                        return;
                    }

                    var catalogNotified =
                        !task.IsCanceled &&
                        task.Result.Any(result =>
                            result.State != AssetSyncState.Failed);
                    if (catalogNotified)
                    {
                        _assetManager.NotifyCatalogChanged();
                    }

                    CompleteSync(catalogNotified);
                };
            });
        }

        private static void CompleteSync(
            bool catalogAlreadyNotified = false)
        {
            Interlocked.Exchange(ref _running, 0);
            Action[] callbacks;
            lock (CompletionLock)
            {
                callbacks = CompletionCallbacks.ToArray();
                CompletionCallbacks.Clear();
            }

            var manualReloadRequested =
                Interlocked.Exchange(
                    ref _manualReloadRequested,
                    0) != 0;
            if (ShouldNotifyManualReload(
                    catalogAlreadyNotified,
                    manualReloadRequested))
            {
                _assetManager.NotifyCatalogChanged();
            }

            for (var i = 0; i < callbacks.Length; i++)
            {
                InvokeCompletion(callbacks[i]);
            }
        }

        internal static bool ShouldNotifyManualReload(
            bool catalogAlreadyNotified,
            bool manualReloadRequested)
        {
            return !catalogAlreadyNotified &&
                   manualReloadRequested;
        }

        private static void InvokeCompletion(Action completed)
        {
            if (completed == null)
            {
                return;
            }

            try
            {
                completed();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
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
