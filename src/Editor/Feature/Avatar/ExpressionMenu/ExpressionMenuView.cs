using System;
using System.Collections.Generic;
using System.Linq;
using BlackStartX.GestureManager.Editor.Modules.Vrc3;
using BlackStartX.GestureManager.Library.VisualElements;
using Ee4v.AvatarEditing;
using Ee4v.Core.I18n;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
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
        private MenuEntry _submenuEntry;
        private int _source;
        private int _offset;
        private int _selected = -1;
        private string _error;
        private bool _selectAddedControl;
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
                var sourcePage = _sources != null && _source > 0 && _source <= _sources.Count ? _sources[_source - 1] : null;
                _root = ExpressionMenuModel.Read(_context.Root, out _sources);
                if (sourcePage != null)
                {
                    var index = _sources.FindIndex(page => sourcePage.Asset != null ? page.Asset == sourcePage.Asset :
                        page.Asset == null && page.Entries.FirstOrDefault()?.Owner == sourcePage.Entries.FirstOrDefault()?.Owner);
                    _source = index + 1;
                    if (index < 0) { _path.Clear(); _offset = 0; _selected = -1; }
                }
                if (_source > _sources.Count) _source = 0;
                _page = _source == 0 ? _root : _sources[_source - 1];
                _submenuEntry = null;
                foreach (var index in _path.ToArray())
                {
                    if (index >= _page.Entries.Count || _page.Entries[index].Submenu == null)
                    { _path.Clear(); _submenuEntry = null; _page = _source == 0 ? _root : _sources[_source - 1]; break; }
                    _submenuEntry = _page.Entries[index];
                    _page = _submenuEntry.Submenu;
                }
                if (_selectAddedControl)
                {
                    _selected = _page.Entries.Count - 1;
                    _offset = _page.Entries.Count > 8 ? Math.Max(0, _selected / 7 * 7) : 0;
                    _selectAddedControl = false;
                }
                _offset = _page.Entries.Count > 8 ? Mathf.Clamp(_offset, 0, (_page.Entries.Count - 1) / 7 * 7) : 0;
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
            if (_selected >= 0 && _selected < _page.Entries.Count) BuildSettings(_page.Entries[_selected]);
            else if (_submenuEntry != null) BuildSettings(_submenuEntry, true);
        }

        private void BuildRadial()
        {
            var ring = new VisualElement();
            ring.AddToClassList("ee4v-expression-menu__ring");
            var labels = new VisualElement { pickingMode = PickingMode.Ignore };
            labels.AddToClassList("ee4v-expression-menu__labels");
            Add(ring);
            var paged = _page.Entries.Count > 8;
            var count = Math.Min(paged ? 7 : 8, _page.Entries.Count - _offset);
            var hasNext = paged && _offset + count < _page.Entries.Count;
            var slices = count + (hasNext ? 1 : 0) + 2;
            var addSlot = slices - 1;
            var back = new UiButton(T("back"), () =>
            {
                if (_offset > 0) _offset = Math.Max(0, _offset - 7);
                else if (_path.Count > 0) _path.RemoveAt(_path.Count - 1);
                _selected = -1; Refresh();
            }, variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SecondaryText);
            var backSlice = PlaceSlot(ring, labels, back, 0, slices, Resources.Load<Texture2D>("Vrc3/BSX_GM_Back"));
            backSlice.SetEnabled(_offset > 0 || _path.Count > 0);
            if (!backSlice.enabledSelf)
            {
                back.SetLabelColor(Color.gray);
                back.Content.Q<Image>().tintColor = Color.gray;
            }
            for (var slot = 0; slot < count; slot++)
            {
                var index = _offset + slot;
                var entry = _page.Entries[index];
                var button = new UiButton(entry.Control.name, () =>
                {
                    if (entry.Submenu != null) OpenSubmenu(index);
                    else SelectControl(index);
                }, variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SecondaryText);
                if (entry.Submenu != null)
                    button.AddManipulator(new ContextualMenuManipulator(evt =>
                        evt.menu.AppendAction(T("settings"), _ => SelectControl(index))));
                button.tooltip = entry.Control.name + "\n" + entry.Control.type + " / " +
                    (entry.Owner == null ? T("ambiguous") : entry.Owner.name);
                var icon = entry.Control.icon != null ? entry.Control.icon : Resources.Load<Texture2D>("Vrc3/BSX_GM_Default");
                PlaceSlot(ring, labels, button, slot + 1, slices, icon, _selected == index);
                var subIcon = RadialMenuUtility.GetSubIcon(entry.Control.type);
                if (subIcon != null)
                {
                    var indicator = new VisualElement { pickingMode = PickingMode.Ignore };
                    indicator.AddToClassList("ee4v-expression-menu__sub-icon");
                    indicator.Add(new Image { image = subIcon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore });
                    button.Content.Add(indicator);
                }
            }
            if (hasNext)
            {
                var next = new UiButton(T("next"), () => { _offset += 7; _selected = -1; Refresh(); },
                    variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SecondaryText);
                PlaceSlot(ring, labels, next, count + 1, slices, FluentUiIcons.LoadTexture("arrow_right.png"));
            }
            var add = new UiButton(T("addItem"), () => Run(AddControl), tooltip: T("add"), variant: UiButtonVariant.Ghost,
                labelTypographyClassName: UiClassNames.SecondaryText);
            var addSlice = PlaceSlot(ring, labels, add, addSlot, slices, FluentUiIcons.LoadTexture("add.png"));
            addSlice.SetEnabled(_context.Edits.CanEditPrefab() && !EditorApplication.isPlayingOrWillChangePlaymode);
            if (!addSlice.enabledSelf)
            {
                add.SetLabelColor(Color.gray);
                add.Content.Q<Image>().tintColor = Color.gray;
            }
            var center = RadialMenuUtility.Prefabs.NewCircle(RadialMenu.Size / 3f,
                RadialMenuUtility.Colors.RadialInner, RadialMenuUtility.Colors.CustomBorder, Position.Absolute);
            center.AddToClassList("ee4v-expression-menu__center");
            ring.Add(center);
            ring.Add(labels);
            var radialSlices = ring.Children().OfType<RadialSlice>().ToArray();
            var cursor = new RadialCursor { pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 125f, top = 125f } };
            ring.Add(cursor);
            void MoveCursor(Vector2 position)
            {
                var delta = position - ring.contentRect.center;
                delta = delta.magnitude < RadialMenu.Size / 2f ? Vector2.ClampMagnitude(delta, RadialMenu.Size / 3f) : Vector2.zero;
                cursor.style.left = ring.contentRect.center.x + delta.x - 25f;
                cursor.style.top = ring.contentRect.center.y + delta.y - 25f;
            }
            void UpdatePointer(Vector2 position)
            {
                MoveCursor(position);
                foreach (var slice in radialSlices) slice.SetHovered(slice.ContainsPoint(position));
            }
            ring.RegisterCallback<PointerEnterEvent>(evt => UpdatePointer(evt.localPosition));
            ring.RegisterCallback<PointerMoveEvent>(evt => UpdatePointer(evt.localPosition));
            ring.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                MoveCursor(ring.contentRect.center);
                foreach (var slice in radialSlices) slice.SetHovered(false);
            });
            if (_page.Entries.Count == 0) Add(UiTextFactory.Create(T("empty"), UiClassNames.SecondaryText));
        }

        private void SelectControl(int index)
        {
            _selected = index;
            _error = null;
            Refresh();
        }

        private void OpenSubmenu(int index)
        {
            if (index < 0 || index >= _page.Entries.Count || _page.Entries[index].Submenu == null) return;
            _path.Add(index);
            _selected = -1;
            _offset = 0;
            _error = null;
            Refresh();
        }

        private static RadialSlice PlaceSlot(VisualElement ring, VisualElement labels, UiButton button, int index, int count,
            Texture2D icon, bool selected = false)
        {
            button.AddToClassList("ee4v-expression-menu__slot");
            button.Content.AddToClassList("ee4v-expression-menu__slot-content");
            button.LabelText.SetWhiteSpace(WhiteSpace.Normal);
            button.SetLabelTextAlign(TextAnchor.MiddleCenter);
            button.SetLabelColor(new Color(0.824f, 0.824f, 0.824f));
            button.SetLabelFontSize(12);
            button.Content.Insert(0, new Image
            {
                image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore
            });
            var degrees = -90f + index * 360f / count;
            var angle = degrees * Mathf.Deg2Rad;
            button.Content.style.left = Length.Percent(50f + Mathf.Cos(angle) * 100f / 3f);
            button.Content.style.top = Length.Percent(50f + Mathf.Sin(angle) * 100f / 3f);
            var slice = new RadialSlice(degrees, 360f / count, selected, button);
            ring.Add(slice);
            button.Content.RemoveFromHierarchy();
            labels.Add(button.Content);
            return slice;
        }

        private sealed class RadialSlice : VisualElement
        {
            private readonly float _angle;
            private readonly float _span;
            private readonly bool _selected;
            private readonly GmgCircleElement _visual;
            private readonly VisualElement _content;
            private bool _hovered;
            private bool _focused;
            private bool _highlighted;
            private bool _highlightInitialized;
            private ValueAnimation<float> _scaleAnimation;

            public RadialSlice(float angle, float span, bool selected, UiButton button)
            {
                _angle = angle;
                _span = span;
                _selected = selected;
                _content = button.Content;
                AddToClassList("ee4v-expression-menu__slice");
                _visual = new GmgCircleElement { Progress = span / 360f, BorderWidth = 2f,
                    BorderColor = RadialMenuUtility.Colors.CustomBorder, pickingMode = PickingMode.Ignore };
                _visual.AddToClassList("ee4v-expression-menu__slice-visual");
                _visual.transform.rotation = Quaternion.Euler(0, 0, angle + 90f - span * 0.5f);
                Add(_visual);
                var lineHolder = new VisualElement { pickingMode = PickingMode.Ignore };
                lineHolder.AddToClassList("ee4v-expression-menu__line-holder");
                var line = RadialMenuUtility.Prefabs.NewBorder(RadialMenu.Size / 2f);
                line.transform.rotation = Quaternion.Euler(0, 0, angle - span * 0.5f);
                lineHolder.Add(line);
                Add(lineHolder);
                Add(button);
                RegisterCallback<FocusInEvent>(_ => { _focused = true; UpdateColors(); });
                RegisterCallback<FocusOutEvent>(_ => { _focused = false; UpdateColors(); });
                RegisterCallback<DetachFromPanelEvent>(_ => StopScaleAnimation());
                UpdateColors();
            }

            public void SetHovered(bool hovered)
            {
                if (_hovered == hovered) return;
                _hovered = hovered;
                UpdateColors();
            }

            public override bool ContainsPoint(Vector2 localPoint)
            {
                var radius = Mathf.Min(contentRect.width, contentRect.height) * 0.5f;
                var delta = localPoint - contentRect.center;
                return radius > 0f && delta.sqrMagnitude <= radius * radius &&
                    delta.sqrMagnitude >= radius * radius / 9f &&
                    Mathf.Abs(Mathf.DeltaAngle(_angle, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg)) <= _span * 0.5f;
            }

            private void UpdateColors()
            {
                var highlighted = enabledInHierarchy && (_selected || _hovered || _focused);
                if (_highlightInitialized && _highlighted == highlighted) return;
                _highlightInitialized = true;
                _highlighted = highlighted;
                _visual.VertexColor = highlighted ? RadialMenuUtility.Colors.CustomSelected : RadialMenuUtility.Colors.CustomMain;
                _visual.CenterColor = highlighted ? RadialMenuUtility.Colors.CenterSelected : RadialMenuUtility.Colors.CenterIdle;
                StopScaleAnimation();
                _scaleAnimation = _content.experimental.animation.Scale(highlighted ? 1.1f : 1f, 100);
                _scaleAnimation.KeepAlive();
            }

            private void StopScaleAnimation()
            {
                if (_scaleAnimation == null) return;
                _scaleAnimation.Stop();
                _scaleAnimation.Recycle();
                _scaleAnimation = null;
            }
        }

        private void BuildSettings(MenuEntry entry, bool currentSubmenu = false)
        {
            Add(UiTextFactory.Create(T("settings"), UiClassNames.SectionTitle));
            var owner = UiTextFactory.CreateObjectField(T("source"));
            owner.SetValueWithoutNotify(entry.Owner); owner.SetEnabled(false); Add(owner);
            if (entry.Submenu != null && !currentSubmenu)
            {
                var index = _selected;
                Add(new UiButton(T("openSubmenu"), () => OpenSubmenu(index)));
            }
            var canEdit = entry.CanEdit && _context.Edits.CanEditPrefab();
            if (!canEdit) Add(UiTextFactory.CreateHelpBox(T("readOnly"), HelpBoxMessageType.Info));
            var settingsStart = childCount;
            var draft = ExpressionMenuModel.Copy(entry.Control);
            // Virtual controls deliberately have no serialized submenu asset reference.
            if (entry.Owner is VRCExpressionsMenu menu) draft.subMenu = menu.controls[entry.Index].subMenu;
            else if (entry.Owner is ModularAvatarMenuItem item) draft.subMenu = item.Control.subMenu;
            var sourcePath = entry.Owner == null ? T("ambiguous") :
                EditorUtility.IsPersistent(entry.Owner) ? AssetDatabase.GetAssetPath(entry.Owner) :
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
                if (currentSubmenu && draft.type != VRCExpressionsMenu.Control.ControlType.SubMenu && _path.Count > 0)
                    _path.RemoveAt(_path.Count - 1);
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
                ExpressionMenuModel.Remove(entry);
                if (currentSubmenu && _path.Count > 0) _path.RemoveAt(_path.Count - 1);
                _selected = -1; NotifyChanged();
            }), variant: UiButtonVariant.Ghost);
            apply.SetEnabled(false); remove.SetEnabled(false);
            confirmed.RegisterValueChangedCallback(evt =>
            { apply.SetEnabled(evt.newValue); remove.SetEnabled(evt.newValue); });
            Add(apply); Add(remove);
            if (!canEdit)
                foreach (var field in Children().Skip(settingsStart)) field.SetEnabled(false);
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
            _selectAddedControl = true; NotifyChanged();
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
