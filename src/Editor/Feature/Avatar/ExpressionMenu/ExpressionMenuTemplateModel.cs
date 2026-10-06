using System;
using System.IO;
using System.Linq;
using Ee4v.AvatarEditing;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Ee4v.ExpressionMenu
{
    internal enum MenuTemplateKind { ObjectToggle, MaterialSwap, MaterialSetter, ShapeChanger }

    internal static class ExpressionMenuTemplateModel
    {
        private static bool Supported(ReactiveComponent effect) => effect is ModularAvatarObjectToggle ||
            effect is ModularAvatarMaterialSwap || effect is ModularAvatarMaterialSetter || effect is ModularAvatarShapeChanger;

        internal static ReactiveComponent[] Effects(ModularAvatarMenuItem item) => item == null ? Array.Empty<ReactiveComponent>() :
            item.GetComponentsInChildren<ReactiveComponent>(true)
                .Where(effect => Supported(effect) && effect.GetComponentInParent<ModularAvatarMenuItem>(true) == item).ToArray();

        internal static bool CanEdit(AvatarEditingContext context, Object target = null)
        {
            if (!context.Edits.CanEditPrefab() || EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (target == null) return true;
            var transform = target is GameObject go ? go.transform : (target as Component)?.transform;
            return transform != null && transform.IsChildOf(context.Root.transform) &&
                (target.hideFlags & HideFlags.NotEditable) == 0 && !PrefabUtility.IsPartOfImmutablePrefab(target);
        }

        internal static void Edit(AvatarEditingContext context, Object target, Action change)
        {
            if (!CanEdit(context, target)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
            Undo.RecordObject(target, "Edit MA gimmick");
            change();
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            Notify(context);
        }

        internal static ModularAvatarMenuItem Create(AvatarEditingContext context, MenuTemplateKind kind, string label,
            VRCExpressionsMenu target = null, GameObject childRoot = null)
        {
            if (!CanEdit(context) || !ExpressionMenuModel.CanWrite(context.PrefabAsset) ||
                childRoot != null && !CanEdit(context, childRoot))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            var path = AssetDatabase.GetAssetPath(context.PrefabAsset);
            var folder = Path.GetDirectoryName(path).Replace('\\', '/') + "/" +
                Path.GetFileNameWithoutExtension(path) + ".ExpressionMenu/Gimmicks/" + Guid.NewGuid().ToString("N");
            EnsureFolder(folder);
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create MA gimmick");
            var root = new GameObject(label);
            Undo.RegisterCreatedObjectUndo(root, "Create MA gimmick");
            try
            {
                Undo.SetTransformParent(root.transform, (childRoot ?? context.Root).transform, "Create MA menu template");
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                var item = Undo.AddComponent<ModularAvatarMenuItem>(root);
                item.PortableControl.Type = PortableControlType.Toggle;
                item.PortableControl.Parameter = "ee4v/Gimmick/" + Path.GetFileName(folder);
                item.PortableControl.Value = 1;
                item.label = label;
                item.automaticValue = false;
                item.isSynced = true;
                item.isSaved = true;
                if (childRoot == null)
                {
                    var installer = Undo.AddComponent<ModularAvatarMenuInstaller>(root);
                    installer.installTargetMenu = target;
                    EditorUtility.SetDirty(installer);
                }
                AddEffect(context, root, kind);
                var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, folder + "/Gimmick.prefab", InteractionMode.AutomatedAction);
                if (saved == null) throw new InvalidOperationException(TemplateText.Get("saveFailed"));
                Undo.CollapseUndoOperations(group);
                return item;
            }
            catch
            {
                if (root != null) Undo.DestroyObjectImmediate(root);
                throw;
            }
        }

        private static ReactiveComponent AddEffect(AvatarEditingContext context, GameObject root, MenuTemplateKind kind)
        {
            if (!CanEdit(context, root)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
            var type = kind == MenuTemplateKind.ObjectToggle ? typeof(ModularAvatarObjectToggle) :
                kind == MenuTemplateKind.MaterialSwap ? typeof(ModularAvatarMaterialSwap) :
                kind == MenuTemplateKind.MaterialSetter ? typeof(ModularAvatarMaterialSetter) : typeof(ModularAvatarShapeChanger);
            var component = Undo.AddComponent(root, type) as ReactiveComponent;
            if (component is ModularAvatarObjectToggle toggle)
                toggle.Objects.Add(new ToggledObject { Object = new AvatarObjectReference(), Active = true });
            else if (component is ModularAvatarMaterialSwap swap)
            {
                swap.Root = Reference(context, context.Root);
                swap.Swaps.Add(new MatSwap());
            }
            else if (component is ModularAvatarMaterialSetter setter)
                setter.Objects.Add(new MaterialSwitchObject { Object = new AvatarObjectReference() });
            else if (component is ModularAvatarShapeChanger changer)
                changer.Shapes.Add(new ChangedShape { Object = new AvatarObjectReference(), ChangeType = ShapeChangeType.Set, Value = 100 });
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            return component;
        }

        internal static AvatarObjectReference Reference(AvatarEditingContext context, GameObject target, bool allowRoot = true)
        {
            if (target == null) return new AvatarObjectReference();
            if (!target.transform.IsChildOf(context.Root.transform)) throw new InvalidOperationException(TemplateText.Get("foreignTarget"));
            if (!allowRoot && target == context.Root) throw new InvalidOperationException(TemplateText.Get("rootTarget"));
            for (var node = target.transform; node != context.Root.transform; node = node.parent)
                if (node.name.Contains("/") || node.parent.Cast<Transform>().Count(child => child.name == node.name) != 1)
                    throw new InvalidOperationException(TemplateText.Get("ambiguousPath"));
            return new AvatarObjectReference { referencePath = target == context.Root ? AvatarObjectReference.AVATAR_ROOT :
                AnimationUtility.CalculateTransformPath(target.transform, context.Root.transform) };
        }

        internal static void RemoveEffect(AvatarEditingContext context, ReactiveComponent effect)
        {
            if (!CanEdit(context, effect)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
            Undo.DestroyObjectImmediate(effect);
            Notify(context);
        }

        internal static bool IsOwned(GameObject root)
        {
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) ?? "";
            return PrefabUtility.IsAnyPrefabInstanceRoot(root) && path.IndexOf(".ExpressionMenu/Gimmicks/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                path.EndsWith("/Gimmick.prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static void Notify(AvatarEditingContext context)
        {
            context.Edits.WorkingSceneDirty = true;
            context.Edits.Changed();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder))))
                throw new InvalidOperationException(TemplateText.Get("saveFailed"));
        }
    }
}


