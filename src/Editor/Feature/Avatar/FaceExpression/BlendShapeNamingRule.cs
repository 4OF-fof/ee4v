using System;
using System.Collections.Generic;

namespace Ee4v.FaceExpression
{
    internal sealed class BlendShapeName
    {
        internal BlendShapeName(string role, string side)
        {
            Role = role ?? string.Empty;
            Side = side ?? string.Empty;
        }

        internal string Role { get; }
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
                side);
            return true;
        }

        internal bool IsMouthMorph(
            string assetGuid,
            long meshLocalId,
            string shapeName)
        {
            return !string.IsNullOrEmpty(assetGuid) &&
                   _mappings.TryGetValue(
                       new MappingKey(assetGuid, meshLocalId, shapeName),
                       out var mapping) &&
                   mapping.mouthMorph;
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
            var separator = remaining.IndexOf('_');
            var hasGroup = separator > 0 && separator < remaining.Length - 1;
            var role = hasGroup
                ? remaining.Substring(separator + 1).Trim()
                : string.Empty;
            var side = string.Empty;
            TakeSide(ref role, out side);
            return new BlendShapeNameMapping
            {
                meshLocalId = meshLocalId,
                meshName = meshName ?? string.Empty,
                shapeName = shapeName ?? string.Empty,
                role = role,
                side = side
            };
        }

        private static bool TakeSide(ref string value, out string side)
        {
            if (TakeSide(ref value, "_left") || TakeSide(ref value, "_L"))
            {
                side = "L";
                return true;
            }

            if (TakeSide(ref value, "_right") || TakeSide(ref value, "_R"))
            {
                side = "R";
                return true;
            }

            side = string.Empty;
            return false;
        }

        private static bool TakeSide(ref string value, string marker)
        {
            var markerIndex = value.LastIndexOf(
                marker,
                StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            var suffixStart = markerIndex + marker.Length;
            if (suffixStart < value.Length &&
                !char.IsWhiteSpace(value[suffixStart]) &&
                !char.IsDigit(value[suffixStart]) &&
                value[suffixStart] != '(')
            {
                return false;
            }

            value = (value.Substring(0, markerIndex) +
                     value.Substring(suffixStart)).Trim();
            return true;
        }
    }
}
