using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal static class ModularAvatarFaceExpressionInstaller
    {
        private const string MergeAnimatorTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator";
        private const string BlinkOverrideTypeName =
            "Ee4v.FaceExpression.FaceExpressionBlinkOverride";
        private const string MenuInstallerTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller";
        private const string MenuItemTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarMenuItem";
        private const string ParametersTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarParameters";

        internal static bool IsAvailable =>
            FindType(MergeAnimatorTypeName) != null &&
            FindType(MenuInstallerTypeName) != null &&
            FindType(ParametersTypeName) != null;

        internal static AnimatorController TryGetController(
            GameObject avatar,
            string generatedRootName)
        {
            var mergeType = FindType(MergeAnimatorTypeName);
            var root = avatar == null
                ? null
                : avatar.transform.Find(generatedRootName)?.gameObject;
            var merge = root == null || mergeType == null
                ? null
                : root.GetComponent(mergeType);
            return merge == null
                ? null
                : GetField(merge, "animator") as AnimatorController;
        }

        internal static void Install(
            GameObject avatar,
            FaceExpressionGenerationPaths paths,
            AnimatorController controller,
            ScriptableObject menu,
            bool disableDescriptorBlink)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            if (!IsAvailable)
            {
                throw new InvalidOperationException("Modular Avatar was not found.");
            }

            if (EditorUtility.IsPersistent(avatar))
            {
                InstallIntoPrefabAsset(
                    avatar,
                    paths,
                    controller,
                    menu,
                    disableDescriptorBlink);
                return;
            }

            InstallIntoTransform(
                avatar.transform,
                paths,
                controller,
                menu,
                disableDescriptorBlink,
                true);
        }

        private static void InstallIntoTransform(
            Transform avatar,
            FaceExpressionGenerationPaths paths,
            AnimatorController controller,
            ScriptableObject menu,
            bool disableDescriptorBlink,
            bool recordUndo)
        {
            var root = avatar.Find(paths.RootName)?.gameObject;
            if (root == null)
            {
                root = new GameObject(paths.RootName);
                root.transform.SetParent(avatar, false);
                if (recordUndo)
                {
                    Undo.RegisterCreatedObjectUndo(
                        root,
                        "Create Face Expression Installer");
                }
            }

            if (PrefabUtility.IsOutermostPrefabInstanceRoot(root))
            {
                PrefabUtility.UnpackPrefabInstance(
                    root,
                    PrefabUnpackMode.OutermostRoot,
                    InteractionMode.AutomatedAction);
            }

            ConfigureRoot(
                root,
                paths.RootName,
                controller,
                menu,
                disableDescriptorBlink);
            var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
                root,
                paths.PrefabPath,
                InteractionMode.AutomatedAction);
            if (saved == null)
            {
                throw new InvalidOperationException(
                    "The generated face expression Prefab could not be saved and connected.");
            }
        }

        private static void ConfigureRoot(
            GameObject root,
            string rootName,
            AnimatorController controller,
            ScriptableObject menu,
            bool disableDescriptorBlink)
        {
            root.name = rootName;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            RemoveLegacyMenuObjects(root);
            RemoveComponents(root, FindType(MenuItemTypeName));

            var mergeType = FindType(MergeAnimatorTypeName);
            RemoveComponents(root, mergeType);
            var merge = AddRequiredComponent(root, mergeType);
            SetRequiredField(merge, "animator", controller);
            SetRequiredEnumField(merge, "layerType", "FX");
            SetRequiredEnumField(merge, "pathMode", "Absolute");
            SetOptionalEnumField(merge, "mergeAnimatorMode", "Append");
            SetOptionalField(merge, "deleteAttachedAnimator", true);
            SetOptionalField(merge, "matchAvatarWriteDefaults", false);
            EditorUtility.SetDirty(merge);

            var installerType = FindType(MenuInstallerTypeName);
            RemoveComponents(root, installerType);
            if (GetMenuControlCount(menu) > 0)
            {
                var installer = AddRequiredComponent(root, installerType);
                SetRequiredField(installer, "menuToAppend", menu);
                EditorUtility.SetDirty(installer);
            }

            var parametersType = FindType(ParametersTypeName);
            RemoveComponents(root, parametersType);
            var parameters = AddRequiredComponent(root, parametersType);
            ConfigureParameter(parameters);

            var blinkOverrideType = FindType(BlinkOverrideTypeName);
            RemoveComponents(root, blinkOverrideType);
            if (disableDescriptorBlink)
            {
                AddRequiredComponent(root, blinkOverrideType);
            }

            EditorUtility.SetDirty(root);
        }

        private static void ConfigureParameter(Component component)
        {
            var field = component.GetType().GetField("parameters");
            if (field == null)
            {
                throw new InvalidOperationException(
                    "ModularAvatarParameters.parameters was not found.");
            }

            var parameters = field.GetValue(component) as IList;
            if (parameters == null)
            {
                parameters = (IList)Activator.CreateInstance(field.FieldType);
            }

            for (var index = parameters.Count - 1; index >= 0; index--)
            {
                if (string.Equals(
                        GetField(parameters[index], "nameOrPrefix") as string,
                        GestureMatrixControllerWriter.MenuParameter,
                        StringComparison.Ordinal))
                {
                    parameters.RemoveAt(index);
                }
            }

            var configType = field.FieldType.GetGenericArguments()[0];
            var config = Activator.CreateInstance(configType);
            SetRequiredField(
                config,
                "nameOrPrefix",
                GestureMatrixControllerWriter.MenuParameter);
            SetRequiredEnumField(config, "syncType", "Int");
            SetOptionalField(config, "internalParameter", true);
            SetOptionalField(config, "isPrefix", false);
            SetOptionalField(config, "localOnly", false);
            SetOptionalField(config, "defaultValue", 0f);
            SetOptionalField(config, "saved", true);
            SetOptionalField(config, "hasExplicitDefaultValue", true);
            parameters.Add(config);
            field.SetValue(component, parameters);
            EditorUtility.SetDirty(component);
        }

        private static void RemoveLegacyMenuObjects(GameObject root)
        {
            for (var index = root.transform.childCount - 1; index >= 0; index--)
            {
                var child = root.transform.GetChild(index);
                if (child.name.StartsWith("ee4v Menu ", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static Component AddRequiredComponent(GameObject target, Type type)
        {
            if (type == null)
            {
                throw new InvalidOperationException("A required Modular Avatar type was not found.");
            }

            return target.AddComponent(type);
        }

        private static int GetMenuControlCount(ScriptableObject menu)
        {
            return GetField(menu, "controls") is IList controls
                ? controls.Count
                : 0;
        }

        private static void RemoveComponents(GameObject target, Type type)
        {
            if (type == null)
            {
                return;
            }

            foreach (var component in target.GetComponents(type))
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static void InstallIntoPrefabAsset(
            GameObject avatar,
            FaceExpressionGenerationPaths paths,
            AnimatorController controller,
            ScriptableObject menu,
            bool disableDescriptorBlink)
        {
            var avatarPath = AssetDatabase.GetAssetPath(avatar);
            if (string.IsNullOrEmpty(avatarPath) ||
                !avatarPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                PrefabUtility.GetPrefabAssetType(avatar) == PrefabAssetType.Model)
            {
                throw new InvalidOperationException(
                    "The selected avatar asset is not an editable Prefab.");
            }

            var assetRoot = avatar.transform.root;
            var targetPath = AnimationUtility.CalculateTransformPath(
                avatar.transform,
                assetRoot);
            var contents = PrefabUtility.LoadPrefabContents(avatarPath);
            try
            {
                var target = string.IsNullOrEmpty(targetPath)
                    ? contents.transform
                    : contents.transform.Find(targetPath);
                if (target == null)
                {
                    throw new InvalidOperationException(
                        "The avatar could not be found in its Prefab contents.");
                }

                InstallIntoTransform(
                    target,
                    paths,
                    controller,
                    menu,
                    disableDescriptorBlink,
                    false);
                if (PrefabUtility.SaveAsPrefabAsset(contents, avatarPath) == null)
                {
                    throw new InvalidOperationException(
                        "The avatar Prefab could not be saved.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static object GetField(object target, string name)
        {
            return target?.GetType().GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static void SetRequiredField(object target, string name, object value)
        {
            var field = FindField(target, name);
            if (field == null)
            {
                throw new InvalidOperationException(
                    target.GetType().FullName + "." + name + " was not found.");
            }

            field.SetValue(target, value);
        }

        private static void SetOptionalField(object target, string name, object value)
        {
            FindField(target, name)?.SetValue(target, value);
        }

        private static void SetRequiredEnumField(object target, string name, string value)
        {
            var field = FindField(target, name);
            if (field == null)
            {
                throw new InvalidOperationException(
                    target.GetType().FullName + "." + name + " was not found.");
            }

            field.SetValue(target, Enum.Parse(field.FieldType, value));
        }

        private static void SetOptionalEnumField(object target, string name, string value)
        {
            var field = FindField(target, name);
            if (field != null)
            {
                field.SetValue(target, Enum.Parse(field.FieldType, value));
            }
        }

        private static FieldInfo FindField(object target, string name)
        {
            return target?.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
