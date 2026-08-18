using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Infrastructure
{
    internal sealed class AssetThumbnailProvider
        : IAssetThumbnailProvider
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private readonly string _cacheRoot;
        private readonly HttpClient _httpClient;

        internal AssetThumbnailProvider(string databasePath)
            : this(databasePath, HttpClient)
        {
        }

        internal AssetThumbnailProvider(
            string databasePath,
            HttpClient httpClient)
        {
            var directory = Path.GetDirectoryName(
                Path.GetFullPath(databasePath));
            _cacheRoot = Path.Combine(
                string.IsNullOrWhiteSpace(directory)
                    ? Directory.GetCurrentDirectory()
                    : directory,
                "cache",
                "asset-manager",
                "thumbnails");
            _httpClient = httpClient ??
                          throw new ArgumentNullException(nameof(httpClient));
        }

        public Task<AssetThumbnail> Get(
            AssetItem item,
            CancellationToken cancellationToken)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                throw new ArgumentNullException(nameof(item));
            }

            return Get(item.Id, item.ThumbnailUrl, cancellationToken);
        }

        public async Task<IReadOnlyDictionary<string, AssetThumbnail>> GetMany(
            IReadOnlyList<AssetItem> items,
            CancellationToken cancellationToken)
        {
            var source = (items ?? Array.Empty<AssetItem>())
                .Where(item => item != null &&
                               !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            using (var gate = new SemaphoreSlim(4, 4))
            {
                var tasks = source.Select(async item =>
                {
                    await gate.WaitAsync(cancellationToken);
                    try
                    {
                        return await Get(
                            item.Id,
                            item.ThumbnailUrl,
                            cancellationToken);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }).ToArray();
                var thumbnails = await Task.WhenAll(tasks);
                var result = new Dictionary<string, AssetThumbnail>(
                    source.Length,
                    StringComparer.Ordinal);
                for (var i = 0; i < source.Length; i++)
                {
                    result[source[i].Id] = thumbnails[i];
                }

                return result;
            }
        }

        private async Task<AssetThumbnail> Get(
            string itemId,
            string sourceUrl,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetHttpUri(sourceUrl, out var uri))
            {
                return Missing("Thumbnail URL was not found.");
            }

            var cachePath = Path.Combine(
                _cacheRoot,
                itemId + "-" + Hash(sourceUrl) + Extension(uri));
            if (File.Exists(cachePath))
            {
                try
                {
                    var data = await Task.Run(
                        () => File.ReadAllBytes(cachePath),
                        cancellationToken);
                    if (data.Length > 0)
                    {
                        return Found(data, cachePath, sourceUrl);
                    }
                }
                catch
                {
                }
            }

            try
            {
                using (var response = await _httpClient.GetAsync(
                           uri,
                           HttpCompletionOption.ResponseHeadersRead,
                           cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return Missing(
                            "Thumbnail download failed: " +
                            response.StatusCode,
                            cachePath,
                            sourceUrl);
                    }

                    var data = await response.Content
                        .ReadAsByteArrayAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (data == null || data.Length == 0)
                    {
                        return Missing(
                            "Thumbnail download returned empty data.",
                            cachePath,
                            sourceUrl);
                    }

                    await Task.Run(() =>
                    {
                        Directory.CreateDirectory(_cacheRoot);
                        File.WriteAllBytes(cachePath, data);
                    }, cancellationToken);
                    return Found(data, cachePath, sourceUrl);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Missing(
                    "Thumbnail download failed: " + exception.Message,
                    cachePath,
                    sourceUrl);
            }
        }

        private static bool TryGetHttpUri(
            string value,
            out Uri uri)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp ||
                 uri.Scheme == Uri.UriSchemeHttps))
            {
                return true;
            }

            uri = null;
            return false;
        }

        private static AssetThumbnail Found(
            byte[] data,
            string path,
            string sourceUrl)
        {
            return new AssetThumbnail
            {
                Found = true,
                Data = data,
                Path = path,
                SourceUrl = sourceUrl,
                MissingReason = string.Empty
            };
        }

        private static AssetThumbnail Missing(
            string reason,
            string path = null,
            string sourceUrl = null)
        {
            return new AssetThumbnail
            {
                Found = false,
                Data = Array.Empty<byte>(),
                Path = path ?? string.Empty,
                SourceUrl = sourceUrl ?? string.Empty,
                MissingReason = reason ?? string.Empty
            };
        }

        private static string Hash(string value)
        {
            using (var sha256 = SHA256.Create())
            {
                return string.Concat(sha256
                    .ComputeHash(Encoding.UTF8.GetBytes(value))
                    .Take(8)
                    .Select(octet => octet.ToString("x2"))
                    .ToArray());
            }
        }

        private static string Extension(Uri uri)
        {
            var extension = Path.GetExtension(uri.AbsolutePath)
                .ToLowerInvariant();
            return extension == ".png" ||
                   extension == ".jpg" ||
                   extension == ".jpeg" ||
                   extension == ".webp"
                ? extension
                : ".png";
        }
    }
}
