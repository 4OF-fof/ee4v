using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ee4v.AssetManager.Infrastructure.Datasources.Blm;
using Ee4v.AssetManager.Infrastructure.Datasources.Eagle;
using Ee4v.SQLite;
using SQLite;
using UnityEngine;

namespace Ee4v.AssetManager.Infrastructure.Persistence.SQLite
{
    internal static partial class AssetManagerDatabase
    {
        private static T InTransaction<T>(SQLiteConnection connection, Func<T> action)
        {
            T result = default(T);
            connection.RunInTransaction(() => { result = action(); });
            return result;
        }

        private static void InTransaction(SQLiteConnection connection, Action action)
        {
            connection.RunInTransaction(action);
        }

        private static void UpsertSyncInfo(SQLiteConnection connection, string sourceType)
        {
            UpsertSyncInfo(connection, sourceType, AssetSyncState.Success);
        }

        private static void UpsertSyncInfo(SQLiteConnection connection, string sourceType, AssetSyncState state)
        {
            connection.Execute("INSERT OR REPLACE INTO sync_info(source_type, last_sync_at, last_sync_status) VALUES (?, ?, ?)", sourceType, Now(), ToDbSyncState(state));
        }

        private static void RecordSyncInfoSafely(string sourceType, AssetSyncState state)
        {
            try
            {
                using (var connection = OpenConnection())
                {
                    UpsertSyncInfo(connection, sourceType, state);
                }
            }
            catch
            {
                // The sync result still reports the datasource failure when status persistence is unavailable.
            }
        }

        private static AssetSyncState ResolveSyncState(int created, int updated, int unchanged, int error)
        {
            if (error <= 0)
            {
                return AssetSyncState.Success;
            }

            return created > 0 || updated > 0 || unchanged > 0
                ? AssetSyncState.Partial
                : AssetSyncState.Failed;
        }

        private static void CountStatus(AssetSyncStatus status, ref int created, ref int updated, ref int unchanged, ref int error)
        {
            if (status == AssetSyncStatus.Created)
            {
                created++;
                return;
            }

            if (status == AssetSyncStatus.Updated)
            {
                updated++;
                return;
            }

            if (status == AssetSyncStatus.Unchanged)
            {
                unchanged++;
                return;
            }

            error++;
        }

        private static AssetSyncStatus MergeStatus(AssetSyncStatus left, AssetSyncStatus right)
        {
            if (left == AssetSyncStatus.Error || right == AssetSyncStatus.Error)
            {
                return AssetSyncStatus.Error;
            }

            if (left == AssetSyncStatus.Created || right == AssetSyncStatus.Created)
            {
                return AssetSyncStatus.Created;
            }

            if (left == AssetSyncStatus.Updated || right == AssetSyncStatus.Updated)
            {
                return AssetSyncStatus.Updated;
            }

            return AssetSyncStatus.Unchanged;
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.Ordinal);
        }

        private static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string Now()
        {
            return DateTime.UtcNow.ToString("O");
        }

