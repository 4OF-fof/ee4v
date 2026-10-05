using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.I18n;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Ee4v.ExpressionMenu
{
    public sealed class ExpressionMenuView : VisualElement
    {
        private readonly AvatarEditingContext _context;
        private readonly List<int> _path = new List<int>();
        private List<MenuPage> _sources;
        private MenuPage _root;
        private MenuPage _page;
        private int _source;
        private int _offset;
        private int _selected = -1;
        private string _error;
        private VRCExpressionsMenu _focusAsset;
        private static string T(string key) => I18N.Get("expressionMenu." + key);

        public ExpressionMenuView(AvatarEditingContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            UiComposition.Prepare(this, "Editor/Feature/Avatar/ExpressionMenu/expression-menu.uss");
            AddToClassList("ee4v-expression-menu");
            Refresh();
        }

        private void Refresh()
        {
            Clear();
            try
            {
                if (_context.Root == null) return;
                _root = ExpressionMenuModel.Read(_context.Root, out _sources);
                if (_focusAsset != null)
                {
                    var index = _sources.FindIndex(page => page.Asset == _focusAsset);
                    if (index >= 0) { _source = index + 1; _path.Clear(); _offset = 0; _selected = _focusAsset.controls.Count - 1; }
                    _focusAsset = null;
                }
                if (_source > _sources.Count) _source = 0;
                _page = _source == 0 ? _root : _sources[_source - 1];
                foreach (var index in _path.ToArray())
                {
                    if (index >= _page.Entries.Count || _page.Entries[index].Submenu == null)
                    { _path.Clear(); _page = _source == 0 ? _root : _sources[_source - 1]; break; }
                    _page = _page.Entries[index].Submenu;
                }
                _offset = Mathf.Clamp(_offset, 0, Mathf.Max(0, (_page.Entries.Count - 1) / 7 * 7));
                if (_selected >= _page.Entries.Count) _selected = -1;
                Build();
            }
            catch (Exception exception)
            {
                Add(UiTextFactory.CreateHelpBox(exception.GetBaseException().Message, HelpBoxMessageType.Error));
                Add(new UiButton(T("refresh"), Refresh));
            }
        }

        private void Build()
        {
            Add(UiTextFactory.Create(T("title"), UiClassNames.SectionTitle));
            var choices = new List<string> { T("effective") };
            choices.AddRange(_sources.Select((page, i) => (i + 1) + ": " + page.Name));
            var source = UiTextFactory.CreatePopupField(T("view"), choices, _source);
            source.RegisterValueChangedCallback(evt =>
            {
                _source = choices.IndexOf(evt.newValue);
                _path.Clear(); _offset = 0; _selected = -1; Refresh();
            });
            Add(source);
            Add(UiTextFactory.Create(_page.Name, UiClassNames.SecondaryText));
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-expression-menu__toolbar");
            toolbar.Add(new UiButton(T("refresh"), Refresh, variant: UiButtonVariant.Ghost));
            toolbar.Add(new UiButton(T("saveAssets"), () => Run(() =>
            {
                foreach (var asset in _sources.Select(page => page.Asset).Where(asset => asset != null).Distinct())
                    if (ExpressionMenuModel.CanWrite(asset)) AssetDatabase.SaveAssetIfDirty(asset);
            }), variant: UiButtonVariant.Ghost));
            Add(toolbar);
            Add(UiTextFactory.CreateHelpBox(T("sourceNotice"), HelpBoxMessageType.Info));
            if (!string.IsNullOrEmpty(_error))
                Add(UiTextFactory.CreateHelpBox(_error, HelpBoxMessageType.Error));
            BuildRadial();
            var add = new UiButton(T("add"), () => Run(AddControl));
            add.SetEnabled(_context.Edits.CanEditPrefab() && !EditorApplication.isPlayingOrWillChangePlaymode);
            Add(add);
            if (_selected >= 0 && _selected < _page.Entries.Count) BuildSettings(_page.Entries[_selected]);
        }

        private void BuildRadial()
        {
            var ring = new VisualElement();
            ring.AddToClassList("ee4v-expression-menu__ring");
            Add(ring);
            var paged = _page.Entries.Count > 8;
            var count = Math.Min(paged ? 7 : 8, _page.Entries.Count - _offset);
            for (var slot = 0; slot < count; slot++)
            {
                var index = _offset + slot;
                var entry = _page.Entries[index];
                var button = new UiButton(entry.Control.name, () =>
                { _selected = index; _error = null; Refresh(); }, variant: UiButtonVariant.Ghost);
                button.tooltip = entry.Control.type + " / " + (entry.Owner == null ? T("ambiguous") : entry.Owner.name);
                button.LabelText.SetWhiteSpace(WhiteSpace.Normal);
                button.SetLabelTextAlign(TextAnchor.MiddleCenter);
                button.SetContentAlignment(Justify.Center);
                button.EnableInClassList("ee4v-expression-menu__slot--selected", _selected == index);
                if (entry.Control.icon != null)
                    button.Insert(0, new Image { image = entry.Control.icon, scaleMode = ScaleMode.ScaleToFit,
                        pickingMode = PickingMode.Ignore });
                PlaceSlot(button, slot);
                ring.Add(button);
            }
            if (paged && _offset + count < _page.Entries.Count)
            {
                var next = new UiButton(T("next"), () => { _offset += 7; _selected = -1; Refresh(); });
                PlaceSlot(next, 7); ring.Add(next);
            }
            var back = new UiButton(T("back"), () =>
            {
                if (_offset > 0) _offset = Math.Max(0, _offset - 7);
                else if (_path.Count > 0) _path.RemoveAt(_path.Count - 1);
                _selected = -1; Refresh();
            }, variant: UiButtonVariant.Ghost);
            back.AddToClassList("ee4v-expression-menu__back");
            back.SetEnabled(_offset > 0 || _path.Count > 0);
            ring.Add(back);
            if (_page.Entries.Count == 0) Add(UiTextFactory.Create(T("empty"), UiClassNames.SecondaryText));
        }

        private static void PlaceSlot(VisualElement slot, int index)
        {
            slot.AddToClassList("ee4v-expression-menu__slot");
            slot.AddToClassList("ee4v-expression-menu__slot-" + index);
        }

        private void BuildSettings(MenuEntry entry)
        {
            Add(UiTextFactory.Create(T("settings"), UiClassNames.SectionTitle));
            var owner = UiTextFactory.CreateObjectField(T("source"));
            owner.SetValueWithoutNotify(entry.Owner); owner.SetEnabled(false); Add(owner);
            if (entry.Submenu != null)
                Add(new UiButton(T("openSubmenu"), () =>
                { _path.Add(_selected); _selected = -1; _offset = 0; Refresh(); }));
            if (!entry.CanEdit || !_context.Edits.CanEditPrefab())
            {
                Add(UiTextFactory.CreateHelpBox(T("readOnly"), HelpBoxMessageType.Info)); return;
            }
            var draft = ExpressionMenuModel.Copy(entry.Control);
            // Virtual controls deliberately have no serialized submenu asset reference.
            if (entry.Owner is VRCExpressionsMenu menu) draft.subMenu = menu.controls[entry.Index].subMenu;
            else if (entry.Owner is ModularAvatarMenuItem item) draft.subMenu = item.Control.subMenu;
            var sourcePath = EditorUtility.IsPersistent(entry.Owner) ? AssetDatabase.GetAssetPath(entry.Owner) :
                AnimationUtility.CalculateTransformPath(((Component)entry.Owner).transform, _context.Root.transform);
            var pathLabel = UiTextFactory.Create(sourcePath, UiClassNames.SecondaryText);
            pathLabel.SetWhiteSpace(WhiteSpace.Normal);
            Add(pathLabel);
            var confirmed = UiTextFactory.CreateToggle(T("confirmSource"));
            Add(confirmed);
            var maSource = entry.Owner as ModularAvatarMenuItem;
            var synced = maSource != null && maSource.isSynced;
            var saved = maSource != null && maSource.isSaved;
            var defaultValue = maSource != null && maSource.isDefault;
            var automaticValue = maSource != null && maSource.automaticValue;
            if (maSource != null)
            {
                AddToggle(T("synced"), synced, value => synced = value);
                AddToggle(T("saved"), saved, value => saved = value);
                AddToggle(T("defaultValue"), defaultValue, value => defaultValue = value);
                AddToggle(T("automaticValue"), automaticValue, value => automaticValue = value);
            }
            void ApplyDraft()
            {
                ExpressionMenuModel.Update(entry, draft);
                if (maSource != null)
                {
                    maSource.isSynced = synced; maSource.isSaved = saved;
                    maSource.isDefault = defaultValue; maSource.automaticValue = automaticValue;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(maSource);
                    EditorUtility.SetDirty(maSource);
                }
                NotifyChanged();
            }
            AddText(T("name"), draft.name, value => draft.name = value);
            var icon = UiTextFactory.CreateObjectField(T("icon"));
            icon.objectType = typeof(Texture2D); icon.allowSceneObjects = false;
            icon.SetValueWithoutNotify(draft.icon);
            icon.RegisterValueChangedCallback(evt => draft.icon = evt.newValue as Texture2D); Add(icon);
            var type = UiTextFactory.CreateEnumField(T("type"), draft.type);
            Add(type);
            var details = new VisualElement(); Add(details);
            void BuildDetails()
            {
                details.Clear();
                AddText(T("parameter"), draft.parameter?.name, value =>
                    draft.parameter = new VRCExpressionsMenu.Control.Parameter { name = value }, details);
                var valueField = UiTextFactory.CreateFloatField(T("value"));
                valueField.SetValueWithoutNotify(draft.value);
                valueField.RegisterValueChangedCallback(evt => draft.value = evt.newValue); details.Add(valueField);
                if (draft.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    var submenu = UiTextFactory.CreateObjectField(T("submenu"));
                    submenu.objectType = typeof(VRCExpressionsMenu); submenu.allowSceneObjects = false;
                    submenu.SetValueWithoutNotify(draft.subMenu);
                    submenu.RegisterValueChangedCallback(evt => draft.subMenu = evt.newValue as VRCExpressionsMenu);
                    submenu.SetEnabled(!(entry.Owner is ModularAvatarMenuItem ma && ma.MenuSource == SubmenuSource.Children));
                    details.Add(submenu);
                    var createSubmenu = new UiButton(T("createSubmenu"), () => Run(() =>
                    {
                        var path = AssetDatabase.GetAssetPath(entry.Owner is VRCExpressionsMenu asset ? asset : _context.PrefabAsset);
                        var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                        newMenu.name = string.IsNullOrWhiteSpace(draft.name) ? "Submenu" : draft.name;
                        var newPath = AssetDatabase.GenerateUniqueAssetPath(
                            System.IO.Path.GetDirectoryName(path).Replace('\\', '/') + "/Submenu.asset");
                        AssetDatabase.CreateAsset(newMenu, newPath);
                        Undo.RegisterCreatedObjectUndo(newMenu, "Create Expression Submenu");
                        try { draft.subMenu = newMenu; ApplyDraft(); }
                        catch { AssetDatabase.DeleteAsset(newPath); throw; }
                    }));
                    var supportsAsset = !(entry.Owner is ModularAvatarMenuItem childItem &&
                        childItem.MenuSource == SubmenuSource.Children);
                    createSubmenu.SetEnabled(confirmed.value && supportsAsset);
                    confirmed.RegisterValueChangedCallback(evt => createSubmenu.SetEnabled(evt.newValue && supportsAsset));
                    details.Add(createSubmenu);
                }
                var axes = draft.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet ? 1 :
                    draft.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ? 2 :
                    draft.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet ? 4 : 0;
                var old = draft.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>();
                draft.subParameters = Enumerable.Range(0, axes).Select(i => new VRCExpressionsMenu.Control.Parameter
                    { name = i < old.Length ? old[i]?.name ?? "" : "" }).ToArray();
                for (var axis = 0; axis < axes; axis++)
                {
                    var index = axis;
                    AddText(T("axis") + " " + (axis + 1), draft.subParameters[axis].name,
                        value => draft.subParameters[index].name = value, details);
                }
                if (axes > 1)
                {
                    var oldLabels = draft.labels ?? Array.Empty<VRCExpressionsMenu.Control.Label>();
                    draft.labels = Enumerable.Range(0, 4).Select(i => i < oldLabels.Length ? oldLabels[i] :
                        new VRCExpressionsMenu.Control.Label()).ToArray();
                    for (var axis = 0; axis < 4; axis++)
                    {
                        var index = axis;
                        AddText(T("axisLabel") + " " + (axis + 1), draft.labels[axis].name,
                            value => draft.labels[index].name = value, details);
                        var labelIcon = UiTextFactory.CreateObjectField(T("icon") + " " + (axis + 1));
                        labelIcon.objectType = typeof(Texture2D); labelIcon.allowSceneObjects = false;
                        labelIcon.SetValueWithoutNotify(draft.labels[axis].icon);
                        labelIcon.RegisterValueChangedCallback(evt => draft.labels[index].icon = evt.newValue as Texture2D);
                        details.Add(labelIcon);
                    }
                }
            }
            type.RegisterValueChangedCallback(evt =>
            { draft.type = (VRCExpressionsMenu.Control.ControlType)evt.newValue; BuildDetails(); });
            BuildDetails();
            var apply = new UiButton(T("apply"), () => Run(() =>
            {
                ApplyDraft();
            }));
            var remove = new UiButton(T("remove"), () => Run(() =>
            {
                ExpressionMenuModel.Remove(entry); _selected = -1; NotifyChanged();
            }), variant: UiButtonVariant.Ghost);
            apply.SetEnabled(false); remove.SetEnabled(false);
            confirmed.RegisterValueChangedCallback(evt =>
            { apply.SetEnabled(evt.newValue); remove.SetEnabled(evt.newValue); });
            Add(apply); Add(remove);
        }

        private void AddText(string label, string value, Action<string> changed, VisualElement host = null)
        {
            var input = new InputField(new InputFieldState(value)) { IsDelayed = true };
            input.ValueChanged += changed;
            (host ?? this).Add(new FormInput(label, input));
        }

        private void AddToggle(string label, bool value, Action<bool> changed)
        {
            var toggle = UiTextFactory.CreateToggle(label);
            toggle.SetValueWithoutNotify(value);
            toggle.RegisterValueChangedCallback(evt => changed(evt.newValue));
            Add(toggle);
        }

        private void AddControl()
        {
            VRCExpressionsMenu target;
            var path = _page.Asset == null ? "" : AssetDatabase.GetAssetPath(_page.Asset);
            if (path.Contains(".ExpressionMenu/")) target = _page.Asset;
            else target = ExpressionMenuInstaller.EnsureMenu(_context.Root, _context.PrefabAsset, _page.Asset, _page.ChildRoot);
            if (!ExpressionMenuModel.CanWrite(target)) throw new InvalidOperationException(T("readOnly"));
            if (target.controls.Count >= 8) throw new InvalidOperationException(T("full"));
            Undo.RecordObject(target, "Add Expression Menu Control");
            target.controls.Add(new VRCExpressionsMenu.Control
            {
                name = T("newControl"), type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "" },
                subParameters = Array.Empty<VRCExpressionsMenu.Control.Parameter>(),
                labels = Array.Empty<VRCExpressionsMenu.Control.Label>(), value = 1
            });
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            _focusAsset = target; NotifyChanged();
        }

        private void NotifyChanged()
        {
            _context.Edits.WorkingSceneDirty = true;
            _context.Edits.Changed();
        }

        private void Run(Action action)
        {
            _error = null;
            try { action(); }
            catch (Exception exception) { _error = exception.GetBaseException().Message; }
            Refresh();
        }
    }
}
