using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SQLite;

namespace Ee4v.AssetManager.Infrastructure.Persistence.SQLite
{
    internal static partial class AssetManagerDatabase
    {
        private static ImportedFileParent ResolveImportedFileParent(SQLiteConnection connection, string itemId, string fileName, string currentFileId = null)
        {
            var variantName = ResolveAvatarVariantName(connection, itemId, fileName, AssetManagerInfrastructureSettings.Current.AvatarNames, currentFileId);

            var variantGroupId = string.IsNullOrWhiteSpace(variantName)
                ? null
                : EnsureImportedVariantGroup(connection, itemId, variantName);

            if (!string.IsNullOrWhiteSpace(variantGroupId))
            {
                return new ImportedFileParent(null, null, variantGroupId);
            }

            return new ImportedFileParent(itemId, null, null);
        }

        private static void ReconcileImportedFileGroups(SQLiteConnection connection, string itemId, string now)
        {
            ReconcileImportedAvatarVariantGroups(connection, itemId, now);
            ReconcileImportedVersionGroups(connection, itemId, now);
            DeleteEmptyImportedGroups(connection, itemId);
            EnsureMissingVersionGroupPrimaries(connection, itemId, now);
        }

        private static void EnsureMissingVersionGroupPrimaries(SQLiteConnection connection, string itemId, string now)
        {
            var groupIds = connection.Query<VersionGroupRow>(
                    @"SELECT *
                      FROM version_group
                      WHERE item_info_id = ?
                        AND primary_file_info_id IS NULL
                        AND EXISTS (
                            SELECT 1
                            FROM file_info
                            WHERE file_info.version_group_id = version_group.id
                              AND file_info.lifecycle = 'active'
                        )",
                    itemId)
                .Select(group => group.id)
                .ToArray();
            for (var i = 0; i < groupIds.Length; i++)
            {
                SelectAutomaticVersionGroupPrimary(connection, groupIds[i], now);
            }
        }

        private static bool UpdateFileInfoParentSnapshot(SQLiteConnection connection, string fileId, ImportedFileParent parent, string now)
        {
            var row = connection.Query<FileRow>("SELECT * FROM file_info WHERE id = ? LIMIT 1", fileId).FirstOrDefault();
            if (row == null)
            {
                return false;
            }

            var changed = !StringEquals(row.item_info_id, parent.ItemId) ||
                          !StringEquals(row.version_group_id, parent.VersionGroupId) ||
                          !StringEquals(row.variant_group_id, parent.VariantGroupId);
            if (!changed)
            {
                return false;
            }

            var previousVersionGroupId = row.version_group_id;
            connection.Execute(
                @"UPDATE file_info
                  SET item_info_id = ?, version_group_id = ?, variant_group_id = ?, updated_at = ?
                  WHERE id = ?",
                parent.ItemId,
                parent.VersionGroupId,
                parent.VariantGroupId,
                now,
                fileId);
            if (!string.IsNullOrWhiteSpace(previousVersionGroupId) &&
                !StringEquals(previousVersionGroupId, parent.VersionGroupId))
            {
                connection.Execute(
                    "UPDATE version_group SET primary_file_info_id = NULL, updated_at = ? WHERE id = ? AND primary_file_info_id = ?",
                    now,
                    previousVersionGroupId,
                    fileId);
            }

            return true;
        }

        private static void EnsureVersionGroupPrimaryIfMissing(SQLiteConnection connection, string versionGroupId, string fileId, string now)
        {
            if (string.IsNullOrWhiteSpace(versionGroupId))
            {
                return;
            }

            var primaryFileId = connection.ExecuteScalar<string>("SELECT primary_file_info_id FROM version_group WHERE id = ? LIMIT 1", versionGroupId);
            if (!string.IsNullOrWhiteSpace(primaryFileId))
            {
                return;
            }

            SelectAutomaticVersionGroupPrimary(connection, versionGroupId, now, fileId);
        }

