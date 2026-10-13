using System;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.AvatarEvaluation;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Ee4v.ExpressionMenu
{
    internal static partial class ExpressionMenuModel
    {
        internal static bool IsGenerated(AvatarMenuEntry entry) => entry.Owner is ModularAvatarMenuItem item &&
            ExpressionMenuTemplateModel.IsOwned(item.gameObject);

        internal static bool CanExclude(AvatarEditingContext context, AvatarMenuEntry entry) => entry?.Owner != null &&
            context.Edits.CanEditPrefab() && CanWrite(context.Root) && CanWrite(context.PrefabAsset) &&
            (!(entry.Owner is VRCExpressionsMenu menu) ||
                menu.controls.Count(control => AvatarMenuExclusionUtility.SameControl(control, entry.SourceControl)) == 1);

        internal static void Exclude(AvatarEditingContext context, AvatarMenuEntry entry)
        {
            if (!CanExclude(context, entry)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
            AddExclusion(context, new AvatarMenuExclusion
            {
                Source = entry.Owner, SourceIndex = entry.Index,
                Control = Copy(entry.SourceControl ?? entry.Control), DisplayName = entry.Control.name
            });
        }

        internal static void ExcludeInstaller(AvatarEditingContext context, ModularAvatarMenuInstaller installer)
        {
            if (installer == null || ExpressionMenuInstaller.IsGeneratedPrefab(installer.gameObject) ||
                !installer.transform.IsChildOf(context.Root.transform) ||
                !context.Edits.CanEditPrefab() || !CanWrite(context.Root) || !CanWrite(context.PrefabAsset))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            AddExclusion(context, new AvatarMenuExclusion { Source = installer, DisplayName = installer.gameObject.name });
        }

        private static void AddExclusion(AvatarEditingContext context, AvatarMenuExclusion record)
        {
            if (AvatarMenuExclusionUtility.Entries(context.Root).Any(existing => existing.Source == record.Source &&
                (!(record.Source is VRCExpressionsMenu) || AvatarMenuExclusionUtility.SameControl(existing.Control, record.Control)))) return;
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Exclude Expression Menu Item");
            var marker = context.Root.GetComponent<AvatarMenuExclusions>() ?? Undo.AddComponent<AvatarMenuExclusions>(context.Root);
            if (!CanWrite(marker)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
            Undo.RecordObject(marker, "Exclude Expression Menu Item");
            record.Id = Guid.NewGuid().ToString("N");
            marker.Entries.Add(record);
            PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            EditorUtility.SetDirty(marker);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
        }

        internal static void Restore(AvatarEditingContext context, string id)
        {
            if (!context.Edits.CanEditPrefab() || !CanWrite(context.Root) || !CanWrite(context.PrefabAsset))
                throw new InvalidOperationException(TemplateText.Get("readOnly"));
            var marker = context.Root.GetComponentsInChildren<AvatarMenuExclusions>(true)
                .FirstOrDefault(value => value.Entries.Any(record => record.Id == id));
            if (marker == null) throw new InvalidOperationException("The excluded menu item no longer exists.");
            if (!CanWrite(marker)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
            Undo.RecordObject(marker, "Restore Expression Menu Item");
            marker.Entries.RemoveAll(record => record.Id == id);
            PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            EditorUtility.SetDirty(marker);
        }
    }
}
