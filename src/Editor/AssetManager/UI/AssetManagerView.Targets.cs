using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetManagerView
    {
        private VisualElement CreateTargetList(
            string itemId,
            IReadOnlyList<AssetFile> files)
        {
            var list = new VisualElement();
            list.AddToClassList("ee4v-asset-manager__target-list");
            var filesById = (files ?? Array.Empty<AssetFile>())
                .ToDictionary(file => file.Id, StringComparer.Ordinal);
            var entries = _manager.GetItemTargets(itemId)
                .Where(target => filesById.ContainsKey(target.FileId))
                .Select(target => new ItemTargetEntry
                {
                    File = filesById[target.FileId],
                    Target = target
                })
                .ToArray();
            if (entries.Length == 0)
            {
                list.Add(UiTextFactory.Create(I18N.Get("common.none")));
                return list;
            }

            var grouped = entries
                .Where(entry => !string.IsNullOrWhiteSpace(
                    entry.Target.GroupName))
                .GroupBy(
                    entry => entry.Target.GroupName,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var ungrouped = entries
                .Where(entry => string.IsNullOrWhiteSpace(
                    entry.Target.GroupName))
                .ToArray();
            for (var index = 0; index < ungrouped.Length; index++)
            {
                list.Add(CreateTargetRow(
                    itemId,
                    ungrouped[index],
                    entries,
                    false));
            }

            for (var groupIndex = 0;
                 groupIndex < grouped.Length;
                 groupIndex++)
            {
                var groupEntries = grouped[groupIndex].ToArray();
                list.Add(CreateTargetGroupRow(
                    itemId,
                    grouped[groupIndex].Key,
                    grouped[groupIndex].Key,
                    groupEntries.Select(entry => entry.Target).ToArray()));
                for (var entryIndex = 0;
                     entryIndex < groupEntries.Length;
                     entryIndex++)
                {
                    list.Add(CreateTargetRow(
                        itemId,
                        groupEntries[entryIndex],
                        entries,
                        true));
                }
            }

            RegisterTargetDrop(
                list,
                payload => SetTargetGroups(
                    itemId,
                    payload.Targets,
                    null));
            return list;
        }

        private VisualElement CreateTargetGroupRow(
            string itemId,
            string label,
            string groupName,
            IReadOnlyList<AssetFileTarget> targets)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-asset-manager__target-group-row");
            var icon = new Image
            {
                image = AssetManagerControls.LoadFluentIconTexture(
                    "folder.png"),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("ee4v-asset-manager__target-icon");
            row.Add(icon);
            row.Add(UiTextFactory.Create(
                label,
                UiClassNames.NavigationItemLabel,
                "ee4v-asset-manager__target-group-name"));
            row.Add(UiTextFactory.Create(
                targets.Count.ToString(),
                UiClassNames.SecondaryText,
                "ee4v-asset-manager__target-group-count"));
            RegisterTargetDrag(
                row,
                new TargetDragPayload
                {
                    GroupName = groupName,
                    Targets = targets
                },
                label);
            RegisterTargetDrop(
                row,
                payload => SetTargetGroups(
                    itemId,
                    payload.Targets,
                    groupName));
            return row;
        }

        private VisualElement CreateTargetRow(
            string itemId,
            ItemTargetEntry entry,
            IReadOnlyList<ItemTargetEntry> entries,
            bool grouped)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-asset-manager__target-item");
            row.EnableInClassList(
                "ee4v-asset-manager__target-tree-child",
                grouped);

            var icon = new Image
            {
                image = AssetManagerControls.LoadFluentIconTexture(
                    string.IsNullOrEmpty(entry.Target.TargetPath)
                        ? "folder_zip.png"
                        : "cube.png"),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("ee4v-asset-manager__target-icon");
            row.Add(icon);
            var value = UiTextFactory.Create(
                FormatTargetName(entry.File, entry.Target),
                UiClassNames.NavigationItemLabel,
                "ee4v-asset-manager__target-value");
            value.tooltip = (entry.File.FileName ?? entry.File.Id) +
                            " / " +
                            (string.IsNullOrEmpty(entry.Target.TargetPath)
                                ? entry.File.FileName
                                : entry.Target.TargetPath);
            row.Add(value);

            RegisterTargetDrag(
                row,
                new TargetDragPayload
                {
                    GroupName = entry.Target.GroupName,
                    Targets = new[] { entry.Target }
                },
                FormatTargetName(entry.File, entry.Target));
            RegisterTargetDrop(
                row,
                payload => GroupDroppedTargets(
                    itemId,
                    payload,
                    entry.Target,
                    entries));
            return row;
        }

        private void GroupDroppedTargets(
            string itemId,
            TargetDragPayload payload,
            AssetFileTarget target,
            IReadOnlyList<ItemTargetEntry> entries)
        {
            if (payload?.Targets == null ||
                payload.Targets.Any(source =>
                    AssetFileTarget.HasSameIdentity(source, target)))
            {
                return;
            }

            var groupName = target.GroupName;
            var targets = payload.Targets.ToList();
            if (string.IsNullOrWhiteSpace(groupName))
            {
                groupName = string.IsNullOrWhiteSpace(payload.GroupName)
                    ? CreateTargetGroupName(entries)
                    : payload.GroupName;
                targets.Add(target);
            }
            SetTargetGroups(itemId, targets, groupName);
        }

        private static string CreateTargetGroupName(
            IReadOnlyList<ItemTargetEntry> entries)
        {
            var names = new HashSet<string>(
                (entries ?? Array.Empty<ItemTargetEntry>())
                    .Select(entry => entry.Target.GroupName)
                    .Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);
            for (var index = 1; ; index++)
            {
                var candidate = "Group " + index;
                if (!names.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        private void SetTargetGroups(
            string itemId,
            IReadOnlyList<AssetFileTarget> targets,
            string groupName)
        {
            var changes = (targets ?? Array.Empty<AssetFileTarget>())
                .Where(target => target != null && !string.Equals(
                    target.GroupName,
                    groupName,
                    StringComparison.OrdinalIgnoreCase))
                .GroupBy(
                    target => target.FileId + "\n" + target.TargetPath,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
            if (changes.Length == 0)
            {
                return;
            }

            Run(() =>
            {
                for (var index = 0; index < changes.Length; index++)
                {
                    _manager.SetItemTargetGroup(
                        itemId,
                        changes[index].FileId,
                        changes[index].TargetPath,
                        groupName);
                }
            });
        }

        private static void RegisterTargetDrag(
            VisualElement element,
            TargetDragPayload payload,
            string label)
        {
            UiDragAndDrop.RegisterStart(
                element,
                TargetDragDataKey,
                () => payload,
                _ => label);
        }

        private static void RegisterTargetDrop(
            VisualElement element,
            Action<TargetDragPayload> onDrop)
        {
            UiDragAndDrop.RegisterMoveTarget(
                element,
                TargetDragDataKey,
                null,
                onDrop,
                active => element.EnableInClassList(
                    "ee4v-asset-manager__target-drop",
                    active));
        }

        private static string FormatTargetName(
            AssetFile file,
            AssetFileTarget target)
        {
            var fileName = string.IsNullOrWhiteSpace(file?.FileName)
                ? file?.Id ?? "file"
                : file.FileName;
            if (string.IsNullOrEmpty(target?.TargetPath))
            {
                return fileName;
            }

            var targetName = Path.GetFileName(target.TargetPath);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                targetName = target.TargetPath;
            }
            return string.Equals(
                    targetName,
                    fileName,
                    StringComparison.OrdinalIgnoreCase)
                ? targetName
                : targetName + "(" + fileName + ")";
        }

    }
}