        private static string ToDbDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToUniversalTime().ToString("O") : null;
        }

        private static DateTime ParseDate(string value)
        {
            DateTime parsed;
            return DateTime.TryParse(value, out parsed) ? parsed : DateTime.MinValue;
        }

        private static DateTime? ParseNullableDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return ParseDate(value);
        }

        private static string GetExtension(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.TrimStart('.');
        }

        private static string NormalizeDatasourceText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormKC);
            var builder = new StringBuilder(normalized.Length);
            for (var i = 0; i < normalized.Length; i++)
            {
                var current = normalized[i];
                if (char.IsHighSurrogate(current) &&
                    i + 1 < normalized.Length &&
                    char.IsLowSurrogate(normalized[i + 1]))
                {
                    var codePoint = char.ConvertToUtf32(current, normalized[i + 1]);
                    var mapped = TryMapMathematicalAlphanumericSymbol(codePoint);
                    if (mapped.HasValue)
                    {
                        builder.Append(mapped.Value);
                    }

                    i++;
                    continue;
                }

                if (current == '\uFE0E' ||
                    current == '\uFE0F' ||
                    current == '\u200D')
                {
                    continue;
                }

                if (char.IsControl(current) &&
                    current != '\r' &&
                    current != '\n' &&
                    current != '\t')
                {
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static char? TryMapMathematicalAlphanumericSymbol(int codePoint)
        {
            var mapped = TryMapAlphabetRange(codePoint, 0x1D400, 26, 'A');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D41A, 26, 'a');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D434, 26, 'A');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D44E, 26, 'a');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D468, 26, 'A');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D482, 26, 'a');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D5A0, 26, 'A');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D5BA, 26, 'a');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D5D4, 26, 'A');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D5EE, 26, 'a');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D608, 26, 'A');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D622, 26, 'a');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D670, 26, 'A');
            if (mapped.HasValue) return mapped;
            mapped = TryMapAlphabetRange(codePoint, 0x1D68A, 26, 'a');
            if (mapped.HasValue) return mapped;

            mapped = TryMapDigitRange(codePoint, 0x1D7CE);
            if (mapped.HasValue) return mapped;
            mapped = TryMapDigitRange(codePoint, 0x1D7D8);
            if (mapped.HasValue) return mapped;
            mapped = TryMapDigitRange(codePoint, 0x1D7E2);
            if (mapped.HasValue) return mapped;
            mapped = TryMapDigitRange(codePoint, 0x1D7EC);
            if (mapped.HasValue) return mapped;
            return TryMapDigitRange(codePoint, 0x1D7F6);
        }

        private static char? TryMapAlphabetRange(int codePoint, int start, int length, char asciiStart)
        {
            if (codePoint < start || codePoint >= start + length)
            {
                return null;
            }

            return (char)(asciiStart + codePoint - start);
        }

        private static char? TryMapDigitRange(int codePoint, int start)
        {
            if (codePoint < start || codePoint >= start + 10)
            {
                return null;
            }

            return (char)('0' + codePoint - start);
        }

        private static string GetSubdomainFromUrl(string shopUrl)
        {
            if (string.IsNullOrWhiteSpace(shopUrl))
            {
                return string.Empty;
            }

            Uri uri;
            if (!Uri.TryCreate(shopUrl, UriKind.Absolute, out uri))
            {
                return string.Empty;
            }

            return uri.Host.EndsWith(".booth.pm", StringComparison.OrdinalIgnoreCase)
                ? uri.Host.Substring(0, uri.Host.Length - ".booth.pm".Length)
                : uri.Host;
        }

        private static string ToDbLifecycle(AssetFileLifecycle lifecycle)
        {
            return lifecycle == AssetFileLifecycle.Archived ? "archived" : "active";
        }

        private static AssetFileLifecycle FromDbLifecycle(string lifecycle)
        {
            return lifecycle == "archived" ? AssetFileLifecycle.Archived : AssetFileLifecycle.Active;
        }

        private static AssetSourceType FromDbSourceType(string sourceType)
        {
            if (sourceType == "eagle") return AssetSourceType.Eagle;
            if (sourceType == "ee4v") return AssetSourceType.Ee4v;
            return AssetSourceType.Blm;
        }

        private static string ToDbSyncState(AssetSyncState state)
        {
            if (state == AssetSyncState.Failed) return "failed";
            if (state == AssetSyncState.Partial) return "partial";
            return "success";
        }

        private static AssetSyncState FromDbSyncState(string state)
        {
            if (state == "failed") return AssetSyncState.Failed;
            if (state == "partial") return AssetSyncState.Partial;
            return AssetSyncState.Success;
        }

        private static string ToDbSmartField(SmartCollectionConditionField field)
        {
            if (field == SmartCollectionConditionField.Description) return "description";
            if (field == SmartCollectionConditionField.Tag) return "tag";
            if (field == SmartCollectionConditionField.SourceType) return "source_type";
            if (field == SmartCollectionConditionField.FileName) return "file_name";
            if (field == SmartCollectionConditionField.Extension) return "extension";
            if (field == SmartCollectionConditionField.Lifecycle) return "lifecycle";
            return "name";
        }

        private static SmartCollectionConditionField FromDbSmartField(string field)
        {
            if (field == "description") return SmartCollectionConditionField.Description;
            if (field == "tag") return SmartCollectionConditionField.Tag;
            if (field == "source_type") return SmartCollectionConditionField.SourceType;
            if (field == "file_name") return SmartCollectionConditionField.FileName;
            if (field == "extension") return SmartCollectionConditionField.Extension;
            if (field == "lifecycle") return SmartCollectionConditionField.Lifecycle;

            SmartCollectionConditionField parsed;
            return Enum.TryParse(field, true, out parsed) ? parsed : SmartCollectionConditionField.Name;
        }

        private static string ToDbSmartOperator(SmartCollectionConditionOperator op)
        {
            if (op == SmartCollectionConditionOperator.Equals) return "equals";
            if (op == SmartCollectionConditionOperator.In) return "in";
            if (op == SmartCollectionConditionOperator.Exists) return "exists";
            return "contains";
        }

        private static SmartCollectionConditionOperator FromDbSmartOperator(string op)
        {
            if (op == "equals") return SmartCollectionConditionOperator.Equals;
            if (op == "in") return SmartCollectionConditionOperator.In;
            if (op == "exists") return SmartCollectionConditionOperator.Exists;

            SmartCollectionConditionOperator parsed;
            return Enum.TryParse(op, true, out parsed) ? parsed : SmartCollectionConditionOperator.Contains;
        }

        internal static AssetManagerException ToAssetManagerException(SQLiteException exception)
        {
            var message = exception.Message ?? string.Empty;
            return new AssetManagerException(GetDatabaseErrorCode(message), message, exception);
        }

        private static AssetManagerErrorCode GetDatabaseErrorCode(string message)
        {
            if (message.IndexOf("collection cycle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AssetManagerErrorCode.CollectionCycle;
            }

            if (message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AssetManagerErrorCode.Duplicate;
            }

            if (message.IndexOf("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AssetManagerErrorCode.NotFound;
            }

            if (message.IndexOf("CHECK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("constraint", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AssetManagerErrorCode.InvalidRequest;
            }

            return AssetManagerErrorCode.DatabaseError;
        }
    }
}
