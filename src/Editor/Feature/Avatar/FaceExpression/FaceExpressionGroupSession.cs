using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class BlendShapeGroup
    {
        internal BlendShapeGroup(string name, string rendererPath = null)
        {
            Name = name;
            RendererPath = rendererPath;
        }

        internal string Name { get; }
        internal string RendererPath { get; }
        internal string Key => string.IsNullOrEmpty(RendererPath)
            ? "header:" + Name
            : "renderer:" + RendererPath;

        internal int Count { get; private set; }

        internal void AddShape()
        {
            Count++;
        }
    }

    internal sealed class FaceMeshOption
    {
        internal FaceMeshOption(string path, string displayName, bool isBody)
        {
            Path = path;
            DisplayName = displayName;
            IsBody = isBody;
        }

        internal string Path { get; }
        internal string DisplayName { get; }
        internal bool IsBody { get; }
    }

    internal static class FaceExpressionGroupSession
    {
        private static IReadOnlyList<BlendShapeGroup> _groups =
            Array.Empty<BlendShapeGroup>();
        private static IReadOnlyList<FaceMeshOption> _availableMeshes =
            Array.Empty<FaceMeshOption>();
        private static readonly List<string> SelectedMeshPaths = new List<string>();

        internal static event Action Changed;
        internal static event Action MeshesChanged;

        internal static IReadOnlyList<BlendShapeGroup> Groups => _groups;

        internal static int TotalCount { get; private set; }

        internal static IReadOnlyList<FaceMeshOption> AvailableMeshes => _availableMeshes;

        internal static IReadOnlyList<FaceMeshOption> SelectedMeshes => _availableMeshes
            .Where(mesh => SelectedMeshPaths.Contains(mesh.Path))
            .ToArray();

        internal static IReadOnlyList<string> RendererPaths => SelectedMeshPaths;

        internal static string SelectedGroupKey { get; private set; }

        internal static string SelectedGroupName =>
            FindGroup(SelectedGroupKey)?.Name;

        internal static void SetAvatar(GameObject avatar)
        {
            var body = FaceExpressionClipEditor.FindBodyRenderer(avatar);
            _availableMeshes = avatar == null
                ? Array.Empty<FaceMeshOption>()
                : avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(renderer => renderer.sharedMesh != null &&
                                       renderer.sharedMesh.blendShapeCount > 0)
                    .Select(renderer =>
                    {
                        var path = AnimationUtility.CalculateTransformPath(
                            renderer.transform,
                            avatar.transform);
                        return new FaceMeshOption(
                            path,
                            renderer.name,
                            renderer == body);
                    })
                    .OrderByDescending(mesh => mesh.IsBody)
                    .ThenBy(mesh => mesh.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            SelectedMeshPaths.Clear();
            if (body != null)
            {
                SelectedMeshPaths.Add(AnimationUtility.CalculateTransformPath(
                    body.transform,
                    avatar.transform));
            }

            MeshesChanged?.Invoke();
        }

        internal static void AddMesh(string path)
        {
            if (_availableMeshes.All(mesh => mesh.Path != path) ||
                SelectedMeshPaths.Contains(path))
            {
                return;
            }

            SelectedMeshPaths.Add(path);
            MeshesChanged?.Invoke();
        }

        internal static void RemoveMesh(string path)
        {
            var mesh = _availableMeshes.FirstOrDefault(option => option.Path == path);
            if (mesh == null || mesh.IsBody || !SelectedMeshPaths.Remove(path))
            {
                return;
            }

            MeshesChanged?.Invoke();
        }

        internal static void UpdateChannels(
            IReadOnlyList<BlendShapeChannel> channels)
        {
            _groups = CreateGroups(channels, out var totalCount);
            TotalCount = totalCount;
            if (!ContainsGroup(SelectedGroupKey))
            {
                SelectedGroupKey = null;
            }

            Changed?.Invoke();
        }

        internal static void SelectGroup(string groupKey)
        {
            var next = ContainsGroup(groupKey) ? groupKey : null;
            if (string.Equals(
                    SelectedGroupKey,
                    next,
                    StringComparison.Ordinal))
            {
                return;
            }

            SelectedGroupKey = next;
            Changed?.Invoke();
        }

        internal static IReadOnlyList<BlendShapeChannel> Filter(
            IReadOnlyList<BlendShapeChannel> channels)
        {
            var selected = FindGroup(SelectedGroupKey);
            if (selected == null)
            {
                return channels ?? Array.Empty<BlendShapeChannel>();
            }

            if (!string.IsNullOrEmpty(selected.RendererPath))
            {
                return (channels ?? Array.Empty<BlendShapeChannel>())
                    .Where(channel => string.Equals(
                        channel.RendererPath,
                        selected.RendererPath,
                        StringComparison.Ordinal))
                    .ToArray();
            }

            var filtered = new List<BlendShapeChannel>();
            var include = false;
            string rendererPath = null;
            for (var index = 0; index < (channels?.Count ?? 0); index++)
            {
                var channel = channels[index];
                if (!string.Equals(rendererPath, channel.RendererPath, StringComparison.Ordinal))
                {
                    rendererPath = channel.RendererPath;
                    include = false;
                }

                if (channel.IsHeader)
                {
                    include = string.Equals(
                        channel.HeaderText,
                        selected.Name,
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
            var meshGroupList = SelectedMeshes
                .Where(mesh => !mesh.IsBody)
                .Select(mesh => new BlendShapeGroup(
                    mesh.DisplayName,
                    mesh.Path))
                .ToArray();
            var meshGroups = meshGroupList.ToDictionary(
                    group => group.RendererPath,
                    group => group,
                    StringComparer.Ordinal);
            groups.AddRange(meshGroupList);
            var groupsByName = new Dictionary<string, BlendShapeGroup>(
                StringComparer.OrdinalIgnoreCase);
            BlendShapeGroup current = null;
            string rendererPath = null;
            for (var index = 0; index < (channels?.Count ?? 0); index++)
            {
                var channel = channels[index];
                if (!string.Equals(rendererPath, channel.RendererPath, StringComparison.Ordinal))
                {
                    rendererPath = channel.RendererPath;
                    current = null;
                }

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
                if (meshGroups.TryGetValue(channel.RendererPath, out var meshGroup))
                {
                    meshGroup.AddShape();
                }

                current?.AddShape();
            }

            return groups;
        }

        private static BlendShapeGroup FindGroup(string groupKey)
        {
            if (string.IsNullOrEmpty(groupKey))
            {
                return null;
            }

            for (var index = 0; index < _groups.Count; index++)
            {
                if (string.Equals(
                        _groups[index].Key,
                        groupKey,
                        StringComparison.Ordinal))
                {
                    return _groups[index];
                }
            }

            return null;
        }

        private static bool ContainsGroup(string groupKey)
        {
            return FindGroup(groupKey) != null;
        }
    }
}
