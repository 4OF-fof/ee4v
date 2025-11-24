using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.UI.Window;
using _4OF.ee4v.AssetManager.UI.Window._Component;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Adapter {
    public static class BoothThumbnailDownloader {
        private static readonly HttpClient Http = new();

        public static void Enqueue(IAssetRepository repository, Dictionary<Ulid, string> jobs) {
            if (repository == null || jobs == null || jobs.Count == 0) return;

            var filtered = jobs.Where(kv => kv.Key != Ulid.Empty && !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (filtered.Count == 0) return;

            var total = filtered.Count;
            var tasks = new List<Task>();

            foreach (var kv in filtered) {
                var folderId = kv.Key;
                var url = kv.Value;

                try {
                    var existingPath = repository.GetFolderThumbnailPath(folderId);
                    if (!string.IsNullOrEmpty(existingPath) && File.Exists(existingPath)) {
                        tasks.Add(Task.CompletedTask);
                        continue;
                    }

                    var downloadTask = Task.Run(async () =>
                    {
                        string tempPath = null;
                        try {
                            using var resp = await Http.GetAsync(url);
                            if (!resp.IsSuccessStatusCode) {
                                Debug.LogWarning($"Booth thumbnail download failed (HTTP {resp.StatusCode}) for {url}");
                                return;
                            }

                            var bytes = await resp.Content.ReadAsByteArrayAsync();
                            if (bytes == null || bytes.Length == 0) return;

                            var ext = Path.GetExtension(url) ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(ext)) {
                                var ct = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
                                ext = ct.Contains("jpeg") ? ".jpg" : ".png";
                            }

                            tempPath = Path.Combine(Path.GetTempPath(), $"ee4v_booththumb_{Guid.NewGuid():N}{ext}");
                            await File.WriteAllBytesAsync(tempPath, bytes);

                            var localTemp = tempPath;
                            EditorApplication.delayCall += () =>
                            {
                                try {
                                    repository.SetFolderThumbnail(folderId, localTemp);
                                }
                                catch (Exception e) {
                                    Debug.LogError($"Failed to set folder thumbnail for {folderId}: {e.Message}");
                                }

                                try {
                                    if (File.Exists(localTemp)) File.Delete(localTemp);
                                }
                                catch {
                                    /* ignore */
                                }
                            };
                        }
                        catch (Exception e) {
                            Debug.LogWarning($"Failed downloading booth image from '{url}' : {e.Message}");
                            if (!string.IsNullOrEmpty(tempPath))
                                try {
                                    File.Delete(tempPath);
                                }
                                catch {
                                    /* ignore */
                                }
                        }
                    });

                    tasks.Add(downloadTask);
                }
                catch (Exception e) {
                    Debug.LogWarning($"Failed scheduling booth thumbnail download for folder {folderId}: {e.Message}");
                    tasks.Add(Task.CompletedTask);
                }
            }

            Task.Run(async () =>
            {
                try {
                    await Task.WhenAll(tasks);
                }
                catch {
                    /* ignore */
                }

                NotifyComplete(total);
            });
            return;

            static void NotifyComplete(int count) {
                EditorApplication.delayCall += () =>
                {
                    try {
                        AssetManagerWindow.ShowToastMessage($"Booth thumbnails finished: {count} attempted", 4f,
                            ToastType.Success);
                    }
                    catch {
                        EditorUtility.DisplayDialog("ee4v", $"Booth thumbnails finished: {count} attempted", "OK");
                    }
                };
            }
        }
    }
}