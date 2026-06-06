using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Ee4v.AssetManager.Api
{
    internal static partial class AssetManagerDatabase
    {
        private static readonly HttpClient ThumbnailHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public static AssetThumbnail GetThumbnail(string itemId)
        {
            using (var connection = OpenConnection())
            {
                EnsureItemExists(connection, itemId);
                var thumbnailUrl = connection.ExecuteScalar<string>(
                    "SELECT thumbnail_url FROM booth_info WHERE item_info_id = ? LIMIT 1",
                    itemId);

                if (string.IsNullOrWhiteSpace(thumbnailUrl))
                {
                    return MissingThumbnail("Thumbnail URL was not found.");
                }

                var cachePath = GetThumbnailCachePath(itemId, thumbnailUrl);
                if (File.Exists(cachePath))
                {
                    try
                    {
                        var cachedData = File.ReadAllBytes(cachePath);
                        if (cachedData.Length > 0)
                        {
                            return FoundThumbnail(cachedData, cachePath, thumbnailUrl);
                        }
                    }
                    catch
                    {
                        // Ignore unreadable cache files and attempt a fresh download.
                    }
                }

                try
                {
                    using (var response = ThumbnailHttpClient.GetAsync(thumbnailUrl).GetAwaiter().GetResult())
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            return MissingThumbnail("Thumbnail download failed: " + response.StatusCode, cachePath, thumbnailUrl);
                        }

                        var data = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        if (data == null || data.Length == 0)
                        {
                            return MissingThumbnail("Thumbnail download returned empty data.", cachePath, thumbnailUrl);
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                        File.WriteAllBytes(cachePath, data);
                        return FoundThumbnail(data, cachePath, thumbnailUrl);
                    }
                }
                catch (Exception exception)
                {
                    return MissingThumbnail("Thumbnail download failed: " + exception.Message, cachePath, thumbnailUrl);
                }
            }
        }

        private static AssetThumbnail FoundThumbnail(byte[] data, string cachePath, string sourceUrl)
        {
            return new AssetThumbnail
            {
                Found = true,
                Data = data,
                Path = cachePath,
                SourceUrl = sourceUrl,
                MissingReason = string.Empty
            };
        }

        private static AssetThumbnail MissingThumbnail(string reason, string cachePath = null, string sourceUrl = null)
        {
            return new AssetThumbnail
            {
                Found = false,
                Data = Array.Empty<byte>(),
                Path = cachePath ?? string.Empty,
                SourceUrl = sourceUrl ?? string.Empty,
                MissingReason = reason ?? string.Empty
            };
        }

        private static string GetThumbnailCachePath(string itemId, string thumbnailUrl)
        {
            var globalPath = Environment.ExpandEnvironmentVariables(Ee4v.Core.Settings.SettingApi.Get(AssetManagerDefinitions.Ee4vGlobalPath));
            var fileName = itemId + "-" + HashThumbnailUrl(thumbnailUrl) + GetThumbnailExtension(thumbnailUrl);
            return Path.GetFullPath(Path.Combine(globalPath, "cache", "thumbnails", "items", fileName));
        }

        private static string HashThumbnailUrl(string thumbnailUrl)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(thumbnailUrl ?? string.Empty));
                return string.Concat(bytes.Take(8).Select(value => value.ToString("x2")).ToArray());
            }
        }

        private static string GetThumbnailExtension(string thumbnailUrl)
        {
            Uri uri;
            if (Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out uri))
            {
                var extension = Path.GetExtension(uri.AbsolutePath);
                if (IsSupportedThumbnailExtension(extension))
                {
                    return extension.ToLowerInvariant();
                }
            }

            return ".png";
        }

        private static bool IsSupportedThumbnailExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            var normalized = extension.ToLowerInvariant();
            return normalized == ".png" ||
                   normalized == ".jpg" ||
                   normalized == ".jpeg" ||
                   normalized == ".webp";
        }
    }
}
