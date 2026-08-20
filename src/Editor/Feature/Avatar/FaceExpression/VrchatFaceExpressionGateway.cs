using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class VrchatFaceExpressionGateway
    {
        private const int FxLayerType = 4;
        private const string OutputRoot = "Assets/ee4v/FaceExpressions";
        private const string DescriptorTypeName =
            "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";

        public bool TryRead(
            GameObject avatar,
            out IReadOnlyDictionary<FaceGesture, AnimationClip> assignments)
        {
            assignments = new Dictionary<FaceGesture, AnimationClip>();
            if (!TryGetFxController(avatar, out _, out var runtimeController))
            {
                return false;
            }

            var controller = runtimeController as AnimatorController;
            if (FaceExpressionControllerWriter.OwnsLayer(controller))
            {
                assignments = FaceExpressionControllerWriter.Read(controller);
            }

            return true;
        }

        public bool TryApply(
            GameObject avatar,
            IReadOnlyDictionary<FaceGesture, AnimationClip> assignments,
            out AnimatorController controller,
            out string error)
        {
            controller = null;
            error = null;
            if (!TryGetFxController(avatar, out var descriptor, out var existing))
            {
                error = "descriptorMissing";
                return false;
            }

            try
            {
                EnsureFolder(OutputRoot);
                controller = GetEditableController(avatar, existing);
                if (controller == null)
                {
                    error = "controllerCreateFailed";
                    return false;
                }

                var outputFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(controller))
                    ?.Replace('\\', '/');
                FaceExpressionControllerWriter.Apply(
                    controller,
                    avatar,
                    assignments,
                    outputFolder);
                AssignFxController(descriptor, controller);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                error = "applyFailed";
                return false;
            }
        }

        private static AnimatorController GetEditableController(
            GameObject avatar,
            RuntimeAnimatorController existing)
        {
            var animatorController = existing as AnimatorController;
            if (FaceExpressionControllerWriter.OwnsLayer(animatorController) &&
                IsEditableAsset(animatorController))
            {
                return animatorController;
            }

            var safeName = SanitizeFileName(avatar.name);
            var destination = AssetDatabase.GenerateUniqueAssetPath(
                OutputRoot + "/" + safeName + " Face Expressions.controller");
            var sourceController = existing as AnimatorController;
            if (sourceController == null && existing is AnimatorOverrideController overrideController)
            {
                sourceController = overrideController.runtimeAnimatorController as AnimatorController;
            }

            var sourcePath = sourceController == null
                ? null
                : AssetDatabase.GetAssetPath(sourceController);
            if (!string.IsNullOrEmpty(sourcePath) && AssetDatabase.CopyAsset(sourcePath, destination))
            {
                var copied = AssetDatabase.LoadAssetAtPath<AnimatorController>(destination);
                if (copied != null)
                {
                    Undo.RegisterCreatedObjectUndo(copied, "Create Face Expression Controller");
                    return copied;
                }
            }

            var created = new AnimatorController
            {
                name = Path.GetFileNameWithoutExtension(destination)
            };
            AssetDatabase.CreateAsset(created, destination);
            Undo.RegisterCreatedObjectUndo(created, "Create Face Expression Controller");
            return created;
        }

        private static bool IsEditableAsset(AnimatorController controller)
        {
            if (controller == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(controller);
            return path.StartsWith("Assets/", StringComparison.Ordinal) &&
                   AssetDatabase.IsOpenForEdit(controller);
        }

        private static bool TryGetFxController(
            GameObject avatar,
            out Component descriptor,
            out RuntimeAnimatorController controller)
        {
            descriptor = FindDescriptor(avatar);
            controller = null;
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

            controller = fxLayer.FindPropertyRelative("animatorController")
                ?.objectReferenceValue as RuntimeAnimatorController;
            return true;
        }

        private static void AssignFxController(Component descriptor, AnimatorController controller)
        {
            Undo.RecordObject(descriptor, "Assign Face Expression Controller");
            var serialized = new SerializedObject(descriptor);
            var customize = serialized.FindProperty("customizeAnimationLayers");
            if (customize != null)
            {
                customize.boolValue = true;
            }

            var fxLayer = FindFxLayer(serialized.FindProperty("baseAnimationLayers"));
            if (fxLayer == null)
            {
                throw new InvalidOperationException("FX layer was not found.");
            }

            var isDefault = fxLayer.FindPropertyRelative("isDefault");
            if (isDefault != null)
            {
                isDefault.boolValue = false;
            }

            var controllerProperty = fxLayer.FindPropertyRelative("animatorController");
            if (controllerProperty == null)
            {
                throw new InvalidOperationException("FX controller property was not found.");
            }

            controllerProperty.objectReferenceValue = controller;
            serialized.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(descriptor);
            EditorUtility.SetDirty(descriptor);
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
                var type = layer.FindPropertyRelative("type");
                if (type != null && type.intValue == FxLayerType)
                {
                    return layer;
                }
            }

            return null;
        }

        private static Component FindDescriptor(GameObject avatar)
        {
            if (avatar == null)
            {
                return null;
            }

            return avatar.GetComponentsInChildren<Component>(true)
                .FirstOrDefault(component =>
                    component != null && component.GetType().FullName == DescriptorTypeName);
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string((value ?? "Avatar")
                .Select(character => invalid.Contains(character) ? '_' : character)
                .ToArray());
        }
    }
}
