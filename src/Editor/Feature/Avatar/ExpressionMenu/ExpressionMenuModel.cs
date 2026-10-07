using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using Ee4v.Core.Settings;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.menu;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Ee4v.ExpressionMenu
{
    internal sealed class MenuEntry
    {
        internal VRCExpressionsMenu.Control Control;
        internal VRCExpressionsMenu.Control SourceControl;
        internal Object Owner;
        internal int Index;
        internal MenuPage Submenu;
        internal bool CanEdit => Owner != null && ExpressionMenuModel.CanWrite(Owner);
    }

    internal sealed class MenuPage
    {
        internal string Name;
        internal VRCExpressionsMenu Asset;
        internal GameObject ChildRoot;
        internal readonly List<MenuEntry> Entries = new List<MenuEntry>();
    }

    /// <summary>Keeps MA's menu resolution and serialized source editing separate.</summary>
    internal static class ExpressionMenuModel
    {
        internal static MenuPage Read(GameObject avatar, out List<MenuPage> sources)
        {
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) throw new InvalidOperationException("VRCAvatarDescriptor is required.");
            var assets = new Dictionary<VRCExpressionsMenu, MenuPage>();
            MenuPage ReadAsset(VRCExpressionsMenu asset)
            {
                if (asset == null) return null;
                if (assets.TryGetValue(asset, out var found)) return found;
                var page = new MenuPage { Name = asset.name, Asset = asset };
                assets.Add(asset, page);
                for (var i = 0; i < asset.controls.Count; i++)
                {
                    var control = asset.controls[i];
                    if (control == null) continue;
                    page.Entries.Add(new MenuEntry
                    {
                        Control = control, SourceControl = control, Owner = asset, Index = i,
                        Submenu = ReadAsset(control.subMenu)
                    });
                }
                return page;
            }
            ReadAsset(descriptor.expressionsMenu);
            foreach (var installer in avatar.GetComponentsInChildren<ModularAvatarMenuInstaller>(true))
            {
                ReadAsset(installer.menuToAppend);
                ReadAsset(installer.installTargetMenu);
            }
            var candidates = new List<MenuEntry>();
            var itemPages = new List<MenuPage>();
            foreach (var item in avatar.GetComponentsInChildren<ModularAvatarMenuItem>(true))
            {
                if (ExpressionMenuTemplateModel.IsDraft(item)) continue;
                var control = item.PortableControl.CloneToVRCSDK();
                var axes = control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet ? 1 :
                    control.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ? 2 :
                    control.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet ? 4 : 0;
                control.subParameters = (control.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>())
                    .Take(axes).ToArray();
                var entry = new MenuEntry { Control = control, SourceControl = item.Control, Owner = item };
                entry.Submenu = ReadAsset(control.subMenu);
                candidates.Add(entry);
                var page = new MenuPage { Name = "MA / " + item.name };
                page.Entries.Add(entry);
                itemPages.Add(page);
            }
            candidates.AddRange(assets.Values.SelectMany(page => page.Entries));
            sources = assets.Values.Concat(itemPages).ToList();

            // MA has no supported public editor resolver. Isolate version-sensitive access.
            var resolver = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("nadena.dev.modular_avatar.core.editor.menu.VirtualMenu", false))
                .FirstOrDefault(type => type != null);
            var forAvatar = resolver?.GetMethod("ForAvatar", BindingFlags.Static | BindingFlags.NonPublic);
            if (forAvatar == null) throw new InvalidOperationException("The installed Modular Avatar menu resolver is unavailable.");
            var args = new object[forAvatar.GetParameters().Length];
            args[0] = descriptor;
            var resolved = forAvatar.Invoke(null, args);
            var root = resolver.GetProperty("RootMenuNode")?.GetValue(resolved) as VirtualMenuNode;
            if (root == null) throw new InvalidOperationException("Modular Avatar did not resolve a root menu.");
            var pages = new Dictionary<VirtualMenuNode, MenuPage>();
            MenuPage ReadNode(VirtualMenuNode node, string name)
            {
                if (node == null) return null;
                if (pages.TryGetValue(node, out var known)) return known;
                var key = node.NodeKey;
                var origin = key?.GetType().GetField("Item1")?.GetValue(key) ?? key;
                var asset = origin as VRCExpressionsMenu;
                var childRoot = origin?.GetType().GetField("root", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(origin) as GameObject;
                var page = new MenuPage { Name = name, Asset = asset, ChildRoot = childRoot };
                pages.Add(node, page);
                foreach (var control in node.Controls)
                {
                    var submenu = ReadNode(control.SubmenuNode, control.name);
                    var matches = candidates.Where(candidate => SameControl(candidate.Control, control) &&
                        (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu ||
                         (candidate.Owner is ModularAvatarMenuItem item && item.MenuSource == SubmenuSource.Children ?
                             submenu?.ChildRoot == (item.menuSource_otherObjectChildren != null ?
                                 item.menuSource_otherObjectChildren : item.gameObject) :
                             candidate.Control.subMenu == submenu?.Asset))).ToArray();
                    var source = matches.Length == 1 ? matches[0] : null;
                    page.Entries.Add(new MenuEntry
                    {
                        Control = control, Submenu = submenu,
                        Owner = source?.Owner, SourceControl = source?.SourceControl, Index = source?.Index ?? -1
                    });
                }
                return page;
            }
            var effective = ReadNode(root, "Expression Menu");
            foreach (var itemPage in itemPages)
            {
                var entry = itemPage.Entries[0];
                if (!(entry.Owner is ModularAvatarMenuItem item) || item.MenuSource != SubmenuSource.Children) continue;
                var childRoot = item.menuSource_otherObjectChildren != null ? item.menuSource_otherObjectChildren : item.gameObject;
                entry.Submenu = pages.Values.FirstOrDefault(page => page.ChildRoot == childRoot);
            }
            return effective;
        }

        private static bool SameControl(VRCExpressionsMenu.Control a, VRCExpressionsMenu.Control b) =>
            a.name == b.name && a.type == b.type && a.icon == b.icon && a.value == b.value &&
            (a.parameter?.name ?? "") == (b.parameter?.name ?? "") &&
            (a.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>()).Select(p => p?.name)
                .SequenceEqual((b.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>()).Select(p => p?.name)) &&
            (a.labels ?? Array.Empty<VRCExpressionsMenu.Control.Label>()).Select(l => (l.name, l.icon))
                .SequenceEqual((b.labels ?? Array.Empty<VRCExpressionsMenu.Control.Label>()).Select(l => (l.name, l.icon)));

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
            if (PrefabUtility.IsPartOfImmutablePrefab(target)) return false;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(target);
            return source == null || CanWrite(source);
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
            if (from == null || to == null || from == to || !from.CanEdit || !to.CanEdit) return false;
            if (from.Owner is VRCExpressionsMenu asset)
                return to.Owner == asset && from.Index != to.Index;
            if (!(from.Owner is ModularAvatarMenuItem first) || !(to.Owner is ModularAvatarMenuItem second)) return false;
            var parent = first.transform.parent;
            return parent != null && parent == second.transform.parent && first != second &&
                CanWrite(parent) && CanWrite(first.transform) && CanWrite(second.transform);
        }

        internal static bool CanRelocate(AvatarEditingContext context, MenuEntry entry, MenuPage destination)
        {
            if (entry == null || destination == null || !entry.CanEdit || !context.Edits.CanEditPrefab() ||
                destination.Entries.Count >= 8) return false;
            var visited = new HashSet<MenuPage>();
            bool Contains(MenuPage page)
            {
                if (page == null || !visited.Add(page)) return false;
                return page == destination || page.Asset != null && page.Asset == destination.Asset ||
                    page.ChildRoot != null && page.ChildRoot == destination.ChildRoot || page.Entries.Any(child => Contains(child.Submenu));
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
            if (!entry.CanEdit) throw new InvalidOperationException("The menu source is read-only or ambiguous.");
            if (entry.Owner is VRCExpressionsMenu asset &&
                (entry.Index < 0 || entry.Index >= asset.controls.Count ||
                 !ReferenceEquals(asset.controls[entry.Index], entry.SourceControl)))
                throw new InvalidOperationException("The menu source changed. Select the control again.");
            if (entry.Owner is ModularAvatarMenuItem item && !ReferenceEquals(item.Control, entry.SourceControl))
                throw new InvalidOperationException("The MA Menu Item changed. Select the control again.");
        }
    }
}