        private static void SelectAutomaticVersionGroupPrimary(SQLiteConnection connection, string versionGroupId, string now, string fallbackFileId = null)
        {
            if (string.IsNullOrWhiteSpace(versionGroupId))
            {
                return;
            }

            var files = connection.Query<FileRow>(
                "SELECT * FROM file_info WHERE version_group_id = ? AND lifecycle = 'active' ORDER BY file_name COLLATE NOCASE, id",
                versionGroupId);
            var selectedFileId = SelectHighestSemanticVersionFile(files);
            if (string.IsNullOrWhiteSpace(selectedFileId))
            {
                selectedFileId = fallbackFileId;
            }

            connection.Execute(
                "UPDATE version_group SET primary_file_info_id = ?, updated_at = ? WHERE id = ?",
                selectedFileId,
                now,
                versionGroupId);
        }

        private static string SelectHighestSemanticVersionFile(IReadOnlyList<FileRow> files)
        {
            if (files == null || files.Count == 0)
            {
                return null;
            }

            var pattern = AssetManagerInfrastructureSettings.Current.VersionGroupRegex;
            FileRow selected = null;
            int[] selectedVersion = null;
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                int[] version;
                var hasVersion = TryParseSemanticVersion(ResolveSemanticVersionValue(file.file_name, pattern), out version);
                if (selected == null ||
                    (hasVersion && selectedVersion == null) ||
                    (hasVersion && selectedVersion != null && CompareSemanticVersions(version, selectedVersion) > 0))
                {
                    selected = file;
                    selectedVersion = hasVersion ? version : null;
                }
            }

            return selected == null ? null : selected.id;
        }

