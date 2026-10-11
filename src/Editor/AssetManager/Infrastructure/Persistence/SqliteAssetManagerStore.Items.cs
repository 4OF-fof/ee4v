using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Ee4v.AssetManager.Contracts;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure.Persistence
{
    internal sealed partial class SqliteAssetManagerStore
    {
        // The tag query binds each ID twice. Stay below SQLite's 999 parameter limit.
        private const int ItemReadBatchSize = 400;

        private string OriginalItemsPath => Path.Combine(
            Path.GetDirectoryName(_databasePath), "Assets", "OriginalItems");

        private sealed class OriginalItemRecord
        {
            public int SchemaVersion { get; set; } = 1;
            public AssetItem Item { get; set; }
        }

        private void RestoreOriginalItems(SQLiteConnection connection)
        {
            if (!Directory.Exists(OriginalItemsPath)) { return; }
            using (var transaction = new DatabaseTransaction(connection))
            {
                foreach (var path in Directory.GetFiles(OriginalItemsPath, "*.json"))
                {
                    var record = JsonConvert.DeserializeObject<OriginalItemRecord>(File.ReadAllText(path));
                    var item = record?.Item;
                    if (record?.SchemaVersion != 1 || item == null || !item.IsOriginal ||
                        !Guid.TryParseExact(item.Id, "N", out _) ||
                        Path.GetFileNameWithoutExtension(path) != item.Id ||
                        string.IsNullOrWhiteSpace(item.Name) || item.SourceType.HasValue)
                    {
                        throw new InvalidDataException("Invalid original Item metadata: " + path);
                    }
                    Execute(connection, transaction,
                        @"INSERT OR IGNORE INTO item(id, name, description, is_archived, created_at, updated_at)
                          VALUES(@p0, @p1, @p2, @p3, @p4, @p5)",
                        item.Id, item.Name, item.Description ?? string.Empty, item.IsArchived ? 1 : 0,
                        item.CreatedAt.ToString("O"), item.UpdatedAt.ToString("O"));
                    var existing = ReadItem(connection, item.Id);
                    if (existing.SourceType.HasValue)
                    {
                        throw new InvalidDataException("An original Item conflicts with a source Item: " + item.Id);
                    }
                    Execute(connection, transaction,
                        "INSERT OR IGNORE INTO original_item(item_id) VALUES(@p0)", item.Id);
                    ReplaceItemTags(connection, transaction, item.Id,
                        (item.Tags ?? Array.Empty<AssetTag>()).Select(tag => tag.Path).ToArray());
                }
                transaction.Commit();
            }
        }

        private sealed class OriginalItemPersistence : IDisposable
        {
            private readonly SqliteAssetManagerStore _store;
            private readonly Dictionary<string, byte[]> _previous = new Dictionary<string, byte[]>();
            private bool _committed;

            internal OriginalItemPersistence(SqliteAssetManagerStore store) { _store = store; }

            private string Capture(AssetItem item)
            {
                if (!Guid.TryParseExact(item.Id, "N", out _))
                {
                    throw new InvalidDataException("Invalid original Item ID.");
                }
                var path = Path.Combine(_store.OriginalItemsPath, item.Id + ".json");
                if (!_previous.ContainsKey(path))
                {
                    _previous.Add(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
                }
                return path;
            }

            internal void Save(AssetItem item)
            {
                if (!item.IsOriginal) { return; }
                Directory.CreateDirectory(_store.OriginalItemsPath);
                var path = Capture(item);
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, JsonConvert.SerializeObject(
                        new OriginalItemRecord { Item = item }, Formatting.Indented));
                    if (File.Exists(path)) { File.Replace(temporary, path, null); }
                    else { File.Move(temporary, path); }
                }
                finally
                {
                    if (File.Exists(temporary)) { File.Delete(temporary); }
                }
            }

            internal void Delete(AssetItem item)
            {
                if (item.IsOriginal) { File.Delete(Capture(item)); }
            }

            internal void Commit() { _committed = true; }

            public void Dispose()
            {
                if (_committed) { return; }
                foreach (var pair in _previous)
                {
                    if (pair.Value == null) { File.Delete(pair.Key); }
                    else { File.WriteAllBytes(pair.Key, pair.Value); }
                }
            }
        }

        private static IReadOnlyList<AssetItem> ReadItems(
            SQLiteConnection connection, IReadOnlyList<ItemRow> rows, string catalogSource)
        {
            var items = rows.Select(MapItem).ToArray();
            for (var offset = 0; offset < items.Length; offset += ItemReadBatchSize)
            {
                var batch = items.Skip(offset).Take(ItemReadBatchSize).ToArray();
                var parameters = batch.Select(item => (object)item.Id).ToArray();
                var placeholders = string.Join(",", parameters.Select(_ => "?"));
                var booth = connection.Query<BoothMetadataRow>(
                    @"SELECT item_id AS ItemId, item_url AS ItemUrl,
                             shop_name AS ShopName, shop_url AS ShopUrl
                      FROM item_booth_metadata WHERE item_id IN (" + placeholders + ")",
                    parameters);
                var files = connection.Query<FileRow>(
                    FileSelect + " WHERE item_id IN (" + placeholders +
                    ")" + (catalogSource == null ? string.Empty : " AND source_type = ?") +
                    " ORDER BY file_name, id", catalogSource == null ? parameters :
                        parameters.Concat(new object[] { catalogSource }).ToArray())
                    .Select(MapFile).ToLookup(file => file.ItemId, StringComparer.Ordinal);
                var tags = connection.Query<TagRow>(
                    @"SELECT links.item_id AS ItemId, tag.id AS Id, tag.path AS Path,
                             MAX(links.source_owned) AS IsSourceOwned
                      FROM (
                        SELECT item_id, tag_id, 0 AS source_owned FROM item_tag
                        WHERE item_id IN (" + placeholders + @")
                        UNION ALL
                        SELECT item_id, tag_id, 1 AS source_owned FROM item_source_tag
                        WHERE item_id IN (" + placeholders + @")
                      ) links JOIN tag ON tag.id = links.tag_id
                      GROUP BY links.item_id, tag.id, tag.path ORDER BY tag.path",
                    parameters.Concat(parameters).ToArray())
                    .ToLookup(tag => tag.ItemId, StringComparer.Ordinal);
                var boothByItem = booth.ToDictionary(row => row.ItemId, StringComparer.Ordinal);
                foreach (var item in batch)
                {
                    if (boothByItem.TryGetValue(item.Id, out var metadata))
                    {
                        item.Booth = new AssetBoothMetadata
                        {
                            ItemUrl = metadata.ItemUrl, ShopName = metadata.ShopName,
                            ShopUrl = metadata.ShopUrl
                        };
                    }
                    item.Files = files[item.Id].ToArray();
                    item.Tags = tags[item.Id].Select(tag => new AssetTag
                    {
                        Id = tag.Id, Path = tag.Path, IsSourceOwned = tag.IsSourceOwned != 0
                    }).ToArray();
                }
            }
            return items;
        }
    }
}
