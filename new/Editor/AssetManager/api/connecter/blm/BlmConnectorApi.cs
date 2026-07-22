using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Ee4v.AssetManager.Api;
using Ee4v.AssetManager.Api.Connecter;
using Ee4v.Core.Settings;
using Ee4v.SQLite;
using SQLite;

namespace Ee4v.AssetManager.Api.Connecter.Blm
{
    internal static class BlmConnectorApi
    {
        private const string BoothBaseUrlFormat = "https://{0}.booth.pm";
        private const string BoothItemUrlFormat = "https://{0}.booth.pm/items/{1}";
        internal static IReadOnlyList<BlmItemRecord> ReadItems(BlmSyncRequest request)
        {
            var databasePath = request != null ? request.DatabasePath : null;
            var resolvedPath = ResolveDatabasePath(databasePath);
            if (!File.Exists(resolvedPath))
            {
                throw new AssetManagerException(AssetManagerErrorCode.DatasourceError, "BLM database was not found: " + resolvedPath);
            }

            SqliteBootstrap.EnsureInitialized();

            using (var snapshot = SqliteDatabaseSnapshot.Create(resolvedPath, "blm-db-snapshots"))
            using (var connection = OpenReadOnlyConnection(snapshot.DatabasePath))
            {
                var itemDirectoryPath = ResolveItemDirectoryPath(request, connection);
                return connection.Query<RegisteredItemQueryRow>(
                        @"SELECT
                            registered_items.id AS RegisteredItemId,
                            booth_items.id AS BoothItemId,
                            COALESCE(overwritten_booth_items.name, booth_items.name) AS Name,
                            booth_items.shop_subdomain AS ShopSubdomain,
                            COALESCE(overwritten_booth_items.description, booth_items.description) AS Description,
                            booth_items.thumbnail_url AS ThumbnailUrl,
                            shops.name AS ShopName,
                            shops.thumbnail_url AS ShopThumbnailUrl,
                            booth_item_update_history.last_updated_at AS LastUpdatedAt
                        FROM registered_items
                        INNER JOIN booth_items
                            ON booth_items.id = registered_items.booth_item_id
                        INNER JOIN shops
                            ON shops.subdomain = booth_items.shop_subdomain
                        LEFT JOIN overwritten_booth_items
                            ON overwritten_booth_items.booth_item_id = booth_items.id
                        LEFT JOIN booth_item_update_history
                            ON booth_item_update_history.booth_item_id = booth_items.id
                        ORDER BY booth_items.id")
                    .Select(row => new BlmItemRecord(
                        row.BoothItemId,
                        row.Name,
                        string.Format(BoothItemUrlFormat, row.ShopSubdomain, row.BoothItemId),
                        row.Description,
                        row.ThumbnailUrl,
                        row.ShopName,
                        string.Format(BoothBaseUrlFormat, row.ShopSubdomain),
                        row.ShopThumbnailUrl,
                        ParseUtcTimestamp(row.LastUpdatedAt),
                        ReadTags(connection, row.BoothItemId),
                        row.RegisteredItemId,
                        ReadRegisteredItemFiles(itemDirectoryPath, row.RegisteredItemId),
                        !string.IsNullOrWhiteSpace(itemDirectoryPath) && Directory.Exists(itemDirectoryPath)))
                    .ToArray();
            }
        }

        private static IReadOnlyList<string> ReadTags(SQLiteConnection connection, long boothItemId)
        {
            try
            {
                var overwritten = connection.Query<TagQueryRow>(
                    "SELECT tag AS Name FROM overwritten_booth_item_tags WHERE booth_item_id = ? ORDER BY tag COLLATE NOCASE",
                    boothItemId)
                    .Select(row => row.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToArray();
                if (overwritten.Length > 0)
                {
                    return overwritten;
                }

                return connection.Query<TagQueryRow>(
                    "SELECT tag AS Name FROM booth_item_tag_relations WHERE booth_item_id = ? ORDER BY tag COLLATE NOCASE",
                    boothItemId)
                    .Select(row => row.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToArray();
            }
            catch (SQLiteException)
            {
                return Array.Empty<string>();
            }
        }

        private static string ResolveDatabasePath(string databasePath = null)
        {
            var candidate = string.IsNullOrWhiteSpace(databasePath)
                ? SettingApi.Get(AssetManagerDefinitions.BlmDatabasePath)
                : Environment.ExpandEnvironmentVariables(databasePath);

            return Path.GetFullPath(candidate);
        }

        private static SQLiteConnection OpenReadOnlyConnection(string databasePath)
        {
            return new SQLiteConnection(
                databasePath,
                SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.PrivateCache);
        }

        private static DateTime? ParseUtcTimestamp(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            DateTime parsed;
            return DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out parsed)
                ? (DateTime?)parsed
                : (DateTime?)null;
        }

        private static string ResolveItemDirectoryPath(BlmSyncRequest request, SQLiteConnection connection)
        {
            if (request != null && !string.IsNullOrWhiteSpace(request.ItemDirectoryPath))
            {
                return NormalizePath(request.ItemDirectoryPath);
            }

            return NormalizePath(ReadPreferenceItemDirectoryPath(connection));
        }

        private static IReadOnlyList<BlmFileRecord> ReadRegisteredItemFiles(string itemDirectoryPath, string registeredItemId)
        {
            if (string.IsNullOrWhiteSpace(itemDirectoryPath) || string.IsNullOrWhiteSpace(registeredItemId))
            {
                return Array.Empty<BlmFileRecord>();
            }

            var registeredItemPath = Path.Combine(itemDirectoryPath, registeredItemId);
            if (!Directory.Exists(registeredItemPath))
            {
                return Array.Empty<BlmFileRecord>();
            }

            return Directory.GetFileSystemEntries(registeredItemPath, "*", SearchOption.TopDirectoryOnly)
                .Select(entryPath =>
                {
                    var relativePath = MakeRelativePath(registeredItemPath, entryPath);
                    var filePath = ResolveFilePath(entryPath);
                    return new BlmFileRecord(relativePath, filePath, GetEntrySizeBytes(filePath));
                })
                .Where(file => !string.IsNullOrWhiteSpace(file.RelativePath))
                .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string ReadPreferenceItemDirectoryPath(SQLiteConnection connection)
        {
            var row = connection.Query<PreferenceBlobQueryRow>(
                    "SELECT item_directory_path AS ItemDirectoryPath FROM preferences ORDER BY id LIMIT 1")
                .FirstOrDefault();
            if (row == null)
            {
                return null;
            }

            return DecodePathBytes(row.ItemDirectoryPath);
        }

        private static string DecodePathBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            var utf8 = CleanPathString(Encoding.UTF8.GetString(bytes));
            if (!ContainsReplacementCharacter(utf8))
            {
                return utf8;
            }

            return CleanPathString(Encoding.Unicode.GetString(bytes));
        }

        private static string NormalizePath(string path)
        {
            var cleaned = CleanPathString(path);
            return string.IsNullOrWhiteSpace(cleaned)
                ? null
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(cleaned));
        }

        private static string CleanPathString(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? null
                : path.Trim().Trim('"').Replace("\0", string.Empty);
        }

        private static bool ContainsReplacementCharacter(string value)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf('\uFFFD') >= 0;
        }

