using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure.Persistence
{
    internal sealed partial class SqliteAssetManagerStore : IAssetVariantIndex
    {
        private static void ExecuteVariantSchema(SQLiteConnection connection)
        {
            connection.Execute(@"CREATE TABLE IF NOT EXISTS variant(
                id TEXT PRIMARY KEY, source_prefab_guid TEXT NOT NULL,
                name TEXT NOT NULL, description TEXT NOT NULL,
                parent_item_id TEXT, root_asset_path TEXT NOT NULL,
                head_revision_id TEXT NOT NULL, updated_at TEXT NOT NULL)");
            connection.Execute(@"CREATE TABLE IF NOT EXISTS variant_revision(
                id TEXT PRIMARY KEY, variant_id TEXT NOT NULL REFERENCES variant(id)
                ON DELETE CASCADE, parent_revision_id TEXT, number INTEGER NOT NULL,
                commit_id TEXT NOT NULL, memo TEXT NOT NULL, created_at TEXT NOT NULL,
                UNIQUE(variant_id, number))");
        }

        public void ReplaceVariantIndex(IReadOnlyList<AssetVariantSnapshot> snapshots)
        {
            Run(() =>
            {
                using (var connection = OpenConnection())
                using (var transaction = new DatabaseTransaction(connection))
                {
                    connection.Execute("DELETE FROM variant_revision");
                    connection.Execute("DELETE FROM variant");
                    foreach (var group in snapshots.GroupBy(snapshot => snapshot.Variant.Id))
                    {
                        var head = group.First(snapshot =>
                            snapshot.Revision.Id == snapshot.Variant.HeadRevisionId);
                        var variant = head.Variant;
                        connection.Execute(@"INSERT INTO variant VALUES(?,?,?,?,?,?,?,?)",
                            variant.Id, variant.SourcePrefabGuid,
                            variant.Name, variant.Description ?? string.Empty,
                            variant.ParentItemId, variant.RootAssetPath,
                            variant.HeadRevisionId, variant.UpdatedAt.ToString("O"));
                        foreach (var snapshot in group)
                        {
                            var revision = snapshot.Revision;
                            connection.Execute(@"INSERT INTO variant_revision VALUES(?,?,?,?,?,?,?)",
                                revision.Id, revision.VariantId, revision.ParentRevisionId,
                                revision.Number, revision.CommitId, revision.Memo ?? string.Empty,
                                revision.CreatedAt.ToString("O"));
                        }
                    }
                    transaction.Commit();
                }
                return true;
            });
        }

        public IReadOnlyList<AssetVariant> GetVariants()
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return (IReadOnlyList<AssetVariant>)connection.Query<VariantRow>(
                        @"SELECT id AS Id, source_prefab_guid AS SourcePrefabGuid,
                        name AS Name, description AS Description,
                        parent_item_id AS ParentItemId, root_asset_path AS RootAssetPath,
                        head_revision_id AS HeadRevisionId, updated_at AS UpdatedAt
                        FROM variant ORDER BY name COLLATE NOCASE, id")
                        .Select(row => new AssetVariant
                        {
                            Id = row.Id, SourcePrefabGuid = row.SourcePrefabGuid,
                            Name = row.Name, Description = row.Description,
                            ParentItemId = row.ParentItemId, RootAssetPath = row.RootAssetPath,
                            HeadRevisionId = row.HeadRevisionId, UpdatedAt = ParseDate(row.UpdatedAt)
                        }).ToArray();
                }
            });
        }

        public IReadOnlyList<AssetVariantRevision> GetVariantRevisions(string variantId)
        {
            return Run(() =>
            {
                using (var connection = OpenConnection())
                {
                    return (IReadOnlyList<AssetVariantRevision>)connection.Query<VariantRevisionRow>(
                        @"SELECT id AS Id, variant_id AS VariantId,
                        parent_revision_id AS ParentRevisionId, number AS Number,
                        commit_id AS CommitId, memo AS Memo, created_at AS CreatedAt
                        FROM variant_revision WHERE variant_id = ? ORDER BY number DESC", variantId)
                        .Select(row => new AssetVariantRevision
                        {
                            Id = row.Id, VariantId = row.VariantId,
                            ParentRevisionId = row.ParentRevisionId, Number = row.Number,
                            CommitId = row.CommitId, Memo = row.Memo,
                            CreatedAt = ParseDate(row.CreatedAt)
                        }).ToArray();
                }
            });
        }

        private sealed class VariantRow
        {
            public string Id { get; set; }
            public string SourcePrefabGuid { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string ParentItemId { get; set; }
            public string RootAssetPath { get; set; }
            public string HeadRevisionId { get; set; }
            public string UpdatedAt { get; set; }
        }

        private sealed class VariantRevisionRow
        {
            public string Id { get; set; }
            public string VariantId { get; set; }
            public string ParentRevisionId { get; set; }
            public int Number { get; set; }
            public string CommitId { get; set; }
            public string Memo { get; set; }
            public string CreatedAt { get; set; }
        }
    }
}
