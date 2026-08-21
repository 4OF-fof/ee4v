using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private const string GeneratedRootName = "ee4v Face Expressions";
        private const string MergeAnimatorTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator";
        private const string MenuInstallerTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller";
        private const string MenuItemTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMenuItem";
        private const string ParametersTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarParameters";

        public bool TryRead(
            GameObject avatar,
            out FaceExpressionConfiguration configuration)
        {
            configuration = new FaceExpressionConfiguration(null, null);
            if (!TryGetFxController(avatar, out _, out var existingController))
            {
                return false;
            }

            var controller = TryGetGeneratedController(avatar) ??
                             existingController as AnimatorController;
            if (GestureMatrixControllerWriter.OwnsLayer(controller))
            {
                configuration = new FaceExpressionConfiguration(
                    GestureMatrixControllerWriter.Read(controller),
                    GestureMatrixControllerWriter.ReadMenuEntries(controller));
            }

            return true;
        }

        public bool TryApply(
            GameObject avatar,
            FaceExpressionConfiguration configuration,
            out AnimatorController controller,
            out string error)
        {
            controller = null;
            error = null;
            if (!TryGetFxController(avatar, out var descriptor, out _))
            {
                error = "descriptorMissing";
                return false;
            }

            try
            {
                if (!TryGetModularAvatarTypes(out var modularAvatarTypes))
                {
                    error = "modularAvatarMissing";
                    return false;
                }

                EnsureFolder(OutputRoot);
                var generatedRoot = GetOrCreateGeneratedRoot(avatar);
                controller = GetGeneratedController(generatedRoot, avatar, modularAvatarTypes.MergeAnimator);
                if (controller == null)
                {
                    error = "controllerCreateFailed";
                    return false;
                }

                var outputFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(controller))
                    ?.Replace('\\', '/');
                GestureMatrixControllerWriter.Apply(
                    controller,
                    avatar,
                    configuration?.Assignments,
                    configuration?.MenuEntries,
                    ReadAvatarBindings(descriptor, avatar),
                    outputFolder);
                ConfigureModularAvatar(
                    generatedRoot,
                    controller,
                    configuration ?? new FaceExpressionConfiguration(null, null),
                    modularAvatarTypes);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                error = "applyFailed";
                return false;
            }
        }

        private static AnimatorController TryGetGeneratedController(GameObject avatar)
        {
            var root = avatar == null ? null : avatar.transform.Find(GeneratedRootName);
            var mergeType = FindType(MergeAnimatorTypeName);
            if (root == null || mergeType == null)
            {
                return null;
            }

            var merge = root.GetComponent(mergeType);
            return merge == null
                ? null
                : GetField(merge, "animator") as AnimatorController;
        }

        private static GameObject GetOrCreateGeneratedRoot(GameObject avatar)
        {
            var existing = avatar.transform.Find(GeneratedRootName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var root = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Face Expression Modular Avatar");
            Undo.SetTransformParent(root.transform, avatar.transform, "Attach Face Expression Modular Avatar");
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static AnimatorController GetGeneratedController(
            GameObject generatedRoot,
            GameObject avatar,
            Type mergeAnimatorType)
        {
            var merge = generatedRoot.GetComponent(mergeAnimatorType);
            var existing = merge == null ? null : GetField(merge, "animator") as AnimatorController;
            if (existing != null && IsEditableAsset(existing))
            {
                return existing;
            }

            var safeName = SanitizeFileName(avatar.name);
            var destination = AssetDatabase.GenerateUniqueAssetPath(
                OutputRoot + "/" + safeName + " Face Expressions.controller");
            var controller = new AnimatorController
            {
                name = Path.GetFileNameWithoutExtension(destination)
            };
            AssetDatabase.CreateAsset(controller, destination);
            Undo.RegisterCreatedObjectUndo(controller, "Create Face Expression Controller");
            return controller;
        }

        private static void ConfigureModularAvatar(
            GameObject root,
            AnimatorController controller,
            FaceExpressionConfiguration configuration,
            ModularAvatarTypes types)
        {
            var merge = GetOrAddComponent(root, types.MergeAnimator);
            SetField(merge, "animator", controller);
            SetEnumField(merge, "layerType", "FX");
            SetEnumField(merge, "pathMode", "Absolute");
            SetEnumField(merge, "mergeAnimatorMode", "Append");
            SetField(merge, "matchAvatarWriteDefaults", false);

            var parameters = GetOrAddComponent(root, types.Parameters);
            ConfigureParameter(parameters);
            GetOrAddComponent(root, types.MenuInstaller);
            var rootMenuItem = GetOrAddComponent(root, types.MenuItem);
            ConfigureMenuItem(
                rootMenuItem,
                "ee4v Expressions",
                "SubMenu",
                string.Empty,
                0f);
            SetEnumField(rootMenuItem, "MenuSource", "Children");

            for (var index = root.transform.childCount - 1; index >= 0; index--)
            {
                var child = root.transform.GetChild(index);
                if (child.name.StartsWith("ee4v Menu ", StringComparison.Ordinal))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            CreateMenuItem(root, types.MenuItem, "ee4v Menu 000", "Gesture Assignments", 0f);
            var entries = GestureMatrixControllerWriter.GetEffectiveMenuEntries(
                configuration.Assignments,
                configuration.MenuEntries);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                CreateMenuItem(
                    root,
                    types.MenuItem,
                    "ee4v Menu " + (index + 1).ToString("000"),
                    entry.Name,
                    index + 1);
            }

            EditorUtility.SetDirty(root);
        }

        private static void ConfigureParameter(Component parameters)
        {
            var field = parameters.GetType().GetField("parameters");
            if (field == null)
            {
                throw new InvalidOperationException("Modular Avatar parameters field was not found.");
            }

            var list = (IList)(field.GetValue(parameters) ?? Activator.CreateInstance(field.FieldType));
            for (var index = list.Count - 1; index >= 0; index--)
            {
                if (string.Equals(
                        GetField(list[index], "nameOrPrefix") as string,
                        GestureMatrixControllerWriter.MenuParameter,
                        StringComparison.Ordinal))
                {
                    list.RemoveAt(index);
                }
            }

            var configType = field.FieldType.GetGenericArguments()[0];
            var config = Activator.CreateInstance(configType);
            SetField(config, "nameOrPrefix", GestureMatrixControllerWriter.MenuParameter);
            SetField(config, "internalParameter", true);
            SetField(config, "isPrefix", false);
            SetEnumField(config, "syncType", "Int");
            SetField(config, "localOnly", false);
            SetField(config, "defaultValue", 0f);
            SetField(config, "saved", true);
            SetField(config, "hasExplicitDefaultValue", true);
            list.Add(config);
            field.SetValue(parameters, list);
            EditorUtility.SetDirty(parameters);
        }

        private static void CreateMenuItem(
            GameObject root,
            Type menuItemType,
            string objectName,
            string label,
            float value)
        {
            var child = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(child, "Create Face Expression Menu Item");
            Undo.SetTransformParent(child.transform, root.transform, "Attach Face Expression Menu Item");
            var item = Undo.AddComponent(child, menuItemType);
            ConfigureMenuItem(
                item,
                label,
                "Toggle",
                GestureMatrixControllerWriter.MenuParameter,
                value);
        }

        private static void ConfigureMenuItem(
            Component item,
            string label,
            string controlType,
            string parameter,
            float value)
        {
            SetField(item, "label", label);
            SetField(item, "isSynced", true);
            SetField(item, "isSaved", true);
            SetField(
                item,
                "isDefault",
                controlType == "Toggle" && value == 0f);
            SetField(item, "automaticValue", false);

            var portable = item.GetType().GetProperty("PortableControl")?.GetValue(item);
            if (portable == null)
            {
                throw new InvalidOperationException("Modular Avatar portable menu control was not found.");
            }

            SetEnumProperty(portable, "Type", controlType);
            SetProperty(portable, "Parameter", parameter);
            SetProperty(portable, "Value", value);
            EditorUtility.SetDirty(item);
        }

        private static Component GetOrAddComponent(GameObject target, Type type)
        {
            return target.GetComponent(type) ?? Undo.AddComponent(target, type);
        }

        private static bool TryGetModularAvatarTypes(out ModularAvatarTypes types)
        {
            types = new ModularAvatarTypes(
                FindType(MergeAnimatorTypeName),
                FindType(MenuInstallerTypeName),
                FindType(MenuItemTypeName),
                FindType(ParametersTypeName));
            return types.IsComplete;
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static object GetField(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException(target.GetType().FullName + "." + name + " was not found.");
            }

            field.SetValue(target, value);
        }

        private static void SetEnumField(object target, string name, string value)
        {
            var field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException(target.GetType().FullName + "." + name + " was not found.");
            }

            field.SetValue(target, Enum.Parse(field.FieldType, value));
        }

        private static void SetProperty(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException(target.GetType().FullName + "." + name + " was not found.");
            }

            property.SetValue(target, value);
        }

        private static void SetEnumProperty(object target, string name, string value)
        {
            var property = target.GetType().GetProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException(target.GetType().FullName + "." + name + " was not found.");
            }

            property.SetValue(target, Enum.Parse(property.PropertyType, value));
        }

        private readonly struct ModularAvatarTypes
        {
            public ModularAvatarTypes(
                Type mergeAnimator,
                Type menuInstaller,
                Type menuItem,
                Type parameters)
            {
                MergeAnimator = mergeAnimator;
                MenuInstaller = menuInstaller;
                MenuItem = menuItem;
                Parameters = parameters;
            }

            public Type MergeAnimator { get; }
            public Type MenuInstaller { get; }
            public Type MenuItem { get; }
            public Type Parameters { get; }
            public bool IsComplete => MergeAnimator != null && MenuInstaller != null &&
                                      MenuItem != null && Parameters != null;
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

        private static FaceExpressionAvatarBindings ReadAvatarBindings(
            Component descriptor,
            GameObject avatar)
        {
            var serialized = new SerializedObject(descriptor);
            var eyeSettings = serialized.FindProperty("customEyeLookSettings");
            var blinkRenderer = eyeSettings
                ?.FindPropertyRelative("eyelidsSkinnedMesh")
                ?.objectReferenceValue as SkinnedMeshRenderer;
            var blinkIndices = eyeSettings?.FindPropertyRelative("eyelidsBlendshapes");
            var blink = ReadIndexedBlendShapes(avatar, blinkRenderer, blinkIndices);

            var mouthRenderer = serialized.FindProperty("VisemeSkinnedMesh")
                ?.objectReferenceValue as SkinnedMeshRenderer;
            var mouthNames = serialized.FindProperty("VisemeBlendShapes");
            var mouth = new List<EditorCurveBinding>(
                ReadNamedBlendShapes(avatar, mouthRenderer, mouthNames));
            AddBlendShapeBinding(
                mouth,
                avatar,
                mouthRenderer,
                serialized.FindProperty("MouthOpenBlendShapeName")?.stringValue);
            return new FaceExpressionAvatarBindings(blink, mouth);
        }

        private static IReadOnlyList<EditorCurveBinding> ReadIndexedBlendShapes(
            GameObject avatar,
            SkinnedMeshRenderer renderer,
            SerializedProperty indices)
        {
            var result = new List<EditorCurveBinding>();
            var mesh = renderer == null ? null : renderer.sharedMesh;
            if (mesh == null || indices == null || !indices.isArray)
            {
                return result;
            }

            // VRChat stores Blink, Looking Up, and Looking Down in this order.
            // Only Blink belongs to the per-expression blinking switch.
            for (var index = 0; index < Mathf.Min(1, indices.arraySize); index++)
            {
                var shapeIndex = indices.GetArrayElementAtIndex(index).intValue;
                if (shapeIndex >= 0 && shapeIndex < mesh.blendShapeCount)
                {
                    AddBlendShapeBinding(
                        result,
                        avatar,
                        renderer,
                        mesh.GetBlendShapeName(shapeIndex));
                }
            }

            return result;
        }

        private static IReadOnlyList<EditorCurveBinding> ReadNamedBlendShapes(
            GameObject avatar,
            SkinnedMeshRenderer renderer,
            SerializedProperty names)
        {
            var result = new List<EditorCurveBinding>();
            if (renderer == null || names == null || !names.isArray)
            {
                return result;
            }

            for (var index = 0; index < names.arraySize; index++)
            {
                AddBlendShapeBinding(
                    result,
                    avatar,
                    renderer,
                    names.GetArrayElementAtIndex(index).stringValue);
            }

            return result;
        }

        private static void AddBlendShapeBinding(
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
                AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    avatar.transform),
                typeof(SkinnedMeshRenderer),
                "blendShape." + shapeName));
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