        private static long? GetEntrySizeBytes(string entryPath)
        {
            try
            {
                return File.Exists(entryPath) ? new FileInfo(entryPath).Length : (long?)null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string ResolveFilePath(string entryPath)
        {
            if (string.IsNullOrWhiteSpace(entryPath) || !Directory.Exists(entryPath))
            {
                return entryPath;
            }

            var singleChildDirectory = GetSingleChildDirectory(entryPath);
            if (string.IsNullOrWhiteSpace(singleChildDirectory))
            {
                return entryPath;
            }

            return string.Equals(
                Path.GetFileName(entryPath),
                Path.GetFileName(singleChildDirectory),
                StringComparison.OrdinalIgnoreCase)
                ? singleChildDirectory
                : entryPath;
        }

        private static string GetSingleChildDirectory(string path)
        {
            try
            {
                using (var directories = Directory.EnumerateDirectories(path).GetEnumerator())
                {
                    if (!directories.MoveNext())
                    {
                        return null;
                    }

                    var first = directories.Current;
                    if (directories.MoveNext())
                    {
                        return null;
                    }

                    using (var files = Directory.EnumerateFiles(path).GetEnumerator())
                    {
                        return files.MoveNext() ? null : first;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static string MakeRelativePath(string rootPath, string filePath)
        {
            var rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(rootPath)));
            var fileUri = new Uri(Path.GetFullPath(filePath));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path) || path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private sealed class RegisteredItemQueryRow
        {
            public string RegisteredItemId { get; set; }

            public long BoothItemId { get; set; }

            public string Name { get; set; }

            public string ShopSubdomain { get; set; }

            public string Description { get; set; }

            public string ThumbnailUrl { get; set; }

            public string ShopName { get; set; }

            public string ShopThumbnailUrl { get; set; }

            public string LastUpdatedAt { get; set; }
        }

        private sealed class PreferenceBlobQueryRow
        {
            public byte[] ItemDirectoryPath { get; set; }
        }

        private sealed class TagQueryRow
        {
            public string Name { get; set; }
        }

    }
}
