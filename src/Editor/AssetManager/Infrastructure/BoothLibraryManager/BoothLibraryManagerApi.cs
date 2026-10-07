using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.SQLite;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure.BoothLibraryManager
{
    // Every query uses a read-only connection and one read transaction, including WAL data.
    public static class BoothLibraryManagerApi
    {
        public static string GetDefaultDatabasePath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "pm.booth.library-manager", "data.db");

        public static bool DatabaseExists(string databasePath = null) =>
            File.Exists(ResolvePath(databasePath));

        public static BoothLibraryManagerItemRecord GetItemById(long boothItemId,
            string databasePath = null)
        {
            TryGetItemById(boothItemId, out var item, databasePath);
            return item;
        }

        public static bool TryGetItemById(long boothItemId,
            out BoothLibraryManagerItemRecord item, string databasePath = null)
        {
            item = null;
            if (boothItemId <= 0 || !DatabaseExists(databasePath)) { return false; }
            using (var connection = Open(databasePath))
            {
                connection.Execute("BEGIN");
                var row = connection.Query<BoothLibraryManagerItemRecord>(
                    BoothQuery + " WHERE b.id = ? LIMIT 1", boothItemId).FirstOrDefault();
                if (row == null) { return false; }
                Complete(connection, row);
                item = row;
                return true;
            }
        }

        public static IReadOnlyList<BoothLibraryManagerItemRecord> GetRegisteredItems(
            string databasePath)
        {
            using (var connection = Open(databasePath))
            {
                connection.Execute("BEGIN");
                var rows = connection.Query<BoothLibraryManagerItemRecord>(
                    @"SELECT r.id AS RegisteredId, COALESCE(b.id, 0) AS BoothItemId,
                        COALESCE(o.name, b.name, u.name) AS Name,
                        COALESCE(o.description, b.description, u.description, '') AS Description,
                        b.thumbnail_url AS ThumbnailUrl, b.shop_subdomain AS ShopSubdomain,
                        COALESCE(s.name, u.shop_name) AS ShopName,
                        s.thumbnail_url AS ShopThumbnailUrl
                      FROM registered_items r
                      LEFT JOIN booth_items b ON b.id = r.booth_item_id
                      LEFT JOIN overwritten_booth_items o ON o.booth_item_id = b.id
                      LEFT JOIN shops s ON s.subdomain = b.shop_subdomain
                      LEFT JOIN user_item_info u ON u.id = r.user_item_info_id
                      ORDER BY r.id");
                foreach (var row in rows) { Complete(connection, row); }
                return rows;
            }
        }

        private const string BoothQuery = @"SELECT b.id AS BoothItemId,
            COALESCE(o.name, b.name) AS Name, b.shop_subdomain AS ShopSubdomain,
            COALESCE(o.description, b.description, '') AS Description,
            b.thumbnail_url AS ThumbnailUrl, s.name AS ShopName,
            s.thumbnail_url AS ShopThumbnailUrl FROM booth_items b
            LEFT JOIN shops s ON s.subdomain = b.shop_subdomain
            LEFT JOIN overwritten_booth_items o ON o.booth_item_id = b.id";

        private static string ResolvePath(string path) => Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(string.IsNullOrWhiteSpace(path)
                ? GetDefaultDatabasePath() : path));

        private static SQLiteConnection Open(string path)
        {
            var resolved = ResolvePath(path);
            if (!File.Exists(resolved)) { throw new FileNotFoundException("BLM database was not found.", resolved); }
            SqliteBootstrap.EnsureInitialized();
            return new SQLiteConnection(resolved, SQLiteOpenFlags.ReadOnly |
                SQLiteOpenFlags.FullMutex | SQLiteOpenFlags.PrivateCache);
        }

        private static void Complete(SQLiteConnection connection, BoothLibraryManagerItemRecord item)
        {
            item.ItemUrl = item.BoothItemId > 0 ? "https://booth.pm/items/" + item.BoothItemId : null;
            item.ShopUrl = string.IsNullOrWhiteSpace(item.ShopSubdomain)
                ? null : "https://" + item.ShopSubdomain + ".booth.pm";
            var tags = connection.Query<TagRow>(
                "SELECT tag AS Tag FROM overwritten_booth_item_tags WHERE booth_item_id = ? ORDER BY tag",
                item.BoothItemId);
            if (tags.Count == 0)
            {
                tags = connection.Query<TagRow>(
                    "SELECT tag AS Tag FROM booth_item_tag_relations WHERE booth_item_id = ? ORDER BY tag",
                    item.BoothItemId);
            }
            item.Tags = tags.Select(value => value.Tag).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        private sealed class TagRow { public string Tag { get; set; } }
    }

    public sealed class BoothLibraryManagerItemRecord
    {
        public string RegisteredId { get; set; }
        public long BoothItemId { get; set; }
        public string Name { get; set; }
        public string ItemUrl { get; set; }
        public string Description { get; set; }
        public string ThumbnailUrl { get; set; }
        public string ShopSubdomain { get; set; }
        public string ShopName { get; set; }
        public string ShopUrl { get; set; }
        public string ShopThumbnailUrl { get; set; }
        [Ignore] public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
    }
}
