using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Api.Connecter.Blm;
using Ee4v.AssetManager.Api.Connecter.Eagle;
using Ee4v.SQLite;
using SQLite;
using UnityEngine;

namespace Ee4v.AssetManager.Api
{
    internal static partial class AssetManagerDatabase
    {
        private static SQLiteConnection OpenConnection()
        {
            SqliteBootstrap.EnsureInitialized();
            var globalPath = Environment.ExpandEnvironmentVariables(Ee4v.Core.Settings.SettingApi.Get(AssetManagerDefinitions.Ee4vGlobalPath));
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
            connection.Execute("CREATE TABLE IF NOT EXISTS sync_info(source_type TEXT PRIMARY KEY CHECK(source_type IN ('blm', 'eagle', 'ee4v')), last_sync_at TEXT, last_sync_status TEXT NOT NULL DEFAULT 'success' CHECK(last_sync_status IN ('success', 'failed', 'partial')))");
            EnsureColumn(connection, "sync_info", "last_sync_status", "last_sync_status TEXT NOT NULL DEFAULT 'success' CHECK(last_sync_status IN ('success', 'failed', 'partial'))");
            connection.Execute("CREATE TABLE IF NOT EXISTS item_info(id TEXT PRIMARY KEY, name TEXT NOT NULL, description TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute("CREATE TABLE IF NOT EXISTS shop_info(id TEXT PRIMARY KEY, name TEXT NOT NULL, subdomain TEXT NOT NULL UNIQUE, thumbnail_url TEXT)");
            connection.Execute("CREATE TABLE IF NOT EXISTS booth_info(id TEXT PRIMARY KEY, item_info_id TEXT NOT NULL UNIQUE REFERENCES item_info(id) ON DELETE CASCADE, booth_item_id INTEGER NOT NULL UNIQUE, shop_info_id TEXT NOT NULL REFERENCES shop_info(id), name TEXT NOT NULL, description TEXT NOT NULL, thumbnail_url TEXT, last_updated_at TEXT)");
            connection.Execute("CREATE TABLE IF NOT EXISTS tag_info(id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE, created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute("CREATE TABLE IF NOT EXISTS item_tag(item_info_id TEXT NOT NULL REFERENCES item_info(id) ON DELETE CASCADE, tag_info_id TEXT NOT NULL REFERENCES tag_info(id) ON DELETE CASCADE, PRIMARY KEY(item_info_id, tag_info_id))");
            connection.Execute("CREATE TABLE IF NOT EXISTS collection_info(id TEXT PRIMARY KEY, name TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute("CREATE TABLE IF NOT EXISTS collection_collection(parent_collection_id TEXT NOT NULL REFERENCES collection_info(id) ON DELETE CASCADE, child_collection_id TEXT NOT NULL UNIQUE REFERENCES collection_info(id) ON DELETE CASCADE, CHECK(parent_collection_id != child_collection_id), PRIMARY KEY(parent_collection_id, child_collection_id))");
            connection.Execute("CREATE TABLE IF NOT EXISTS item_collection(item_info_id TEXT NOT NULL REFERENCES item_info(id) ON DELETE CASCADE, collection_info_id TEXT NOT NULL REFERENCES collection_info(id) ON DELETE CASCADE, PRIMARY KEY(item_info_id, collection_info_id))");
            connection.Execute("CREATE TABLE IF NOT EXISTS smart_collection_info(collection_info_id TEXT PRIMARY KEY REFERENCES collection_info(id) ON DELETE CASCADE, match_mode TEXT NOT NULL CHECK(match_mode IN ('all', 'any')), created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute("CREATE TABLE IF NOT EXISTS smart_collection_condition(id TEXT PRIMARY KEY, collection_info_id TEXT NOT NULL REFERENCES smart_collection_info(collection_info_id) ON DELETE CASCADE, field TEXT NOT NULL CHECK(field IN ('name', 'description', 'tag', 'source_type', 'file_name', 'extension', 'lifecycle')), operator TEXT NOT NULL CHECK(operator IN ('contains', 'equals', 'in', 'exists')), query_text TEXT, created_at TEXT NOT NULL, updated_at TEXT NOT NULL, CHECK(operator = 'exists' OR query_text IS NOT NULL))");
            connection.Execute("CREATE TABLE IF NOT EXISTS file_info(id TEXT PRIMARY KEY, item_info_id TEXT NOT NULL REFERENCES item_info(id) ON DELETE CASCADE, file_name TEXT NOT NULL, extension TEXT, size_bytes INTEGER, download_id INTEGER, lifecycle TEXT NOT NULL CHECK(lifecycle IN ('active', 'archived')), created_at TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS unique_file_info_download_id ON file_info(download_id) WHERE download_id IS NOT NULL");
            connection.Execute("CREATE TABLE IF NOT EXISTS file_dependency(dependent_file_info_id TEXT NOT NULL REFERENCES file_info(id) ON DELETE CASCADE, dependency_file_info_id TEXT NOT NULL REFERENCES file_info(id) ON DELETE CASCADE, dependency_type TEXT NOT NULL CHECK(dependency_type IN ('requires')), CHECK(dependent_file_info_id != dependency_file_info_id), PRIMARY KEY(dependent_file_info_id, dependency_file_info_id, dependency_type))");
            connection.Execute("CREATE TABLE IF NOT EXISTS file_import_target(id TEXT PRIMARY KEY, file_info_id TEXT NOT NULL REFERENCES file_info(id) ON DELETE CASCADE, relative_path TEXT NOT NULL)");
            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS unique_file_import_target_file_path ON file_import_target(file_info_id, relative_path)");
            connection.Execute("CREATE TABLE IF NOT EXISTS eagle_file_origin(file_info_id TEXT PRIMARY KEY REFERENCES file_info(id) ON DELETE CASCADE, eagle_item_id TEXT NOT NULL UNIQUE, file_path_cache TEXT, is_deleted INTEGER CHECK(is_deleted IS NULL OR is_deleted IN (0, 1)), imported_at TEXT)");
            connection.Execute("CREATE TABLE IF NOT EXISTS blm_file_origin(file_info_id TEXT PRIMARY KEY REFERENCES file_info(id) ON DELETE CASCADE, registered_item_id TEXT NOT NULL, relative_path TEXT NOT NULL, file_path_cache TEXT, imported_at TEXT)");
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
