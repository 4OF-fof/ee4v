using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    public sealed class BlendShapePresetSummary
    {
        public string AssetGuid { get; set; }
        public string AssetPath { get; set; }
        public string Name { get; set; }
        public int MappingCount { get; set; }
    }

    public sealed class BlendShapePresetMappingData
    {
        public string MeshLocalId { get; set; }
        public string MeshName { get; set; }
        public string ShapeName { get; set; }
        public string HeaderText { get; set; }
        public string Role { get; set; }
        public string Side { get; set; }
        public bool MouthMorph { get; set; }
        public string AppearancePart { get; set; }
        public string AppearanceGroup { get; set; }
    }

    public sealed class BlendShapePresetData
    {
        public string AssetGuid { get; set; }
        public string AssetPath { get; set; }
        public string Name { get; set; }
        public string Revision { get; set; }
        public IReadOnlyList<BlendShapePresetMappingData> Mappings { get; set; }
    }

    public sealed class BlendShapePresetMappingChange
    {
        public string MeshLocalId { get; set; }
        public string ShapeName { get; set; }
        public string Role { get; set; }
        public string Side { get; set; }
        public bool? MouthMorph { get; set; }
        public string AppearancePart { get; set; }
        public string AppearanceGroup { get; set; }
    }

    public sealed class BlendShapePresetEditResult
    {
        public bool Changed { get; set; }
        public bool DryRun { get; set; }
        public string AssetGuid { get; set; }
        public string Revision { get; set; }
        public int UpdatedMappingCount { get; set; }
    }

    public static class BlendShapePresetApi
    {
        private static readonly HashSet<string> AppearanceParts =
            new HashSet<string>(StringComparer.Ordinal)
            {
                string.Empty,
                BlendShapeAppearancePart.Expression,
                BlendShapeAppearancePart.Head,
                BlendShapeAppearancePart.Chest,
                BlendShapeAppearancePart.Waist,
                BlendShapeAppearancePart.Shoulders,
                BlendShapeAppearancePart.Arms,
                BlendShapeAppearancePart.Hands,
                BlendShapeAppearancePart.Legs,
                BlendShapeAppearancePart.Feet,
                BlendShapeAppearancePart.Other
            };

        public static IReadOnlyList<BlendShapePresetSummary> ListPresets()
        {
            return BlendShapePresetStorage.Shared.Load().presets
                .Select(preset => new BlendShapePresetSummary
                {
                    AssetGuid = preset.assetGuid,
                    AssetPath = preset.assetPath,
                    Name = preset.name,
                    MappingCount = preset.mappings.Count
                })
                .ToArray();
        }

        public static BlendShapePresetData GetPreset(string assetGuid)
        {
            var preset = LoadPreset(assetGuid);
            return preset == null ? null : ToData(preset);
        }

        public static BlendShapePresetEditResult UpdatePreset(
            string assetGuid,
            IReadOnlyList<BlendShapePresetMappingChange> changes,
            string expectedRevision,
            bool dryRun = false)
        {
            if (string.IsNullOrWhiteSpace(expectedRevision))
            {
                throw new ArgumentException("A preset revision is required.",
                    nameof(expectedRevision));
            }

            if (changes == null || changes.Count == 0)
            {
                throw new ArgumentException("At least one mapping change is required.",
                    nameof(changes));
            }

            var saved = LoadPreset(assetGuid);
            if (saved == null)
            {
                throw new InvalidOperationException(
                    "The saved BlendShape preset was not found.");
            }

            var revision = Revision(saved);
            if (!string.Equals(expectedRevision, revision, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The BlendShape preset changed. Read it again before updating.");
            }

            var draft = JsonUtility.FromJson<BlendShapeFbxPreset>(
                JsonUtility.ToJson(saved));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var updated = 0;
            foreach (var change in changes)
            {
                if (change == null ||
                    !long.TryParse(change.MeshLocalId, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var meshLocalId) ||
                    string.IsNullOrEmpty(change.ShapeName))
                {
                    throw new ArgumentException(
                        "Each change requires meshLocalId and shapeName.",
                        nameof(changes));
                }

                var key = meshLocalId.ToString(CultureInfo.InvariantCulture) +
                          "\n" + change.ShapeName;
                if (!seen.Add(key))
                {
                    throw new ArgumentException(
                        "A BlendShape mapping was specified more than once.",
                        nameof(changes));
                }

                var matches = draft.mappings.Where(mapping =>
                    mapping.meshLocalId == meshLocalId &&
                    string.Equals(mapping.shapeName, change.ShapeName,
                        StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1 || !string.IsNullOrEmpty(matches[0].headerText))
                {
                    throw new ArgumentException(
                        "The mapping was not found or is a section header: " +
                        change.ShapeName, nameof(changes));
                }

                if (change.Role == null && change.Side == null &&
                    !change.MouthMorph.HasValue &&
                    change.AppearancePart == null &&
                    change.AppearanceGroup == null)
                {
                    throw new ArgumentException(
                        "Each change must specify an editable field.",
                        nameof(changes));
                }

                if (change.Side != null && change.Side != string.Empty &&
                    change.Side != "L" && change.Side != "R")
                {
                    throw new ArgumentException(
                        "Side must be empty, L, or R.", nameof(changes));
                }

                if (change.AppearancePart != null &&
                    !AppearanceParts.Contains(change.AppearancePart))
                {
                    throw new ArgumentException(
                        "Appearance part is not supported.", nameof(changes));
                }

                var mappingToUpdate = matches[0];
                var before = JsonUtility.ToJson(mappingToUpdate);
                if (change.Role != null)
                {
                    mappingToUpdate.role = change.Role.Trim();
                }
                if (change.Side != null)
                {
                    mappingToUpdate.side = change.Side;
                }
                if (change.MouthMorph.HasValue)
                {
                    mappingToUpdate.mouthMorph = change.MouthMorph.Value;
                }
                if (change.AppearancePart != null)
                {
                    mappingToUpdate.appearancePart = change.AppearancePart;
                }
                if (change.AppearanceGroup != null)
                {
                    mappingToUpdate.appearanceGroup = change.AppearanceGroup.Trim();
                }
                if (!string.Equals(before, JsonUtility.ToJson(mappingToUpdate),
                        StringComparison.Ordinal))
                {
                    updated++;
                }
            }

            if (updated > 0 && !dryRun)
            {
                BlendShapePresetStorage.Shared.Save(draft);
            }

            return new BlendShapePresetEditResult
            {
                Changed = updated > 0,
                DryRun = dryRun,
                AssetGuid = assetGuid,
                Revision = updated > 0 ? Revision(draft) : revision,
                UpdatedMappingCount = updated
            };
        }

        private static BlendShapeFbxPreset LoadPreset(string assetGuid)
        {
            if (string.IsNullOrWhiteSpace(assetGuid))
            {
                throw new ArgumentException("An FBX asset GUID is required.",
                    nameof(assetGuid));
            }

            return BlendShapeNamePresetSetting.Find(
                BlendShapePresetStorage.Shared.Load(), assetGuid);
        }

        private static BlendShapePresetData ToData(BlendShapeFbxPreset preset)
        {
            return new BlendShapePresetData
            {
                AssetGuid = preset.assetGuid,
                AssetPath = preset.assetPath,
                Name = preset.name,
                Revision = Revision(preset),
                Mappings = preset.mappings.Select(mapping =>
                    new BlendShapePresetMappingData
                    {
                        MeshLocalId = mapping.meshLocalId.ToString(
                            CultureInfo.InvariantCulture),
                        MeshName = mapping.meshName,
                        ShapeName = mapping.shapeName,
                        HeaderText = mapping.headerText,
                        Role = mapping.role,
                        Side = mapping.side,
                        MouthMorph = mapping.mouthMorph,
                        AppearancePart = mapping.appearancePart,
                        AppearanceGroup = mapping.appearanceGroup
                    }).ToArray()
            };
        }

        private static string Revision(BlendShapeFbxPreset preset)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(preset));
                return BitConverter.ToString(sha256.ComputeHash(bytes))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
