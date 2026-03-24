using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.SQLite;
using SQLite;

namespace Ee4v.AssetManager
{
    public static class AssetManagerApi
    {
        private const string BoothBaseUrlFormat = "https://{0}.booth.pm";
        private const string BoothItemUrlFormat = "https://{0}.booth.pm/items/{1}";
        private const string BoothLibraryRelativePath = "pm.booth.library-manager\\data.db";

        public static string GetDefaultBoothLibraryDatabasePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                BoothLibraryRelativePath);
        }

        public static string ResolveBoothLibraryDatabasePath(string databasePath = null)
        {
            var candidate = string.IsNullOrWhiteSpace(databasePath)
                ? GetDefaultBoothLibraryDatabasePath()
                : Environment.ExpandEnvironmentVariables(databasePath);

            return Path.GetFullPath(candidate);
        }

        public static bool BoothLibraryDatabaseExists(string databasePath = null)
        {
            return File.Exists(ResolveBoothLibraryDatabasePath(databasePath));
        }

        public static BoothLibraryItemRecord GetBoothLibraryItemById(long boothItemId, string databasePath = null)
        {
            BoothLibraryItemRecord item;
            return TryGetBoothLibraryItemById(boothItemId, out item, databasePath) ? item : null;
        }

        public static bool TryGetBoothLibraryItemById(long boothItemId, out BoothLibraryItemRecord item, string databasePath = null)
        {
            item = null;
            if (boothItemId <= 0)
            {
                return false;
            }

            var resolvedPath = ResolveBoothLibraryDatabasePath(databasePath);
            if (!File.Exists(resolvedPath))
            {
                return false;
            }

            SqliteBootstrap.EnsureInitialized();

            using (var snapshot = BoothLibraryDatabaseSnapshot.Create(resolvedPath))
            using (var connection = OpenReadOnlyConnection(snapshot.DatabasePath))
            {
                var row = connection.Query<BoothItemQueryRow>(
                        @"SELECT
                            booth_items.id AS BoothItemId,
                            COALESCE(overwritten_booth_items.name, booth_items.name) AS Name,
                            booth_items.shop_subdomain AS ShopSubdomain,
                            COALESCE(overwritten_booth_items.description, booth_items.description) AS Description,
                            booth_items.thumbnail_url AS ThumbnailUrl,
                            shops.name AS ShopName,
                            shops.thumbnail_url AS ShopThumbnailUrl
                        FROM booth_items
                        INNER JOIN shops
                            ON shops.subdomain = booth_items.shop_subdomain
                        LEFT JOIN overwritten_booth_items
                            ON overwritten_booth_items.booth_item_id = booth_items.id
                        WHERE booth_items.id = ?
                        LIMIT 1",
                        boothItemId)
                    .FirstOrDefault();

                if (row == null)
                {
                    return false;
                }

                var tags = LoadTags(connection, boothItemId);
                item = new BoothLibraryItemRecord(
                    row.BoothItemId,
                    row.Name,
                    string.Format(BoothItemUrlFormat, row.ShopSubdomain, row.BoothItemId),
                    row.Description,
                    row.ThumbnailUrl,
                    row.ShopName,
                    string.Format(BoothBaseUrlFormat, row.ShopSubdomain),
                    row.ShopThumbnailUrl,
                    tags);
                return true;
            }
        }

        private static SQLiteConnection OpenReadOnlyConnection(string databasePath)
        {
            return new SQLiteConnection(
                databasePath,
                SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.PrivateCache);
        }

        private static IReadOnlyList<string> LoadTags(SQLiteConnection connection, long boothItemId)
        {
            var overriddenTags = connection.Query<TagQueryRow>(
                    @"SELECT tag AS Tag
                      FROM overwritten_booth_item_tags
                      WHERE booth_item_id = ?
                      ORDER BY tag COLLATE NOCASE, tag",
                    boothItemId)
                .Select(row => row.Tag)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToArray();

            if (overriddenTags.Length > 0)
            {
                return overriddenTags;
            }

            return connection.Query<TagQueryRow>(
                    @"SELECT tag AS Tag
                      FROM booth_item_tag_relations
                      WHERE booth_item_id = ?
                      ORDER BY tag COLLATE NOCASE, tag",
                    boothItemId)
                .Select(row => row.Tag)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToArray();
        }

        private sealed class BoothItemQueryRow
        {
            public long BoothItemId { get; set; }

            public string Name { get; set; }

            public string ShopSubdomain { get; set; }

            public string Description { get; set; }

            public string ThumbnailUrl { get; set; }

            public string ShopName { get; set; }

            public string ShopThumbnailUrl { get; set; }
        }

        private sealed class TagQueryRow
        {
            public string Tag { get; set; }
        }
    }
}
