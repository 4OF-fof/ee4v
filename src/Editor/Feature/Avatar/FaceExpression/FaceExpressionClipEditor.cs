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
            string headerText = null)
        {
            RendererPath = rendererPath ?? string.Empty;
            Name = name ?? string.Empty;
            Value = value;
            Animated = animated;
            HeaderText = headerText;
        }

        public string RendererPath { get; }
        public string Name { get; }
        public float Value
        {
            get => _value;
            set => _value = Mathf.Clamp(value, 0f, 100f);
        }
        public bool Animated { get; set; }
        public string HeaderText { get; }
        public bool IsHeader => !string.IsNullOrEmpty(HeaderText);

        public EditorCurveBinding Binding => EditorCurveBinding.FloatCurve(
            RendererPath,
            typeof(SkinnedMeshRenderer),
            "blendShape." + Name);
    }

    internal static class FaceExpressionClipEditor
    {
        private const float ClipLength = 1f / 60f;

        public static IReadOnlyList<BlendShapeChannel> Read(
            GameObject avatar,
            AnimationClip clip,
            IReadOnlyList<string> separators)
        {
            if (avatar == null)
            {
                return Array.Empty<BlendShapeChannel>();
            }

            var channels = new List<BlendShapeChannel>();
            var renderer = FindBodyRenderer(avatar);
            if (renderer == null)
            {
                return channels;
            }

            var path = AnimationUtility.CalculateTransformPath(
                renderer.transform,
                avatar.transform);
            var mesh = renderer.sharedMesh;
            for (var index = 0; index < mesh.blendShapeCount; index++)
            {
                var name = mesh.GetBlendShapeName(index);
                var binding = EditorCurveBinding.FloatCurve(
                    path,
                    typeof(SkinnedMeshRenderer),
                    "blendShape." + name);
                var curve = clip == null ? null : AnimationUtility.GetEditorCurve(clip, binding);
                TryGetHeader(name, separators, out var headerText);
                channels.Add(new BlendShapeChannel(
                    path,
                    name,
                    curve == null ? renderer.GetBlendShapeWeight(index) : curve.Evaluate(0f),
                    curve != null,
                    headerText));
            }

            return channels;
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
                    ? AnimationCurve.Constant(
                        0f,
                        ClipLength,
                        channel.Value)
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

        private static bool TryGetHeader(
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
    }
}
