using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ee4v.UI;
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
                    var parent = item.transform.parent?.gameObject;
                    Undo.DestroyObjectImmediate(item.gameObject);
                    ExpressionMenuInstaller.SaveGeneratedPrefab(parent);
                }
                else Undo.DestroyObjectImmediate(item);
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
