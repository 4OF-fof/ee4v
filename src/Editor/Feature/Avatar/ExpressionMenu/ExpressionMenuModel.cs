using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.AvatarEvaluation;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using Ee4v.Core.Settings;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;
using MenuEntry = Ee4v.Core.AvatarEvaluation.AvatarMenuEntry;
using MenuPage = Ee4v.Core.AvatarEvaluation.AvatarMenuPage;

namespace Ee4v.ExpressionMenu
{
    /// <summary>Keeps MA's menu resolution and serialized source editing separate.</summary>
    internal static partial class ExpressionMenuModel
    {
        internal static MenuPage Read(GameObject avatar, out List<MenuPage> sources) =>
            AvatarAuthoringMenu.Read(avatar, out sources, ExpressionMenuTemplateModel.IsDraft);

        internal static bool CanEdit(MenuEntry entry) => entry != null && CanWrite(entry.Owner);

        internal static Texture2D DisplayIcon(Texture2D icon) =>
            icon != null ? icon : UiBuiltinIconResolver.LoadTexture(UiBuiltinIcon.GameObject) as Texture2D;

        internal static bool CanWrite(Object target)
        {
            if (target == null || EditorApplication.isPlayingOrWillChangePlaymode ||
                (target.hideFlags & HideFlags.NotEditable) != 0) return false;
            if (EditorUtility.IsPersistent(target))
            {
                var path = AssetDatabase.GetAssetPath(target);
                return path.StartsWith("Assets/", StringComparison.Ordinal) && AssetDatabase.IsOpenForEdit(path);
            }
            if (!(target is GameObject) && !(target is Component)) return true;
            return !PrefabUtility.IsPartOfImmutablePrefab(target);
        }

        internal static VRCExpressionsMenu.Control Copy(VRCExpressionsMenu.Control control) =>
            new VRCExpressionsMenu.Control
            {
                name = control.name, type = control.type, icon = control.icon,
                value = control.value, style = control.style, subMenu = control.subMenu,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = control.parameter?.name ?? "" },
                subParameters = (control.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>())
                    .Select(p => new VRCExpressionsMenu.Control.Parameter { name = p?.name ?? "" }).ToArray(),
                labels = (control.labels ?? Array.Empty<VRCExpressionsMenu.Control.Label>())
                    .Select(l => new VRCExpressionsMenu.Control.Label { name = l.name, icon = l.icon }).ToArray()
            };

