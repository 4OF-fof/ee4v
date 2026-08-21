using System;
using System.Collections.Generic;

namespace Ee4v.FaceExpression
{
    internal sealed class BlendShapeName
    {
        internal BlendShapeName(string role, string variation, string side)
        {
            Role = role ?? string.Empty;
            Variation = variation ?? string.Empty;
            Side = side ?? string.Empty;
        }

        internal string Role { get; }
        internal string Variation { get; }
        internal string Side { get; }
    }

    internal sealed class BlendShapeNamingRule
    {
        private readonly Dictionary<MappingKey, BlendShapeNameMapping> _mappings;

        internal BlendShapeNamingRule(BlendShapeNamePresetState state)
        {
            _mappings = new Dictionary<MappingKey, BlendShapeNameMapping>();
            var presets = state?.presets;
            if (presets == null)
            {
                return;
            }

            for (var presetIndex = 0; presetIndex < presets.Count; presetIndex++)
            {
                var preset = presets[presetIndex];
                if (preset?.mappings == null || string.IsNullOrEmpty(preset.assetGuid))
                {
                    continue;
                }

                for (var mappingIndex = 0; mappingIndex < preset.mappings.Count; mappingIndex++)
                {
                    var mapping = preset.mappings[mappingIndex];
                    if (mapping == null || string.IsNullOrEmpty(mapping.shapeName))
                    {
                        continue;
                    }

                    _mappings[new MappingKey(
                        preset.assetGuid,
                        mapping.meshLocalId,
                        mapping.shapeName)] = mapping;
                }
            }
        }

        internal bool TryParse(BlendShapeChannel channel, out BlendShapeName name)
        {
            name = null;
            if (channel == null ||
                string.IsNullOrEmpty(channel.SourceAssetGuid) ||
                !_mappings.TryGetValue(
                    new MappingKey(
                        channel.SourceAssetGuid,
                        channel.SourceMeshLocalId,
                        channel.Name),
                    out var mapping) ||
                string.IsNullOrWhiteSpace(mapping.role))
            {
                return false;
            }

            var side = string.Equals(mapping.side, "L", StringComparison.OrdinalIgnoreCase)
                ? "L"
                : string.Equals(mapping.side, "R", StringComparison.OrdinalIgnoreCase)
                    ? "R"
                    : string.Empty;
            name = new BlendShapeName(
                mapping.role.Trim(),
                (mapping.variation ?? string.Empty).Trim(),
                side);
            return true;
        }

        private readonly struct MappingKey : IEquatable<MappingKey>
        {
            private readonly string _assetGuid;
            private readonly long _meshLocalId;
            private readonly string _shapeName;

            internal MappingKey(string assetGuid, long meshLocalId, string shapeName)
            {
                _assetGuid = assetGuid ?? string.Empty;
                _meshLocalId = meshLocalId;
                _shapeName = shapeName ?? string.Empty;
            }

            public bool Equals(MappingKey other)
            {
                return _meshLocalId == other._meshLocalId &&
                       string.Equals(_assetGuid, other._assetGuid, StringComparison.Ordinal) &&
                       string.Equals(_shapeName, other._shapeName, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is MappingKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = StringComparer.Ordinal.GetHashCode(_assetGuid);
                    hash = (hash * 397) ^ _meshLocalId.GetHashCode();
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_shapeName);
                    return hash;
                }
            }
        }
    }

    internal static class BlendShapeNameClassifier
    {
        internal static BlendShapeNameMapping Classify(
            long meshLocalId,
            string meshName,
            string shapeName)
        {
            var remaining = (shapeName ?? string.Empty).Trim();
            var variationParts = new List<string>();
            var side = string.Empty;

            for (var pass = 0; pass < 5; pass++)
            {
                if (TakeQualifier(ref remaining, out var qualifier))
                {
                    variationParts.Insert(0, qualifier);
                    continue;
                }

                if (string.IsNullOrEmpty(side) && TakeSide(ref remaining, out side))
                {
                    continue;
                }

                if (TakeNumber(ref remaining, out var number))
                {
                    variationParts.Insert(0, number);
                    continue;
                }

                break;
            }

            var separator = remaining.IndexOf('_');
            var hasGroup = separator > 0 && separator < remaining.Length - 1;
            return new BlendShapeNameMapping
            {
                meshLocalId = meshLocalId,
                meshName = meshName ?? string.Empty,
                shapeName = shapeName ?? string.Empty,
                role = hasGroup ? remaining.Substring(separator + 1).Trim() : string.Empty,
                variation = string.Join(" / ", variationParts),
                side = side
            };
        }

        private static bool TakeQualifier(ref string value, out string qualifier)
        {
            qualifier = string.Empty;
            value = value.TrimEnd();
            if (!value.EndsWith(")", StringComparison.Ordinal))
            {
                return false;
            }

            var opening = value.LastIndexOf('(');
            if (opening <= 0 || opening >= value.Length - 2)
            {
                return false;
            }

            qualifier = value.Substring(opening + 1, value.Length - opening - 2).Trim();
            value = value.Substring(0, opening).TrimEnd();
            return qualifier.Length > 0;
        }

        private static bool TakeSide(ref string value, out string side)
        {
            value = value.TrimEnd();
            if (TakeSuffix(ref value, "_left") || TakeSuffix(ref value, "_L"))
            {
                side = "L";
                return true;
            }

            if (TakeSuffix(ref value, "_right") || TakeSuffix(ref value, "_R"))
            {
                side = "R";
                return true;
            }

            side = string.Empty;
            return false;
        }

        private static bool TakeSuffix(ref string value, string suffix)
        {
            if (!value.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }

            var remaining = value.Substring(0, value.Length - suffix.Length);
            if (!HasGroupAndRole(remaining))
            {
                return false;
            }

            value = remaining.TrimEnd();
            return true;
        }

        private static bool TakeNumber(ref string value, out string number)
        {
            value = value.TrimEnd();
            var start = value.Length;
            while (start > 0 && char.IsDigit(value[start - 1]))
            {
                start--;
            }

            if (start == value.Length)
            {
                number = string.Empty;
                return false;
            }

            var prefixEnd = start;
            while (prefixEnd > 0 &&
                   (value[prefixEnd - 1] == '_' || char.IsWhiteSpace(value[prefixEnd - 1])))
            {
                prefixEnd--;
            }

            var remaining = value.Substring(0, prefixEnd).TrimEnd();
            if (!HasGroupAndRole(remaining))
            {
                number = string.Empty;
                return false;
            }

            number = value.Substring(start);
            value = remaining;
            return true;
        }

        private static bool HasGroupAndRole(string value)
        {
            var separator = value.IndexOf('_');
            return separator > 0 && separator < value.Length - 1;
        }
    }
}
