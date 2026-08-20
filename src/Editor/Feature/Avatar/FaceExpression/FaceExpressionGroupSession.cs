using System;
using System.Collections.Generic;

namespace Ee4v.FaceExpression
{
    internal sealed class BlendShapeGroup
    {
        internal BlendShapeGroup(string name)
        {
            Name = name;
        }

        internal string Name { get; }

        internal int Count { get; private set; }

        internal void AddShape()
        {
            Count++;
        }
    }

    internal static class FaceExpressionGroupSession
    {
        private static IReadOnlyList<BlendShapeGroup> _groups =
            Array.Empty<BlendShapeGroup>();

        internal static event Action Changed;

        internal static IReadOnlyList<BlendShapeGroup> Groups => _groups;

        internal static int TotalCount { get; private set; }

        internal static string SelectedGroupName { get; private set; }

        internal static void UpdateChannels(
            IReadOnlyList<BlendShapeChannel> channels)
        {
            _groups = CreateGroups(channels, out var totalCount);
            TotalCount = totalCount;
            if (!ContainsGroup(SelectedGroupName))
            {
                SelectedGroupName = null;
            }

            Changed?.Invoke();
        }

        internal static void SelectGroup(string groupName)
        {
            var next = ContainsGroup(groupName) ? groupName : null;
            if (string.Equals(
                    SelectedGroupName,
                    next,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SelectedGroupName = next;
            Changed?.Invoke();
        }

        internal static IReadOnlyList<BlendShapeChannel> Filter(
            IReadOnlyList<BlendShapeChannel> channels)
        {
            if (string.IsNullOrEmpty(SelectedGroupName))
            {
                return channels ?? Array.Empty<BlendShapeChannel>();
            }

            var filtered = new List<BlendShapeChannel>();
            var include = false;
            for (var index = 0; index < (channels?.Count ?? 0); index++)
            {
                var channel = channels[index];
                if (channel.IsHeader)
                {
                    include = string.Equals(
                        channel.HeaderText,
                        SelectedGroupName,
                        StringComparison.OrdinalIgnoreCase);
                }

                if (include)
                {
                    filtered.Add(channel);
                }
            }

            return filtered;
        }

        private static IReadOnlyList<BlendShapeGroup> CreateGroups(
            IReadOnlyList<BlendShapeChannel> channels,
            out int totalCount)
        {
            totalCount = 0;
            var groups = new List<BlendShapeGroup>();
            var groupsByName = new Dictionary<string, BlendShapeGroup>(
                StringComparer.OrdinalIgnoreCase);
            BlendShapeGroup current = null;
            for (var index = 0; index < (channels?.Count ?? 0); index++)
            {
                var channel = channels[index];
                if (channel.IsHeader)
                {
                    if (!groupsByName.TryGetValue(
                            channel.HeaderText,
                            out current))
                    {
                        current = new BlendShapeGroup(channel.HeaderText);
                        groupsByName.Add(current.Name, current);
                        groups.Add(current);
                    }

                    continue;
                }

                totalCount++;
                current?.AddShape();
            }

            return groups;
        }

        private static bool ContainsGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                return false;
            }

            for (var index = 0; index < _groups.Count; index++)
            {
                if (string.Equals(
                        _groups[index].Name,
                        groupName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
