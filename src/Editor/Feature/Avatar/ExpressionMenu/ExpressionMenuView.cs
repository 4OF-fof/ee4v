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
        private readonly bool _showsEditSource;
        private readonly List<int> _path = new List<int>();
        private List<MenuPage> _sources;
        private MenuPage _root;
        private MenuPage _page;
        private MenuEntry _submenuEntry;
        private int _source;
        private int _offset;
        private int _selected = -1;
        private bool _editingCurrentSubmenu;
        private string _error;
        private ModularAvatarMenuItem _selectAddedSource;
        private bool _openAddedSubmenu;
        private VisualElement _menuHost;
        private static string T(string key) => I18N.Get("expressionMenu." + key);

        public ExpressionMenuView(AvatarEditingContext context, bool showsEditSource = false)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _showsEditSource = showsEditSource;
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
                if (_selectAddedSource != null)
                {
                    if (LocateSource(_selectAddedSource) && _openAddedSubmenu)
                    {
                        _path.Add(_selected);
                        _selected = -1;
                        _offset = 0;
                    }
                    _selectAddedSource = null;
                    _openAddedSubmenu = false;
                }
                _page = _source == 0 ? _root : _sources[_source - 1];
                _submenuEntry = null;
                foreach (var index in _path.ToArray())
                {
                    if (index >= _page.Entries.Count || _page.Entries[index].Submenu == null)
                    { _path.Clear(); _submenuEntry = null; _page = _source == 0 ? _root : _sources[_source - 1]; break; }
                    _submenuEntry = _page.Entries[index];
                    _page = _submenuEntry.Submenu;
                }
                _offset = _page.Entries.Count > 8 ? Mathf.Clamp(_offset, 0, (_page.Entries.Count - 1) / 7 * 7) : 0;
                if (_selected >= _page.Entries.Count) _selected = -1;
                if (_submenuEntry == null) _editingCurrentSubmenu = false;
                Build();
            }
            catch (Exception exception)
            {
                Add(UiTextFactory.CreateHelpBox(exception.GetBaseException().Message, HelpBoxMessageType.Error));
            }
        }

        private void Build()
        {
            if (!string.IsNullOrEmpty(_error))
                Add(UiTextFactory.CreateHelpBox(_error, HelpBoxMessageType.Error));
            var selectedEntry = _editingCurrentSubmenu ? _submenuEntry :
                _selected >= 0 && _selected < _page.Entries.Count ? _page.Entries[_selected] : null;
            if (selectedEntry != null)
            {
                var navigation = new VisualElement();
                navigation.AddToClassList("ee4v-expression-menu__navigation");
                navigation.Add(new UiButton(T("backToMenu"), () =>
                {
                    _selected = -1;
                    _editingCurrentSubmenu = false;
                    _error = null;
                    Refresh();
                }, icon: FluentUiIcons.CreateState("arrow_left.png")) { name = "expressionMenuBack" });
                navigation.Add(UiTextFactory.Create(selectedEntry.Control.name, UiClassNames.SectionTitle));
                Add(navigation);
            }
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "expressionMenuScroll" };
            scroll.AddToClassList("ee4v-expression-menu__scroll");
            Add(scroll);
            _menuHost = new VisualElement { name = "expressionMenuEditor" };
            _menuHost.AddToClassList("ee4v-expression-menu__editor");
            _menuHost.EnableInClassList("ee4v-expression-menu__editor--detail", selectedEntry != null);
            scroll.Add(_menuHost);
            if (selectedEntry != null)
            {
                var item = selectedEntry.Owner as ModularAvatarMenuItem;
                if (item != null && (ExpressionMenuTemplateModel.HasActions(item) ||
                    item.PortableControl.Type == PortableControlType.SubMenu))
                    _menuHost.Add(new ExpressionMenuTemplateEditor(_context, item, Refresh));
                BuildSettings(selectedEntry, _editingCurrentSubmenu);
                return;
            }
            _menuHost.Add(UiTextFactory.Create(T("title"), UiClassNames.SectionTitle));
            if (_showsEditSource)
            {
                var choices = new List<string> { T("effective") };
                choices.AddRange(_sources.Select((page, i) => (i + 1) + ": " + page.Name));
                var source = UiTextFactory.CreatePopupField(T("view"), choices, _source);
                source.RegisterValueChangedCallback(evt =>
                {
                    _source = choices.IndexOf(evt.newValue);
                    _path.Clear(); _offset = 0; _selected = -1; _editingCurrentSubmenu = false; Refresh();
                });
                _menuHost.Add(source);
                _menuHost.Add(UiTextFactory.Create(_page.Name, UiClassNames.SecondaryText));
            }
            var overview = new VisualElement();
            overview.AddToClassList("ee4v-expression-menu__overview");
            _menuHost.Add(overview);
            var menu = new VisualElement();
            menu.AddToClassList("ee4v-expression-menu__menu-column");
            overview.Add(menu);
            BuildRadial(menu);
            if (_submenuEntry != null)
                BuildQuickSettings(_submenuEntry, () =>
                {
                    _editingCurrentSubmenu = true;
                    _selected = -1;
                    _error = null;
                    Refresh();
                }, host: menu);
            var items = new VisualElement();
            items.AddToClassList("ee4v-expression-menu__items");
            overview.Add(items);
            var count = Math.Min(_page.Entries.Count > 8 ? 7 : 8, _page.Entries.Count - _offset);
            for (var slot = 0; slot < count; slot++)
            {
                var index = _offset + slot;
                BuildQuickSettings(_page.Entries[index], () => SelectControl(index),
                    _page.Entries[index].Submenu == null ? null : (Action)(() => OpenSubmenu(index)), items);
            }
        }

        private void BuildQuickSettings(MenuEntry entry, Action edit, Action open = null, VisualElement host = null)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-expression-menu__quick-item");
            (host ?? _menuHost).Add(row);
            var iconHost = new VisualElement();
            iconHost.AddToClassList("ee4v-expression-menu__quick-icon-host");
            row.Add(iconHost);
            var preview = new Image { image = ExpressionMenuModel.DisplayIcon(entry.Control.icon),
                scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            preview.AddToClassList("ee4v-expression-menu__quick-icon-preview");
            iconHost.Add(preview);
            var draft = ExpressionMenuModel.Copy(entry.SourceControl ?? entry.Control);
            var source = entry.Owner as ModularAvatarMenuItem;
            void Change(Action maChange, Action controlChange)
            {
                if (!CanInteract(entry)) throw new InvalidOperationException(T("readOnly"));
                if (source != null) ExpressionMenuTemplateModel.Edit(_context, source, maChange);
                else
                {
                    controlChange();
                    ExpressionMenuModel.Update(entry, draft);
                    NotifyChanged();
                }
            }
            var icon = UiTextFactory.CreateObjectField("");
            icon.objectType = typeof(Texture2D);
            icon.allowSceneObjects = false;
            icon.tooltip = T("icon");
            icon.AddToClassList("ee4v-expression-menu__quick-icon");
            icon.SetValueWithoutNotify(entry.Control.icon);
            icon.SetEnabled(CanInteract(entry));
            icon.RegisterValueChangedCallback(evt => Run(() =>
            {
                try { Change(() => source.Control.icon = evt.newValue as Texture2D, () => draft.icon = evt.newValue as Texture2D); }
                catch { icon.SetValueWithoutNotify(evt.previousValue); throw; }
            }));
            iconHost.Add(icon);
            var details = new VisualElement();
            details.AddToClassList("ee4v-expression-menu__quick-details");
            row.Add(details);
            var name = new InputField(new InputFieldState(entry.Control.name)) { IsDelayed = true };
            name.tooltip = T("name");
            name.SetEnabled(CanInteract(entry));
            name.ValueChanged += value => Run(() => Change(() => source.label = value, () => draft.name = value));
            details.Add(name);
            var actions = new VisualElement();
            actions.AddToClassList("ee4v-expression-menu__quick-actions");
            details.Add(actions);
            if (open != null) actions.Add(new UiButton(T("openSubmenu"), open));
            var editButton = new UiButton(T("editDetails"), edit);
            editButton.SetEnabled(CanInteract(entry));
            actions.Add(editButton);
        }

        private void BuildRadial(VisualElement host)
        {
            var ring = new VisualElement();
            ring.AddToClassList("ee4v-expression-menu__ring");
            var labels = new VisualElement { pickingMode = PickingMode.Ignore };
            labels.AddToClassList("ee4v-expression-menu__labels");
            host.Add(ring);
            var paged = _page.Entries.Count > 8;
            var count = Math.Min(paged ? 7 : 8, _page.Entries.Count - _offset);
            var hasNext = paged && _offset + count < _page.Entries.Count;
            var hasBack = _offset > 0 || _path.Count > 0;
            var slices = count + (hasBack ? 2 : 1);
            var addSlot = slices - 1;
            if (hasBack)
            {
                var back = new UiButton(T("back"), () =>
                {
                    if (_offset > 0) _offset = Math.Max(0, _offset - 7);
                    else _path.RemoveAt(_path.Count - 1);
                    _selected = -1;
                    _editingCurrentSubmenu = false;
                    _error = null;
                    Refresh();
                }, variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SecondaryText);
                PlaceSlot(ring, labels, back, 0, slices, Resources.Load<Texture2D>("Vrc3/BSX_GM_Back"));
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
                    {
                        evt.menu.AppendAction(T("settings"), _ => SelectControl(index),
                            _ => CanInteract(entry) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                    }));
                button.tooltip = entry.Control.name + "\n" + entry.Control.type + " / " +
                    (entry.Owner == null ? T("ambiguous") : entry.Owner.name);
                var icon = ExpressionMenuModel.DisplayIcon(entry.Control.icon);
                var slice = PlaceSlot(ring, labels, button, slot + (hasBack ? 1 : 0), slices, icon);
                var subIcon = RadialMenuUtility.GetSubIcon(entry.Control.type);
                if (subIcon != null)
                {
                    var indicator = new VisualElement { pickingMode = PickingMode.Ignore };
                    indicator.AddToClassList("ee4v-expression-menu__sub-icon");
                    indicator.Add(new Image { image = subIcon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore });
                    button.Content.Add(indicator);
                }
                slice.SetAvailable(entry.Submenu != null || CanInteract(entry), button);
            }
            if (hasNext)
            {
                var next = new UiButton(T("next"), () => { _offset += 7; _selected = -1; Refresh(); },
                    variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SecondaryText);
                PlaceSlot(ring, labels, next, addSlot, slices, FluentUiIcons.LoadTexture("arrow_right.png"));
            }
            else
            {
                var add = new UiButton(T("addItem"), ShowAddMenu, tooltip: T("add"), variant: UiButtonVariant.Ghost,
                    labelTypographyClassName: UiClassNames.SecondaryText);
                var addSlice = PlaceSlot(ring, labels, add, addSlot, slices, FluentUiIcons.LoadTexture("add.png"));
                addSlice.SetAvailable(CanAddToPage(), add);
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
            if (_page.Entries.Count == 0) _menuHost.Add(UiTextFactory.Create(T("empty"), UiClassNames.SecondaryText));
        }

        private void SelectControl(int index)
        {
            if (index < 0 || index >= _page.Entries.Count || !CanInteract(_page.Entries[index])) return;
            _selected = index;
            _editingCurrentSubmenu = false;
            _error = null;
            Refresh();
        }

        private bool LocateSource(UnityEngine.Object source)
        {
            if (_root == null || source == null) return false;
            var visited = new HashSet<MenuPage>();
            var path = new List<int>();
            int selected = -1;
            bool Find(MenuPage page)
            {
                if (!visited.Add(page)) return false;
                for (var i = 0; i < page.Entries.Count; i++)
                {
                    var entry = page.Entries[i];
                    if (entry.Owner == source)
                    {
                        selected = i;
                        return true;
                    }
                    if (entry.Submenu == null) continue;
                    path.Add(i);
                    if (Find(entry.Submenu)) return true;
                    path.RemoveAt(path.Count - 1);
                }
                return false;
            }
            if (!Find(_root)) return false;
            _source = 0;
            _path.Clear();
            _path.AddRange(path);
            _selected = selected;
            _editingCurrentSubmenu = false;
            var page = _root;
            foreach (var index in path) page = page.Entries[index].Submenu;
            _offset = page.Entries.Count > 8 ? selected / 7 * 7 : 0;
            return true;
        }

        private void OpenSubmenu(int index)
        {
            if (index < 0 || index >= _page.Entries.Count || _page.Entries[index].Submenu == null) return;
            _path.Add(index);
            _selected = -1;
            _editingCurrentSubmenu = false;
            _offset = 0;
            _error = null;
            Refresh();
        }

        private bool CanInteract(MenuEntry entry) => entry.CanEdit && _context.Edits.CanEditPrefab() &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            (!(entry.Owner is Component component) || ExpressionMenuTemplateModel.CanEdit(_context, component));

        // Install target assets are references; new controls are stored in a separate prefab.
        private bool CanAddToPage() => _context.Edits.CanEditPrefab() && !EditorApplication.isPlayingOrWillChangePlaymode &&
            (_page.ChildRoot == null || ExpressionMenuTemplateModel.CanEdit(_context, _page.ChildRoot));

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

            public void SetAvailable(bool available, UiButton button)
            {
                SetEnabled(available);
                _content.SetEnabled(available);
                button.SetLabelColor(available ? new Color(0.824f, 0.824f, 0.824f) : Color.gray);
                _content.Query<Image>().ForEach(icon => icon.tintColor = available ? Color.white : Color.gray);
                _highlightInitialized = false;
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
                _visual.VertexColor = !enabledSelf ? new Color(0.18f, 0.18f, 0.18f) :
                    highlighted ? RadialMenuUtility.Colors.CustomSelected : RadialMenuUtility.Colors.CustomMain;
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
            var host = _menuHost;
            host.Add(UiTextFactory.Create(T("settings"), UiClassNames.SectionTitle));
            var maSource = entry.Owner as ModularAvatarMenuItem;
            var simple = ExpressionMenuTemplateModel.HasActions(maSource);
            var submenuControl = entry.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu;
            var compact = simple || submenuControl && maSource != null &&
                ExpressionMenuTemplateModel.IsOwned(maSource.gameObject) && maSource.MenuSource == SubmenuSource.Children;
            var canEdit = entry.CanEdit && _context.Edits.CanEditPrefab() && !EditorApplication.isPlayingOrWillChangePlaymode &&
                (!(entry.Owner is Component sourceComponent) || ExpressionMenuTemplateModel.CanEdit(_context, sourceComponent));
            if (!canEdit) host.Add(UiTextFactory.CreateHelpBox(T("readOnly"), HelpBoxMessageType.Info));
            if (!compact)
            {
                var owner = UiTextFactory.CreateObjectField(T("source"));
                owner.SetValueWithoutNotify(entry.Owner); owner.SetEnabled(false); host.Add(owner);
            }
            if (entry.Submenu != null && !currentSubmenu)
            {
                var index = _selected;
                var open = new UiButton(T("openSubmenu"), () => OpenSubmenu(index));
                open.SetEnabled(canEdit);
                host.Add(open);
            }
            var settingsStart = host.childCount;
            var draft = ExpressionMenuModel.Copy(entry.Control);
            if (entry.Owner is VRCExpressionsMenu menu) draft.subMenu = menu.controls[entry.Index].subMenu;
            else if (maSource != null) draft.subMenu = maSource.Control.subMenu;
            var synced = maSource != null && maSource.isSynced;
            var saved = maSource != null && maSource.isSaved;
            var defaultValue = maSource != null && maSource.isDefault;
            var automaticValue = maSource != null && maSource.automaticValue;
            void ApplyDraft()
            {
                ExpressionMenuModel.Update(entry, draft);
                if (maSource != null)
                {
                    if (!simple)
                    {
                        maSource.isSynced = synced; maSource.isSaved = saved;
                        maSource.isDefault = defaultValue; maSource.automaticValue = automaticValue;
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(maSource);
                    EditorUtility.SetDirty(maSource);
                    ExpressionMenuInstaller.SaveGeneratedPrefab(maSource.gameObject);
                }
                if (currentSubmenu && draft.type != VRCExpressionsMenu.Control.ControlType.SubMenu && _path.Count > 0)
                    _path.RemoveAt(_path.Count - 1);
                NotifyChanged();
            }
            void Change(Action change) => Run(() =>
            {
                if (!canEdit) throw new InvalidOperationException(T("readOnly"));
                change();
                ApplyDraft();
            });
            if (maSource == null || !simple && !submenuControl)
            {
                AddText(T("name"), draft.name, value => Change(() => draft.name = value), host);
                var icon = UiTextFactory.CreateObjectField(T("icon"));
                icon.objectType = typeof(Texture2D); icon.allowSceneObjects = false;
                icon.SetValueWithoutNotify(draft.icon);
                icon.RegisterValueChangedCallback(evt => Change(() => draft.icon = evt.newValue as Texture2D)); host.Add(icon);
            }
            if (maSource != null && !submenuControl && !simple)
            {
                AddToggle(T("synced"), synced, value => Change(() => synced = value), host);
                if (!compact)
                {
                    AddToggle(T("saved"), saved, value => Change(() => saved = value), host);
                    AddToggle(T("defaultValue"), defaultValue, value => Change(() => defaultValue = value), host);
                    AddToggle(T("automaticValue"), automaticValue, value => Change(() => automaticValue = value), host);
                }
            }
            if (!compact)
            {
                var type = UiTextFactory.CreateEnumField(T("type"), draft.type);
                host.Add(type);
                var details = new VisualElement(); host.Add(details);
                void BuildDetails()
                {
                    details.Clear();
                    AddText(T("parameter"), draft.parameter?.name, value => Change(() =>
                        draft.parameter = new VRCExpressionsMenu.Control.Parameter { name = value }), details);
                    var valueField = UiTextFactory.CreateFloatField(T("value"));
                    valueField.isDelayed = true;
                    valueField.SetValueWithoutNotify(draft.value);
                    valueField.RegisterValueChangedCallback(evt => Change(() => draft.value = evt.newValue)); details.Add(valueField);
                    if (draft.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        var supportsAsset = !(maSource != null && maSource.MenuSource == SubmenuSource.Children);
                        var submenu = UiTextFactory.CreateObjectField(T("submenu"));
                        submenu.objectType = typeof(VRCExpressionsMenu); submenu.allowSceneObjects = false;
                        submenu.SetValueWithoutNotify(draft.subMenu);
                        submenu.RegisterValueChangedCallback(evt => Change(() => draft.subMenu = evt.newValue as VRCExpressionsMenu));
                        submenu.SetEnabled(supportsAsset);
                        details.Add(submenu);
                        var createSubmenu = new UiButton(T("createSubmenu"), () => Run(() =>
                        {
                            if (!canEdit) throw new InvalidOperationException(T("readOnly"));
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
                        createSubmenu.SetEnabled(supportsAsset);
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
                            value => Change(() => draft.subParameters[index].name = value), details);
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
                                value => Change(() => draft.labels[index].name = value), details);
                            var labelIcon = UiTextFactory.CreateObjectField(T("icon") + " " + (axis + 1));
                            labelIcon.objectType = typeof(Texture2D); labelIcon.allowSceneObjects = false;
                            labelIcon.SetValueWithoutNotify(draft.labels[axis].icon);
                            labelIcon.RegisterValueChangedCallback(evt => Change(() => draft.labels[index].icon = evt.newValue as Texture2D));
                            details.Add(labelIcon);
                        }
                    }
                }
                type.RegisterValueChangedCallback(evt => Change(() =>
                {
                    draft.type = (VRCExpressionsMenu.Control.ControlType)evt.newValue;
                    BuildDetails();
                }));
                BuildDetails();
            }
            host.Add(new UiButton(T("remove"), () => Run(() =>
            {
                if (!canEdit) throw new InvalidOperationException(T("readOnly"));
                if (simple && ExpressionMenuTemplateModel.IsOwned(maSource.gameObject))
                    ExpressionMenuTemplateModel.Delete(_context, maSource);
                else ExpressionMenuModel.Remove(entry);
                if (currentSubmenu && _path.Count > 0) _path.RemoveAt(_path.Count - 1);
                _selected = -1; _editingCurrentSubmenu = false; NotifyChanged();
            }), variant: UiButtonVariant.Ghost));
            if (!canEdit)
                foreach (var field in host.Children().Skip(settingsStart)) field.SetEnabled(false);
        }

        private void AddText(string label, string value, Action<string> changed, VisualElement host = null)
        {
            var input = new InputField(new InputFieldState(value)) { IsDelayed = true };
            input.ValueChanged += changed;
            (host ?? _menuHost).Add(new FormInput(label, input));
        }

        private void AddToggle(string label, bool value, Action<bool> changed, VisualElement host = null)
        {
            var toggle = UiTextFactory.CreateToggle(label);
            toggle.SetValueWithoutNotify(value);
            toggle.RegisterValueChangedCallback(evt => changed(evt.newValue));
            (host ?? _menuHost).Add(toggle);
        }

        private void AddControl(bool submenu)
        {
            if (!CanAddToPage()) throw new InvalidOperationException(T("readOnly"));
            _selectAddedSource = ExpressionMenuTemplateModel.Create(_context,
                T(submenu ? "newSubmenu" : "newControl"), _page.Asset, _page.ChildRoot, submenu);
            _openAddedSubmenu = submenu;
            NotifyChanged();
        }

        private void ShowAddMenu()
        {
            var choices = new GenericMenu();
            choices.AddItem(UiTextFactory.CreateGuiContent(T("addControl")), false, () => Run(() => AddControl(false)));
            choices.AddItem(UiTextFactory.CreateGuiContent(T("addSubmenu")), false, () => Run(() => AddControl(true)));
            choices.ShowAsContext();
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
