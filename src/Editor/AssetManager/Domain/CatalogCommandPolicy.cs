using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Domain
{
    internal sealed class AssetRuleException : Exception
    {
        internal AssetRuleException(string message)
            : base(message)
        {
        }
    }

    internal static class AssetManagerRules
    {
        internal static void Require(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new AssetRuleException(field + " is required.");
            }
        }

        internal static string NormalizeTagPath(string value)
        {
            var path = (value ?? string.Empty).Trim();
            if (path.StartsWith("#", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }

            var segments = path.Split('/');
            var normalized = new List<string>(segments.Length);
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i].Trim();
                if (segment.Length == 0)
                {
                    throw new AssetRuleException(
                        "Tag path cannot contain empty segments.");
                }

                normalized.Add(segment.ToLowerInvariant());
            }

            if (normalized.Count == 0)
            {
                throw new AssetRuleException("Tag path is required.");
            }

            return string.Join("/", normalized);
        }

        internal static string NormalizeExtension(string value)
        {
            var extension = (value ?? string.Empty).Trim();
            while (extension.StartsWith(".", StringComparison.Ordinal))
            {
                extension = extension.Substring(1);
            }

            if (extension.Length == 0)
            {
                throw new AssetRuleException("File extension is required.");
            }

            return extension.ToLowerInvariant();
        }

        internal static string NormalizeTargetPath(string value)
        {
            if (value == null)
            {
                throw new AssetRuleException(
                    "Target path cannot be null.");
            }

            if (value.Length == 0)
            {
                return string.Empty;
            }

            var path = value.Replace('\\', '/').Trim();
            if (path.Length == 0)
            {
                throw new AssetRuleException(
                    "Target path cannot contain only whitespace.");
            }

            if (path.StartsWith("/", StringComparison.Ordinal) ||
                path.IndexOf('\0') >= 0 ||
                path.IndexOf(':') >= 0)
            {
                throw new AssetRuleException(
                    "Target path must be relative to the file.");
            }

            var segments = path.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0 ||
                    segments[i] == "." ||
                    segments[i] == "..")
                {
                    throw new AssetRuleException(
                        "Target path cannot contain empty or traversal segments.");
                }
            }

            return string.Join("/", segments);
        }

        internal static void ValidateFilter(AssetFilterNode root)
        {
            if (root == null)
            {
                throw new AssetRuleException("Filter root is required.");
            }

            var visited = new HashSet<AssetFilterNode>();
            ValidateNode(root, visited);
        }

        internal static bool Matches(AssetItem item, AssetFilterNode root)
        {
            if (root == null)
            {
                return true;
            }

            switch (root.Type)
            {
                case AssetFilterNodeType.And:
                    return All(item, root.Children);
                case AssetFilterNodeType.Or:
                    return Any(item, root.Children);
                case AssetFilterNodeType.Not:
                    return !Matches(item, root.Children[0]);
                case AssetFilterNodeType.Condition:
                    return MatchesCondition(item, root);
                default:
                    return false;
            }
        }

        private static void ValidateNode(
            AssetFilterNode node,
            ISet<AssetFilterNode> visited)
        {
            if (node == null)
            {
                throw new AssetRuleException("Filter node is required.");
            }

            if (!visited.Add(node))
            {
                throw new AssetRuleException(
                    "Filter nodes must form a tree.");
            }

            var children =
                node.Children ?? Array.Empty<AssetFilterNode>();
            switch (node.Type)
            {
                case AssetFilterNodeType.And:
                case AssetFilterNodeType.Or:
                    if (children.Count < 2)
                    {
                        throw new AssetRuleException(
                            "AND and OR require at least two children.");
                    }

                    RequireGroupFieldsEmpty(node);
                    break;
                case AssetFilterNodeType.Not:
                    if (children.Count != 1)
                    {
                        throw new AssetRuleException(
                            "NOT requires exactly one child.");
                    }

                    RequireGroupFieldsEmpty(node);
                    break;
                case AssetFilterNodeType.Condition:
                    if (children.Count != 0 ||
                        !node.ConditionType.HasValue ||
                        !Enum.IsDefined(
                            typeof(AssetFilterConditionType),
                            node.ConditionType.Value))
                    {
                        throw new AssetRuleException(
                            "Condition node is invalid.");
                    }

                    Require(node.Value, "condition value");
                    if (node.ConditionType.Value ==
                        AssetFilterConditionType.HasTag)
                    {
                        NormalizeTagPath(node.Value);
                    }
                    else if (node.ConditionType.Value ==
                             AssetFilterConditionType.HasFileExtension)
                    {
                        NormalizeExtension(node.Value);
                    }

                    return;
                default:
                    throw new AssetRuleException(
                        "Filter node type is invalid.");
            }

            for (var i = 0; i < children.Count; i++)
            {
                ValidateNode(children[i], visited);
            }
        }

        private static void RequireGroupFieldsEmpty(AssetFilterNode node)
        {
            if (node.ConditionType.HasValue ||
                !string.IsNullOrWhiteSpace(node.Value))
            {
                throw new AssetRuleException(
                    "Logical nodes cannot contain condition values.");
            }
        }

        private static bool All(
            AssetItem item,
            IReadOnlyList<AssetFilterNode> children)
        {
            for (var i = 0; i < children.Count; i++)
            {
                if (!Matches(item, children[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Any(
            AssetItem item,
            IReadOnlyList<AssetFilterNode> children)
        {
            for (var i = 0; i < children.Count; i++)
            {
                if (Matches(item, children[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesCondition(
            AssetItem item,
            AssetFilterNode condition)
        {
            var value = condition.Value ?? string.Empty;
            switch (condition.ConditionType.Value)
            {
                case AssetFilterConditionType.NameContains:
                    return Contains(item.Name, value);
                case AssetFilterConditionType.DescriptionContains:
                    return Contains(item.Description, value);
                case AssetFilterConditionType.HasTag:
                    return HasTag(item.Tags, NormalizeTagPath(value));
                case AssetFilterConditionType.HasFileExtension:
                    return HasExtension(
                        item.Files,
                        NormalizeExtension(value));
                default:
                    return false;
            }
        }

        private static bool Contains(string source, string value)
        {
            return (source ?? string.Empty).IndexOf(
                       value,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasTag(
            IReadOnlyList<AssetTag> tags,
            string path)
        {
            var prefix = path + "/";
            var source = tags ?? Array.Empty<AssetTag>();
            for (var i = 0; i < source.Count; i++)
            {
                var candidate = source[i].Path ?? string.Empty;
                if (string.Equals(
                        candidate,
                        path,
                        StringComparison.Ordinal) ||
                    candidate.StartsWith(
                        prefix,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasExtension(
            IReadOnlyList<AssetFile> files,
            string extension)
        {
            var source = files ?? Array.Empty<AssetFile>();
            for (var i = 0; i < source.Count; i++)
            {
                if (!source[i].IsArchived &&
                    source[i].IsAvailable &&
                    string.Equals(
                        source[i].Extension,
                        extension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
