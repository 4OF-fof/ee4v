using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.SQLite;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure.Persistence.SQLite
{
    internal static partial class AssetManagerDatabase
    {
        public static IReadOnlyList<AssetDependency> GetDependencies(DependencyEndpointRequest source)
        {
            using (var connection = OpenConnection())
            {
                var sourceColumns = ToSourceColumns(connection, source);
                var sourceWhere = BuildSourceWhereClause(sourceColumns);
                var rows = connection.Query<DependencyRow>(
                    @"SELECT *
                      FROM dependency
                      WHERE " + sourceWhere.Sql + @"
                      ORDER BY target_file_info_id, target_version_group_id",
                    sourceWhere.Parameters);
                return rows.Select(ToAssetDependency).ToArray();
            }
        }

        public static void SetDependencies(DependencyEndpointRequest source, IReadOnlyList<DependencyEndpointRequest> targets)
        {
            using (var connection = OpenConnection())
            {
                InTransaction(connection, () =>
                {
                    var sourceColumns = ToSourceColumns(connection, source);
                    var targetColumns = (targets ?? Array.Empty<DependencyEndpointRequest>())
                        .Select(target => ToTargetColumns(connection, target))
                        .ToArray();
                    for (var i = 0; i < targetColumns.Length; i++)
                    {
                        if (sourceColumns.SameNode(targetColumns[i]))
                        {
                            throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Self dependency is not allowed.");
                        }
                    }

                    var sourceWhere = BuildSourceWhereClause(sourceColumns);
                    connection.Execute("DELETE FROM dependency WHERE " + sourceWhere.Sql, sourceWhere.Parameters);

                    for (var i = 0; i < targetColumns.Length; i++)
                    {
                        connection.Execute(
                            @"INSERT OR IGNORE INTO dependency(
                                source_file_info_id,
                                source_version_group_id,
                                source_variant_group_id,
                                target_file_info_id,
                                target_version_group_id)
                              VALUES (?, ?, ?, ?, ?)",
                            sourceColumns.FileId,
                            sourceColumns.VersionGroupId,
                            sourceColumns.VariantGroupId,
                            targetColumns[i].FileId,
                            targetColumns[i].VersionGroupId);
                    }
                });
            }
        }

        private static AssetDependency ToAssetDependency(DependencyRow row)
        {
            return new AssetDependency
            {
                Source = ToSourceEndpoint(row),
                Target = ToTargetEndpoint(row)
            };
        }

        private static AssetDependencyEndpoint ToSourceEndpoint(DependencyRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.source_file_info_id))
            {
                return new AssetDependencyEndpoint { Type = AssetDependencyEndpointType.File, Id = row.source_file_info_id };
            }

            if (!string.IsNullOrWhiteSpace(row.source_version_group_id))
            {
                return new AssetDependencyEndpoint { Type = AssetDependencyEndpointType.VersionGroup, Id = row.source_version_group_id };
            }

            return new AssetDependencyEndpoint { Type = AssetDependencyEndpointType.VariantGroup, Id = row.source_variant_group_id };
        }

        private static AssetDependencyEndpoint ToTargetEndpoint(DependencyRow row)
        {
            return !string.IsNullOrWhiteSpace(row.target_file_info_id)
                ? new AssetDependencyEndpoint { Type = AssetDependencyEndpointType.File, Id = row.target_file_info_id }
                : new AssetDependencyEndpoint { Type = AssetDependencyEndpointType.VersionGroup, Id = row.target_version_group_id };
        }

        private static DependencyColumns ToSourceColumns(SQLiteConnection connection, DependencyEndpointRequest endpoint)
        {
            var columns = ToDependencyColumns(connection, endpoint, allowVariant: true);
            return columns;
        }

        private static DependencyWhere BuildSourceWhereClause(DependencyColumns source)
        {
            var clauses = new List<string>();
            var parameters = new List<object>();
            AddNullableClause(clauses, parameters, "source_file_info_id", source.FileId);
            AddNullableClause(clauses, parameters, "source_version_group_id", source.VersionGroupId);
            AddNullableClause(clauses, parameters, "source_variant_group_id", source.VariantGroupId);
            return new DependencyWhere(string.Join(" AND ", clauses.ToArray()), parameters.ToArray());
        }

        private static void AddNullableClause(ICollection<string> clauses, ICollection<object> parameters, string columnName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                clauses.Add(columnName + " IS NULL");
                return;
            }

            clauses.Add(columnName + " = ?");
            parameters.Add(value);
        }

        private static DependencyColumns ToTargetColumns(SQLiteConnection connection, DependencyEndpointRequest endpoint)
        {
            return ToDependencyColumns(connection, endpoint, allowVariant: false);
        }

        private static DependencyColumns ToDependencyColumns(SQLiteConnection connection, DependencyEndpointRequest endpoint, bool allowVariant)
        {
            if (endpoint == null || string.IsNullOrWhiteSpace(endpoint.Id))
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Dependency endpoint is required.");
            }

            if (endpoint.Type == AssetDependencyEndpointType.File)
            {
                EnsureFileExists(connection, endpoint.Id);
                return new DependencyColumns(endpoint.Id, null, null);
            }

            if (endpoint.Type == AssetDependencyEndpointType.VersionGroup)
            {
                EnsureVersionGroupExists(connection, endpoint.Id);
                return new DependencyColumns(null, endpoint.Id, null);
            }

            if (!allowVariant)
            {
                throw new AssetManagerException(AssetManagerErrorCode.InvalidRequest, "Variant group cannot be a dependency target.");
            }

            EnsureVariantGroupExists(connection, endpoint.Id);
            return new DependencyColumns(null, null, endpoint.Id);
        }

        private sealed class DependencyColumns
        {
            public DependencyColumns(string fileId, string versionGroupId, string variantGroupId)
            {
                FileId = fileId;
                VersionGroupId = versionGroupId;
                VariantGroupId = variantGroupId;
            }

            public string FileId { get; private set; }
            public string VersionGroupId { get; private set; }
            public string VariantGroupId { get; private set; }

            public bool SameNode(DependencyColumns other)
            {
                return other != null &&
                       StringEquals(FileId, other.FileId) &&
                       StringEquals(VersionGroupId, other.VersionGroupId) &&
                       StringEquals(VariantGroupId, other.VariantGroupId);
            }
        }

        private sealed class DependencyWhere
        {
            public DependencyWhere(string sql, object[] parameters)
            {
                Sql = sql;
                Parameters = parameters;
            }

            public string Sql { get; private set; }
            public object[] Parameters { get; private set; }
        }
    }
}
