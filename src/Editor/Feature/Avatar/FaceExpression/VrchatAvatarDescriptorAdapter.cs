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
            GameObject avatar)
        {
            var serialized = new SerializedObject(descriptor);
            var eyeSettings = serialized.FindProperty("customEyeLookSettings");
            var blinkRenderer = eyeSettings
                ?.FindPropertyRelative("eyelidsSkinnedMesh")
                ?.objectReferenceValue as SkinnedMeshRenderer;
            var blinkIndices = eyeSettings?.FindPropertyRelative("eyelidsBlendshapes");
            var blink = ReadBlinkBinding(avatar, blinkRenderer, blinkIndices);

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
            return new FaceExpressionAvatarBindings(blink, mouth);
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

        private static IReadOnlyList<EditorCurveBinding> ReadBlinkBinding(
            GameObject avatar,
            SkinnedMeshRenderer renderer,
            SerializedProperty indices)
        {
            var result = new List<EditorCurveBinding>();
            var mesh = renderer == null ? null : renderer.sharedMesh;
            if (mesh == null || indices == null || !indices.isArray || indices.arraySize == 0)
            {
                return result;
            }

            // VRChat stores Blink, Looking Up, and Looking Down in this order.
            var shapeIndex = indices.GetArrayElementAtIndex(0).intValue;
            if (shapeIndex >= 0 && shapeIndex < mesh.blendShapeCount)
            {
                AddBinding(result, avatar, renderer, mesh.GetBlendShapeName(shapeIndex));
            }

            return result;
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
    }
}
