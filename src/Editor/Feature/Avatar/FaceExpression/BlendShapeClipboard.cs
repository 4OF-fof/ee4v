using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal static class BlendShapeClipboard
    {
        private const string Prefix = "ee4v-blendshape-v1:";

        internal static bool Copy(
            IReadOnlyList<BlendShapeChannel> channels)
        {
            var payload = new Payload();
            for (var index = 0; index < (channels?.Count ?? 0); index++)
            {
                var channel = channels[index];
                if (channel == null ||
                    channel.IsHeader ||
                    string.IsNullOrEmpty(channel.Name) ||
                    float.IsNaN(channel.Value) ||
                    float.IsInfinity(channel.Value))
                {
                    continue;
                }

                payload.values.Add(new Value
                {
                    name = channel.Name,
                    weight = channel.Value
                });
            }

            if (payload.values.Count == 0)
            {
                return false;
            }

            EditorGUIUtility.systemCopyBuffer =
                Prefix + JsonUtility.ToJson(payload);
            return true;
        }

        internal static bool CanPaste(SkinnedMeshRenderer renderer)
        {
            return CountMatches(renderer, ReadValues()) > 0;
        }

        internal static int Paste(SkinnedMeshRenderer renderer)
        {
            var values = ReadValues();
            var matchCount = CountMatches(renderer, values);
            if (matchCount == 0)
            {
                return 0;
            }

            Undo.RecordObject(renderer, "Paste BlendShapes");
            var mesh = renderer.sharedMesh;
            for (var index = 0; index < mesh.blendShapeCount; index++)
            {
                if (values.TryGetValue(
                        mesh.GetBlendShapeName(index),
                        out var weight))
                {
                    renderer.SetBlendShapeWeight(index, weight);
                }
            }

            if (PrefabUtility.IsPartOfPrefabInstance(renderer))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    renderer);
            }

            EditorUtility.SetDirty(renderer);
            return matchCount;
        }

        private static Dictionary<string, float> ReadValues()
        {
            var text = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(text) ||
                !text.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                var payload = JsonUtility.FromJson<Payload>(
                    text.Substring(Prefix.Length));
                if (payload?.values == null)
                {
                    return null;
                }

                var result = new Dictionary<string, float>(
                    StringComparer.Ordinal);
                for (var index = 0; index < payload.values.Count; index++)
                {
                    var value = payload.values[index];
                    if (value == null ||
                        string.IsNullOrEmpty(value.name) ||
                        float.IsNaN(value.weight) ||
                        float.IsInfinity(value.weight))
                    {
                        continue;
                    }

                    result[value.name] = value.weight;
                }

                return result;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static int CountMatches(
            SkinnedMeshRenderer renderer,
            IReadOnlyDictionary<string, float> values)
        {
            var mesh = renderer == null ? null : renderer.sharedMesh;
            if (mesh == null || values == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < mesh.blendShapeCount; index++)
            {
                if (values.ContainsKey(mesh.GetBlendShapeName(index)))
                {
                    count++;
                }
            }

            return count;
        }

        [Serializable]
        private sealed class Payload
        {
            public List<Value> values = new List<Value>();
        }

        [Serializable]
        private sealed class Value
        {
            public string name;
            public float weight;
        }
    }

    internal static class BlendShapeClipboardMenu
    {
        private const string MenuPath =
            "CONTEXT/SkinnedMeshRenderer/BlendShapeとしてペースト";

        [MenuItem(MenuPath, true)]
        private static bool ValidatePaste(MenuCommand command)
        {
            return BlendShapeClipboard.CanPaste(
                command.context as SkinnedMeshRenderer);
        }

        [MenuItem(MenuPath)]
        private static void Paste(MenuCommand command)
        {
            BlendShapeClipboard.Paste(
                command.context as SkinnedMeshRenderer);
        }
    }
}
