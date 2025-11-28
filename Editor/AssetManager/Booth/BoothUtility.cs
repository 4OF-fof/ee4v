using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Booth.Dialog;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.UI;
using _4OF.ee4v.AssetManager.UI.Component;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Booth {
    public static class BoothUtility {
        public enum BoothUrlType {
            ItemUrl,
            ShopUrl,
            DownloadUrl,
            Other
        }

        public static BoothUrlType ClassifyBoothUrl(string url) {
            if (string.IsNullOrWhiteSpace(url))
                return BoothUrlType.Other;

            if (Regex.IsMatch(url, @"https?://(?:[^\.]+\.booth\.pm|booth\.pm/[^/]+)/items/\d+",
                    RegexOptions.IgnoreCase))
                return BoothUrlType.ItemUrl;

            if (Regex.IsMatch(url, @"https?://[^\.]+\.booth\.pm/?$", RegexOptions.IgnoreCase))
                return BoothUrlType.ShopUrl;

            return Regex.IsMatch(url, @"https?://booth\.pm/downloadables/\d+", RegexOptions.IgnoreCase)
                ? BoothUrlType.DownloadUrl
                : BoothUrlType.Other;
        }


        public static bool TryParseShopItemUrl(string url, out string shopDomain, out string itemId) {
            shopDomain = string.Empty;
            itemId = string.Empty;
            if (string.IsNullOrWhiteSpace(url)) return false;

            var m = Regex.Match(url,
                @"^https?://(?<sub>[^\.]+)\.booth\.pm/items/(?<id>\d+)",
                RegexOptions.IgnoreCase);
            if (!m.Success) return false;

            shopDomain = m.Groups["sub"].Value;
            itemId = m.Groups["id"].Value;
            return true;
        }

        public static async void FetchAndDownloadThumbnails(List<BoothItemFolder> folders, IAssetRepository repository,
            Action<VisualElement> showDialog) {
            var jobs = new Dictionary<Ulid, string>();
            using var client = new HttpClient();

            foreach (var folder in folders) {
                if (string.IsNullOrEmpty(folder.ItemUrl)) continue;

                var jsonUrl = folder.ItemUrl + ".json";

                try {
                    var jsonStr = await client.GetStringAsync(jsonUrl);
                    var json = JObject.Parse(jsonStr);

                    if (json["images"] is JArray images && images.Count > 0) {
                        var originalUrl = images[0]["original"]?.ToString();
                        if (!string.IsNullOrEmpty(originalUrl)) jobs[folder.ID] = originalUrl;
                    }
                }
                catch (Exception e) {
                    Debug.LogWarning($"[AssetManager] Failed to fetch info for {folder.Name}: {e.Message}");
                }
            }

            if (jobs.Count > 0)
                EditorApplication.delayCall += () =>
                {
                    BoothThumbnailDownloader.Enqueue(repository, jobs);
                    showDialog?.Invoke(DownloadThumbnailDialog.CreateContent());
                };
            else
                EditorApplication.delayCall += () =>
                {
                    try {
                        AssetManagerWindow.ShowToastMessage(
                            I18N.Get("UI.AssetManager.DownloadThumbnail.NoImagesFound") ?? "No thumbnails found.",
                            2f, ToastType.Error);
                    }
                    catch {
                        /* ignore */
                    }
                };
        }
    }
}