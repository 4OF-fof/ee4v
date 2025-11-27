using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.UI;
using _4OF.ee4v.AssetManager.UI.Component;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Booth {
    public static class BoothThumbnailDownloader {
        private static readonly HttpClient Http = new();
        private static int _completedCount;

        public static bool IsRunning { get; private set; }

        public static int TotalCount { get; private set; }

        public static int CompletedCount => _completedCount;

        public static event Action<int, int> OnProgressChanged;
        public static event Action OnStarted;
        public static event Action OnCompleted;

        public static void Enqueue(IAssetRepository repository, Dictionary<Ulid, string> jobs) {
            if (IsRunning) {
                Debug.LogWarning(I18N.Get("Debug.AssetManager.Download.AlreadyRunning"));
                return;
            }

            if (repository == null || jobs == null || jobs.Count == 0) return;

            var filtered = jobs.Where(kv => kv.Key != Ulid.Empty && !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (filtered.Count == 0) {
                TotalCount = 0;
                _completedCount = 0;
                IsRunning = false;
                return;
            }

            var total = filtered.Count;
            TotalCount = total;
            _completedCount = 0;
            IsRunning = true;
            EditorApplication.delayCall += () =>
            {
                try {
                    OnStarted?.Invoke();
                }
                catch {
                    /* ignore */
                }

                try {
                    OnProgressChanged?.Invoke(TotalCount, _completedCount);
                }
                catch {
                    /* ignore */
                }
            };

            Task.Run(async () =>
            {
                try {
                    foreach (var (folderId, url) in filtered) {
                        try {
                            var existingPath = repository.GetFolderThumbnailPath(folderId);
                            if (!string.IsNullOrEmpty(existingPath) && File.Exists(existingPath)) {
                                Interlocked.Increment(ref _completedCount);
                                var completedNow = _completedCount;
                                EditorApplication.delayCall += () =>
                                {
                                    try {
                                        OnProgressChanged?.Invoke(TotalCount, completedNow);
                                    }
                                    catch {
                                        /* ignore */
                                    }
                                };
                                await Task.Delay(1000);
                                continue;
                            }

                            string tempPath = null;
                            try {
                                using var resp = await Http.GetAsync(url);
                                if (!resp.IsSuccessStatusCode) {
                                    Debug.LogWarning(I18N.Get("Debug.AssetManager.Download.HttpFailedFmt", resp.StatusCode, url));
                                }
                                else {
                                    var bytes = await resp.Content.ReadAsByteArrayAsync();
                                    if (bytes != null && bytes.Length > 0) {
                                        var ext = Path.GetExtension(url) ?? string.Empty;
                                        if (string.IsNullOrWhiteSpace(ext)) {
                                            var ct = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
                                            ext = ct.Contains("jpeg") ? ".jpg" : ".png";
                                        }

                                        tempPath = Path.Combine(Path.GetTempPath(),
                                            $"ee4v_booththumb_{Guid.NewGuid():N}{ext}");
                                        await File.WriteAllBytesAsync(tempPath, bytes);

                                        var localTemp = tempPath;
                                        EditorApplication.delayCall += () =>
                                        {
                                            try {
                                                repository.SetFolderThumbnail(folderId, localTemp);
                                            }
                                            catch (Exception e) {
                                                Debug.LogError(I18N.Get("Debug.AssetManager.Download.SetFolderThumbnailFailedFmt", folderId, e.Message));
                                            }

                                            try {
                                                if (File.Exists(localTemp)) File.Delete(localTemp);
                                            }
                                            catch {
                                                /* ignore */
                                            }
                                        };
                                    }
                                }
                            }
                            catch (Exception e) {
                                Debug.LogWarning(I18N.Get("Debug.AssetManager.Download.FailedToDownloadFromUrlFmt", url, e.Message));
                                if (!string.IsNullOrEmpty(tempPath))
                                    try {
                                        File.Delete(tempPath);
                                    }
                                    catch {
                                        /* ignore */
                                    }
                            }

                            Interlocked.Increment(ref _completedCount);
                            var now = _completedCount;
                            EditorApplication.delayCall += () =>
                            {
                                try {
                                    OnProgressChanged?.Invoke(TotalCount, now);
                                }
                                catch {
                                    /* ignore */
                                }
                            };
                        }
                        catch (Exception e) {
                            Debug.LogWarning(I18N.Get("Debug.AssetManager.Download.FailedProcessingFolderFmt", folderId, e.Message));
                            Interlocked.Increment(ref _completedCount);
                            var now = _completedCount;
                            EditorApplication.delayCall += () =>
                            {
                                try {
                                    OnProgressChanged?.Invoke(TotalCount, now);
                                }
                                catch {
                                    /* ignore */
                                }
                            };
                        }

                        await Task.Delay(1000);
                    }
                }
                catch {
                    /* ignore */
                }

                NotifyComplete(total);
                IsRunning = false;
                EditorApplication.delayCall += () =>
                {
                    try {
                        OnCompleted?.Invoke();
                    }
                    catch {
                        /* ignore */
                    }
                };
            });
            return;

            static void NotifyComplete(int count) {
                EditorApplication.delayCall += () =>
                {
                    try {
                        AssetManagerWindow.ShowToastMessage(I18N.Get("UI.AssetManager.DownloadThumbnail.FinishedAttemptedFmt", count), 4f,
                            ToastType.Success);
                    }
                    catch {
                        EditorUtility.DisplayDialog(I18N.Get("UI.Core.AppName"), I18N.Get("UI.AssetManager.DownloadThumbnail.FinishedAttemptedFmt", count), I18N.Get("UI.Core.OK"));
                    }
                };
            }
        }
    }
}