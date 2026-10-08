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
    internal enum MenuTemplateKind { ObjectToggle = 0, MaterialSwap = 1, ShapeChanger = 2, ParameterValue = 4, MaterialValue = 5, Transform = 6, Component = 7 }

    internal static class ExpressionMenuTemplateModel
    {
        private static bool Supported(ReactiveComponent effect) => effect is ModularAvatarObjectToggle ||
            effect is ModularAvatarMaterialSwap || effect is ModularAvatarShapeChanger;

        internal static ReactiveComponent[] Effects(ModularAvatarMenuItem item) => item == null ? Array.Empty<ReactiveComponent>() :
            item.GetComponentsInChildren<ReactiveComponent>(true)
                .Where(effect => Supported(effect) && effect.GetComponentInParent<ModularAvatarMenuItem>(true) == item).ToArray();

        internal static bool HasActions(ModularAvatarMenuItem item) => Effects(item).Length > 0 ||
            ExpressionMenuAnimationRecipe.Find(item) != null;

        internal static bool CanEdit(AvatarEditingContext context, Object target = null)
        {
            if (!context.Edits.CanEditPrefab() || EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (target == null) return true;
            if (target is ModularAvatarMenuItem item && (!ExpressionMenuAnimationRecipe.CanWriteBacking(item) ||
                item.GetComponent<ModularAvatarParameters>() != null &&
                !ExpressionMenuModel.CanWrite(item.GetComponent<ModularAvatarParameters>()))) return false;
            var transform = target is GameObject go ? go.transform : (target as Component)?.transform;
            return transform != null && transform.IsChildOf(context.Root.transform) &&
                ExpressionMenuModel.CanWrite(target) &&
                (!ExpressionMenuInstaller.IsGeneratedPrefab(transform.gameObject) ||
                 ExpressionMenuModel.CanWrite(PrefabUtility.GetCorrespondingObjectFromSource(
                     PrefabUtility.GetNearestPrefabInstanceRoot(transform.gameObject))));
        }

        internal static void Edit(AvatarEditingContext context, Object target, Action change)
        {
            if (!CanEdit(context, target))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            Undo.RecordObject(target, "Edit avatar gimmick");
            change();
            if (target is ModularAvatarMenuItem menuItem) ExpressionMenuAnimationRecipe.SyncInitial(menuItem);
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            var transform = target is GameObject go ? go.transform : (target as Component)?.transform;
            if (transform != null) ExpressionMenuInstaller.SaveGeneratedPrefab(transform.gameObject);
            Notify(context);
        }

        internal static ModularAvatarMenuItem Create(AvatarEditingContext context, string label,
            VRCExpressionsMenu target = null, GameObject childRoot = null, bool submenu = false)
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
            string groupingFolder = null;
            try
            {
                var parent = childRoot;
                if (parent == null)
                {
                    parent = context.Root.transform.Cast<Transform>().Select(child => child.gameObject)
                        .FirstOrDefault(ExpressionMenuInstaller.IsGroupingRoot);
                    if (parent == null)
                    {
                        groupingFolder = ProjectAssetSettings.EnsureAssetFolder(
                            "Animation/ExpressionMenu/" + avatarName + "/" + Guid.NewGuid().ToString("N"));
                        parent = new GameObject(GameObjectUtility.GetUniqueNameForSibling(context.Root.transform,
                            avatarName + "_ExpressionMenu"));
                        parent.transform.SetParent(context.Root.transform, false);
                        Undo.RegisterCreatedObjectUndo(parent, "Create Expression Menu Root");
                        if (PrefabUtility.SaveAsPrefabAssetAndConnect(parent, groupingFolder + "/ExpressionMenu.prefab",
                                InteractionMode.AutomatedAction) == null)
                            throw new InvalidOperationException(TemplateText.Get("saveFailed"));
                    }
                    if (!CanEdit(context, parent)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
                    // Keep each registered menu subtree intact when collecting earlier direct placements.
                    foreach (var child in context.Root.transform.Cast<Transform>().ToArray())
                        if (IsOwned(child.gameObject) && CanEdit(context, child.gameObject) &&
                            (!PrefabUtility.IsPartOfPrefabInstance(context.Root) ||
                             PrefabUtility.IsAddedGameObjectOverride(child.gameObject)))
                            Undo.SetTransformParent(child, parent.transform, "Group Expression Menu Items");
                }
                root.transform.SetParent(parent.transform, false);
                var item = root.AddComponent<ModularAvatarMenuItem>();
                item.PortableControl.Type = submenu ? PortableControlType.SubMenu : PortableControlType.Toggle;
                item.PortableControl.Parameter = submenu ? "" : "ee4v/Menu/" + id;
                item.PortableControl.Value = submenu ? 0 : 1;
                item.label = label;
                item.automaticValue = false;
                item.isSynced = !submenu;
                item.isSaved = !submenu;
                item.isDefault = false;
                item.MenuSource = SubmenuSource.Children;
                if (childRoot == null)
                {
                    var installer = root.AddComponent<ModularAvatarMenuInstaller>();
                    installer.installTargetMenu = target;
                    EditorUtility.SetDirty(installer);
                }
                var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, folder + "/MenuItem.prefab", InteractionMode.AutomatedAction);
                if (saved == null) throw new InvalidOperationException(TemplateText.Get("saveFailed"));
                Undo.RegisterCreatedObjectUndo(root, "Create Expression Menu Item");
                if (!submenu) ExpressionMenuAnimationRecipe.Ensure(item);
                ExpressionMenuInstaller.SaveGeneratedPrefab(parent);
                Undo.CollapseUndoOperations(group);
                return item;
            }
            catch
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
                if (root != null) Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(folder);
                if (groupingFolder != null) AssetDatabase.DeleteAsset(groupingFolder);
                throw;
            }
        }

        internal static bool IsDraft(ModularAvatarMenuItem item) => item != null &&
            IsOwned(item.gameObject) && item.CompareTag("EditorOnly");

        internal static void Delete(AvatarEditingContext context, ModularAvatarMenuItem item)
        {
            if (item == null || !IsOwned(item.gameObject) || !CanEdit(context, item))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            var parent = item.transform.parent?.gameObject;
            Undo.DestroyObjectImmediate(item.gameObject);
            ExpressionMenuInstaller.SaveGeneratedPrefab(parent);
            Notify(context);
        }

        internal static void AddAction(AvatarEditingContext context, ModularAvatarMenuItem item, MenuTemplateKind kind)
        {
            if (item == null || !CanEdit(context, item) || !IsOwned(item.gameObject))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            if (!AvailableActions(item).Contains(kind)) throw new InvalidOperationException(TemplateText.Get("incompatibleMode"));
            var recipe = ExpressionMenuAnimationRecipe.Ensure(item);
            if (kind == MenuTemplateKind.MaterialValue)
                ExpressionMenuAnimationRecipe.Change(context, item, () => recipe.MaterialValues.Add(new MenuMaterialValueAction
                {
                    Targets = { new MenuMaterialValueTarget() }
                }));
            else if (kind == MenuTemplateKind.Transform)
                ExpressionMenuAnimationRecipe.Change(context, item, () => recipe.Transforms.Add(new MenuTransformAction
                {
                    Targets = { new MenuTransformTarget() }
                }));
            else if (kind == MenuTemplateKind.Component)
                ExpressionMenuAnimationRecipe.Change(context, item, () => recipe.Components.Add(new MenuComponentAction
                {
                    Targets = { new MenuComponentTarget() }
                }));
            else if (kind == MenuTemplateKind.ParameterValue)
                ExpressionMenuAnimationRecipe.Change(context, item, () => recipe.ParameterActions.Add(new MenuParameterAction()));
            else if (ExpressionMenuAnimationRecipe.IsContinuous(item))
                ExpressionMenuAnimationRecipe.Change(context, item, () => recipe.RadialShapes.Add(new MenuRadialShapeAction()));
            else
            {
                ExpressionMenuAnimationRecipe.Change(context, item, () =>
                {
                    var action = new MenuReactiveAction { Kind = kind, Root = Reference(context, context.Root) };
                    if (kind == MenuTemplateKind.ObjectToggle)
                        action.Objects.Add(new ToggledObject { Object = new AvatarObjectReference(), Active = true });
                    if (kind == MenuTemplateKind.MaterialSwap) action.Swaps.Add(new MatSwap());
                    recipe.ReactiveActions.Add(action);
                });
            }
        }

        internal static MenuTemplateKind[] AvailableActions(ModularAvatarMenuItem item)
        {
            switch (ExpressionMenuAnimationRecipe.EffectiveMode(item))
            {
                case MenuBehaviorMode.Toggle: return (MenuTemplateKind[])Enum.GetValues(typeof(MenuTemplateKind));
                case MenuBehaviorMode.Button: return new[] { MenuTemplateKind.ParameterValue };
                case MenuBehaviorMode.Radial: return new[] { MenuTemplateKind.ShapeChanger, MenuTemplateKind.ParameterValue, MenuTemplateKind.MaterialValue, MenuTemplateKind.Transform };
                case MenuBehaviorMode.Puppet: return new[] { MenuTemplateKind.Transform };
                default: return Array.Empty<MenuTemplateKind>();
            }
        }

        internal static void RemoveAction(AvatarEditingContext context, ModularAvatarMenuItem item, ReactiveComponent effect)
        {
            if (item == null || !CanEdit(context, item) || !CanEdit(context, effect) ||
                !IsOwned(item.gameObject) || !Effects(item).Contains(effect))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            ExpressionMenuAnimationRecipe.Ensure(item);
            ExpressionMenuAnimationRecipe.Change(context, item, () => Undo.DestroyObjectImmediate(effect));
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
