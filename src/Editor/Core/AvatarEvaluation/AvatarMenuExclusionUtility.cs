using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Ee4v.Core.AvatarEvaluation
{
    public static class AvatarMenuExclusionUtility
    {
        public static IReadOnlyList<AvatarMenuExclusion> Entries(GameObject avatar) =>
            avatar.GetComponentsInChildren<AvatarMenuExclusions>(true)
                .SelectMany(marker => marker.Entries).ToArray();

        public static bool IsExcluded(AvatarMenuEntry entry, IReadOnlyList<AvatarMenuExclusion> entries) =>
            entry.Owner != null && entries.Any(record => record.Source == entry.Owner &&
                (!(entry.Owner is VRCExpressionsMenu) || SameControl(record.Control, entry.SourceControl)));

        public static bool IsCurrent(AvatarMenuExclusion entry) => entry.Source != null &&
            (!(entry.Source is VRCExpressionsMenu menu) || menu.controls.Count(control => SameControl(control, entry.Control)) == 1);

        public static bool SameControl(VRCExpressionsMenu.Control first, VRCExpressionsMenu.Control second) =>
            first != null && second != null && first.name == second.name && first.type == second.type &&
            first.icon == second.icon && first.style == second.style && first.value == second.value &&
            first.subMenu == second.subMenu && (first.parameter?.name ?? "") == (second.parameter?.name ?? "") &&
            (first.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>()).Select(value => value?.name ?? "")
                .SequenceEqual((second.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>()).Select(value => value?.name ?? "")) &&
            (first.labels ?? Array.Empty<VRCExpressionsMenu.Control.Label>()).Select(value => (value.name, value.icon))
                .SequenceEqual((second.labels ?? Array.Empty<VRCExpressionsMenu.Control.Label>()).Select(value => (value.name, value.icon)));

        public static int ResolveIndex(AvatarMenuExclusion record, VRCExpressionsMenu menu)
        {
            var matches = menu.controls.Select((control, index) => (control, index))
                .Where(value => SameControl(value.control, record.Control)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Excluded menu item ‘{record.DisplayName}’ changed or is ambiguous. Restore it in Expression Menu and exclude it again.");
            return matches[0].index;
        }

        public sealed class BuildFilter
        {
            private readonly HashSet<string> _names = new HashSet<string>();
            private readonly Dictionary<ModularAvatarMenuItem, string> _labels = new Dictionary<ModularAvatarMenuItem, string>();
            private readonly Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> _copies = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();
            private readonly Dictionary<VRCExpressionsMenu.Control, string> _originalNames = new Dictionary<VRCExpressionsMenu.Control, string>();

            public void Prepare(GameObject avatar, Action<Object> saveAsset)
            {
                var entries = Entries(avatar);
                if (entries.Count == 0) return;
                var assetEntries = entries.Where(entry => entry.Source is VRCExpressionsMenu).ToArray();
                if (assetEntries.Length > 0)
                {
                    VRCExpressionsMenu Copy(VRCExpressionsMenu original)
                    {
                        if (original == null) return null;
                        if (_copies.TryGetValue(original, out var known)) return known;
                        var copy = Object.Instantiate(original);
                        copy.name = original.name;
                        _copies.Add(original, copy);
                        saveAsset(copy);
                        for (var index = 0; index < copy.controls.Count; index++)
                            copy.controls[index].subMenu = Copy(original.controls[index].subMenu);
                        return copy;
                    }
                    var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
                    descriptor.expressionsMenu = Copy(descriptor.expressionsMenu);
                    foreach (var installer in avatar.GetComponentsInChildren<ModularAvatarMenuInstaller>(true))
                    {
                        installer.menuToAppend = Copy(installer.menuToAppend);
                        installer.installTargetMenu = Copy(installer.installTargetMenu);
                    }
                    foreach (var item in avatar.GetComponentsInChildren<ModularAvatarMenuItem>(true))
                        if (item.Control != null) item.Control.subMenu = Copy(item.Control.subMenu);
                    foreach (var record in assetEntries)
                    {
                        var original = (VRCExpressionsMenu)record.Source;
                        var index = ResolveIndex(record, original);
                        if (!_copies.TryGetValue(original, out var copy)) continue;
                        Tag(copy.controls[index]);
                    }
                }
                foreach (var record in entries)
                {
                    if (record.Source is ModularAvatarMenuItem item)
                    {
                        if (!item.transform.IsChildOf(avatar.transform))
                            throw new InvalidOperationException("Menu exclusions must reference this avatar.");
                        if (_labels.ContainsKey(item)) continue;
                        _labels.Add(item, item.label);
                        item.label = NewName();
                    }
                    else if (record.Source is ModularAvatarMenuInstaller installer)
                    {
                        if (!installer.transform.IsChildOf(avatar.transform))
                            throw new InvalidOperationException("Menu exclusions must reference this avatar.");
                        Object.DestroyImmediate(installer);
                    }
                }
            }

            private string NewName()
            {
                var name = "__ee4v_menu_excluded_" + Guid.NewGuid().ToString("N");
                _names.Add(name);
                return name;
            }

            private void Tag(VRCExpressionsMenu.Control control)
            {
                if (_originalNames.ContainsKey(control)) return;
                _originalNames.Add(control, control.name);
                control.name = NewName();
            }

            public void Apply(GameObject avatar)
            {
                var visited = new HashSet<VRCExpressionsMenu>();
                void Filter(VRCExpressionsMenu menu)
                {
                    if (menu == null || !visited.Add(menu)) return;
                    foreach (var control in menu.controls) Filter(control.subMenu);
                    menu.controls.RemoveAll(control => _names.Contains(control.name));
                }
                Filter(avatar.GetComponent<VRCAvatarDescriptor>().expressionsMenu);
                foreach (var label in _labels) if (label.Key != null) label.Key.label = label.Value;
                foreach (var value in _originalNames) value.Key.name = value.Value;
                foreach (var marker in avatar.GetComponentsInChildren<AvatarMenuExclusions>(true)) Object.DestroyImmediate(marker);
            }
        }
    }
}
