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
        private static void UpsertSyncInfo(SQLiteConnection connection, string sourceType)
        {
            connection.Execute("INSERT OR REPLACE INTO sync_info(source_type, last_sync_at) VALUES (?, ?)", sourceType, Now());
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
