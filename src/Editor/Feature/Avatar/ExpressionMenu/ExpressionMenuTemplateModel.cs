using System;
using System.IO;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.Settings;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Ee4v.ExpressionMenu
{
    internal enum MenuTemplateKind { ObjectToggle, MaterialSwap, ShapeChanger }

    internal static class ExpressionMenuTemplateModel
    {
        private static bool Supported(ReactiveComponent effect) => effect is ModularAvatarObjectToggle ||
            effect is ModularAvatarMaterialSwap || effect is ModularAvatarShapeChanger;

        internal static ReactiveComponent[] Effects(ModularAvatarMenuItem item) => item == null ? Array.Empty<ReactiveComponent>() :
            item.GetComponentsInChildren<ReactiveComponent>(true)
                .Where(effect => Supported(effect) && effect.GetComponentInParent<ModularAvatarMenuItem>(true) == item).ToArray();

        internal static bool CanEdit(AvatarEditingContext context, Object target = null)
        {
            if (!context.Edits.CanEditPrefab() || EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (target == null) return true;
            var transform = target is GameObject go ? go.transform : (target as Component)?.transform;
            return transform != null && transform.IsChildOf(context.Root.transform) &&
                (target.hideFlags & HideFlags.NotEditable) == 0 && !PrefabUtility.IsPartOfImmutablePrefab(target) &&
                (!ExpressionMenuInstaller.IsGeneratedPrefab(transform.gameObject) ||
                 ExpressionMenuModel.CanWrite(PrefabUtility.GetCorrespondingObjectFromSource(
                     PrefabUtility.GetNearestPrefabInstanceRoot(transform.gameObject))));
        }

        internal static void Edit(AvatarEditingContext context, Object target, Action change, ModularAvatarMenuItem initialItem = null)
        {
            if (!CanEdit(context, target) || initialItem != null && !CanEdit(context, initialItem))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            Undo.RecordObject(target, "Edit MA gimmick");
            if (initialItem != null)
            {
                Undo.RecordObject(initialItem, "Edit initial value");
                initialItem.isDefault = true;
                EditorUtility.SetDirty(initialItem);
                PrefabUtility.RecordPrefabInstancePropertyModifications(initialItem);
            }
            change();
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            var transform = target is GameObject go ? go.transform : (target as Component)?.transform;
            if (transform != null) ExpressionMenuInstaller.SaveGeneratedPrefab(transform.gameObject);
            Notify(context);
        }

        internal static ModularAvatarMenuItem Create(AvatarEditingContext context, MenuTemplateKind? kind, string label,
            VRCExpressionsMenu target = null, GameObject childRoot = null)
        {
            if (!CanEdit(context) || context.Root == null || EditorUtility.IsPersistent(context.Root) ||
                !ExpressionMenuModel.CanWrite(context.PrefabAsset) ||
                childRoot != null && !CanEdit(context, childRoot))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            var invalid = Path.GetInvalidFileNameChars();
            var avatarName = new string(context.PrefabAsset.name.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
            var id = Guid.NewGuid().ToString("N");
            var folder = ProjectAssetSettings.EnsureAssetFolder("Animation/ExpressionMenu/" + avatarName + "/" + id);
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Expression Menu Item");
            var root = new GameObject(label);
            var registered = false;
            try
            {
                root.transform.SetParent((childRoot ?? context.Root).transform, false);
                var item = root.AddComponent<ModularAvatarMenuItem>();
                item.PortableControl.Type = PortableControlType.Toggle;
                item.PortableControl.Parameter = "ee4v/Menu/" + id;
                item.PortableControl.Value = 1;
                item.label = label;
                item.automaticValue = false;
                item.isSynced = true;
                item.isSaved = true;
                item.isDefault = kind == MenuTemplateKind.ShapeChanger || kind == MenuTemplateKind.ObjectToggle;
                item.MenuSource = SubmenuSource.Children;
                if (childRoot == null)
                {
                    var installer = root.AddComponent<ModularAvatarMenuInstaller>();
                    installer.installTargetMenu = target;
                    EditorUtility.SetDirty(installer);
                }
                if (kind.HasValue) AddEffect(context, root, kind.Value);
                var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, folder + "/MenuItem.prefab", InteractionMode.AutomatedAction);
                if (saved == null) throw new InvalidOperationException(TemplateText.Get("saveFailed"));
                Undo.RegisterCreatedObjectUndo(root, "Create Expression Menu Item");
                registered = true;
                ExpressionMenuInstaller.SaveGeneratedPrefab(childRoot);
                Undo.CollapseUndoOperations(group);
                return item;
            }
            catch
            {
                if (root != null)
                {
                    if (registered) Undo.DestroyObjectImmediate(root);
                    else Object.DestroyImmediate(root);
                }
                AssetDatabase.DeleteAsset(folder);
                throw;
            }
        }

        private static ReactiveComponent AddEffect(AvatarEditingContext context, GameObject root, MenuTemplateKind kind)
        {
            if (!CanEdit(context, root)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
            var type = kind == MenuTemplateKind.ObjectToggle ? typeof(ModularAvatarObjectToggle) :
                kind == MenuTemplateKind.MaterialSwap ? typeof(ModularAvatarMaterialSwap) : typeof(ModularAvatarShapeChanger);
            var component = root.AddComponent(type) as ReactiveComponent;
            if (component is ModularAvatarObjectToggle toggle)
                toggle.Objects.Add(new ToggledObject { Object = new AvatarObjectReference(), Active = true });
            else if (component is ModularAvatarMaterialSwap swap)
            {
                swap.Root = Reference(context, context.Root);
                swap.Swaps.Add(new MatSwap());
            }
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

        internal static bool IsOwned(GameObject root)
        {
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) ?? "";
            return PrefabUtility.IsAnyPrefabInstanceRoot(root) &&
                (path.IndexOf("/Animation/ExpressionMenu/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                 path.EndsWith("/MenuItem.prefab", StringComparison.OrdinalIgnoreCase) ||
                 path.IndexOf(".ExpressionMenu/Gimmicks/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                 path.EndsWith("/Gimmick.prefab", StringComparison.OrdinalIgnoreCase));
        }

        private static void Notify(AvatarEditingContext context)
        {
            context.Edits.WorkingSceneDirty = true;
            context.Edits.Changed();
        }

    }
}
