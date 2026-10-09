using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ee4v.AvatarEditing;
using Ee4v.Core.AvatarEvaluation;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Ee4v.ExpressionMenu
{
    /// <summary>Authoring operations shared with the UI; no MCP types or database access.</summary>
    public static partial class ExpressionMenuApi
    {
        [Serializable] public sealed class Snapshot
        {
            public string Revision;
            public List<Page> Pages = new List<Page>();
            public List<Entry> Entries = new List<Entry>();
        }
        [Serializable] public sealed class Page
        {
            public string Path, Name, AssetPath;
            public bool CanAdd, CanCopy;
        }
        [Serializable] public sealed class Entry
        {
            public string Path, Name, Type, IconPath, Parameter, SourceAssetPath, SourceObjectId, SourceRevision;
            public string[] SubParameters;
            public float Value, InitialValue;
            public bool Editable, HasSubmenu, Cyclic, Owned, Synced, Saved, CanEditActions;
            public List<ActionData> Actions = new List<ActionData>();
        }

        public static Snapshot Inspect(GameObject avatar)
        {
            var context = Context(avatar);
            var root = ExpressionMenuModel.Read(avatar, out _);
            var result = new Snapshot();
            var ancestors = new HashSet<AvatarMenuPage>();
            var canCopy = ExpressionMenuModel.CreateEditableCopyCheck(context);
            void Visit(AvatarMenuPage page, string path, int depth)
            {
                if (page == null || !ancestors.Add(page)) return;
                if (depth > 32 || result.Entries.Count + page.Controls.Count > 2048)
                    throw new InvalidOperationException("Menu exceeds inspection depth or entry limits.");
                result.Pages.Add(new Page
                {
                    Path = path, Name = page.Name, AssetPath = AssetDatabase.GetAssetPath(page.Asset),
                    CanAdd = context.Edits.CanEditPrefab() && ExpressionMenuModel.CanWrite(context.PrefabAsset) &&
                        (page.ChildRoot == null || ExpressionMenuTemplateModel.CanEdit(context, page.ChildRoot)),
                    CanCopy = canCopy(page.Asset)
                });
                for (var i = 0; i < page.Controls.Count; i++)
                {
                    var entry = page.Controls[i];
                    var item = entry.Owner as ModularAvatarMenuItem;
                    var entryPath = path.Length == 0 ? i.ToString() : path + "/" + i;
                    var recipe = ExpressionMenuAnimationRecipe.Find(item);
                    result.Entries.Add(new Entry
                    {
                        Path = entryPath, Name = entry.Control.name, Type = entry.Control.type.ToString(),
                        IconPath = AssetDatabase.GetAssetPath(entry.Control.icon), Parameter = entry.Control.parameter?.name,
                        SubParameters = (entry.Control.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>()).Select(value => value?.name).ToArray(),
                        Value = entry.Control.value, HasSubmenu = entry.Submenu != null, Cyclic = entry.Submenu != null && ancestors.Contains(entry.Submenu),
                        SourceAssetPath = AssetDatabase.GetAssetPath(entry.Owner), SourceObjectId = entry.Owner == null ? null : GlobalObjectId.GetGlobalObjectIdSlow(entry.Owner).ToString(),
                        SourceRevision = SourceRevision(entry.Owner, item, recipe),
                        Editable = context.Edits.CanEditPrefab() && ExpressionMenuModel.CanEdit(entry) &&
                            (item == null || ExpressionMenuTemplateModel.CanEdit(context, item)),
                        Owned = item != null && ExpressionMenuTemplateModel.IsOwned(item.gameObject),
                        Synced = item != null && item.isSynced, Saved = item != null && item.isSaved,
                        InitialValue = item != null && ExpressionMenuAnimationRecipe.EffectiveMode(item) == MenuBehaviorMode.Radial ? recipe?.InitialValue ?? 0 :
                            item != null && item.isDefault ? 1 : 0,
                        CanEditActions = item != null && ExpressionMenuTemplateModel.IsOwned(item.gameObject) && ExpressionMenuTemplateModel.CanEdit(context, item),
                        Actions = ReadActions(context, item, recipe)
                    });
                    if (entry.Submenu != null) Visit(entry.Submenu, entryPath, depth + 1);
                }
                ancestors.Remove(page);
            }
            Visit(root, string.Empty, 0);
            using (var hash = SHA256.Create())
                result.Revision = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(result)))).Replace("-", "").ToLowerInvariant();
            return result;
        }

        public static Snapshot Create(GameObject avatar, string pagePath, string name, bool submenu, string expectedRevision)
        {
            CheckRevision(avatar, expectedRevision);
            var context = EditableContext(avatar);
            var page = PageAt(ExpressionMenuModel.Read(avatar, out _), pagePath);
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A menu item name is required.");
            ExpressionMenuTemplateModel.Create(context, name.Trim(), page.Asset, page.ChildRoot, submenu);
            Notify(avatar);
            return Inspect(avatar);
        }

        public static Snapshot Edit(GameObject avatar, string entryPath, string expectedRevision,
            string name = null, string iconPath = null, float? initialValue = null)
        {
            CheckRevision(avatar, expectedRevision);
            var context = EditableContext(avatar);
            var entry = EntryAt(ExpressionMenuModel.Read(avatar, out _), entryPath);
            if (name == null && iconPath == null && !initialValue.HasValue) throw new ArgumentException("Supply an edit.");
            if (name != null && string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name may not be empty.");
            var icon = iconPath == null || iconPath.Length == 0 ? null : LoadAsset<Texture2D>(iconPath);
            var item = entry.Owner as ModularAvatarMenuItem;
            if (item != null && !ExpressionMenuTemplateModel.CanEdit(context, item)) throw new InvalidOperationException("Menu item is read-only.");
            if (initialValue.HasValue)
            {
                if (item == null) throw new InvalidOperationException("Initial values require a MA menu item.");
                var mode = ExpressionMenuAnimationRecipe.EffectiveMode(item);
                if (float.IsNaN(initialValue.Value) || float.IsInfinity(initialValue.Value) ||
                    mode == MenuBehaviorMode.Toggle && initialValue != 0 && initialValue != 1 ||
                    mode == MenuBehaviorMode.Radial && (initialValue < 0 || initialValue > 100) ||
                    mode != MenuBehaviorMode.Toggle && mode != MenuBehaviorMode.Radial)
                    throw new ArgumentException("Toggle initial value is 0/1, Radial initial value is 0–100; Button/Puppet do not have initial values.");
            }
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            try
            {
                var control = ExpressionMenuModel.Copy(entry.Control);
                if (name != null) control.name = name.Trim();
                if (iconPath != null) control.icon = icon;
                if (item == null) ExpressionMenuModel.Update(entry, control);
                else ExpressionMenuTemplateModel.Edit(context, item, () =>
                {
                    item.PortableControl.SetFrom(control);
                    if (name != null) item.label = control.name;
                    if (initialValue.HasValue)
                    {
                        var recipe = ExpressionMenuAnimationRecipe.Find(item);
                        if (ExpressionMenuAnimationRecipe.EffectiveMode(item) == MenuBehaviorMode.Toggle) item.isDefault = initialValue == 1;
                        else
                        {
                            if (recipe == null) throw new InvalidOperationException("Generated recipe is required for a Radial initial value.");
                            Undo.RecordObject(recipe, "Edit menu initial value");
                            recipe.InitialValue = initialValue.Value;
                            EditorUtility.SetDirty(recipe);
                            AssetDatabase.SaveAssetIfDirty(recipe);
                        }
                    }
                });
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            Notify(avatar);
            return Inspect(avatar);
        }

        public static Snapshot SetType(GameObject avatar, string path, string type, bool discardSettings, string expectedRevision)
        {
            CheckRevision(avatar, expectedRevision);
            var context = EditableContext(avatar);
            var item = OwnedItem(EntryAt(ExpressionMenuModel.Read(avatar, out _), path));
            if (!Enum.TryParse(type, false, out VRCExpressionsMenu.Control.ControlType controlType) ||
                !Enum.IsDefined(typeof(VRCExpressionsMenu.Control.ControlType), controlType) || controlType == VRCExpressionsMenu.Control.ControlType.SubMenu)
                throw new ArgumentException("Use Toggle, RadialPuppet, Button, TwoAxisPuppet or FourAxisPuppet.");
            var mode = controlType == VRCExpressionsMenu.Control.ControlType.Toggle ? MenuBehaviorMode.Toggle :
                controlType == VRCExpressionsMenu.Control.ControlType.RadialPuppet ? MenuBehaviorMode.Radial :
                controlType == VRCExpressionsMenu.Control.ControlType.Button ? MenuBehaviorMode.Button : MenuBehaviorMode.Puppet;
            if (item.Control.type != controlType && ExpressionMenuAnimationRecipe.HasSettingsToDiscard(item) && !discardSettings)
                throw new InvalidOperationException("This type change discards actions and initial values. Set discardSettings explicitly.");
            ExpressionMenuAnimationRecipe.SetModeDiscardingSettings(context, item, mode,
                controlType == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet ? 4 : 2);
            Notify(avatar);
            return Inspect(avatar);
        }

        public static Snapshot Move(GameObject avatar, string path, string destinationPage, string beforeEntry, string expectedRevision)
        {
            CheckRevision(avatar, expectedRevision);
            var context = EditableContext(avatar);
            var root = ExpressionMenuModel.Read(avatar, out _);
            var entry = EntryAt(root, path);
            if (beforeEntry != null) ExpressionMenuModel.Move(entry, EntryAt(root, beforeEntry));
            else ExpressionMenuModel.Relocate(context, entry, PageAt(root, destinationPage));
            Notify(avatar);
            return Inspect(avatar);
        }

        public static Snapshot Remove(GameObject avatar, string path, bool includeChildren, string expectedRevision)
        {
            CheckRevision(avatar, expectedRevision);
            EditableContext(avatar);
            var entry = EntryAt(ExpressionMenuModel.Read(avatar, out _), path);
            if (entry.Submenu?.Controls.Count > 0 && !includeChildren) throw new InvalidOperationException("Submenu is not empty. Set includeChildren explicitly.");
            ExpressionMenuModel.Remove(entry);
            Notify(avatar);
            return Inspect(avatar);
        }

        public static Snapshot CopyEditable(GameObject avatar, string pagePath, string expectedRevision)
        {
            CheckRevision(avatar, expectedRevision);
            var context = EditableContext(avatar);
            var page = PageAt(ExpressionMenuModel.Read(avatar, out _), pagePath);
            ExpressionMenuModel.CreateEditableCopy(context, page.Asset);
            Notify(avatar);
            return Inspect(avatar);
        }

        public static byte[] RenderPreview(GameObject avatar, string path, float[] values, float elapsed, int width, int height, out string parameterOutputs)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use runtime controls and lighting capture in Play Mode.");
            if (width < 64 || width > 1024 || height < 64 || height > 1024 || float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0)
                throw new ArgumentException("Invalid preview dimensions or elapsed time.");
            var context = Context(avatar);
            var entry = EntryAt(ExpressionMenuModel.Read(avatar, out _), path);
            var item = entry.Owner as ModularAvatarMenuItem ?? throw new InvalidOperationException("SDK-only controls require Play Mode preview.");
            var axes = ExpressionMenuAnimationRecipe.AxisCount(item);
            if (values == null || values.Length != axes || values.Any(value => float.IsNaN(value) || float.IsInfinity(value) ||
                value < ExpressionMenuAnimationRecipe.SourceMinimum(item) || value > 1)) throw new ArgumentException("Supply one normalized input for each control axis.");
            using (var animation = ExpressionMenuAnimationRecipe.CreatePreviewAnimation(context, item))
            using (var renderer = new AvatarPreviewRenderer(avatar, isolatedSnapshot: true))
            {
                renderer.SetAnimationSampler(frame => animation.Sample(frame, values, elapsed));
                renderer.SampleAnimation(elapsed);
                var renderers = renderer.Root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Avatar has no renderers.");
                var bounds = renderer.GetBounds(renderers[0]);
                foreach (var source in renderers.Skip(1)) bounds.Encapsulate(renderer.GetBounds(source));
                var distance = Mathf.Max(0.1f, bounds.extents.magnitude) / Mathf.Sin(15 * Mathf.Deg2Rad) * Mathf.Max(1, (float)height / width);
                renderer.Camera.transform.rotation = renderer.Root.transform.rotation * Quaternion.Euler(0, 180, 0);
                renderer.Camera.transform.position = bounds.center - renderer.Camera.transform.forward * distance;
                parameterOutputs = animation.DescribeParameters(values);
                return renderer.RenderPng(width, height);
            }
        }

        private static AvatarEditingContext Context(GameObject avatar)
        {
            if (avatar == null) throw new ArgumentNullException(nameof(avatar));
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(avatar);
            bool CanEdit() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorUtility.IsPersistent(avatar) &&
                avatar.scene.IsValid() && PrefabUtility.IsAnyPrefabInstanceRoot(avatar) && ExpressionMenuModel.CanWrite(avatar) &&
                prefab != null && ExpressionMenuModel.CanWrite(prefab);
            Action changed = () => Notify(avatar);
            return new AvatarEditingContext(new AvatarEditingServices(CanEdit, _ => false, () => true,
                () => AssetDatabase.GetAssetPath(prefab), _ => changed(), changed, message => { throw new InvalidOperationException(message); }, _ => { }),
                new AvatarEditingHost(changed, changed, () => { }, UnityEditorInternal.InternalEditorUtility.RepaintAllViews, () => { }))
                { Root = avatar, PrefabAsset = prefab };
        }
        private static AvatarEditingContext EditableContext(GameObject avatar)
        {
            var context = Context(avatar);
            if (!context.Edits.CanEditPrefab()) throw new InvalidOperationException("An editable avatar Prefab instance in Edit Mode is required.");
            return context;
        }
        private static void Notify(GameObject avatar)
        {
            if (!EditorUtility.IsPersistent(avatar) && avatar.scene.IsValid()) EditorSceneManager.MarkSceneDirty(avatar.scene);
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        private static void CheckRevision(GameObject avatar, string expected)
        {
            if (string.IsNullOrEmpty(expected) || Inspect(avatar).Revision != expected) throw new InvalidOperationException("Menu revision changed. Inspect again before editing.");
        }
        private static AvatarMenuPage PageAt(AvatarMenuPage root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root;
            return EntryAt(root, path).Submenu ?? throw new ArgumentException("Path is not a submenu.");
        }
        private static AvatarMenuEntry EntryAt(AvatarMenuPage root, string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("An entry path from Inspect is required.");
            var parts = path.Split('/');
            if (parts.Length > 32) throw new ArgumentException("Menu path is too deep.");
            AvatarMenuEntry entry = null;
            foreach (var part in parts)
            {
                if (root == null || !int.TryParse(part, out var index) || index < 0 || index >= root.Controls.Count) throw new ArgumentException("Invalid menu path.");
                entry = root.Controls[index];
                root = entry.Submenu;
            }
            return entry;
        }
        private static ModularAvatarMenuItem OwnedItem(AvatarMenuEntry entry)
        {
            var item = entry.Owner as ModularAvatarMenuItem;
            if (item == null || !ExpressionMenuTemplateModel.IsOwned(item.gameObject)) throw new InvalidOperationException("This operation requires an ee4v-generated menu item.");
            return item;
        }
        private static T LoadAsset<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new ArgumentException("Asset not found: " + path);

        private static string SourceRevision(UnityEngine.Object owner, ModularAvatarMenuItem item, ExpressionMenuAnimationRecipe recipe)
        {
            var serialized = owner == null ? "" : EditorJsonUtility.ToJson(owner);
            if (recipe != null) serialized += EditorJsonUtility.ToJson(recipe);
            if (item != null) foreach (var effect in ExpressionMenuTemplateModel.Effects(item)) serialized += EditorJsonUtility.ToJson(effect);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(serialized))).Replace("-", "");
        }
    }
}
