using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class BlendShapeChannel
    {
        private float _value;

        public BlendShapeChannel(
            string rendererPath,
            string name,
            float value,
            bool animated,
            string headerText = null,
            string rendererDisplayName = null,
            string sourceAssetGuid = null,
            long sourceMeshLocalId = 0L,
            float? initialValue = null)
        {
            RendererPath = rendererPath ?? string.Empty;
            Name = name ?? string.Empty;
            Value = value;
            InitialValue = Mathf.Clamp(initialValue ?? value, 0f, 100f);
            Animated = animated;
            HeaderText = headerText;
            RendererDisplayName = rendererDisplayName;
            SourceAssetGuid = sourceAssetGuid ?? string.Empty;
            SourceMeshLocalId = sourceMeshLocalId;
        }

        public string RendererPath { get; }
        public string Name { get; }
        public float Value
        {
            get => _value;
            set => _value = Mathf.Clamp(value, 0f, 100f);
        }
        public bool Animated { get; set; }
        public float InitialValue { get; }
        public string HeaderText { get; }
        public string RendererDisplayName { get; }
        public string SourceAssetGuid { get; }
        public long SourceMeshLocalId { get; }
        public bool IsHeader => !string.IsNullOrEmpty(HeaderText);
        public string DisplayName => string.IsNullOrEmpty(RendererDisplayName)
            ? Name
            : RendererDisplayName + " / " + Name;
        public string DisplayHeaderText => string.IsNullOrEmpty(RendererDisplayName)
            ? HeaderText
            : RendererDisplayName + " / " + HeaderText;

        public EditorCurveBinding Binding => EditorCurveBinding.FloatCurve(
            RendererPath,
            typeof(SkinnedMeshRenderer),
            "blendShape." + Name);
    }

    internal static class FaceExpressionClipEditor
    {
        public static IReadOnlyList<BlendShapeChannel> Read(
            GameObject avatar,
            AnimationClip clip,
            IReadOnlyList<string> separators,
            IReadOnlyList<string> rendererPaths = null)
        {
            if (avatar == null)
            {
                return Array.Empty<BlendShapeChannel>();
            }

            var channels = new List<BlendShapeChannel>();
            var renderers = ResolveRenderers(avatar, rendererPaths);
            for (var rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                var path = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    avatar.transform);
                var mesh = renderer.sharedMesh;
                var sourceAssetGuid = string.Empty;
                var sourceMeshLocalId = 0L;
                var meshAssetPath = AssetDatabase.GetAssetPath(mesh);
                if (string.Equals(
                        System.IO.Path.GetExtension(meshAssetPath),
                        ".fbx",
                        StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        mesh,
                        out sourceAssetGuid,
                        out sourceMeshLocalId);
                }
                for (var index = 0; index < mesh.blendShapeCount; index++)
                {
                    var name = mesh.GetBlendShapeName(index);
                    var binding = EditorCurveBinding.FloatCurve(
                        path,
                        typeof(SkinnedMeshRenderer),
                        "blendShape." + name);
                    var curve = clip == null ? null : AnimationUtility.GetEditorCurve(clip, binding);
                    var initialValue = renderer.GetBlendShapeWeight(index);
                    TryGetHeader(name, separators, out var headerText);
                    channels.Add(new BlendShapeChannel(
                        path,
                        name,
                        curve == null ? initialValue : curve.Evaluate(0f),
                        curve != null,
                        headerText,
                        renderers.Count > 1 ? renderer.name : null,
                        sourceAssetGuid,
                        sourceMeshLocalId,
                        initialValue));
                }
            }

            return channels;
        }

        private static IReadOnlyList<SkinnedMeshRenderer> ResolveRenderers(
            GameObject avatar,
            IReadOnlyList<string> rendererPaths)
        {
            if (rendererPaths == null)
            {
                var body = FindBodyRenderer(avatar);
                return body == null
                    ? Array.Empty<SkinnedMeshRenderer>()
                    : new[] { body };
            }

            var result = new List<SkinnedMeshRenderer>();
            for (var index = 0; index < rendererPaths.Count; index++)
            {
                var path = rendererPaths[index] ?? string.Empty;
                var target = string.IsNullOrEmpty(path)
                    ? avatar.transform
                    : avatar.transform.Find(path);
                var renderer = target == null
                    ? null
                    : target.GetComponent<SkinnedMeshRenderer>();
                if (renderer?.sharedMesh != null && !result.Contains(renderer))
                {
                    result.Add(renderer);
                }
            }

            return result;
        }

        public static void Write(AnimationClip clip, BlendShapeChannel channel)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            if (channel.IsHeader)
            {
                return;
            }

            Undo.RecordObject(clip, "Edit Face Expression");
            AnimationUtility.SetEditorCurve(
                clip,
                channel.Binding,
                channel.Animated
                    ? new AnimationCurve(new Keyframe(0f, channel.Value))
                    : null);
            clip.frameRate = 60f;
            EditorUtility.SetDirty(clip);
        }

        public static AnimationClip Create(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var clip = new AnimationClip
            {
                frameRate = 60f,
                name = System.IO.Path.GetFileNameWithoutExtension(assetPath)
            };
            AssetDatabase.CreateAsset(clip, assetPath);
            Undo.RegisterCreatedObjectUndo(clip, "Create Face Expression");
            AssetDatabase.SaveAssets();
            return clip;
        }

        internal static bool TryGetHeader(
            string shapeName,
            IReadOnlyList<string> separators,
            out string headerText)
        {
            headerText = null;
            if (separators == null)
            {
                return false;
            }

            for (var index = 0; index < separators.Count; index++)
            {
                if (TryGetHeader(
                        shapeName,
                        separators[index],
                        out headerText))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetHeader(
            string shapeName,
            string separator,
            out string headerText)
        {
            headerText = null;
            var name = (shapeName ?? string.Empty).Trim();
            var marker = (separator ?? string.Empty).Trim();
            if (name.Length == 0 || marker.Length == 0)
            {
                return false;
            }

            string candidate;
            if (marker.All(character => character == marker[0]))
            {
                if (CountLeading(name, marker[0]) < marker.Length ||
                    CountTrailing(name, marker[0]) < marker.Length)
                {
                    return false;
                }

                candidate = name.Trim(marker[0]).Trim();
            }
            else
            {
                if (!name.StartsWith(marker, StringComparison.Ordinal) ||
                    !name.EndsWith(marker, StringComparison.Ordinal) ||
                    name.Length <= marker.Length * 2)
                {
                    return false;
                }

                candidate = name.Substring(
                        marker.Length,
                        name.Length - marker.Length * 2)
                    .Trim();
            }

            if (candidate.Length == 0)
            {
                return false;
            }

            headerText = candidate;
            return true;
        }

        private static int CountLeading(string value, char marker)
        {
            var count = 0;
            while (count < value.Length && value[count] == marker)
            {
                count++;
            }

            return count;
        }

        private static int CountTrailing(string value, char marker)
        {
            var count = 0;
            while (count < value.Length && value[value.Length - count - 1] == marker)
            {
                count++;
            }

            return count;
        }

        internal static SkinnedMeshRenderer FindBodyRenderer(GameObject avatar)
        {
            return avatar == null
                ? null
                : avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(renderer =>
                        renderer.sharedMesh != null &&
                        string.Equals(
                            renderer.name,
                            "Body",
                            StringComparison.OrdinalIgnoreCase));
        }

        internal static IReadOnlyList<string> GetRendererPaths(GameObject avatar)
        {
            if (avatar == null)
            {
                return Array.Empty<string>();
            }

            return avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.sharedMesh != null &&
                                   renderer.sharedMesh.blendShapeCount > 0)
                .Select(renderer => AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    avatar.transform))
                .ToArray();
        }
    }
}
