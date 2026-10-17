using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetLibrary;
using Ee4v.SQLite;
using Newtonsoft.Json;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure.Persistence
{
    internal sealed partial class SqliteAssetManagerStore
    {
        private static void ExecuteExpressionSchema(SQLiteConnection connection)
        {
            foreach (var sql in new[]
            {
                @"CREATE TABLE IF NOT EXISTS face_expression(
                    id TEXT PRIMARY KEY, name TEXT NOT NULL, folder TEXT NOT NULL,
                    root_guid TEXT NOT NULL UNIQUE, primary_fbx_guid TEXT NOT NULL,
                    revision TEXT NOT NULL, metadata_json TEXT NOT NULL)",
                @"CREATE INDEX IF NOT EXISTS ix_face_expression_fbx
                    ON face_expression(primary_fbx_guid, name)",
                @"CREATE TABLE IF NOT EXISTS face_expression_target(
                    expression_id TEXT NOT NULL REFERENCES face_expression(id) ON DELETE CASCADE,
                    fbx_guid TEXT NOT NULL, mesh_local_id INTEGER NOT NULL,
                    renderer_path TEXT NOT NULL, shape_name TEXT NOT NULL,
                    PRIMARY KEY(expression_id, renderer_path, shape_name))",
                @"CREATE TABLE IF NOT EXISTS face_expression_asset(
                    expression_id TEXT NOT NULL REFERENCES face_expression(id) ON DELETE CASCADE,
                    asset_guid TEXT NOT NULL, relative_path TEXT NOT NULL,
                    hash TEXT NOT NULL, meta_hash TEXT NOT NULL,
                    PRIMARY KEY(expression_id, asset_guid))",
                @"CREATE TABLE IF NOT EXISTS face_expression_dependency(
                    expression_id TEXT NOT NULL REFERENCES face_expression(id) ON DELETE CASCADE,
                    dependent_guid TEXT NOT NULL, asset_guid TEXT NOT NULL, metadata_json TEXT NOT NULL,
                    PRIMARY KEY(expression_id, dependent_guid, asset_guid))",
                @"CREATE TABLE IF NOT EXISTS face_expression_import(
                    project_key TEXT NOT NULL, expression_id TEXT NOT NULL,
                    asset_guid TEXT NOT NULL, revision TEXT NOT NULL, baseline_hash TEXT NOT NULL,
                    PRIMARY KEY(project_key, expression_id, asset_guid))",
                @"CREATE TABLE IF NOT EXISTS face_expression_project_location(
                    project_key TEXT NOT NULL, expression_id TEXT NOT NULL,
                    root_guid TEXT NOT NULL, asset_path TEXT NOT NULL, folder TEXT NOT NULL,
                    PRIMARY KEY(project_key, root_guid))"
            }) connection.Execute(sql);
        }

        internal IReadOnlyList<ExpressionLibraryEntry> GetExpressions(string fbxGuid = null)
        {
            using (var connection = OpenConnection())
            {
                var sql = "SELECT metadata_json FROM face_expression";
                var values = Array.Empty<object>();
                if (!string.IsNullOrEmpty(fbxGuid))
                {
                    sql += " WHERE primary_fbx_guid = ?";
                    values = new object[] { fbxGuid };
                }
                return connection.QueryScalars<string>(sql + " ORDER BY name COLLATE NOCASE, id", values)
                    .Select(JsonConvert.DeserializeObject<ExpressionLibraryEntry>).ToArray();
            }
        }

        internal void ReplaceExpressions(IReadOnlyList<ExpressionLibraryEntry> entries)
        {
            using (var connection = OpenConnection())
            using (var transaction = new DatabaseTransaction(connection))
            {
                var previous = connection.QueryScalars<string>("SELECT metadata_json FROM face_expression")
                    .Select(JsonConvert.DeserializeObject<ExpressionLibraryEntry>).ToDictionary(entry => entry.Id);
                foreach (var id in previous.Keys.Except(entries.Select(entry => entry.Id)))
                    connection.Execute("DELETE FROM face_expression WHERE id = ?", id);
                foreach (var entry in entries)
                {
                    if (previous.TryGetValue(entry.Id, out var old) &&
                        JsonConvert.SerializeObject(old) == JsonConvert.SerializeObject(entry)) continue;
                    connection.Execute("DELETE FROM face_expression WHERE id = ?", entry.Id);
                    WriteExpression(connection, entry);
                }
                connection.Execute("DELETE FROM face_expression_project_location WHERE expression_id NOT IN (SELECT id FROM face_expression)");
                transaction.Commit();
            }
        }

        internal void SaveExpression(ExpressionLibraryEntry entry)
        {
            using (var connection = OpenConnection())
            using (var transaction = new DatabaseTransaction(connection))
            {
                connection.Execute("DELETE FROM face_expression WHERE id = ?", entry.Id);
                WriteExpression(connection, entry);
                transaction.Commit();
            }
        }

        internal void DeleteExpression(string id)
        {
            using (var connection = OpenConnection())
            using (var transaction = new DatabaseTransaction(connection))
            {
                connection.Execute("DELETE FROM face_expression WHERE id = ?", id);
                connection.Execute("DELETE FROM face_expression_import WHERE expression_id = ?", id);
                connection.Execute("DELETE FROM face_expression_project_location WHERE expression_id = ?", id);
                transaction.Commit();
            }
        }

        private static void WriteExpression(SQLiteConnection connection, ExpressionLibraryEntry entry)
        {
            connection.Execute("INSERT INTO face_expression VALUES(?, ?, ?, ?, ?, ?, ?)",
                entry.Id, entry.Name, entry.Folder, entry.RootGuid, entry.PrimaryFbxGuid,
                entry.Revision, JsonConvert.SerializeObject(entry));
            foreach (var target in entry.Targets)
                connection.Execute("INSERT INTO face_expression_target VALUES(?, ?, ?, ?, ?)",
                    entry.Id, target.FbxGuid, target.MeshLocalId, target.RendererPath, target.ShapeName);
            foreach (var asset in entry.Assets)
                connection.Execute("INSERT INTO face_expression_asset VALUES(?, ?, ?, ?, ?)",
                    entry.Id, asset.Guid, asset.Path, asset.Hash, asset.MetaHash);
            foreach (var dependency in entry.Dependencies)
                connection.Execute("INSERT INTO face_expression_dependency VALUES(?, ?, ?, ?)",
                    entry.Id, dependency.DependentGuid, dependency.AssetGuid,
                    JsonConvert.SerializeObject(dependency));
        }

        internal string GetExpressionImportRevision(string projectKey, string guid)
        {
            using (var connection = OpenConnection())
                return connection.ExecuteScalar<string>(
                    @"SELECT imported.revision FROM face_expression_import imported
                      JOIN face_expression expression ON expression.id = imported.expression_id
                      WHERE imported.project_key = ? AND imported.asset_guid = ? AND expression.root_guid = imported.asset_guid LIMIT 1",
                    projectKey, guid);
        }

        internal IReadOnlyList<ExpressionProjectLocation> GetExpressionProjectLocations(string projectKey)
        {
            using (var connection = OpenConnection())
                return connection.Query<ExpressionProjectLocation>(
                    @"SELECT expression_id AS ExpressionId, root_guid AS RootGuid,
                             asset_path AS AssetPath, folder AS OriginalFolder
                      FROM face_expression_project_location WHERE project_key = ?", projectKey);
        }

        internal void SaveExpressionProjectLocations(string projectKey, IReadOnlyList<ExpressionProjectLocation> locations)
        {
            using (var connection = OpenConnection())
            using (var transaction = new DatabaseTransaction(connection))
            {
                foreach (var location in locations)
                    connection.Execute("INSERT OR REPLACE INTO face_expression_project_location VALUES(?, ?, ?, ?, ?)",
                        projectKey, location.ExpressionId, location.RootGuid, location.AssetPath, location.OriginalFolder);
                transaction.Commit();
            }
        }

        internal string GetExpressionImportHash(string projectKey, string id, string guid)
        {
            using (var connection = OpenConnection())
                return connection.ExecuteScalar<string>(
                    "SELECT baseline_hash FROM face_expression_import WHERE project_key = ? AND expression_id = ? AND asset_guid = ?",
                    projectKey, id, guid);
        }

        internal void RecordExpressionImports(string projectKey, ExpressionLibraryEntry entry,
            IReadOnlyDictionary<string, string> hashes)
        {
            using (var connection = OpenConnection())
            using (var transaction = new DatabaseTransaction(connection))
            {
                foreach (var pair in hashes)
                    connection.Execute("INSERT OR REPLACE INTO face_expression_import VALUES(?, ?, ?, ?, ?)",
                        projectKey, entry.Id, pair.Key, entry.Revision, pair.Value);
                transaction.Commit();
            }
        }
    }
}