        private static bool TryParseSemanticVersion(string value, out int[] version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split('.');
            if (parts.Length == 0 || parts.Length > 4)
            {
                return false;
            }

            var parsed = new int[4];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out parsed[i]) || parsed[i] < 0)
                {
                    return false;
                }
            }

            version = parsed;
            return true;
        }

        private static int CompareSemanticVersions(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            for (var i = 0; i < 4; i++)
            {
                var comparison = left[i].CompareTo(right[i]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return 0;
        }

        private static string ResolveSemanticVersionValue(string fileName, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            Match match;
            try
            {
                match = Regex.Match(fileName, pattern);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (!match.Success)
            {
                return null;
            }

            if (match.Groups["name"] != null && match.Groups["name"].Success)
            {
                return match.Groups["name"].Value.Trim();
            }

            var numericMatch = Regex.Match(match.Value, @"\d+(?:\.\d+){0,3}");
            return numericMatch.Success ? numericMatch.Value : null;
        }

        private static string ResolveVersionSeriesName(string fileName, string pattern, string avatarNamesText, string variantGroupName)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            Match match;
            try
            {
                match = Regex.Match(fileName, pattern);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (!match.Success)
            {
                return null;
            }

            var baseName = RemoveExtension(fileName);
            var removeIndex = Math.Min(match.Index, baseName.Length);
            var removeLength = Math.Min(match.Length, baseName.Length - removeIndex);
            var value = removeLength <= 0
                ? baseName
                : baseName.Remove(removeIndex, removeLength);
            value = NormalizeVersionSeriesName(value);
            value = string.IsNullOrWhiteSpace(variantGroupName)
                ? RemoveConfiguredAvatarTokenFromSeriesName(value, avatarNamesText)
                : RemoveVariantGroupTokenFromSeriesName(value, variantGroupName);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string RemoveVariantGroupTokenFromSeriesName(string value, string variantGroupName)
        {
            var normalizedVariantGroupName = NormalizeVersionSeriesName(variantGroupName);
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(normalizedVariantGroupName))
            {
                return value;
            }

            var nextValue = Regex.Replace(
                value,
                @"(^|_)" + Regex.Escape(normalizedVariantGroupName) + @"(?=$|_)",
                "_",
                RegexOptions.IgnoreCase);
            nextValue = Regex.Replace(nextValue, "_+", "_").Trim('_');
            return NormalizeVersionSeriesName(nextValue);
        }

        private static string RemoveConfiguredAvatarTokenFromSeriesName(string value, string avatarNamesText)
        {
            var normalizedValue = NormalizeGroupingText(value);
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return value;
            }

            var matchedAvatarNames = FindContainedAvatarNames(normalizedValue, ParseAvatarNames(avatarNamesText));
            if (matchedAvatarNames.Count == 0)
            {
                return value;
            }

            var nextValue = value ?? string.Empty;
            for (var i = 0; i < matchedAvatarNames.Count; i++)
            {
                var avatarSeriesName = NormalizeVersionSeriesName(matchedAvatarNames[i].DisplayName);
                nextValue = Regex.Replace(nextValue, @"(^|_)" + Regex.Escape(avatarSeriesName) + @"(?=$|_)", "_", RegexOptions.IgnoreCase);
            }

            nextValue = Regex.Replace(nextValue, "_+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(nextValue)
                ? value
                : NormalizeVersionSeriesName(nextValue);
        }

        private static string NormalizeVersionSeriesName(string value)
        {
            value = NormalizeDatasourceText(value);
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            value = Regex.Replace(value, @"[\s_\-.]+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }

        private static void ReconcileImportedAvatarVariantGroups(SQLiteConnection connection, string itemId, string now)
        {
            var avatarNamesText = AssetManagerInfrastructureSettings.Current.AvatarNames;
            var avatarNames = ParseAvatarNames(avatarNamesText);
            if (avatarNames.Count == 0)
            {
                return;
            }

            var rows = QueryFilesForItem(connection, itemId);
            var candidates = rows
                .Select(row => CreateAvatarGroupingCandidate(row, avatarNames))
                .Where(candidate => candidate != null)
                .ToArray();
            var groupedSignatures = candidates
                .GroupBy(candidate => candidate.Signature)
                .Where(group => group.Count() >= 2)
                .Select(group => group.Key)
                .ToArray();

            for (var i = 0; i < candidates.Length; i++)
            {
                if (!groupedSignatures.Any(signature => StringEquals(signature, candidates[i].Signature)))
                {
                    continue;
                }

                var variantGroupId = EnsureImportedVariantGroup(connection, itemId, candidates[i].GroupName);
                if (!string.IsNullOrWhiteSpace(candidates[i].Row.version_group_id))
                {
                    MoveVersionGroupToVariantGroup(connection, candidates[i].Row.version_group_id, variantGroupId, now);
                    continue;
                }

                UpdateFileInfoParentSnapshot(connection, candidates[i].Row.id, new ImportedFileParent(null, null, variantGroupId), now);
            }
        }

        private static void ReconcileImportedVersionGroups(SQLiteConnection connection, string itemId, string now)
        {
            var pattern = AssetManagerInfrastructureSettings.Current.VersionGroupRegex;
            var avatarNamesText = AssetManagerInfrastructureSettings.Current.AvatarNames;
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return;
            }

            var rows = QueryFilesForItem(connection, itemId);
            var candidates = rows
                .Select(row => CreateVersionGroupingCandidate(connection, row, pattern, avatarNamesText))
                .Where(candidate => candidate != null)
                .ToArray();
            var groupedKeys = candidates
                .GroupBy(candidate => candidate.Key)
                .Where(group => group.Select(candidate => candidate.VersionValue).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 2)
                .Select(group => group.Key)
                .ToArray();

            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (!groupedKeys.Any(key => StringEquals(key, candidate.Key)))
                {
                    continue;
                }

                var versionGroupId = EnsureImportedVersionGroup(connection, itemId, candidate.VariantGroupId, candidate.SeriesName);
                if (!string.IsNullOrWhiteSpace(candidate.Row.version_group_id))
                {
                    MergeVersionGroupInto(connection, candidate.Row.version_group_id, versionGroupId, now);
                    continue;
                }

                UpdateFileInfoParentSnapshot(connection, candidate.Row.id, new ImportedFileParent(null, versionGroupId, null), now);
            }
        }

        private static string ResolveAvatarVariantName(SQLiteConnection connection, string itemId, string fileName, string avatarNamesText, string currentFileId)
        {
            var avatarNames = ParseAvatarNames(avatarNamesText);
            if (avatarNames.Count == 0 || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var candidate = CreateAvatarGroupingCandidate(fileName, avatarNames);
            if (candidate == null)
            {
                return null;
            }

            return HasMatchingAvatarGroupingSignature(connection, itemId, avatarNames, candidate.Signature, currentFileId)
                ? candidate.GroupName
                : null;
        }

        private static void MoveVersionGroupToVariantGroup(SQLiteConnection connection, string versionGroupId, string variantGroupId, string now)
        {
            var source = connection.Query<VersionGroupRow>("SELECT * FROM version_group WHERE id = ? LIMIT 1", versionGroupId).FirstOrDefault();
            if (source == null || StringEquals(source.variant_group_id, variantGroupId))
            {
                return;
            }

            var target = connection.Query<VersionGroupRow>(
                    "SELECT * FROM version_group WHERE item_info_id = ? AND variant_group_id = ? AND name = ? AND id != ? LIMIT 1",
                    source.item_info_id,
                    variantGroupId,
                    source.name,
                    source.id)
                .FirstOrDefault();
            if (target == null)
            {
                connection.Execute("UPDATE version_group SET variant_group_id = ?, updated_at = ? WHERE id = ?", variantGroupId, now, source.id);
                return;
            }

            connection.Execute("UPDATE file_info SET version_group_id = ?, updated_at = ? WHERE version_group_id = ?", target.id, now, source.id);
            connection.Execute("UPDATE OR IGNORE dependency SET source_version_group_id = ? WHERE source_version_group_id = ?", target.id, source.id);
            connection.Execute("DELETE FROM dependency WHERE source_version_group_id = ?", source.id);
            connection.Execute("UPDATE OR IGNORE dependency SET target_version_group_id = ? WHERE target_version_group_id = ?", target.id, source.id);
            connection.Execute("DELETE FROM dependency WHERE target_version_group_id = ?", source.id);
            if (string.IsNullOrWhiteSpace(target.primary_file_info_id) && !string.IsNullOrWhiteSpace(source.primary_file_info_id))
            {
                connection.Execute("UPDATE version_group SET primary_file_info_id = ?, updated_at = ? WHERE id = ?", source.primary_file_info_id, now, target.id);
            }

            connection.Execute("DELETE FROM version_group WHERE id = ?", source.id);
        }

        private static VersionGroupingCandidate CreateVersionGroupingCandidate(SQLiteConnection connection, FileRow row, string pattern, string avatarNamesText)
        {
            var variantGroupId = row.variant_group_id;
            if (!string.IsNullOrWhiteSpace(row.version_group_id))
            {
                var versionGroup = connection.Query<VersionGroupRow>("SELECT * FROM version_group WHERE id = ? LIMIT 1", row.version_group_id).FirstOrDefault();
                variantGroupId = versionGroup == null ? variantGroupId : versionGroup.variant_group_id;
            }

            var variantGroupName = string.IsNullOrWhiteSpace(variantGroupId)
                ? null
                : connection.ExecuteScalar<string>("SELECT name FROM variant_group WHERE id = ? LIMIT 1", variantGroupId);
            var seriesName = ResolveVersionSeriesName(row.file_name, pattern, avatarNamesText, variantGroupName);
            if (string.IsNullOrWhiteSpace(seriesName))
            {
                return null;
            }

            var versionValue = ResolveVersionValue(row.file_name, pattern);
            if (string.IsNullOrWhiteSpace(versionValue))
            {
                return null;
            }

            return new VersionGroupingCandidate(row, seriesName, versionValue, variantGroupId);
        }

        private static string ResolveVersionValue(string fileName, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            Match match;
            try
            {
                match = Regex.Match(fileName, pattern);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (!match.Success)
            {
                return null;
            }

            var value = match.Groups["name"] != null && match.Groups["name"].Success
                ? match.Groups["name"].Value
                : match.Value;
            value = NormalizeVersionSeriesName(value);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static void MergeVersionGroupInto(SQLiteConnection connection, string sourceVersionGroupId, string targetVersionGroupId, string now)
        {
            if (string.IsNullOrWhiteSpace(sourceVersionGroupId) ||
                string.IsNullOrWhiteSpace(targetVersionGroupId) ||
                StringEquals(sourceVersionGroupId, targetVersionGroupId))
            {
                return;
            }

            var source = connection.Query<VersionGroupRow>("SELECT * FROM version_group WHERE id = ? LIMIT 1", sourceVersionGroupId).FirstOrDefault();
            var target = connection.Query<VersionGroupRow>("SELECT * FROM version_group WHERE id = ? LIMIT 1", targetVersionGroupId).FirstOrDefault();
            if (source == null || target == null)
            {
                return;
            }

            connection.Execute("UPDATE file_info SET version_group_id = ?, updated_at = ? WHERE version_group_id = ?", target.id, now, source.id);
            connection.Execute("UPDATE OR IGNORE dependency SET source_version_group_id = ? WHERE source_version_group_id = ?", target.id, source.id);
            connection.Execute("DELETE FROM dependency WHERE source_version_group_id = ?", source.id);
            connection.Execute("UPDATE OR IGNORE dependency SET target_version_group_id = ? WHERE target_version_group_id = ?", target.id, source.id);
            connection.Execute("DELETE FROM dependency WHERE target_version_group_id = ?", source.id);
            if (string.IsNullOrWhiteSpace(target.primary_file_info_id) && !string.IsNullOrWhiteSpace(source.primary_file_info_id))
            {
                connection.Execute("UPDATE version_group SET primary_file_info_id = ?, updated_at = ? WHERE id = ?", source.primary_file_info_id, now, target.id);
            }

            connection.Execute("DELETE FROM version_group WHERE id = ?", source.id);
        }

        private static void DeleteEmptyImportedGroups(SQLiteConnection connection, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            connection.Execute(
                @"DELETE FROM version_group
                  WHERE item_info_id = ?
                    AND id NOT IN (
                        SELECT DISTINCT version_group_id
                        FROM file_info
                        WHERE version_group_id IS NOT NULL
                    )",
                itemId);
            connection.Execute(
                @"DELETE FROM variant_group
                  WHERE item_info_id = ?
                    AND id NOT IN (
                        SELECT DISTINCT variant_group_id
                        FROM file_info
                        WHERE variant_group_id IS NOT NULL
                    )
                    AND id NOT IN (
                        SELECT DISTINCT variant_group_id
                        FROM version_group
                        WHERE variant_group_id IS NOT NULL
                    )",
                itemId);
        }

        private static IReadOnlyList<AvatarNameCandidate> ParseAvatarNames(string avatarNamesText)
        {
            if (string.IsNullOrWhiteSpace(avatarNamesText))
            {
                return Array.Empty<AvatarNameCandidate>();
            }

            var values = avatarNamesText.Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var results = new List<AvatarNameCandidate>();
            for (var i = 0; i < values.Length; i++)
            {
                var displayName = NormalizeDatasourceText(values[i]).Trim();
                var normalizedName = NormalizeGroupingText(displayName);
                if (string.IsNullOrWhiteSpace(displayName) ||
                    string.IsNullOrWhiteSpace(normalizedName) ||
                    results.Any(candidate => StringEquals(candidate.NormalizedName, normalizedName)))
                {
                    continue;
                }

                results.Add(new AvatarNameCandidate(displayName, normalizedName));
            }

            return results;
        }

        private static bool ContainsGroupingToken(string text, string token)
        {
            var index = text.IndexOf(token, StringComparison.Ordinal);
            while (index >= 0)
            {
                var beforeOk = index == 0 || text[index - 1] == '_';
                var afterIndex = index + token.Length;
                var afterOk = afterIndex >= text.Length || text[afterIndex] == '_';
                if (beforeOk && afterOk)
                {
                    return true;
                }

                index = text.IndexOf(token, index + 1, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool HasMatchingAvatarGroupingSignature(SQLiteConnection connection, string itemId, IReadOnlyList<AvatarNameCandidate> avatarNames, string signature, string currentFileId)
        {
            return QueryFilesForItem(connection, itemId)
                .Where(row => string.IsNullOrWhiteSpace(currentFileId) || !StringEquals(row.id, currentFileId))
                .Select(row => CreateAvatarGroupingCandidate(row.file_name, avatarNames))
                .Any(candidate => candidate != null && StringEquals(candidate.Signature, signature));
        }

        private static AvatarGroupingCandidate CreateAvatarGroupingCandidate(FileRow row, IReadOnlyList<AvatarNameCandidate> avatarNames)
        {
            var candidate = CreateAvatarGroupingCandidate(row.file_name, avatarNames);
            if (candidate == null)
            {
                return null;
            }

            candidate.Row = row;
            return candidate;
        }

        private static AvatarGroupingCandidate CreateAvatarGroupingCandidate(string fileName, IReadOnlyList<AvatarNameCandidate> avatarNames)
        {
            var normalizedFileName = NormalizeGroupingText(RemoveExtension(fileName));
            if (string.IsNullOrWhiteSpace(normalizedFileName))
            {
                return null;
            }

            var matchedAvatarNames = FindContainedAvatarNames(normalizedFileName, avatarNames);
            if (matchedAvatarNames.Count == 0)
            {
                return null;
            }

            var signature = CreateNonAvatarGroupingSignature(normalizedFileName, matchedAvatarNames);
            if (string.IsNullOrWhiteSpace(signature))
            {
                return null;
            }

            var groupName = CreateNonAvatarGroupingName(fileName, matchedAvatarNames);
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return null;
            }

            return new AvatarGroupingCandidate(groupName, signature);
        }

        private static IReadOnlyList<AvatarNameCandidate> FindContainedAvatarNames(string normalizedText, IReadOnlyList<AvatarNameCandidate> avatarNames)
        {
            return avatarNames
                .Where(candidate => ContainsGroupingToken(normalizedText, candidate.NormalizedName))
                .OrderByDescending(candidate => candidate.NormalizedName.Length)
                .ToArray();
        }

        private static string CreateNonAvatarGroupingSignature(string text, IReadOnlyList<AvatarNameCandidate> avatarNames)
        {
            var signature = text ?? string.Empty;
            for (var i = 0; i < avatarNames.Count; i++)
            {
                signature = RemoveGroupingToken(signature, avatarNames[i].NormalizedName);
            }

            signature = RemoveVersionLikeTokens(signature);
            signature = Regex.Replace(signature, "_+", "_").Trim('_');
            return signature;
        }

        private static string CreateNonAvatarGroupingName(string fileName, IReadOnlyList<AvatarNameCandidate> avatarNames)
        {
            var value = NormalizeVersionSeriesName(RemoveExtension(fileName));
            for (var i = 0; i < avatarNames.Count; i++)
            {
                var avatarSeriesName = NormalizeVersionSeriesName(avatarNames[i].DisplayName);
                value = Regex.Replace(value ?? string.Empty, @"(^|_)" + Regex.Escape(avatarSeriesName) + @"(?=$|_)", "_", RegexOptions.IgnoreCase);
            }

            value = RemoveVersionLikeTokens(value);
            value = Regex.Replace(value, "_+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(value) ? null : NormalizeVersionSeriesName(value);
        }

        private static string RemoveGroupingToken(string text, string token)
        {
            return Regex.Replace(text ?? string.Empty, @"(^|_)" + Regex.Escape(token) + @"(?=$|_)", "_");
        }

        private static string RemoveVersionLikeTokens(string text)
        {
            return Regex.Replace(
                text ?? string.Empty,
                @"(?i)(?:^|_)(?:v|ver|version)?\d+(?:_\d+){0,3}(?=$|_)",
                "_");
        }

        private static string RemoveExtension(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var extensionIndex = value.LastIndexOf('.');
            return extensionIndex <= 0 ? value : value.Substring(0, extensionIndex);
        }

        private static string NormalizeGroupingText(string value)
        {
            value = NormalizeDatasourceText(value);
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var chars = value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
            return Regex.Replace(new string(chars), "_+", "_").Trim('_');
        }

        private static string EnsureImportedVariantGroup(SQLiteConnection connection, string itemId, string name)
        {
            var existing = connection.Query<VariantGroupRow>(
                    "SELECT * FROM variant_group WHERE item_info_id = ? AND name = ? LIMIT 1",
                    itemId,
                    name)
                .FirstOrDefault();
            if (existing != null)
            {
                return existing.id;
            }

            var now = Now();
            var id = NewId();
            connection.Execute(
                "INSERT INTO variant_group(id, item_info_id, name, created_at, updated_at) VALUES (?, ?, ?, ?, ?)",
                id,
                itemId,
                name,
                now,
                now);
            return id;
        }

        private static string EnsureImportedVersionGroup(SQLiteConnection connection, string itemId, string variantGroupId, string name)
        {
            var sql = string.IsNullOrWhiteSpace(variantGroupId)
                ? "SELECT * FROM version_group WHERE item_info_id = ? AND variant_group_id IS NULL AND name = ? LIMIT 1"
                : "SELECT * FROM version_group WHERE item_info_id = ? AND variant_group_id = ? AND name = ? LIMIT 1";
            var parameters = string.IsNullOrWhiteSpace(variantGroupId)
                ? new object[] { itemId, name }
                : new object[] { itemId, variantGroupId, name };
            var existing = connection.Query<VersionGroupRow>(sql, parameters).FirstOrDefault();
            if (existing != null)
            {
                return existing.id;
            }

            var now = Now();
            var id = NewId();
            connection.Execute(
                "INSERT INTO version_group(id, item_info_id, variant_group_id, name, primary_file_info_id, created_at, updated_at) VALUES (?, ?, ?, ?, NULL, ?, ?)",
                id,
                itemId,
                variantGroupId,
                name,
                now,
                now);
            return id;
        }

        private sealed class ImportedFileParent
        {
            public ImportedFileParent(string itemId, string versionGroupId, string variantGroupId)
            {
                ItemId = itemId;
                VersionGroupId = versionGroupId;
                VariantGroupId = variantGroupId;
            }

            public string ItemId { get; private set; }
            public string VersionGroupId { get; private set; }
            public string VariantGroupId { get; private set; }
        }

        private sealed class AvatarNameCandidate
        {
            public AvatarNameCandidate(string displayName, string normalizedName)
            {
                DisplayName = displayName;
                NormalizedName = normalizedName;
            }

            public string DisplayName { get; private set; }
            public string NormalizedName { get; private set; }
        }

        private sealed class AvatarGroupingCandidate
        {
            public AvatarGroupingCandidate(string groupName, string signature)
            {
                GroupName = groupName;
                Signature = signature;
            }

            public string GroupName { get; private set; }
            public string Signature { get; private set; }
            public FileRow Row { get; set; }
        }

        private sealed class VersionGroupingCandidate
        {
            public VersionGroupingCandidate(FileRow row, string seriesName, string versionValue, string variantGroupId)
            {
                Row = row;
                SeriesName = seriesName;
                VersionValue = versionValue;
                VariantGroupId = variantGroupId;
                Key = (variantGroupId ?? string.Empty) + "\n" + seriesName;
            }

            public FileRow Row { get; private set; }
            public string SeriesName { get; private set; }
            public string VersionValue { get; private set; }
            public string VariantGroupId { get; private set; }
            public string Key { get; private set; }
        }
    }
}
