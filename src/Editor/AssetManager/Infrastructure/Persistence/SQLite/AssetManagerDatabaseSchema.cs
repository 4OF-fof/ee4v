using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Infrastructure.Datasources.Blm;
using Ee4v.AssetManager.Infrastructure.Datasources.Eagle;
using Ee4v.SQLite;
using SQLite;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure.Persistence.SQLite
{
    internal static partial class AssetManagerDatabase
    {
        private static SQLiteConnection OpenConnection()
        {
            SqliteBootstrap.EnsureInitialized();
            var globalPath = Environment.ExpandEnvironmentVariables(AssetManagerInfrastructureSettings.Current.GlobalPath);
            var databasePath = Path.GetFullPath(Path.Combine(globalPath, DatabaseFileName));
            var directory = Path.GetDirectoryName(databasePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var connection = new SQLiteConnection(databasePath);
            EnsureSchema(connection);
            return connection;
        }

        private static void EnsureSchema(SQLiteConnection connection)
        {
            connection.Execute("PRAGMA foreign_keys = ON");
            connection.Execute("CREATE TABLE IF NOT EXISTS schema_version(version INTEGER PRIMARY KEY CHECK(version >= 1), created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            var schemaVersions = connection.Query<SchemaVersionRow>("SELECT version FROM schema_version");
            if (schemaVersions.Any(row => row.version != CurrentSchemaVersion))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.DatabaseError,
                    "AssetManager database schema is incompatible. Delete the database and let ee4v recreate it.");
            }
            connection.Execute("CREATE TABLE IF NOT EXISTS sync_info(source_type TEXT PRIMARY KEY CHECK(source_type IN ('blm', 'eagle', 'ee4v')), last_sync_at TEXT, last_sync_status TEXT NOT NULL DEFAULT 'success' CHECK(last_sync_status IN ('success', 'failed', 'partial')))");
            EnsureColumn(connection, "sync_info", "last_sync_status", "last_sync_status TEXT NOT NULL DEFAULT 'success' CHECK(last_sync_status IN ('success', 'failed', 'partial'))");
            connection.Execute("CREATE TABLE IF NOT EXISTS item_info(id TEXT PRIMARY KEY, name TEXT NOT NULL, description TEXT NOT NULL, is_available INTEGER NOT NULL DEFAULT 1 CHECK(is_available IN (0, 1)), created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute("CREATE TABLE IF NOT EXISTS shop_info(id TEXT PRIMARY KEY, name TEXT NOT NULL, subdomain TEXT NOT NULL UNIQUE, thumbnail_url TEXT)");
            connection.Execute("CREATE TABLE IF NOT EXISTS booth_info(id TEXT PRIMARY KEY, item_info_id TEXT NOT NULL UNIQUE REFERENCES item_info(id) ON DELETE CASCADE, booth_item_id INTEGER NOT NULL UNIQUE, shop_info_id TEXT NOT NULL REFERENCES shop_info(id), name TEXT NOT NULL, description TEXT NOT NULL, thumbnail_url TEXT, last_updated_at TEXT)");
            connection.Execute("CREATE TABLE IF NOT EXISTS item_source_origin(source_type TEXT NOT NULL CHECK(source_type IN ('blm', 'eagle')), source_id TEXT NOT NULL, item_info_id TEXT NOT NULL REFERENCES item_info(id) ON DELETE CASCADE, source_name TEXT NOT NULL, source_description TEXT NOT NULL, is_missing INTEGER NOT NULL DEFAULT 0 CHECK(is_missing IN (0, 1)), imported_at TEXT, PRIMARY KEY(source_type, source_id))");
            connection.Execute("CREATE INDEX IF NOT EXISTS index_item_source_origin_item ON item_source_origin(item_info_id)");
            connection.Execute("CREATE TABLE IF NOT EXISTS datasource_tag(source_type TEXT NOT NULL CHECK(source_type IN ('blm', 'eagle')), source_id TEXT NOT NULL, item_info_id TEXT NOT NULL REFERENCES item_info(id) ON DELETE CASCADE, name TEXT NOT NULL, PRIMARY KEY(source_type, source_id, name), FOREIGN KEY(source_type, source_id) REFERENCES item_source_origin(source_type, source_id) ON DELETE CASCADE)");
            connection.Execute("CREATE TABLE IF NOT EXISTS tag_info(id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE, created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute("CREATE TABLE IF NOT EXISTS item_tag(item_info_id TEXT NOT NULL REFERENCES item_info(id) ON DELETE CASCADE, tag_info_id TEXT NOT NULL REFERENCES tag_info(id) ON DELETE CASCADE, PRIMARY KEY(item_info_id, tag_info_id))");
            connection.Execute("CREATE TABLE IF NOT EXISTS collection_info(id TEXT PRIMARY KEY, name TEXT NOT NULL, icon TEXT NOT NULL CHECK(icon IN ('folder', 'star', 'package', 'tag', 'search')), icon_asset_guid TEXT, created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute("CREATE TABLE IF NOT EXISTS collection_collection(parent_collection_id TEXT NOT NULL REFERENCES collection_info(id) ON DELETE CASCADE, child_collection_id TEXT NOT NULL UNIQUE REFERENCES collection_info(id) ON DELETE CASCADE, CHECK(parent_collection_id != child_collection_id), PRIMARY KEY(parent_collection_id, child_collection_id))");
            connection.Execute("CREATE TABLE IF NOT EXISTS item_collection(item_info_id TEXT NOT NULL REFERENCES item_info(id) ON DELETE CASCADE, collection_info_id TEXT NOT NULL REFERENCES collection_info(id) ON DELETE CASCADE, PRIMARY KEY(item_info_id, collection_info_id))");
            connection.Execute("CREATE TABLE IF NOT EXISTS smart_collection_info(collection_info_id TEXT PRIMARY KEY REFERENCES collection_info(id) ON DELETE CASCADE, match_mode TEXT NOT NULL CHECK(match_mode IN ('all', 'any')), created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute("CREATE TABLE IF NOT EXISTS smart_collection_condition(id TEXT PRIMARY KEY, collection_info_id TEXT NOT NULL REFERENCES smart_collection_info(collection_info_id) ON DELETE CASCADE, field TEXT NOT NULL CHECK(field IN ('name', 'description', 'tag', 'file_name', 'extension')), operator TEXT NOT NULL CHECK(operator IN ('contains', 'equals', 'in', 'exists')), query_text TEXT, created_at TEXT NOT NULL, updated_at TEXT NOT NULL, CHECK(operator = 'exists' OR query_text IS NOT NULL))");
            connection.Execute("CREATE TABLE IF NOT EXISTS variant_group(id TEXT PRIMARY KEY, item_info_id TEXT NOT NULL REFERENCES item_info(id) ON DELETE CASCADE, name TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute("CREATE TABLE IF NOT EXISTS version_group(id TEXT PRIMARY KEY, item_info_id TEXT NOT NULL REFERENCES item_info(id) ON DELETE CASCADE, variant_group_id TEXT REFERENCES variant_group(id) ON DELETE CASCADE, name TEXT NOT NULL, primary_file_info_id TEXT REFERENCES file_info(id) ON DELETE SET NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute(
                @"CREATE TABLE IF NOT EXISTS file_info(
                    id TEXT PRIMARY KEY,
                    item_info_id TEXT REFERENCES item_info(id) ON DELETE CASCADE,
                    version_group_id TEXT REFERENCES version_group(id) ON DELETE CASCADE,
                    variant_group_id TEXT REFERENCES variant_group(id) ON DELETE CASCADE,
                    file_name TEXT NOT NULL,
                    extension TEXT,
                    size_bytes INTEGER,
                    download_id INTEGER,
                    lifecycle TEXT NOT NULL CHECK(lifecycle IN ('active', 'archived')),
                    is_available INTEGER NOT NULL DEFAULT 1 CHECK(is_available IN (0, 1)),
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    CHECK (
                      (item_info_id IS NOT NULL AND version_group_id IS NULL AND variant_group_id IS NULL)
                      OR
                      (item_info_id IS NULL AND version_group_id IS NOT NULL AND variant_group_id IS NULL)
                      OR
                      (item_info_id IS NULL AND version_group_id IS NULL AND variant_group_id IS NOT NULL)
                    ))");
            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS unique_file_info_download_id ON file_info(download_id) WHERE download_id IS NOT NULL");
            connection.Execute(
                @"CREATE TABLE IF NOT EXISTS dependency(
                    source_file_info_id TEXT REFERENCES file_info(id) ON DELETE CASCADE,
                    source_version_group_id TEXT REFERENCES version_group(id) ON DELETE CASCADE,
                    source_variant_group_id TEXT REFERENCES variant_group(id) ON DELETE CASCADE,
                    target_file_info_id TEXT REFERENCES file_info(id) ON DELETE CASCADE,
                    target_version_group_id TEXT REFERENCES version_group(id) ON DELETE CASCADE,
                    CHECK (
                      (source_file_info_id IS NOT NULL AND source_version_group_id IS NULL AND source_variant_group_id IS NULL)
                      OR
                      (source_file_info_id IS NULL AND source_version_group_id IS NOT NULL AND source_variant_group_id IS NULL)
                      OR
                      (source_file_info_id IS NULL AND source_version_group_id IS NULL AND source_variant_group_id IS NOT NULL)
                    ),
                    CHECK (
                      (target_file_info_id IS NOT NULL AND target_version_group_id IS NULL)
                      OR
                      (target_file_info_id IS NULL AND target_version_group_id IS NOT NULL)
                    ),
                    CHECK (source_file_info_id IS NULL OR target_file_info_id IS NULL OR source_file_info_id != target_file_info_id),
                    CHECK (source_version_group_id IS NULL OR target_version_group_id IS NULL OR source_version_group_id != target_version_group_id))");
            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS unique_dependency_file_to_file ON dependency(source_file_info_id, target_file_info_id) WHERE source_file_info_id IS NOT NULL AND target_file_info_id IS NOT NULL");
            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS unique_dependency_file_to_version ON dependency(source_file_info_id, target_version_group_id) WHERE source_file_info_id IS NOT NULL AND target_version_group_id IS NOT NULL");
            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS unique_dependency_version_to_file ON dependency(source_version_group_id, target_file_info_id) WHERE source_version_group_id IS NOT NULL AND target_file_info_id IS NOT NULL");
            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS unique_dependency_version_to_version ON dependency(source_version_group_id, target_version_group_id) WHERE source_version_group_id IS NOT NULL AND target_version_group_id IS NOT NULL");
            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS unique_dependency_variant_to_file ON dependency(source_variant_group_id, target_file_info_id) WHERE source_variant_group_id IS NOT NULL AND target_file_info_id IS NOT NULL");
            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS unique_dependency_variant_to_version ON dependency(source_variant_group_id, target_version_group_id) WHERE source_variant_group_id IS NOT NULL AND target_version_group_id IS NOT NULL");
            connection.Execute("CREATE TABLE IF NOT EXISTS file_import_target(id TEXT PRIMARY KEY, file_info_id TEXT NOT NULL REFERENCES file_info(id) ON DELETE CASCADE, relative_path TEXT NOT NULL)");
            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS unique_file_import_target_file_path ON file_import_target(file_info_id, relative_path)");
            connection.Execute("CREATE TABLE IF NOT EXISTS eagle_file_origin(file_info_id TEXT PRIMARY KEY REFERENCES file_info(id) ON DELETE CASCADE, eagle_item_id TEXT NOT NULL UNIQUE, file_path_cache TEXT, is_deleted INTEGER CHECK(is_deleted IS NULL OR is_deleted IN (0, 1)), imported_at TEXT)");
            connection.Execute("CREATE TABLE IF NOT EXISTS blm_file_origin(file_info_id TEXT PRIMARY KEY REFERENCES file_info(id) ON DELETE CASCADE, registered_item_id TEXT NOT NULL, relative_path TEXT NOT NULL, file_path_cache TEXT, is_missing INTEGER NOT NULL DEFAULT 0 CHECK(is_missing IN (0, 1)), imported_at TEXT)");
            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS unique_blm_file_origin_registered_relative_path ON blm_file_origin(registered_item_id, relative_path)");
            connection.Execute("CREATE TABLE IF NOT EXISTS ee4v_file_origin(file_info_id TEXT PRIMARY KEY REFERENCES file_info(id) ON DELETE CASCADE, ee4v_file_id TEXT NOT NULL UNIQUE, file_path_cache TEXT NOT NULL, imported_at TEXT)");
            EnsureCollectionCycleTriggers(connection);
            var now = Now();
            connection.Execute("INSERT OR IGNORE INTO schema_version(version, created_at, updated_at) VALUES (?, ?, ?)", CurrentSchemaVersion, now, now);
        }

        private static void EnsureColumn(SQLiteConnection connection, string tableName, string columnName, string columnDefinition)
        {
            var exists = connection.Query<TableInfoRow>("PRAGMA table_info('" + tableName + "')")
                .Any(row => string.Equals(row.name, columnName, StringComparison.Ordinal));
            if (exists)
            {
                return;
            }

            connection.Execute("ALTER TABLE " + tableName + " ADD COLUMN " + columnDefinition);
        }

        private static void EnsureCollectionCycleTriggers(SQLiteConnection connection)
        {
            connection.Execute(
                @"CREATE TRIGGER IF NOT EXISTS prevent_collection_collection_cycle_insert
                  BEFORE INSERT ON collection_collection
                  BEGIN
                    SELECT RAISE(ABORT, 'collection cycle is not allowed')
                    WHERE NEW.parent_collection_id = NEW.child_collection_id
                       OR EXISTS (
                         WITH RECURSIVE descendants(id) AS (
                           SELECT child_collection_id
                           FROM collection_collection
                           WHERE parent_collection_id = NEW.child_collection_id
                           UNION
                           SELECT collection_collection.child_collection_id
                           FROM collection_collection
                           INNER JOIN descendants
                             ON collection_collection.parent_collection_id = descendants.id
                         )
                         SELECT 1 FROM descendants WHERE id = NEW.parent_collection_id
                       );
                  END");
            connection.Execute(
                @"CREATE TRIGGER IF NOT EXISTS prevent_collection_collection_cycle_update
                  BEFORE UPDATE OF parent_collection_id, child_collection_id ON collection_collection
                  BEGIN
                    SELECT RAISE(ABORT, 'collection cycle is not allowed')
                    WHERE NEW.parent_collection_id = NEW.child_collection_id
                       OR EXISTS (
                         WITH RECURSIVE descendants(id) AS (
                           SELECT child_collection_id
                           FROM collection_collection
                           WHERE parent_collection_id = NEW.child_collection_id
                             AND NOT (
                               parent_collection_id = OLD.parent_collection_id
                               AND child_collection_id = OLD.child_collection_id
                             )
                           UNION
                           SELECT collection_collection.child_collection_id
                           FROM collection_collection
                           INNER JOIN descendants
                             ON collection_collection.parent_collection_id = descendants.id
                         )
                         SELECT 1 FROM descendants WHERE id = NEW.parent_collection_id
                       );
                  END");
        }
    }
}