        internal static void Update(MenuEntry entry, VRCExpressionsMenu.Control value)
        {
            ValidateEntry(entry);
            Undo.RecordObject(entry.Owner, "Edit Expression Menu");
            if (entry.Owner is VRCExpressionsMenu asset)
                asset.controls[entry.Index] = value;
            else if (entry.Owner is ModularAvatarMenuItem item)
            {
                item.PortableControl.SetFrom(value);
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
            EditorUtility.SetDirty(entry.Owner);
            if (entry.Owner is VRCExpressionsMenu) AssetDatabase.SaveAssetIfDirty(entry.Owner);
        }

        internal static void Remove(MenuEntry entry)
        {
            ValidateEntry(entry);
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete Expression Menu Control");
            var parent = (entry.Owner as ModularAvatarMenuItem)?.transform.parent?.gameObject;
            var parentAsset = ExpressionMenuInstaller.IsGeneratedPrefab(parent) ?
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(parent)) : null;
            try
            {
                if (entry.Owner is VRCExpressionsMenu asset)
                {
                    Undo.RecordObject(asset, "Remove Expression Menu Control");
                    asset.controls.RemoveAt(entry.Index);
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
                else if (entry.Owner is ModularAvatarMenuItem item)
                {
                    if (ExpressionMenuTemplateModel.IsOwned(item.gameObject))
                    {
                        Undo.DestroyObjectImmediate(item.gameObject);
                        ExpressionMenuInstaller.SaveGeneratedPrefab(parent);
                    }
                    else Undo.DestroyObjectImmediate(item);
                }
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                if (entry.Owner is VRCExpressionsMenu asset) AssetDatabase.SaveAssetIfDirty(asset);
                if (parentAsset != null) PrefabUtility.SavePrefabAsset(parentAsset);
                throw;
            }
        }

        internal static bool CanMove(MenuEntry from, MenuEntry to)
        {
            if (from == to || !CanEdit(from) || !CanEdit(to)) return false;
            if (from.Owner is VRCExpressionsMenu asset)
                return to.Owner == asset && from.Index != to.Index;
            if (!(from.Owner is ModularAvatarMenuItem first) || !(to.Owner is ModularAvatarMenuItem second)) return false;
            var parent = first.transform.parent;
            return parent != null && parent == second.transform.parent && first != second &&
                CanWrite(parent) && CanWrite(first.transform) && CanWrite(second.transform);
        }

        internal static bool CanRelocate(AvatarEditingContext context, MenuEntry entry, MenuPage destination)
        {
            if (destination == null || !CanEdit(entry) || !context.Edits.CanEditPrefab() ||
                destination.Controls.Count >= 8) return false;
            var visited = new HashSet<MenuPage>();
            bool Contains(MenuPage page)
            {
                if (page == null || !visited.Add(page)) return false;
                return page == destination || page.Asset != null && page.Asset == destination.Asset ||
                    page.ChildRoot != null && page.ChildRoot == destination.ChildRoot || page.Controls.Any(child => Contains(child.Submenu));
            }
            if (Contains(entry.Submenu)) return false;
            if (destination.ChildRoot != null && !ExpressionMenuTemplateModel.CanEdit(context, destination.ChildRoot) ||
                destination.Asset != null && !CanWrite(destination.Asset)) return false;
            if (entry.Owner is VRCExpressionsMenu asset)
                return asset != destination.Asset &&
                    (destination.Asset != null && destination.ChildRoot == null || CanWrite(context.PrefabAsset));
            if (!(entry.Owner is ModularAvatarMenuItem item) || !ExpressionMenuTemplateModel.CanEdit(context, item) ||
                !CanWrite(item.transform) || item.transform.parent != null && !CanWrite(item.transform.parent)) return false;
            if (destination.ChildRoot != null && destination.ChildRoot.transform.IsChildOf(item.transform)) return false;
            var installer = item.GetComponent<ModularAvatarMenuInstaller>();
            if (installer != null && (!CanWrite(installer) || installer.menuToAppend != null)) return false;
            if (installer != null && context.Root.GetComponentsInChildren<Component>(true).Any(component =>
                component != null && component.GetType().Name == "ModularAvatarMenuInstallTarget" &&
                new SerializedObject(component).FindProperty("installer")?.objectReferenceValue == installer)) return false;
            return !PrefabUtility.IsPartOfPrefabInstance(item) || ExpressionMenuTemplateModel.IsOwned(item.gameObject) ||
                PrefabUtility.IsAddedGameObjectOverride(item.gameObject);
        }

        internal static void Relocate(AvatarEditingContext context, MenuEntry entry, MenuPage destination)
        {
            ValidateEntry(entry);
            if (!CanRelocate(context, entry, destination)) throw new InvalidOperationException("This menu destination is unavailable.");
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Move Expression Menu Control");
            string createdFolder = null;
            string createdRootFolder = null;
            var restoreAssets = new HashSet<Object>();
            if (entry.Owner is VRCExpressionsMenu sourceAsset) restoreAssets.Add(sourceAsset);
            if (destination.Asset != null) restoreAssets.Add(destination.Asset);
            void RecordGeneratedAsset(GameObject target)
            {
                if (!ExpressionMenuInstaller.IsGeneratedPrefab(target)) return;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target));
                if (asset != null && restoreAssets.Add(asset)) Undo.RegisterFullObjectHierarchyUndo(asset, "Move Expression Menu Control");
            }
            try
            {
                if (entry.Owner is VRCExpressionsMenu asset && destination.ChildRoot == null && destination.Asset != null)
                {
                    Undo.RecordObjects(new Object[] { asset, destination.Asset }, "Move Expression Menu Control");
                    var control = asset.controls[entry.Index];
                    asset.controls.RemoveAt(entry.Index);
                    destination.Asset.controls.Add(control);
                    EditorUtility.SetDirty(asset);
                    EditorUtility.SetDirty(destination.Asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(destination.Asset);
                }
                else
                {
                    var parent = destination.ChildRoot ?? context.Root.transform.Cast<Transform>().Select(child => child.gameObject)
                        .FirstOrDefault(ExpressionMenuInstaller.IsGroupingRoot);
                    if (parent == null)
                    {
                        if (!CanWrite(context.PrefabAsset)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
                        createdRootFolder = ProjectAssetSettings.EnsureAssetFolder("Animation/ExpressionMenu/" + Guid.NewGuid().ToString("N"));
                        parent = new GameObject(GameObjectUtility.GetUniqueNameForSibling(context.Root.transform, "ExpressionMenu"));
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent, context.Root.scene);
                        parent.transform.SetParent(context.Root.transform, false);
                        Undo.RegisterCreatedObjectUndo(parent, "Create Expression Menu Root");
                        if (PrefabUtility.SaveAsPrefabAssetAndConnect(parent, createdRootFolder + "/ExpressionMenu.prefab", InteractionMode.AutomatedAction) == null)
                            throw new InvalidOperationException(TemplateText.Get("saveFailed"));
                    }
                    if (!ExpressionMenuTemplateModel.CanEdit(context, parent))
                        throw new InvalidOperationException(TemplateText.Get("readOnly"));
                    var original = entry.Owner as ModularAvatarMenuItem;
                    RecordGeneratedAsset(original?.gameObject);
                    ModularAvatarMenuItem moved;
                    if (original == null)
                    {
                        // A serialized control becomes an equivalent MA control when entering a Children submenu.
                        createdFolder = ProjectAssetSettings.EnsureAssetFolder("Animation/ExpressionMenu/" + Guid.NewGuid().ToString("N"));
                        var go = new GameObject(entry.Control.name);
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, context.Root.scene);
                        go.transform.SetParent(parent.transform, false);
                        Undo.RegisterCreatedObjectUndo(go, "Move Expression Menu Control");
                        moved = Undo.AddComponent<ModularAvatarMenuItem>(go);
                        moved.PortableControl.SetFrom(Copy(entry.SourceControl));
                        moved.label = entry.Control.name;
                        moved.MenuSource = SubmenuSource.MenuAsset;
                        moved.automaticValue = false;
                        if (PrefabUtility.SaveAsPrefabAssetAndConnect(go, createdFolder + "/MenuItem.prefab", InteractionMode.AutomatedAction) == null)
                            throw new InvalidOperationException(TemplateText.Get("saveFailed"));
                        var source = (VRCExpressionsMenu)entry.Owner;
                        Undo.RecordObject(source, "Move Expression Menu Control");
                        source.controls.RemoveAt(entry.Index);
                        EditorUtility.SetDirty(source);
                        AssetDatabase.SaveAssetIfDirty(source);
                    }
                    else if (ExpressionMenuTemplateModel.IsOwned(original.gameObject))
                    {
                        // Reinstantiate the same generated prefab instead of reparenting an inherited nested prefab.
                        ExpressionMenuInstaller.SaveGeneratedPrefab(original.gameObject);
                        var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(original.gameObject);
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                        Undo.RegisterCreatedObjectUndo(go, "Move Expression Menu Control");
                        moved = go.GetComponent<ModularAvatarMenuItem>();
                        if (moved == null) throw new InvalidOperationException("The generated menu prefab has no control.");
                        Undo.DestroyObjectImmediate(original.gameObject);
                    }
                    else
                    {
                        moved = original;
                        Undo.SetTransformParent(moved.transform, parent.transform, "Move Expression Menu Control");
                    }
                    var installer = moved.GetComponent<ModularAvatarMenuInstaller>();
                    if (destination.ChildRoot != null)
                    {
                        if (installer != null) Undo.DestroyObjectImmediate(installer);
                    }
                    else
                    {
                        if (installer == null) installer = Undo.AddComponent<ModularAvatarMenuInstaller>(moved.gameObject);
                        Undo.RecordObject(installer, "Move Expression Menu Control");
                        installer.installTargetMenu = destination.Asset;
                        EditorUtility.SetDirty(installer);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(installer);
                    }
                    // Keep hierarchy changes as avatar overrides. Applying a removed inherited nested prefab
                    // and recording its recreated scene instance in the same Undo group duplicates it on Redo.
                    // The host's avatar save persists the destination and installer overrides together.
                }
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                foreach (var asset in restoreAssets)
                    if (asset is GameObject prefab) PrefabUtility.SavePrefabAsset(prefab);
                    else if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
                if (createdFolder != null) AssetDatabase.DeleteAsset(createdFolder);
                if (createdRootFolder != null) AssetDatabase.DeleteAsset(createdRootFolder);
                throw;
            }
        }
        internal static void Move(MenuEntry from, MenuEntry to)
        {
            ValidateEntry(from);
            ValidateEntry(to);
            if (!CanMove(from, to)) throw new InvalidOperationException("These controls cannot be reordered together.");
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Reorder Expression Menu");
            GameObject generatedAsset = null;
            try
            {
                if (from.Owner is VRCExpressionsMenu asset)
                {
                    Undo.RecordObject(asset, "Reorder Expression Menu");
                    var control = asset.controls[from.Index];
                    asset.controls.RemoveAt(from.Index);
                    asset.controls.Insert(to.Index, control);
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
                else
                {
                    var first = ((ModularAvatarMenuItem)from.Owner).transform;
                    var second = ((ModularAvatarMenuItem)to.Owner).transform;
                    var parent = first.parent;
                    var targetIndex = second.GetSiblingIndex();
                    Undo.RegisterFullObjectHierarchyUndo(parent.gameObject, "Reorder Expression Menu");
                    if (ExpressionMenuInstaller.IsGeneratedPrefab(parent.gameObject))
                    {
                        ExpressionMenuInstaller.SaveGeneratedPrefab(parent.gameObject);
                        var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(parent.gameObject);
                        generatedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        var sourceFirst = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(first, path);
                        var sourceSecond = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(second, path);
                        if (sourceFirst == null || sourceSecond == null || sourceFirst.parent != sourceSecond.parent ||
                            !CanWrite(generatedAsset)) throw new InvalidOperationException("The menu hierarchy changed. Select the control again.");
                        Undo.RegisterFullObjectHierarchyUndo(generatedAsset, "Reorder Expression Menu");
                        sourceFirst.SetSiblingIndex(sourceSecond.GetSiblingIndex());
                        EditorUtility.SetDirty(sourceFirst.parent);
                        PrefabUtility.SavePrefabAsset(generatedAsset, out var saved);
                        if (!saved) throw new InvalidOperationException(TemplateText.Get("saveFailed"));
                    }
                    first.SetSiblingIndex(targetIndex);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(first);
                    EditorUtility.SetDirty(parent);
                }
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                if (generatedAsset != null) PrefabUtility.SavePrefabAsset(generatedAsset);
                throw;
            }
        }

        private static void ValidateEntry(MenuEntry entry)
        {
            if (!CanEdit(entry)) throw new InvalidOperationException("The menu source is read-only or ambiguous.");
            if (entry.Owner is VRCExpressionsMenu asset &&
                (entry.Index < 0 || entry.Index >= asset.controls.Count ||
                 !ReferenceEquals(asset.controls[entry.Index], entry.SourceControl)))
                throw new InvalidOperationException("The menu source changed. Select the control again.");
            if (entry.Owner is ModularAvatarMenuItem item && !ReferenceEquals(item.Control, entry.SourceControl))
                throw new InvalidOperationException("The MA Menu Item changed. Select the control again.");
        }
    }
}
