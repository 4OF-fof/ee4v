using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AvatarEditing;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Ee4v.ExpressionMenu
{
    internal static partial class ExpressionMenuModel
    {
        private sealed class MenuReference
        {
            internal Component Owner;
            internal string PropertyPath;
            internal VRCExpressionsMenu Menu;
        }

        internal static Func<VRCExpressionsMenu, bool> CreateEditableCopyCheck(AvatarEditingContext context)
        {
            if (context.Root == null || EditorUtility.IsPersistent(context.Root) ||
                !context.Edits.CanEditPrefab() || EditorApplication.isPlayingOrWillChangePlaymode) return _ => false;
            var path = context.Edits.GetAssetPath();
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'))) return _ => false;
            var references = ReadMenuReferences(context.Root);
            return menu =>
            {
                if (menu == null) return false;
                var menus = MenusToCopy(menu, references);
                return references.Any(reference => menus.Contains(reference.Menu)) &&
                    references.Where(reference => menus.Contains(reference.Menu)).All(reference => CanWrite(reference.Owner));
            };
        }

        internal static bool CanCreateEditableCopy(AvatarEditingContext context, VRCExpressionsMenu menu) =>
            menu != null && CreateEditableCopyCheck(context)(menu);

        internal static Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> CreateEditableCopy(
            AvatarEditingContext context, VRCExpressionsMenu menu)
        {
            if (!CanCreateEditableCopy(context, menu)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
            if (!context.Edits.FlushChanges()) return null;
            if (!CanCreateEditableCopy(context, menu)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
            var references = ReadMenuReferences(context.Root);
            var originals = MenusToCopy(menu, references);
            var replacements = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();
            var createdPaths = new List<string>();
            var prefabPath = context.Edits.GetAssetPath();
            var directory = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
            var folderName = Path.GetFileNameWithoutExtension(prefabPath) + ".ExpressionMenu";
            var folder = directory + "/" + folderName;
            var createdFolder = false;
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Editable Expression Menu Copy");
            try
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(directory, folderName)))
                        throw new InvalidOperationException(TemplateText.Get("saveFailed"));
                    createdFolder = true;
                }
                foreach (var original in originals)
                {
                    var copy = Object.Instantiate(original);
                    copy.name = original.name;
                    copy.hideFlags = HideFlags.None;
                    var name = original.name;
                    foreach (var character in Path.GetInvalidFileNameChars()) name = name.Replace(character, '_');
                    if (string.IsNullOrWhiteSpace(name)) name = "Menu";
                    var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset");
                    createdPaths.Add(path);
                    try
                    {
                        AssetDatabase.CreateAsset(copy, path);
                        replacements.Add(original, copy);
                        if (!CanWrite(copy)) throw new InvalidOperationException(TemplateText.Get("readOnly"));
                    }
                    catch
                    {
                        if (!AssetDatabase.Contains(copy)) Object.DestroyImmediate(copy);
                        throw;
                    }
                }
                foreach (var copy in replacements.Values)
                {
                    foreach (var control in copy.controls)
                        if (control?.subMenu != null && replacements.TryGetValue(control.subMenu, out var submenu))
                            control.subMenu = submenu;
                    EditorUtility.SetDirty(copy);
                    AssetDatabase.SaveAssetIfDirty(copy);
                }
                var changed = references.Where(reference => replacements.ContainsKey(reference.Menu))
                    .GroupBy(reference => reference.Owner).ToArray();
                Undo.RecordObjects(changed.Select(referencesByOwner => (Object)referencesByOwner.Key).ToArray(),
                    "Replace Expression Menu References");
                foreach (var referencesByOwner in changed)
                {
                    var serialized = new SerializedObject(referencesByOwner.Key);
                    foreach (var reference in referencesByOwner)
                    {
                        var property = serialized.FindProperty(reference.PropertyPath);
                        if (property == null || property.objectReferenceValue != reference.Menu)
                            throw new InvalidOperationException("The menu source changed. Select the menu again.");
                        property.objectReferenceValue = replacements[reference.Menu];
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(referencesByOwner.Key);
                    EditorUtility.SetDirty(referencesByOwner.Key);
                }
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
                return replacements;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                foreach (var path in createdPaths) AssetDatabase.DeleteAsset(path);
                if (createdFolder) AssetDatabase.DeleteAsset(folder);
                throw;
            }
        }

        private static List<MenuReference> ReadMenuReferences(GameObject root)
        {
            var references = new List<MenuReference>();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue is VRCExpressionsMenu menu)
                        references.Add(new MenuReference { Owner = component, PropertyPath = property.propertyPath, Menu = menu });
            }
            return references;
        }

        private static HashSet<VRCExpressionsMenu> MenusToCopy(VRCExpressionsMenu menu, List<MenuReference> references)
        {
            var reachable = new HashSet<VRCExpressionsMenu>();
            void Visit(VRCExpressionsMenu current, HashSet<VRCExpressionsMenu> visited)
            {
                if (current == null || !visited.Add(current)) return;
                foreach (var control in current.controls)
                    if (control != null) Visit(control.subMenu, visited);
            }
            foreach (var reference in references) Visit(reference.Menu, reachable);
            var copies = new HashSet<VRCExpressionsMenu>();
            if (!reachable.Contains(menu)) return copies;
            Visit(menu, copies);
            bool added;
            do
            {
                added = false;
                foreach (var ancestor in reachable)
                    if (!copies.Contains(ancestor) && ancestor.controls.Any(control =>
                            control?.subMenu != null && copies.Contains(control.subMenu)))
                        added |= copies.Add(ancestor);
            } while (added);
            return copies;
        }
    }
}
