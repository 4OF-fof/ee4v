using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.menu;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Ee4v.Core.AvatarEvaluation
{
    /// <summary>A resolved authoring control with its editable source, not a built avatar control.</summary>
    public sealed class AvatarMenuEntry
    {
        public VRCExpressionsMenu.Control Control { get; internal set; }
        public VRCExpressionsMenu.Control SourceControl { get; internal set; }
        public Object Owner { get; internal set; }
        public int Index { get; internal set; }
        public AvatarMenuPage Submenu { get; internal set; }
    }

    public sealed class AvatarMenuPage
    {
        public string Name { get; internal set; }
        public VRCExpressionsMenu Asset { get; internal set; }
        public GameObject ChildRoot { get; internal set; }
        internal readonly List<AvatarMenuEntry> Entries = new List<AvatarMenuEntry>();
        public ReadOnlyCollection<AvatarMenuEntry> Controls { get; }

        public AvatarMenuPage() => Controls = Entries.AsReadOnly();
    }

    /// <summary>Resolves the authoring menu and source references through Modular Avatar without building the avatar.</summary>
    public static class AvatarAuthoringMenu
    {
        public static AvatarMenuPage Read(GameObject avatar, out List<AvatarMenuPage> sources,
            Func<ModularAvatarMenuItem, bool> excludeItem = null)
        {
            if (avatar == null) throw new ArgumentNullException(nameof(avatar));
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) throw new InvalidOperationException("VRCAvatarDescriptor is required.");
            var assets = new Dictionary<VRCExpressionsMenu, AvatarMenuPage>();
            AvatarMenuPage ReadAsset(VRCExpressionsMenu asset)
            {
                if (asset == null) return null;
                if (assets.TryGetValue(asset, out var found)) return found;
                var page = new AvatarMenuPage { Name = asset.name, Asset = asset };
                assets.Add(asset, page);
                for (var i = 0; i < asset.controls.Count; i++)
                {
                    var control = asset.controls[i];
                    if (control == null) continue;
                    page.Entries.Add(new AvatarMenuEntry
                    {
                        Control = control, SourceControl = control, Owner = asset, Index = i,
                        Submenu = control.type == VRCExpressionsMenu.Control.ControlType.SubMenu ? ReadAsset(control.subMenu) : null
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
            var candidates = new List<AvatarMenuEntry>();
            var itemPages = new List<AvatarMenuPage>();
            foreach (var item in avatar.GetComponentsInChildren<ModularAvatarMenuItem>(true))
            {
                if (excludeItem?.Invoke(item) == true) continue;
                var control = item.PortableControl.CloneToVRCSDK();
                var axes = control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet ? 1 :
                    control.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ? 2 :
                    control.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet ? 4 : 0;
                control.subParameters = (control.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>())
                    .Take(axes).ToArray();
                var entry = new AvatarMenuEntry { Control = control, SourceControl = item.Control, Owner = item };
                entry.Submenu = control.type == VRCExpressionsMenu.Control.ControlType.SubMenu ? ReadAsset(control.subMenu) : null;
                candidates.Add(entry);
                var page = new AvatarMenuPage { Name = "MA / " + item.name };
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
            var pages = new Dictionary<VirtualMenuNode, AvatarMenuPage>();
            AvatarMenuPage ReadNode(VirtualMenuNode node, string name)
            {
                if (node == null) return null;
                if (pages.TryGetValue(node, out var known)) return known;
                var key = node.NodeKey;
                var origin = key?.GetType().GetField("Item1")?.GetValue(key) ?? key;
                var asset = origin as VRCExpressionsMenu;
                var childRoot = origin?.GetType().GetField("root", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(origin) as GameObject;
                var page = new AvatarMenuPage { Name = name, Asset = asset, ChildRoot = childRoot };
                pages.Add(node, page);
                foreach (var control in node.Controls)
                {
                    var submenu = control.type == VRCExpressionsMenu.Control.ControlType.SubMenu ?
                        ReadNode(control.SubmenuNode, control.name) : null;
                    var matches = candidates.Where(candidate => SameControl(candidate.Control, control) &&
                        (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu ||
                         (candidate.Owner is ModularAvatarMenuItem item && item.MenuSource == SubmenuSource.Children ?
                             submenu?.ChildRoot == (item.menuSource_otherObjectChildren != null ?
                                 item.menuSource_otherObjectChildren : item.gameObject) :
                             candidate.Control.subMenu == submenu?.Asset))).ToArray();
                    var source = matches.Length == 1 ? matches[0] : null;
                    page.Entries.Add(new AvatarMenuEntry
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
                if (entry.Control.type != VRCExpressionsMenu.Control.ControlType.SubMenu ||
                    !(entry.Owner is ModularAvatarMenuItem item) || item.MenuSource != SubmenuSource.Children) continue;
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
    }
}
