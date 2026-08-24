using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal static class VrchatAvatarDescriptorAdapter
    {
        private const int FxLayerType = 4;
        private const string DescriptorTypeName =
            "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";

        internal static bool TryGet(
            GameObject avatar,
            out Component descriptor,
            out RuntimeAnimatorController fxController)
        {
            descriptor = FindDescriptor(avatar);
            fxController = null;
            if (descriptor == null)
            {
                return false;
            }

            var serialized = new SerializedObject(descriptor);
            var fxLayer = FindFxLayer(serialized.FindProperty("baseAnimationLayers"));
            if (fxLayer == null)
            {
                return false;
            }

            fxController = fxLayer.FindPropertyRelative("animatorController")
                ?.objectReferenceValue as RuntimeAnimatorController;
            return true;
        }

        internal static FaceExpressionAvatarBindings ReadBindings(
            Component descriptor,
            GameObject avatar,
            IReadOnlyList<string> separators,
            BlendShapeNamingRule namingRule)
        {
            var serialized = new SerializedObject(descriptor);
            var eyeSettings = serialized.FindProperty("customEyeLookSettings");
            var blinkRenderer = eyeSettings
                ?.FindPropertyRelative("eyelidsSkinnedMesh")
                ?.objectReferenceValue as SkinnedMeshRenderer;
            var blinkIndices = eyeSettings?.FindPropertyRelative("eyelidsBlendshapes");
            var blink = ReadEyelidBinding(
                avatar,
                blinkRenderer,
                blinkIndices,
                0);
            var eyes = new List<EditorCurveBinding>();
            AddEyelidBinding(eyes, avatar, blinkRenderer, blinkIndices, 1);
            AddEyelidBinding(eyes, avatar, blinkRenderer, blinkIndices, 2);
            AddBlinkGroupBindings(
                eyes,
                avatar,
                blinkRenderer,
                blinkIndices,
                separators);
            var blinkKeys = new HashSet<string>(
                blink.Select(BindingKey),
                StringComparer.Ordinal);
            eyes = eyes
                .Where(binding => !blinkKeys.Contains(BindingKey(binding)))
                .GroupBy(BindingKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            var mouthRenderer = serialized.FindProperty("VisemeSkinnedMesh")
                ?.objectReferenceValue as SkinnedMeshRenderer;
            var mouth = new List<EditorCurveBinding>();
            AddNamedBindings(
                mouth,
                avatar,
                mouthRenderer,
                serialized.FindProperty("VisemeBlendShapes"));
            AddBinding(
                mouth,
                avatar,
                mouthRenderer,
                serialized.FindProperty("MouthOpenBlendShapeName")?.stringValue);
            var mouthMorph = ReadPresetMouthMorphBindings(
                avatar,
                namingRule);
            return new FaceExpressionAvatarBindings(
                blink,
                eyes,
                mouth,
                mouthMorph);
        }

        private static IReadOnlyList<EditorCurveBinding> ReadPresetMouthMorphBindings(
            GameObject avatar,
            BlendShapeNamingRule namingRule)
        {
            if (avatar == null || namingRule == null)
            {
                return Array.Empty<EditorCurveBinding>();
            }

            var result = new List<EditorCurveBinding>();
            foreach (var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        mesh,
                        out var assetGuid,
                        out long meshLocalId))
                {
                    continue;
                }

                for (var index = 0; index < mesh.blendShapeCount; index++)
                {
                    var shapeName = mesh.GetBlendShapeName(index);
                    if (namingRule.IsMouthMorph(
                            assetGuid,
                            meshLocalId,
                            shapeName))
                    {
                        AddBinding(result, avatar, renderer, shapeName);
                    }
                }
            }

            return result
                .GroupBy(BindingKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
        }

        internal static bool TryReadMouthMorphSuggestion(
            GameObject avatar,
            IReadOnlyList<string> separators,
            out Mesh mesh,
            out IReadOnlyList<string> shapeNames)
        {
            mesh = null;
            shapeNames = Array.Empty<string>();
            var descriptor = FindDescriptor(avatar);
            if (descriptor == null)
            {
                return false;
            }

            var serialized = new SerializedObject(descriptor);
            var renderer = serialized.FindProperty("VisemeSkinnedMesh")
                ?.objectReferenceValue as SkinnedMeshRenderer;
            mesh = renderer == null ? null : renderer.sharedMesh;
            if (mesh == null)
            {
                return false;
            }

            var mouthNames = new List<string>();
            AddNames(
                mouthNames,
                serialized.FindProperty("VisemeBlendShapes"));
            var mouthOpen = serialized.FindProperty("MouthOpenBlendShapeName")
                ?.stringValue;
            if (!string.IsNullOrWhiteSpace(mouthOpen))
            {
                mouthNames.Add(mouthOpen);
            }

            shapeNames = FindMouthMorphNames(
                mesh,
                mouthNames,
                separators);
            return true;
        }

        internal static IReadOnlyList<string> FindMouthMorphNames(
            Mesh mesh,
            IEnumerable<string> mouthNames,
            IReadOnlyList<string> separators)
        {
            if (mesh == null)
            {
                return Array.Empty<string>();
            }

            var excludedNames = new HashSet<string>(
                mouthNames ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var groupRanges = new HashSet<(int Start, int End)>();
            foreach (var mouthName in excludedNames)
            {
                var mouthIndex = mesh.GetBlendShapeIndex(mouthName);
                var headerIndex = FindPreviousHeader(
                    mesh,
                    mouthIndex,
                    separators);
                if (headerIndex < 0)
                {
                    continue;
                }

                groupRanges.Add((
                    headerIndex + 1,
                    FindNextHeader(mesh, mouthIndex + 1, separators)));
            }

            var result = new List<string>();
            foreach (var range in groupRanges.OrderBy(range => range.Start))
            {
                for (var index = range.Start; index < range.End; index++)
                {
                    var shapeName = mesh.GetBlendShapeName(index);
                    if (!excludedNames.Contains(shapeName))
                    {
                        result.Add(shapeName);
                    }
                }
            }

            return result
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static int FindPreviousHeader(
            Mesh mesh,
            int startIndex,
            IReadOnlyList<string> separators)
        {
            for (var index = startIndex - 1; index >= 0; index--)
            {
                if (FaceExpressionClipEditor.TryGetHeader(
                        mesh.GetBlendShapeName(index),
                        separators,
                        out _))
                {
                    return index;
                }
            }

            return -1;
        }

        private static int FindNextHeader(
            Mesh mesh,
            int startIndex,
            IReadOnlyList<string> separators)
        {
            for (var index = startIndex; index < mesh.blendShapeCount; index++)
            {
                if (FaceExpressionClipEditor.TryGetHeader(
                        mesh.GetBlendShapeName(index),
                        separators,
                        out _))
                {
                    return index;
                }
            }

            return mesh.blendShapeCount;
        }

        private static Component FindDescriptor(GameObject avatar)
        {
            return avatar == null
                ? null
                : avatar.GetComponentsInChildren<Component>(true)
                    .FirstOrDefault(component =>
                        component != null && component.GetType().FullName == DescriptorTypeName);
        }

        private static SerializedProperty FindFxLayer(SerializedProperty layers)
        {
            if (layers == null || !layers.isArray)
            {
                return null;
            }

            for (var index = 0; index < layers.arraySize; index++)
            {
                var layer = layers.GetArrayElementAtIndex(index);
                if (layer.FindPropertyRelative("type")?.intValue == FxLayerType)
                {
                    return layer;
                }
            }

            return null;
        }

        private static IReadOnlyList<EditorCurveBinding> ReadEyelidBinding(
            GameObject avatar,
            SkinnedMeshRenderer renderer,
            SerializedProperty indices,
            int slot)
        {
            var result = new List<EditorCurveBinding>();
            AddEyelidBinding(result, avatar, renderer, indices, slot);
            return result;
        }

        private static void AddEyelidBinding(
            ICollection<EditorCurveBinding> result,
            GameObject avatar,
            SkinnedMeshRenderer renderer,
            SerializedProperty indices,
            int slot)
        {
            var mesh = renderer == null ? null : renderer.sharedMesh;
            if (mesh == null || indices == null || !indices.isArray ||
                slot < 0 || slot >= indices.arraySize)
            {
                return;
            }

            // VRChat stores Blink, Looking Up, and Looking Down in this order.
            var shapeIndex = indices.GetArrayElementAtIndex(slot).intValue;
            if (shapeIndex >= 0 && shapeIndex < mesh.blendShapeCount)
            {
                AddBinding(result, avatar, renderer, mesh.GetBlendShapeName(shapeIndex));
            }
        }

        private static void AddBlinkGroupBindings(
            ICollection<EditorCurveBinding> result,
            GameObject avatar,
            SkinnedMeshRenderer renderer,
            SerializedProperty indices,
            IReadOnlyList<string> separators)
        {
            var mesh = renderer == null ? null : renderer.sharedMesh;
            if (mesh == null || indices == null || !indices.isArray ||
                indices.arraySize == 0)
            {
                return;
            }

            var blinkIndex = indices.GetArrayElementAtIndex(0).intValue;
            if (blinkIndex < 0 || blinkIndex >= mesh.blendShapeCount)
            {
                return;
            }

            var headerIndex = -1;
            for (var index = 0; index < blinkIndex; index++)
            {
                if (FaceExpressionClipEditor.TryGetHeader(
                        mesh.GetBlendShapeName(index),
                        separators,
                        out _))
                {
                    headerIndex = index;
                }
            }

            if (headerIndex < 0)
            {
                return;
            }

            for (var index = headerIndex + 1; index < mesh.blendShapeCount; index++)
            {
                var shapeName = mesh.GetBlendShapeName(index);
                if (FaceExpressionClipEditor.TryGetHeader(
                        shapeName,
                        separators,
                        out _))
                {
                    break;
                }

                AddBinding(result, avatar, renderer, shapeName);
            }
        }

        private static void AddNamedBindings(
            ICollection<EditorCurveBinding> result,
            GameObject avatar,
            SkinnedMeshRenderer renderer,
            SerializedProperty names)
        {
            if (renderer == null || names == null || !names.isArray)
            {
                return;
            }

            for (var index = 0; index < names.arraySize; index++)
            {
                AddBinding(
                    result,
                    avatar,
                    renderer,
                    names.GetArrayElementAtIndex(index).stringValue);
            }
        }

        private static void AddNames(
            ICollection<string> result,
            SerializedProperty names)
        {
            if (names == null || !names.isArray)
            {
                return;
            }

            for (var index = 0; index < names.arraySize; index++)
            {
                var name = names.GetArrayElementAtIndex(index).stringValue;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    result.Add(name);
                }
            }
        }

        private static void AddBinding(
            ICollection<EditorCurveBinding> result,
            GameObject avatar,
            SkinnedMeshRenderer renderer,
            string shapeName)
        {
            if (avatar == null || renderer == null || string.IsNullOrWhiteSpace(shapeName))
            {
                return;
            }

            result.Add(EditorCurveBinding.FloatCurve(
                AnimationUtility.CalculateTransformPath(renderer.transform, avatar.transform),
                typeof(SkinnedMeshRenderer),
                "blendShape." + shapeName));
        }

        private static string BindingKey(EditorCurveBinding binding)
        {
            return binding.path + "\n" + binding.propertyName;
        }
    }
}
