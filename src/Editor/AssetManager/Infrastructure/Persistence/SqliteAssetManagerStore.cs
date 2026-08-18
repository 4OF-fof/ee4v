using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.SQLite;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure.Persistence
{
    internal sealed class SqliteAssetManagerStore
        : IAssetManagerStore
    {
        private const int SchemaVersion = 1;
        private const string FileSelect =
            @"SELECT id AS Id, item_id AS ItemId,
                     file_name AS FileName, extension AS Extension,
                     source_type AS SourceType,
                     source_id AS SourceId, source_path AS SourcePath,
                     is_available AS IsAvailable,
                     is_archived AS IsArchived,
                     created_at AS CreatedAt, updated_at AS UpdatedAt
              FROM file";
        private readonly string _databasePath;

        internal SqliteAssetManagerStore(string databasePath)
        {
            _databasePath = Path.GetFullPath(databasePath);
            InitializeDatabase();
        }

        public IReadOnlyList<AssetItem> GetItems()
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var ids = QueryStrings(
                        connection,
                        null,
                        "SELECT id FROM item ORDER BY name, id");
                    return ids
                        .Select(id => ReadItem(connection, id))
                        .ToArray();
                }
            });
        }

        public AssetItem GetItem(string itemId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return ReadItem(connection, itemId);
                }
            });
        }

        public AssetItem CreateItem(CreateAssetItemRequest request)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var id = NewId();
                    var now = Now();
                    Execute(
                        connection,
                        null,
                        @"INSERT INTO item(
                            id, name, description, source_type, source_id,
                            is_archived, created_at, updated_at)
                          VALUES(
                            @p0, @p1, @p2, NULL, NULL, 0, @p3, @p3)",
                        id,
                        request.Name.Trim(),
                        request.Description ?? string.Empty,
                        now);
                    return ReadItem(connection, id);
                }
            });
        }

        public AssetItem UpdateItem(
            string itemId,
            UpdateAssetItemRequest request)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var changed = Execute(
                        connection,
                        null,
                        @"UPDATE item
                          SET name = @p0,
                              description = @p1,
                              updated_at = @p2
                          WHERE id = @p3",
                        request.Name.Trim(),
                        request.Description ?? string.Empty,
                        Now(),
                        itemId);
                    RequireChanged(changed, "Item was not found.");
                    return ReadItem(connection, itemId);
                }
            });
        }

        public IReadOnlyList<AssetItem> SetItemArchived(
            IReadOnlyList<string> itemIds,
            bool archived)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        RequireItem(connection, itemIds[i], transaction);
                    }

                    var now = Now();
                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"UPDATE item
                              SET is_archived = @p0, updated_at = @p1
                              WHERE id = @p2",
                            archived ? 1 : 0,
                            now,
                            itemIds[i]);
                    }

                    transaction.Commit();
                    return itemIds
                        .Select(id => ReadItem(connection, id))
                        .ToArray();
                }
            });
        }

        public void DeleteItem(IReadOnlyList<string> itemIds)
        {
            Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        RequireItem(connection, itemIds[i], transaction);
                    }

                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            "DELETE FROM file WHERE item_id = @p0",
                            itemIds[i]);
                        Execute(
                            connection,
                            transaction,
                            "DELETE FROM item WHERE id = @p0",
                            itemIds[i]);
                    }

                    DeleteUnusedTags(connection, transaction);
                    transaction.Commit();
                    return true;
                }
            });
        }

        public AssetFile GetFile(string fileId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return ReadFile(connection, fileId);
                }
            });
        }

        public AssetItem GetItemBySource(
            AssetSourceType sourceType,
            string sourceId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var id = ScalarString(
                        connection,
                        null,
                        @"SELECT id FROM item
                          WHERE source_type = @p0 AND source_id = @p1",
                        ToSourceType(sourceType),
                        sourceId);
                    if (id == null)
                    {
                        throw NotFound("Item source was not found.");
                    }

                    return ReadItem(connection, id);
                }
            });
        }

        public AssetFile GetFileBySource(
            AssetSourceType sourceType,
            string sourceId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var id = ScalarString(
                        connection,
                        null,
                        @"SELECT id FROM file
                          WHERE source_type = @p0 AND source_id = @p1",
                        ToSourceType(sourceType),
                        sourceId);
                    if (id == null)
                    {
                        throw NotFound("File source was not found.");
                    }

                    return ReadFile(connection, id);
                }
            });
        }

        public IReadOnlyList<AssetFile> GetFiles(
            string itemId,
            bool includeArchived)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    RequireItem(connection, itemId, null);
                    return ReadFiles(
                        connection,
                        includeArchived
                            ? "item_id = @p0"
                            : "item_id = @p0 AND is_archived = 0",
                        itemId);
                }
            });
        }

        public IReadOnlyList<AssetFile> GetUnassignedFiles(
            bool includeArchived)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return ReadFiles(
                        connection,
                        includeArchived
                            ? "item_id IS NULL"
                            : "item_id IS NULL AND is_archived = 0");
                }
            });
        }

        public IReadOnlyList<AssetFile> SetFileItem(
            IReadOnlyList<string> fileIds,
            string itemId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    if (itemId != null)
                    {
                        RequireItem(connection, itemId, transaction);
                    }

                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        RequireFile(connection, fileIds[i], transaction);
                    }

                    var now = Now();
                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"UPDATE file
                              SET item_id = @p0, updated_at = @p1
                              WHERE id = @p2",
                            itemId,
                            now,
                            fileIds[i]);
                    }

                    transaction.Commit();
                    return fileIds
                        .Select(id => ReadFile(connection, id))
                        .ToArray();
                }
            });
        }

        public IReadOnlyList<AssetFile> SetFileArchived(
            IReadOnlyList<string> fileIds,
            bool archived)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        RequireFile(connection, fileIds[i], transaction);
                    }

                    var now = Now();
                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"UPDATE file
                              SET is_archived = @p0, updated_at = @p1
                              WHERE id = @p2",
                            archived ? 1 : 0,
                            now,
                            fileIds[i]);
                    }

                    transaction.Commit();
                    return fileIds
                        .Select(id => ReadFile(connection, id))
                        .ToArray();
                }
            });
        }

        public void DeleteFile(IReadOnlyList<string> fileIds)
        {
            Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        RequireFile(connection, fileIds[i], transaction);
                    }

                    for (var i = 0; i < fileIds.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"UPDATE item
                              SET source_type = NULL,
                                  source_id = NULL,
                                  updated_at = @p0
                              WHERE source_type = 'ee4v'
                                AND source_id = (
                                  SELECT source_id FROM file
                                  WHERE id = @p1
                                    AND source_type = 'ee4v')",
                            Now(),
                            fileIds[i]);
                        Execute(
                            connection,
                            transaction,
                            "DELETE FROM file WHERE id = @p0",
                            fileIds[i]);
                    }

                    transaction.Commit();
                    return true;
                }
            });
        }

        public IReadOnlyList<AssetFileTarget> GetFileTargets(
            string fileId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    RequireFile(connection, fileId, null);
                    return ReadFileTargets(connection, fileId);
                }
            });
        }

        public IReadOnlyList<AssetFileTarget> ReplaceFileTargets(
            string fileId,
            IReadOnlyList<string> normalizedTargetPaths)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    RequireFile(connection, fileId, transaction);
                    Execute(
                        connection,
                        transaction,
                        "DELETE FROM file_target WHERE file_id = @p0",
                        fileId);
                    for (var i = 0;
                         i < normalizedTargetPaths.Count;
                         i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"INSERT INTO file_target(file_id, target_path)
                              VALUES(@p0, @p1)",
                            fileId,
                            normalizedTargetPaths[i]);
                    }

                    transaction.Commit();
                    return ReadFileTargets(connection, fileId);
                }
            });
        }

        public IReadOnlyList<AssetFileDependency> GetFileDependencies(
            string fileId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    RequireFile(connection, fileId, null);
                    return ReadFileDependencies(connection, fileId);
                }
            });
        }

        public IReadOnlyList<AssetFileDependency> ReplaceFileDependencies(
            string dependentFileId,
            IReadOnlyList<string> dependencyFileIds)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    RequireFile(connection, dependentFileId, transaction);
                    for (var i = 0; i < dependencyFileIds.Count; i++)
                    {
                        RequireFile(
                            connection,
                            dependencyFileIds[i],
                            transaction);
                    }

                    Execute(
                        connection,
                        transaction,
                        @"DELETE FROM file_dependency
                          WHERE dependent_file_id = @p0",
                        dependentFileId);
                    for (var i = 0; i < dependencyFileIds.Count; i++)
                    {
                        Execute(
                            connection,
                            transaction,
                            @"INSERT INTO file_dependency(
                                dependent_file_id, dependency_file_id)
                              VALUES(@p0, @p1)",
                            dependentFileId,
                            dependencyFileIds[i]);
                    }

                    transaction.Commit();
                    return ReadFileDependencies(
                        connection,
                        dependentFileId);
                }
            });
        }

        public IReadOnlyList<AssetTag> GetTags()
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return ReadTags(connection, null, null);
                }
            });
        }

        public IReadOnlyList<AssetItem> SetItemTags(
            IReadOnlyList<string> itemIds,
            IReadOnlyList<string> normalizedPaths)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        RequireItem(connection, itemIds[i], transaction);
                    }

                    for (var i = 0; i < itemIds.Count; i++)
                    {
                        ReplaceItemTags(
                            connection,
                            transaction,
                            itemIds[i],
                            normalizedPaths);
                    }

                    DeleteUnusedTags(connection, transaction);
                    transaction.Commit();
                    return itemIds
                        .Select(id => ReadItem(connection, id))
                        .ToArray();
                }
            });
        }

        public IReadOnlyList<AssetCollection> GetCollections()
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var ids = QueryStrings(
                        connection,
                        null,
                        "SELECT id FROM collection ORDER BY name, id");
                    return ids.Select(id =>
                            ReadCollection(connection, id))
                        .ToArray();
                }
            });
        }

        public AssetCollection GetCollection(string collectionId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return ReadCollection(connection, collectionId);
                }
            });
        }

        public AssetCollection CreateCollection(
            CreateAssetCollectionRequest request)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    EnsureCollectionNameAvailable(
                        connection,
                        transaction,
                        request.Name,
                        null);
                    var id = NewId();
                    var now = Now();
                    Execute(
                        connection,
                        transaction,
                        @"INSERT INTO collection(
                            id, name, created_at, updated_at)
                          VALUES(@p0, @p1, @p2, @p2)",
                        id,
                        request.Name.Trim(),
                        now);
                    InsertNode(
                        connection,
                        transaction,
                        id,
                        null,
                        0,
                        request.Root);
                    transaction.Commit();
                    return ReadCollection(connection, id);
                }
            });
        }

        public AssetCollection UpdateCollection(
            string collectionId,
            UpdateAssetCollectionRequest request)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    EnsureCollectionExists(
                        connection,
                        transaction,
                        collectionId);
                    EnsureCollectionNameAvailable(
                        connection,
                        transaction,
                        request.Name,
                        collectionId);
                    Execute(
                        connection,
                        transaction,
                        @"UPDATE collection
                          SET name = @p0, updated_at = @p1
                          WHERE id = @p2",
                        request.Name.Trim(),
                        Now(),
                        collectionId);
                    Execute(
                        connection,
                        transaction,
                        @"DELETE FROM collection_node
                          WHERE collection_id = @p0",
                        collectionId);
                    InsertNode(
                        connection,
                        transaction,
                        collectionId,
                        null,
                        0,
                        request.Root);
                    transaction.Commit();
                    return ReadCollection(connection, collectionId);
                }
            });
        }

        public void DeleteCollection(string collectionId)
        {
            Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    var changed = Execute(
                        connection,
                        null,
                        "DELETE FROM collection WHERE id = @p0",
                        collectionId);
                    RequireChanged(
                        changed,
                        "Collection was not found.");
                    return true;
                }
            });
        }

        public AssetSyncResult ApplySourceSnapshot(
            AssetSourceType sourceType,
            AssetSourceSnapshot snapshot,
            bool markMissingFiles)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    var created = 0;
                    var updated = 0;
                    var unchanged = 0;
                    var errors = 0;
                    var seenFiles = new HashSet<string>(
                        StringComparer.Ordinal);
                    var items = snapshot == null
                        ? Array.Empty<AssetSourceSnapshotItem>()
                        : snapshot.Items;
                    var source = ToSourceType(sourceType);
                    for (var i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        if (item == null ||
                            string.IsNullOrWhiteSpace(item.SourceId))
                        {
                            errors++;
                            continue;
                        }

                        var itemId = UpsertSourceItem(
                            connection,
                            transaction,
                            source,
                            item,
                            ref created,
                            ref updated,
                            ref unchanged);
                        var files = item.Files ??
                                    Array.Empty<AssetSourceSnapshotFile>();
                        for (var fileIndex = 0;
                             fileIndex < files.Count;
                             fileIndex++)
                        {
                            var file = files[fileIndex];
                            if (file == null ||
                                string.IsNullOrWhiteSpace(file.SourceId) ||
                                !seenFiles.Add(file.SourceId))
                            {
                                errors++;
                                continue;
                            }

                            UpsertSourceFile(
                                connection,
                                transaction,
                                source,
                                itemId,
                                file,
                                ref created,
                                ref updated,
                                ref unchanged);
                        }

                        if (item.Tags != null)
                        {
                            ReplaceItemTags(
                                connection,
                                transaction,
                                itemId,
                                item.Tags);
                        }
                    }

                    var unassignedFiles = snapshot == null
                        ? Array.Empty<AssetSourceSnapshotFile>()
                        : snapshot.Files;
                    for (var i = 0; i < unassignedFiles.Count; i++)
                    {
                        var file = unassignedFiles[i];
                        if (file == null ||
                            string.IsNullOrWhiteSpace(file.SourceId) ||
                            !seenFiles.Add(file.SourceId))
                        {
                            errors++;
                            continue;
                        }

                        UpsertSourceFile(
                            connection,
                            transaction,
                            source,
                            null,
                            file,
                            ref created,
                            ref updated,
                            ref unchanged);
                    }

                    if (markMissingFiles)
                    {
                        MarkMissingSourceFiles(
                            connection,
                            transaction,
                            source,
                            seenFiles);
                    }

                    DeleteUnusedTags(connection, transaction);
                    transaction.Commit();
                    var state = errors == 0
                        ? AssetSyncState.Success
                        : created + updated + unchanged > 0
                            ? AssetSyncState.Partial
                            : AssetSyncState.Failed;
                    return new AssetSyncResult(
                        created,
                        updated,
                        unchanged,
                        errors,
                        state);
                }
            });
        }

        private void InitializeDatabase()
        {
            Run(() =>
            {
                var directory = Path.GetDirectoryName(_databasePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(_databasePath))
                {
                    using (var existing = OpenConnection())
                    {
                        var version = existing.ExecuteScalar<int>(
                            "PRAGMA user_version");
                        if (version != SchemaVersion)
                        {
                            existing.Close();
                            File.Delete(_databasePath);
                        }
                    }
                }

                using (var connection = OpenConnection())
                {
                    ExecuteSchema(connection);
                }

                return true;
            });
        }

        private static void ExecuteSchema(SQLiteConnection connection)
        {
            var statements = new[]
            {
                @"CREATE TABLE IF NOT EXISTS item(
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL CHECK(trim(name) <> ''),
                    description TEXT NOT NULL DEFAULT '',
                    source_type TEXT,
                    source_id TEXT,
                    is_archived INTEGER NOT NULL DEFAULT 0
                      CHECK(is_archived IN (0, 1)),
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    CHECK((source_type IS NULL) = (source_id IS NULL)),
                    CHECK(source_type IS NULL OR source_type IN(
                      'eagle', 'ee4v'))
                  )",
                @"CREATE UNIQUE INDEX IF NOT EXISTS ux_item_source
                    ON item(source_type, source_id)
                    WHERE source_type IS NOT NULL",
                @"CREATE TABLE IF NOT EXISTS file(
                    id TEXT PRIMARY KEY,
                    item_id TEXT REFERENCES item(id) ON DELETE SET NULL,
                    file_name TEXT NOT NULL CHECK(trim(file_name) <> ''),
                    extension TEXT,
                    source_type TEXT NOT NULL CHECK(source_type IN(
                      'eagle', 'ee4v')),
                    source_id TEXT NOT NULL,
                    source_path TEXT,
                    is_available INTEGER NOT NULL
                      CHECK(is_available IN (0, 1)),
                    is_archived INTEGER NOT NULL DEFAULT 0
                      CHECK(is_archived IN (0, 1)),
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    UNIQUE(source_type, source_id)
                  )",
                @"CREATE INDEX IF NOT EXISTS ix_file_item
                    ON file(item_id)",
                @"CREATE INDEX IF NOT EXISTS ix_file_extension_item
                    ON file(extension, item_id)",
                @"CREATE TABLE IF NOT EXISTS file_target(
                    file_id TEXT NOT NULL
                      REFERENCES file(id) ON DELETE CASCADE,
                    target_path TEXT NOT NULL COLLATE NOCASE
                      CHECK(target_path = trim(target_path))
                      CHECK(instr(target_path, '\') = 0)
                      CHECK(instr(target_path, ':') = 0)
                      CHECK(instr(target_path, char(0)) = 0)
                      CHECK(substr(target_path, 1, 1) <> '/')
                      CHECK(substr(target_path, -1, 1) <> '/')
                      CHECK(instr(target_path, '//') = 0)
                      CHECK(target_path NOT IN ('.', '..'))
                      CHECK(target_path NOT LIKE './%')
                      CHECK(target_path NOT LIKE '../%')
                      CHECK(target_path NOT LIKE '%/./%')
                      CHECK(target_path NOT LIKE '%/../%')
                      CHECK(target_path NOT LIKE '%/.')
                      CHECK(target_path NOT LIKE '%/..'),
                    PRIMARY KEY(file_id, target_path)
                  )",
                @"CREATE TABLE IF NOT EXISTS file_dependency(
                    dependent_file_id TEXT NOT NULL
                      REFERENCES file(id) ON DELETE CASCADE,
                    dependency_file_id TEXT NOT NULL
                      REFERENCES file(id) ON DELETE CASCADE,
                    PRIMARY KEY(
                      dependent_file_id, dependency_file_id),
                    CHECK(dependent_file_id <> dependency_file_id)
                  )",
                @"CREATE INDEX IF NOT EXISTS
                    ix_file_dependency_reverse
                    ON file_dependency(
                      dependency_file_id, dependent_file_id)",
                @"CREATE TRIGGER IF NOT EXISTS
                    prevent_file_dependency_cycle
                  BEFORE INSERT ON file_dependency
                  BEGIN
                    SELECT RAISE(
                      ABORT, 'file dependency cycle')
                    WHERE EXISTS(
                      WITH RECURSIVE dependencies(id) AS (
                        SELECT dependency_file_id
                        FROM file_dependency
                        WHERE dependent_file_id =
                          NEW.dependency_file_id
                        UNION
                        SELECT edge.dependency_file_id
                        FROM file_dependency edge
                        INNER JOIN dependencies current
                          ON edge.dependent_file_id = current.id
                      )
                      SELECT 1 FROM dependencies
                      WHERE id = NEW.dependent_file_id
                    );
                  END",
                @"CREATE TABLE IF NOT EXISTS tag(
                    id TEXT PRIMARY KEY,
                    path TEXT NOT NULL UNIQUE
                      CHECK(trim(path) <> '')
                      CHECK(path = lower(path))
                      CHECK(substr(path, 1, 1) <> '/')
                      CHECK(substr(path, -1, 1) <> '/')
                      CHECK(instr(path, '//') = 0)
                      CHECK(instr(path, '#') = 0)
                  )",
                @"CREATE TABLE IF NOT EXISTS item_tag(
                    item_id TEXT NOT NULL
                      REFERENCES item(id) ON DELETE CASCADE,
                    tag_id TEXT NOT NULL
                      REFERENCES tag(id) ON DELETE CASCADE,
                    PRIMARY KEY(item_id, tag_id)
                  )",
                @"CREATE INDEX IF NOT EXISTS ix_item_tag_reverse
                    ON item_tag(tag_id, item_id)",
                @"CREATE TABLE IF NOT EXISTS collection(
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL UNIQUE CHECK(trim(name) <> ''),
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                  )",
                @"CREATE TABLE IF NOT EXISTS collection_node(
                    id TEXT PRIMARY KEY,
                    collection_id TEXT NOT NULL,
                    parent_node_id TEXT,
                    node_type TEXT NOT NULL
                      CHECK(node_type IN ('and', 'or', 'not', 'condition')),
                    condition_type TEXT
                      CHECK(condition_type IS NULL OR condition_type IN(
                        'name_contains', 'description_contains',
                        'has_tag', 'has_file_extension')),
                    value TEXT,
                    sort_order INTEGER NOT NULL CHECK(sort_order >= 0),
                    UNIQUE(collection_id, id),
                    FOREIGN KEY(collection_id)
                      REFERENCES collection(id) ON DELETE CASCADE,
                    FOREIGN KEY(collection_id, parent_node_id)
                      REFERENCES collection_node(collection_id, id)
                      ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED,
                    CHECK(
                      (node_type = 'condition' AND
                       condition_type IS NOT NULL AND
                       value IS NOT NULL AND trim(value) <> '') OR
                      (node_type <> 'condition' AND
                       condition_type IS NULL AND value IS NULL))
                  )",
                @"CREATE UNIQUE INDEX IF NOT EXISTS ux_collection_root
                    ON collection_node(collection_id)
                    WHERE parent_node_id IS NULL",
                @"CREATE UNIQUE INDEX IF NOT EXISTS ux_collection_sibling
                    ON collection_node(
                      collection_id, parent_node_id, sort_order)
                    WHERE parent_node_id IS NOT NULL",
                @"CREATE INDEX IF NOT EXISTS ix_collection_node_tree
                    ON collection_node(
                      collection_id, parent_node_id, sort_order)",
                @"CREATE TRIGGER IF NOT EXISTS
                    prevent_collection_node_cycle
                  BEFORE UPDATE OF parent_node_id ON collection_node
                  WHEN NEW.parent_node_id IS NOT NULL
                  BEGIN
                    SELECT RAISE(ABORT, 'collection node cycle')
                    WHERE NEW.parent_node_id = NEW.id OR EXISTS(
                      WITH RECURSIVE descendants(id) AS (
                        SELECT id FROM collection_node
                        WHERE parent_node_id = NEW.id
                        UNION
                        SELECT node.id FROM collection_node node
                        INNER JOIN descendants child
                          ON node.parent_node_id = child.id
                      )
                      SELECT 1 FROM descendants
                      WHERE id = NEW.parent_node_id
                    );
                  END",
                "PRAGMA user_version = 1"
            };
            for (var i = 0; i < statements.Length; i++)
            {
                Execute(connection, null, statements[i]);
            }
        }

        private static AssetItem ReadItem(
            SQLiteConnection connection,
            string itemId)
        {
            var row = connection.Query<ItemRow>(
                    @"SELECT id AS Id, name AS Name,
                             description AS Description,
                             source_type AS SourceType,
                             source_id AS SourceId,
                             is_archived AS IsArchived,
                             created_at AS CreatedAt,
                             updated_at AS UpdatedAt
                      FROM item WHERE id = ?",
                    itemId)
                .SingleOrDefault();
            if (row == null)
            {
                throw NotFound("Item was not found.");
            }

            var item = new AssetItem
            {
                Id = row.Id,
                Name = row.Name,
                Description = row.Description,
                SourceType = row.SourceType == null
                    ? (AssetSourceType?)null
                    : ParseSourceType(row.SourceType),
                SourceId = row.SourceId,
                IsArchived = row.IsArchived != 0,
                CreatedAt = ParseDate(row.CreatedAt),
                UpdatedAt = ParseDate(row.UpdatedAt)
            };

            item.Tags = ReadTags(connection, itemId, null);
            item.Files = ReadFiles(
                connection,
                "item_id = @p0",
                itemId);
            return item;
        }

        private static AssetFile ReadFile(
            SQLiteConnection connection,
            string fileId)
        {
            var row = connection.Query<FileRow>(
                    FileSelect + " WHERE id = ?",
                    fileId)
                .SingleOrDefault();
            if (row == null)
            {
                throw NotFound("File was not found.");
            }

            return MapFile(row);
        }

        private static IReadOnlyList<AssetFile> ReadFiles(
            SQLiteConnection connection,
            string where,
            params object[] parameters)
        {
            return connection.Query<FileRow>(
                    FileSelect + " WHERE " + where +
                    " ORDER BY file_name, id",
                    parameters)
                .Select(MapFile)
                .ToArray();
        }

        private static AssetFile MapFile(FileRow row)
        {
            return new AssetFile
            {
                Id = row.Id,
                ItemId = row.ItemId,
                FileName = row.FileName,
                Extension = row.Extension,
                SourceType = ParseSourceType(row.SourceType),
                SourceId = row.SourceId,
                SourcePath = row.SourcePath,
                IsAvailable = row.IsAvailable != 0,
                IsArchived = row.IsArchived != 0,
                CreatedAt = ParseDate(row.CreatedAt),
                UpdatedAt = ParseDate(row.UpdatedAt)
            };
        }

        private static IReadOnlyList<AssetFileTarget> ReadFileTargets(
            SQLiteConnection connection,
            string fileId)
        {
            return connection.Query<FileTargetRow>(
                    @"SELECT file_id AS FileId,
                             target_path AS TargetPath
                      FROM file_target
                      WHERE file_id = ?
                      ORDER BY target_path COLLATE NOCASE",
                    fileId)
                .Select(row => new AssetFileTarget
                {
                    FileId = row.FileId,
                    TargetPath = row.TargetPath
                })
                .ToArray();
        }

        private static IReadOnlyList<AssetFileDependency>
            ReadFileDependencies(
                SQLiteConnection connection,
                string fileId)
        {
            return connection.Query<FileDependencyRow>(
                    @"SELECT dependent_file_id AS DependentFileId,
                             dependency_file_id AS DependencyFileId
                      FROM file_dependency
                      WHERE dependent_file_id = ?
                      ORDER BY dependency_file_id",
                    fileId)
                .Select(row => new AssetFileDependency
                {
                    DependentFileId = row.DependentFileId,
                    DependencyFileId = row.DependencyFileId
                })
                .ToArray();
        }

        private static IReadOnlyList<AssetTag> ReadTags(
            SQLiteConnection connection,
            string itemId,
            DatabaseTransaction transaction)
        {
            var sql = itemId == null
                ? "SELECT id AS Id, path AS Path FROM tag ORDER BY path"
                : @"SELECT tag.id AS Id, tag.path AS Path
                    FROM tag
                    INNER JOIN item_tag ON item_tag.tag_id = tag.id
                    WHERE item_tag.item_id = ?
                    ORDER BY tag.path";
            var rows = itemId == null
                ? connection.Query<TagRow>(sql)
                : connection.Query<TagRow>(sql, itemId);
            return rows.Select(row => new AssetTag
                {
                    Id = row.Id,
                    Path = row.Path
                })
                .ToArray();
        }

        private static void ReplaceItemTags(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string itemId,
            IReadOnlyList<string> paths)
        {
            Execute(
                connection,
                transaction,
                "DELETE FROM item_tag WHERE item_id = @p0",
                itemId);
            var source = paths ?? Array.Empty<string>();
            for (var i = 0; i < source.Count; i++)
            {
                var tagId = ScalarString(
                    connection,
                    transaction,
                    "SELECT id FROM tag WHERE path = @p0",
                    source[i]);
                if (tagId == null)
                {
                    tagId = NewId();
                    Execute(
                        connection,
                        transaction,
                        @"INSERT INTO tag(id, path)
                          VALUES(@p0, @p1)",
                        tagId,
                        source[i]);
                }

                Execute(
                    connection,
                    transaction,
                    @"INSERT INTO item_tag(item_id, tag_id)
                      VALUES(@p0, @p1)",
                    itemId,
                    tagId);
            }
        }

        private static void DeleteUnusedTags(
            SQLiteConnection connection,
            DatabaseTransaction transaction)
        {
            Execute(
                connection,
                transaction,
                @"DELETE FROM tag
                  WHERE NOT EXISTS(
                    SELECT 1 FROM item_tag
                    WHERE item_tag.tag_id = tag.id)");
        }

        private static AssetCollection ReadCollection(
            SQLiteConnection connection,
            string collectionId)
        {
            var collection = connection.Query<CollectionRow>(
                    @"SELECT name AS Name, created_at AS CreatedAt,
                             updated_at AS UpdatedAt
                      FROM collection WHERE id = ?",
                    collectionId)
                .SingleOrDefault();
            if (collection == null)
            {
                throw NotFound("Collection was not found.");
            }

            var rows = connection.Query<CollectionNodeRow>(
                @"SELECT id AS Id, parent_node_id AS ParentId,
                         node_type AS NodeType,
                         condition_type AS ConditionType,
                         value AS Value, sort_order AS SortOrder
                  FROM collection_node
                  WHERE collection_id = ?
                  ORDER BY sort_order, id",
                collectionId);

            var root = rows.SingleOrDefault(row => row.ParentId == null);
            if (root == null)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.DatabaseError,
                    "Collection root was not found.");
            }

            return new AssetCollection
            {
                Id = collectionId,
                Name = collection.Name,
                Root = BuildNode(root, rows),
                CreatedAt = ParseDate(collection.CreatedAt),
                UpdatedAt = ParseDate(collection.UpdatedAt)
            };
        }

        private static AssetFilterNode BuildNode(
            CollectionNodeRow row,
            IReadOnlyList<CollectionNodeRow> rows)
        {
            return new AssetFilterNode
            {
                Type = ParseNodeType(row.NodeType),
                ConditionType = ParseConditionType(row.ConditionType),
                Value = row.Value,
                Children = rows
                    .Where(child => child.ParentId == row.Id)
                    .OrderBy(child => child.SortOrder)
                    .ThenBy(child => child.Id, StringComparer.Ordinal)
                    .Select(child => BuildNode(child, rows))
                    .ToArray()
            };
        }

        private static void InsertNode(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string collectionId,
            string parentId,
            int sortOrder,
            AssetFilterNode node)
        {
            var id = NewId();
            Execute(
                connection,
                transaction,
                @"INSERT INTO collection_node(
                    id, collection_id, parent_node_id, node_type,
                    condition_type, value, sort_order)
                  VALUES(@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
                id,
                collectionId,
                parentId,
                NodeType(node.Type),
                node.ConditionType.HasValue
                    ? ConditionType(node.ConditionType.Value)
                    : null,
                node.Type == AssetFilterNodeType.Condition
                    ? node.Value.Trim()
                    : null,
                sortOrder);
            var children = node.Children ??
                           Array.Empty<AssetFilterNode>();
            for (var i = 0; i < children.Count; i++)
            {
                InsertNode(
                    connection,
                    transaction,
                    collectionId,
                    id,
                    i,
                    children[i]);
            }
        }

        private static string UpsertSourceItem(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string source,
            AssetSourceSnapshotItem item,
            ref int created,
            ref int updated,
            ref int unchanged)
        {
            var itemId = ScalarString(
                connection,
                transaction,
                @"SELECT id FROM item
                  WHERE source_type = @p0 AND source_id = @p1",
                source,
                item.SourceId);
            var name = string.IsNullOrWhiteSpace(item.Name)
                ? item.SourceId
                : item.Name.Trim();
            var description = item.Description ?? string.Empty;
            var now = Now();
            if (itemId == null)
            {
                itemId = NewId();
                Execute(
                    connection,
                    transaction,
                    @"INSERT INTO item(
                        id, name, description, source_type, source_id,
                        is_archived, created_at, updated_at)
                      VALUES(
                        @p0, @p1, @p2, @p3, @p4, 0, @p5, @p5)",
                    itemId,
                    name,
                    description,
                    source,
                    item.SourceId,
                    now);
                created++;
                return itemId;
            }

            var changed = Execute(
                connection,
                transaction,
                @"UPDATE item
                  SET name = @p0, description = @p1, updated_at = @p2
                  WHERE id = @p3
                    AND (name <> @p0 OR description <> @p1)",
                name,
                description,
                now,
                itemId);
            if (changed > 0)
            {
                updated++;
            }
            else
            {
                unchanged++;
            }

            return itemId;
        }

        private static void UpsertSourceFile(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string source,
            string itemId,
            AssetSourceSnapshotFile file,
            ref int created,
            ref int updated,
            ref int unchanged)
        {
            var fileId = ScalarString(
                connection,
                transaction,
                @"SELECT id FROM file
                  WHERE source_type = @p0 AND source_id = @p1",
                source,
                file.SourceId);
            var fileName = string.IsNullOrWhiteSpace(file.FileName)
                ? file.SourceId
                : file.FileName.Trim();
            var extension = string.IsNullOrWhiteSpace(file.Extension)
                ? null
                : file.Extension.Trim().TrimStart('.').ToLowerInvariant();
            var now = Now();
            if (fileId == null)
            {
                Execute(
                    connection,
                    transaction,
                    @"INSERT INTO file(
                        id, item_id, file_name, extension, source_type,
                        source_id, source_path, is_available,
                        is_archived, created_at, updated_at)
                      VALUES(
                        @p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7,
                        0, @p8, @p8)",
                    NewId(),
                    itemId,
                    fileName,
                    extension,
                    source,
                    file.SourceId,
                    file.SourcePath,
                    file.IsAvailable ? 1 : 0,
                    now);
                created++;
                return;
            }

            var changed = Execute(
                connection,
                transaction,
                @"UPDATE file
                  SET item_id = CASE
                        WHEN @p0 IS NULL THEN item_id
                        ELSE @p0
                      END,
                      file_name = @p1,
                      extension = @p2,
                      source_path = @p3,
                      is_available = @p4,
                      updated_at = @p5
                  WHERE id = @p6 AND (
                    (@p0 IS NOT NULL AND item_id IS NOT @p0) OR
                    file_name <> @p1 OR
                    extension IS NOT @p2 OR
                    source_path IS NOT @p3 OR
                    is_available <> @p4)",
                itemId,
                fileName,
                extension,
                file.SourcePath,
                file.IsAvailable ? 1 : 0,
                now,
                fileId);
            if (changed > 0)
            {
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        private static void MarkMissingSourceFiles(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string source,
            ISet<string> seenFiles)
        {
            var sourceIds = QueryStrings(
                connection,
                transaction,
                @"SELECT source_id FROM file
                  WHERE source_type = @p0",
                source);
            foreach (var sourceId in sourceIds)
            {
                if (seenFiles.Contains(sourceId))
                {
                    continue;
                }

                Execute(
                    connection,
                    transaction,
                    @"UPDATE file
                      SET is_available = 0, updated_at = @p0
                      WHERE source_type = @p1
                        AND source_id = @p2
                        AND is_available <> 0",
                    Now(),
                    source,
                    sourceId);
            }
        }

        private static void EnsureCollectionNameAvailable(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string name,
            string excludingId)
        {
            var existing = excludingId == null
                ? ScalarString(
                    connection,
                    transaction,
                    "SELECT id FROM collection WHERE name = @p0",
                    name.Trim())
                : ScalarString(
                    connection,
                    transaction,
                    @"SELECT id FROM collection
                      WHERE name = @p0 AND id <> @p1",
                    name.Trim(),
                    excludingId);
            if (existing != null)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.Duplicate,
                    "Collection name already exists.");
            }
        }

        private static void EnsureCollectionExists(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string collectionId)
        {
            if (ScalarString(
                    connection,
                    transaction,
                    "SELECT id FROM collection WHERE id = @p0",
                    collectionId) == null)
            {
                throw NotFound("Collection was not found.");
            }
        }

        private static void RequireItem(
            SQLiteConnection connection,
            string itemId,
            DatabaseTransaction transaction)
        {
            if (ScalarString(
                    connection,
                    transaction,
                    "SELECT id FROM item WHERE id = @p0",
                    itemId) == null)
            {
                throw NotFound("Item was not found.");
            }
        }

        private static void RequireFile(
            SQLiteConnection connection,
            string fileId,
            DatabaseTransaction transaction)
        {
            if (ScalarString(
                    connection,
                    transaction,
                    "SELECT id FROM file WHERE id = @p0",
                    fileId) == null)
            {
                throw NotFound("File was not found.");
            }
        }

        private SQLiteConnection OpenConnection()
        {
            SqliteBootstrap.EnsureInitialized();
            var connection = new SQLiteConnection(
                _databasePath,
                SQLiteOpenFlags.ReadWrite |
                SQLiteOpenFlags.Create |
                SQLiteOpenFlags.FullMutex |
                SQLiteOpenFlags.PrivateCache);
            Execute(connection, null, "PRAGMA foreign_keys = ON");
            return connection;
        }

        private static int Execute(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string sql,
            params object[] parameters)
        {
            return connection.Execute(sql, parameters);
        }

        private static string ScalarString(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string sql,
            params object[] parameters)
        {
            return connection.ExecuteScalar<string>(
                sql,
                parameters);
        }

        private static IReadOnlyList<string> QueryStrings(
            SQLiteConnection connection,
            DatabaseTransaction transaction,
            string sql,
            params object[] parameters)
        {
            return connection.QueryScalars<string>(
                sql,
                parameters);
        }

        private static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string Now()
        {
            return DateTime.UtcNow.ToString("O");
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        }

        private static void RequireChanged(int changed, string message)
        {
            if (changed == 0)
            {
                throw NotFound(message);
            }
        }

        private static AssetManagerException NotFound(string message)
        {
            return new AssetManagerException(
                AssetManagerErrorCode.NotFound,
                message);
        }

        private static string ToSourceType(AssetSourceType sourceType)
        {
            return sourceType == AssetSourceType.Ee4v
                ? "ee4v"
                : "eagle";
        }

        private static AssetSourceType ParseSourceType(string value)
        {
            return value == "ee4v"
                ? AssetSourceType.Ee4v
                : AssetSourceType.Eagle;
        }

        private static string NodeType(AssetFilterNodeType type)
        {
            return type == AssetFilterNodeType.And
                ? "and"
                : type == AssetFilterNodeType.Or
                    ? "or"
                    : type == AssetFilterNodeType.Not
                        ? "not"
                        : "condition";
        }

        private static AssetFilterNodeType ParseNodeType(string value)
        {
            return value == "and"
                ? AssetFilterNodeType.And
                : value == "or"
                    ? AssetFilterNodeType.Or
                    : value == "not"
                        ? AssetFilterNodeType.Not
                        : AssetFilterNodeType.Condition;
        }

        private static string ConditionType(
            AssetFilterConditionType type)
        {
            switch (type)
            {
                case AssetFilterConditionType.NameContains:
                    return "name_contains";
                case AssetFilterConditionType.DescriptionContains:
                    return "description_contains";
                case AssetFilterConditionType.HasTag:
                    return "has_tag";
                default:
                    return "has_file_extension";
            }
        }

        private static AssetFilterConditionType? ParseConditionType(
            string value)
        {
            if (value == null)
            {
                return null;
            }

            return value == "name_contains"
                ? AssetFilterConditionType.NameContains
                : value == "description_contains"
                    ? AssetFilterConditionType.DescriptionContains
                    : value == "has_tag"
                        ? AssetFilterConditionType.HasTag
                        : AssetFilterConditionType.HasFileExtension;
        }

        private T Run<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (AssetManagerException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.DatabaseError,
                    "AssetManager database operation failed.",
                    exception);
            }
        }

        private sealed class DatabaseTransaction : IDisposable
        {
            private readonly SQLiteConnection _connection;
            private bool _committed;

            internal DatabaseTransaction(SQLiteConnection connection)
            {
                _connection = connection;
                _connection.BeginTransaction();
            }

            internal void Commit()
            {
                _connection.Commit();
                _committed = true;
            }

            public void Dispose()
            {
                if (!_committed)
                {
                    _connection.Rollback();
                }
            }
        }

        private sealed class ItemRow
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string SourceType { get; set; }
            public string SourceId { get; set; }
            public int IsArchived { get; set; }
            public string CreatedAt { get; set; }
            public string UpdatedAt { get; set; }
        }

        private sealed class FileRow
        {
            public string Id { get; set; }
            public string ItemId { get; set; }
            public string FileName { get; set; }
            public string Extension { get; set; }
            public string SourceType { get; set; }
            public string SourceId { get; set; }
            public string SourcePath { get; set; }
            public int IsAvailable { get; set; }
            public int IsArchived { get; set; }
            public string CreatedAt { get; set; }
            public string UpdatedAt { get; set; }
        }

        private sealed class FileTargetRow
        {
            public string FileId { get; set; }
            public string TargetPath { get; set; }
        }

        private sealed class FileDependencyRow
        {
            public string DependentFileId { get; set; }
            public string DependencyFileId { get; set; }
        }

        private sealed class TagRow
        {
            public string Id { get; set; }
            public string Path { get; set; }
        }

        private sealed class CollectionRow
        {
            public string Name { get; set; }
            public string CreatedAt { get; set; }
            public string UpdatedAt { get; set; }
        }

        private sealed class CollectionNodeRow
        {
            public string Id { get; set; }
            public string ParentId { get; set; }
            public string NodeType { get; set; }
            public string ConditionType { get; set; }
            public string Value { get; set; }
            public int SortOrder { get; set; }
        }
    }
}
