using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Settings;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class GestureAssignmentViewText
    {
        public string Avatar { get; set; }
        public string LeftHand { get; set; }
        public string RightHand { get; set; }
        public string AllGestures { get; set; }
        public string Selection { get; set; }
        public string NoSelection { get; set; }
        public string ExpressionSettings { get; set; }
        public string GlobalSettings { get; set; }
        public string Clip { get; set; }
        public string EnableBlink { get; set; }
        public string FixMouth { get; set; }
        public string Unassigned { get; set; }
        public string MenuOnly { get; set; }
        public string MenuOnlyHint { get; set; }
        public string MenuName { get; set; }
        public string EnableMenuIcons { get; set; }
        public string AddMenuExpression { get; set; }
        public string Remove { get; set; }
        public Func<FaceGesture, string> GestureName { get; set; }
    }

    internal sealed class GestureAssignmentView : VisualElement
    {
        private const int ColumnCount = 8;
        private const float OverviewHeaderHeight = 28f;
        private const float OverviewHeaderWidth = 80f;
        private const float OverviewDividerHeight = 4f;

        private readonly ObjectField _avatarField;
        private readonly VisualElement _matrix;
        private readonly VisualElement _overviewGrid;
        private readonly VisualElement _extraRow;
        private readonly Dictionary<GestureCombination, GestureAssignmentCell> _cells =
            new Dictionary<GestureCombination, GestureAssignmentCell>();
        private readonly GestureAssignmentViewText _text;
        private readonly GestureAssignmentSession _session;
        private readonly Action<AnimationClip, Rect> _drawPreview;
        private readonly bool _readOnly;
        private bool _hasAvatar;
        private bool _rendering;
        private bool _subscribed;

        public GestureAssignmentView(
            GestureAssignmentViewText text,
            Action<AnimationClip, Rect> drawPreview = null,
            bool showAvatarField = true,
            GestureAssignmentSession session = null,
            bool readOnly = false)
        {
            _text = text ?? new GestureAssignmentViewText();
            _session = session ?? new GestureAssignmentSession();
            _drawPreview = drawPreview;
            _readOnly = readOnly;
            AddToClassList("ee4v-gesture-assignment");
            EnableInClassList("ee4v-gesture-assignment--overview", _readOnly);

            var toolbar = new ActionBar();
            toolbar.AddToClassList("ee4v-gesture-assignment__toolbar");
            _avatarField = UiTextFactory.CreateObjectField(
                _text.Avatar,
                "ee4v-gesture-assignment__avatar");
            _avatarField.objectType = typeof(GameObject);
            _avatarField.allowSceneObjects = true;
            _avatarField.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    AvatarChanged?.Invoke(evt.newValue as GameObject);
                }
            });
            if (showAvatarField)
            {
                toolbar.Leading.Add(_avatarField);
                Add(toolbar);
            }

            _matrix = _readOnly
                ? new VisualElement()
                : new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _matrix.AddToClassList("ee4v-gesture-assignment__matrix");
            var grid = _matrix;
            if (_readOnly)
            {
                _overviewGrid = new VisualElement();
                _overviewGrid.AddToClassList("ee4v-gesture-assignment__overview-grid");
                _matrix.Add(_overviewGrid);
                _matrix.RegisterCallback<GeometryChangedEvent>(_ => RefreshOverviewLayout());
                grid = _overviewGrid;
            }
            var header = new VisualElement();
            header.AddToClassList("ee4v-gesture-assignment__matrix-row");
            header.AddToClassList("ee4v-gesture-assignment__matrix-row--header");
            var corner = CreateHeader(
                _text.LeftHand + " ↓ / " + _text.RightHand + " →",
                false);
            corner.AddToClassList(
                "ee4v-gesture-assignment__matrix-header--corner");
            header.Add(corner);
            foreach (FaceGesture right in Enum.GetValues(typeof(FaceGesture)))
            {
                header.Add(CreateHeader(GetGestureName(right), false));
            }

            grid.Add(header);
            foreach (FaceGesture left in Enum.GetValues(typeof(FaceGesture)))
            {
                var row = CreateCellRow();
                row.Add(CreateHeader(GetGestureName(left), true));
                foreach (FaceGesture right in Enum.GetValues(typeof(FaceGesture)))
                {
                    var combination = new GestureCombination(left, right);
                    var cell = new GestureAssignmentCell(
                        _text.Clip,
                        _text.Unassigned,
                        () => _session.Select(combination),
                        clip => _session.SetClip(combination, clip),
                        rect => DrawPreview(combination, rect));
                    _cells.Add(combination, cell);
                    cell.SetReadOnly(_readOnly);
                    RegisterRemovalMenu(cell, I18N.Get("assignments.clear"),
                        () => _hasAvatar && _session.GetAssignment(combination).Clip != null,
                        () => { _session.Activate(); _session.SetClip(combination, null); });
                    row.Add(cell);
                }

                grid.Add(row);
            }

            _extraRow = new VisualElement();
            _extraRow.AddToClassList("ee4v-gesture-assignment__extra-row");
            grid.Add(_extraRow);
            Add(_matrix);

            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button != 0) { return; }
                for (var element = evt.target as VisualElement; element != null && element != this; element = element.parent)
                {
                    if (element is GestureAssignmentCell || element is Button || element is ObjectField || element is Scroller)
                    {
                        return;
                    }
                }
                _session.ClearSelection();
            });
            RefreshState();
        }

        public event Action<GameObject> AvatarChanged;

        public void SetAvatar(GameObject avatar)
        {
            _hasAvatar = avatar != null;
            _rendering = true;
            _avatarField.SetValueWithoutNotify(avatar);
            _rendering = false;
            RefreshState();
        }

        public void SetConfiguration(FaceExpressionConfiguration configuration)
        {
            _session.SetConfiguration(configuration);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _session.Changed += RefreshState;
            _subscribed = true;
            RefreshState();
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _session.Changed -= RefreshState;
            _subscribed = false;
        }

        private void RefreshState()
        {
            foreach (var pair in _cells)
            {
                pair.Value.SetAssignment(
                    _session.GetAssignment(pair.Key));
                pair.Value.SetClipEnabled(_hasAvatar && !_readOnly);
                pair.Value.EnableInClassList(
                    "ee4v-gesture-assignment__cell--selected",
                    _session.IsCombinationSelected(pair.Key));
            }

            RenderMenuEntries();
        }

        private UiTextElement CreateHeader(string value, bool rowHeader)
        {
            var header = UiTextFactory.Create(
                value,
                UiClassNames.FormLabel,
                "ee4v-gesture-assignment__matrix-header");
            header.EnableInClassList(
                "ee4v-gesture-assignment__matrix-header--row",
                rowHeader);
            header.SetTextAlign(TextAnchor.MiddleCenter);
            return header;
        }

        private string GetGestureName(FaceGesture gesture)
        {
            return _text.GestureName?.Invoke(gesture) ?? gesture.ToString();
        }

        private VisualElement CreateCellRow()
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-gesture-assignment__matrix-row");
            if (_readOnly) { return row; }
            row.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var cell = row[1];
                var dataWidth = evt.newRect.width - row[0].resolvedStyle.width;
                var cellWidth = cell.resolvedStyle.minWidth.value;
                var cellHeight = cell.resolvedStyle.minHeight.value;
                if (dataWidth <= 0f || cellWidth <= 0f || cellHeight <= 0f)
                {
                    return;
                }

                var height = Mathf.Max(
                    cellHeight,
                    dataWidth / ColumnCount * cellHeight / cellWidth);
                if (!Mathf.Approximately(row.resolvedStyle.height, height))
                {
                    row.style.height = height;
                }
            });
            return row;
        }

        private void DrawPreview(GestureCombination combination, Rect rect)
        {
            _drawPreview?.Invoke(
                _session.GetAssignment(combination).Clip,
                rect);
        }

        private void RenderMenuEntries()
        {
            _extraRow.Clear();
            var items = new List<VisualElement>();
            if (_readOnly) { RenderEffectiveMenuCells(items); }
            foreach (var entry in _readOnly ? Array.Empty<FaceExpressionMenuEntry>() : _session.MenuEntries)
            {
                var cell = new GestureAssignmentCell(
                    _text.Clip,
                    _text.Unassigned,
                    () => _session.Select(entry),
                    clip => _session.SetClip(entry, clip),
                    rect => _drawPreview?.Invoke(entry.Assignment.Clip, rect));
                cell.SetAssignment(entry.Assignment);
                cell.EnableInClassList(
                    "ee4v-gesture-assignment__cell--selected",
                    ReferenceEquals(
                        entry,
                        _session.SelectedMenuEntry));
                cell.SetReadOnly(_readOnly);
                cell.SetClipEnabled(_hasAvatar && !_readOnly);
                RegisterRemovalMenu(cell, _text.Remove,
                    () => _hasAvatar && _session.MenuEntries.Contains(entry),
                    () =>
                    {
                        _session.Activate();
                        _session.Select(entry);
                        _session.RemoveSelectedMenuExpression();
                    });
                items.Add(cell);
            }

            var add = new UiButton(
                "+",
                _session.AddMenuExpression,
                labelTypographyClassName: UiClassNames.NavigationItemLabel);
            add.AddToClassList("ee4v-gesture-assignment__cell");
            add.AddToClassList("ee4v-gesture-assignment__extra-add");
            add.tooltip = _text.AddMenuExpression;
            add.SetLabelTextAlign(TextAnchor.MiddleCenter);
            add.SetLabelFontSize(28);
            if (!_readOnly) { items.Add(add); }

            for (var offset = 0; offset < items.Count; offset += ColumnCount)
            {
                var row = CreateCellRow();
                VisualElement header;
                if (offset == 0)
                {
                    header = CreateHeader(_readOnly ? I18N.Get("assignments.menuBadge") : _text.MenuOnly, true);
                }
                else
                {
                    header = new VisualElement();
                    header.AddToClassList(
                        "ee4v-gesture-assignment__matrix-header");
                    header.AddToClassList(
                        "ee4v-gesture-assignment__matrix-header--row");
                    header.pickingMode = PickingMode.Ignore;
                }

                header.tooltip = _readOnly ? I18N.Get("assignments.menuOverviewHint") : _text.MenuOnlyHint;
                row.Add(header);
                for (var column = 0; column < ColumnCount; column++)
                {
                    var index = offset + column;
                    if (index < items.Count)
                    {
                        row.Add(items[index]);
                        continue;
                    }

                    var placeholder = new VisualElement();
                    placeholder.AddToClassList(
                        "ee4v-gesture-assignment__cell");
                    placeholder.AddToClassList(
                        "ee4v-gesture-assignment__extra-placeholder");
                    placeholder.pickingMode = PickingMode.Ignore;
                    row.Add(placeholder);
                }

                _extraRow.Add(row);
            }
            _extraRow.style.display = items.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshOverviewLayout();
        }

        private void RenderEffectiveMenuCells(List<VisualElement> items)
        {
            var configuration = _session.CreateConfiguration();
            var menuEntries = GestureMatrixControllerWriter.GetEffectiveMenuEntries(
                configuration.Assignments, configuration.MenuEntries);
            var explicitEntries = _session.MenuEntries.Where(entry => entry.Assignment.Clip != null).ToArray();
            var explicitIndex = 0;
            foreach (var item in menuEntries)
            {
                var explicitEntry = item.IsExplicit ? explicitEntries[explicitIndex++] : null;
                var source = item.IsExplicit ? default : configuration.Assignments
                    .Where(pair => pair.Key.Left == (item.LeftGesture ?? FaceGesture.Neutral) &&
                        pair.Value.Clip == item.Assignment.Clip &&
                        (string.IsNullOrWhiteSpace(pair.Value.MenuName)
                            ? pair.Value.Clip.name : pair.Value.MenuName.Trim()) == item.Name)
                    .OrderBy(pair => pair.Key.Right)
                    .Select(pair => pair.Key).First();
                var cell = new GestureAssignmentCell(_text.Clip, _text.Unassigned,
                    () =>
                    {
                        if (explicitEntry != null) { _session.Select(explicitEntry); }
                        else { _session.Select(source); }
                    }, null, rect => _drawPreview?.Invoke(item.Assignment.Clip, rect));
                cell.SetAssignment(item.Assignment);
                cell.SetReadOnly(true);
                cell.SetCaption(item.Name);
                var sourceName = explicitEntry != null ? I18N.Get("assignments.menuSelection") :
                    GetGestureName(source.Left) + "+" + GetGestureName(source.Right);
                cell.tooltip = item.Name + "\n" + sourceName + "\n" + item.Assignment.Clip.name;
                cell.EnableInClassList("ee4v-gesture-assignment__cell--selected", explicitEntry != null
                    ? ReferenceEquals(explicitEntry, _session.SelectedMenuEntry)
                    : _session.IsCombinationSelected(source));
                RegisterRemovalMenu(cell, explicitEntry != null ? _text.Remove :
                    I18N.Get("assignments.clear") + " (" + sourceName + ")",
                    () => _hasAvatar && (explicitEntry != null
                        ? _session.MenuEntries.Contains(explicitEntry)
                        : _session.GetAssignment(source).Clip == item.Assignment.Clip),
                    () =>
                    {
                        _session.Activate();
                        if (explicitEntry != null)
                        {
                            _session.Select(explicitEntry);
                            _session.RemoveSelectedMenuExpression();
                        }
                        else { _session.SetClip(source, null); }
                    });
                items.Add(cell);
            }
        }

        internal static void RegisterRemovalMenu(VisualElement element, string label, Func<bool> canRemove, Action remove)
        {
            element.RegisterCallback<ContextClickEvent>(evt =>
            {
                var menu = new GenericMenu();
                var content = UiTextFactory.CreateGuiContent(label);
                if (canRemove()) { menu.AddItem(content, false, () => { if (canRemove()) { remove(); } }); }
                else { menu.AddDisabledItem(content); }
                menu.ShowAsContext();
                evt.StopPropagation();
            });
        }

        private void RefreshOverviewLayout()
        {
            if (!_readOnly || _matrix.contentRect.width <= 0f || _matrix.contentRect.height <= 0f) { return; }
            var width = _matrix.contentRect.width;
            var height = _matrix.contentRect.height;
            var headerWidth = Mathf.Min(OverviewHeaderWidth, width / (ColumnCount + 1));
            var headerHeight = Mathf.Min(OverviewHeaderHeight, height);
            var dividerHeight = _extraRow.childCount > 0 ? Mathf.Min(OverviewDividerHeight, height - headerHeight) : 0f;
            var rowCount = ColumnCount + _extraRow.childCount;
            var cellSize = Mathf.Max(0f, Mathf.Min((width - headerWidth) / ColumnCount,
                (height - headerHeight - dividerHeight) / rowCount));
            _overviewGrid.style.width = headerWidth + ColumnCount * cellSize;
            _overviewGrid.style.height = headerHeight + rowCount * cellSize + dividerHeight;
            for (var index = 0; index <= ColumnCount; index++)
            {
                SetOverviewRowLayout(_overviewGrid[index], index == 0 ? headerHeight : cellSize, headerWidth, cellSize);
            }
            _extraRow.style.height = _extraRow.childCount * cellSize + dividerHeight;
            _extraRow.style.borderTopWidth = dividerHeight;
            foreach (var row in _extraRow.Children())
            {
                SetOverviewRowLayout(row, cellSize, headerWidth, cellSize);
            }
        }

        private static void SetOverviewRowLayout(VisualElement row, float height, float headerWidth, float cellSize)
        {
            row.style.height = height;
            row[0].style.flexBasis = headerWidth;
            row[0].style.width = headerWidth;
            for (var column = 1; column <= ColumnCount; column++)
            {
                row[column].style.flexBasis = cellSize;
                row[column].style.width = cellSize;
            }
        }

    }

    internal sealed class GestureAssignmentPickerView : VisualElement
    {
        private const int ExpressionTab = 0;
        private const int SettingsTab = 1;
        private readonly GestureAssignmentSession _session;
        private readonly GestureAssignmentViewText _text;
        private readonly Dictionary<FaceGesture, UiButton> _left = new Dictionary<FaceGesture, UiButton>();
        private readonly Dictionary<FaceGesture, UiButton> _right = new Dictionary<FaceGesture, UiButton>();
        private readonly UiButton _allLeft;
        private readonly UiButton _allRight;
        private readonly UiTextElement _currentClip;
        private readonly UiTextElement _candidateName;
        private readonly ObjectField _candidateField;
        private readonly InputField _menuNameField;
        private readonly UiButton _assign;
        private readonly UiButton _addMenu;
        private readonly VisualElement _handSelectors;
        private readonly VisualElement _expressionPanel;
        private readonly VisualElement _settingsPanel;
        private readonly VisualElement _menuEntries;
        private readonly GestureAssignmentSettingsView _settingsView;
        private readonly List<UiButton> _tabs = new List<UiButton>();
        private AnimationClip _candidate;
        private bool _hasAvatar;
        private bool _creatingMenu;
        private bool _menuNameEdited;
        private bool _updatingCommonInputs;
        private string _draftMenuName = string.Empty;
        private int _selectedTab;
        private bool _observedHasSelection;
        private bool _observedAllLeft;
        private bool _observedAllRight;
        private GestureCombination _observedCombination;
        private FaceExpressionMenuEntry _observedMenuEntry;

        internal GestureAssignmentPickerView(GestureAssignmentViewText text, GestureAssignmentSession session,
            ISettingsService settings = null)
        {
            _text = text;
            _session = session;
            AddToClassList("ee4v-gesture-picker");
            Add(new SectionHeader(I18N.Get("assignments.expressionInputs")));
            var inputs = new VisualElement();
            inputs.AddToClassList("ee4v-gesture-picker__inputs");
            _candidateField = UiTextFactory.CreateObjectField(_text.Clip);
            _candidateField.objectType = typeof(AnimationClip);
            _candidateField.allowSceneObjects = false;
            _candidateField.RegisterValueChangedCallback(evt => CandidateChanged?.Invoke(evt.newValue as AnimationClip));
            inputs.Add(_candidateField);
            _menuNameField = new InputField { IsDelayed = true };
            _menuNameField.ValueChanged += value =>
            {
                if (_updatingCommonInputs) { return; }
                _draftMenuName = value;
                _menuNameEdited = true;
            };
            var menuName = new FormInput(_text.MenuName, _menuNameField);
            menuName.AddToClassList("ee4v-gesture-picker__name-input");
            inputs.Add(menuName);
            Add(inputs);

            var tabs = new VisualElement();
            tabs.AddToClassList("ee4v-gesture-picker__tabs");
            var tabKeys = new[] { "assignments.expressionTab", "assignments.settingsTab" };
            for (var index = 0; index < tabKeys.Length; index++)
            {
                var tabIndex = index;
                var button = new UiButton(I18N.Get(tabKeys[index]), () => SelectTab(tabIndex),
                    variant: UiButtonVariant.Ghost);
                button.AddToClassList("ee4v-gesture-picker__tab");
                _tabs.Add(button);
                tabs.Add(button);
            }
            Add(tabs);
            _expressionPanel = CreatePanel();
            _settingsPanel = CreatePanel();

            _handSelectors = new VisualElement();
            _handSelectors.AddToClassList("ee4v-gesture-picker__hand-selectors");
            _handSelectors.Add(CreateHand(_text.LeftHand, true, _left, out var allLeft));
            _handSelectors.Add(CreateHand(_text.RightHand, false, _right, out var allRight));
            _allLeft = allLeft;
            _allRight = allRight;
            _expressionPanel.Add(_handSelectors);
            _addMenu = new UiButton(I18N.Get("assignments.menuSelection"), BeginMenuRegistration);
            _addMenu.AddToClassList("ee4v-gesture-picker__menu-selection");
            _expressionPanel.Add(_addMenu);

            _settingsView = new GestureAssignmentSettingsView(_text, settings, _session, embedded: true);
            _settingsPanel.Add(_settingsView);

            _menuEntries = new VisualElement();
            _menuEntries.AddToClassList("ee4v-gesture-picker__menu");
            _expressionPanel.Add(_menuEntries);
            ReviewActions = new VisualElement();
            ReviewActions.AddToClassList("ee4v-gesture-picker__review-actions");
            _expressionPanel.Add(ReviewActions);

            Footer = new VisualElement();
            Footer.AddToClassList("ee4v-gesture-picker__footer");
            var change = new VisualElement();
            change.AddToClassList("ee4v-gesture-picker__change");
            _currentClip = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText,
                "ee4v-gesture-picker__current-clip");
            _candidateName = UiTextFactory.Create(string.Empty, "ee4v-gesture-picker__candidate-name");
            change.Add(_currentClip);
            change.Add(new Icon(FluentUiIcons.CreateState("arrow_right.png", UiSizeTokens.Size12)));
            change.Add(_candidateName);
            Footer.Add(change);
            FooterActions = new VisualElement();
            FooterActions.AddToClassList("ee4v-gesture-picker__actions");
            _assign = new UiButton(I18N.Get("assignments.assign"), ApplyCandidate);
            _assign.SetPrimaryActionEnabled(true);
            _assign.AddToClassList("ee4v-gesture-picker__assign");
            FooterActions.Add(_assign);
            Footer.Add(FooterActions);
            Add(Footer);
            RegisterCallback<AttachToPanelEvent>(_ => { _session.Changed += Refresh; Refresh(); });
            RegisterCallback<DetachFromPanelEvent>(_ => _session.Changed -= Refresh);
            Refresh();
        }

        internal VisualElement Footer { get; }
        internal VisualElement FooterActions { get; }
        internal VisualElement ReviewActions { get; }
        internal event Action<AnimationClip> CandidateChanged;
        internal event Action<AnimationClip, string> ExpressionApplyRequested;

        private VisualElement CreatePanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("ee4v-gesture-picker__panel");
            Add(panel);
            return panel;
        }

        private void SelectTab(int tab)
        {
            _selectedTab = tab;
            Refresh();
        }

        private void RefreshTabLayout()
        {
            for (var index = 0; index < _tabs.Count; index++)
            {
                _tabs[index].EnableInClassList("ee4v-gesture-picker__tab--selected", index == _selectedTab);
                _tabs[index].SetLabelColor(index == _selectedTab ? UiColorTokens.TextPrimary : UiColorTokens.TextMuted);
            }
            _expressionPanel.style.display = _selectedTab == ExpressionTab ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsPanel.style.display = _selectedTab == SettingsTab ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsView.SetMenuDraft(_creatingMenu && _hasAvatar);
            Footer.style.display = _selectedTab == SettingsTab ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void BeginMenuRegistration()
        {
            _selectedTab = ExpressionTab;
            _creatingMenu = true;
            _menuNameEdited = false;
            _draftMenuName = string.Empty;
            _session.ClearSelection();
            Refresh();
        }

        private void ApplyCandidate()
        {
            if (!_hasAvatar || _candidate == null) { return; }
            _draftMenuName = _menuNameField.Value;
            var appliedName = !_menuNameEdited && !_session.IsMenuSelection &&
                _session.HasMixedSelection(value => value.MenuName) ? null : _draftMenuName;
            _session.Activate();
            _menuNameEdited = false;
            if (_creatingMenu)
            {
                _session.AddMenuExpression(_candidate, _draftMenuName,
                    _settingsView.DraftEnableBlink, _settingsView.DraftFixMouth);
            }
            else
            {
                ExpressionApplyRequested?.Invoke(_candidate, appliedName);
            }
            Refresh();
        }

        private void RefreshCommonInputs()
        {
            if (!_menuNameEdited && _session.HasSelection && !_creatingMenu)
            {
                _draftMenuName = _session.HasMixedSelection(value => value.MenuName) && !_session.IsMenuSelection
                    ? string.Empty : _session.SelectedMenuName;
            }
            _updatingCommonInputs = true;
            _menuNameField.SetValueWithoutNotify(_draftMenuName);
            var mixedName = !_menuNameEdited && !_creatingMenu &&
                !_session.IsMenuSelection && _session.HasMixedSelection(value => value.MenuName);
            _menuNameField.SetPlaceholder(mixedName ? I18N.Get("assignments.mixedValues") :
                _candidate != null ? _candidate.name : I18N.Get("assignments.clipNamePlaceholder"));
            _menuNameField.SetEnabled(_hasAvatar);
            _updatingCommonInputs = false;
        }

        internal void SetCandidate(AnimationClip clip)
        {
            _candidate = clip;
            _candidateField.SetValueWithoutNotify(clip);
            Refresh();
        }

        internal void SetAvatar(GameObject avatar)
        {
            _hasAvatar = avatar != null;
            _candidateField.SetEnabled(_hasAvatar);
            _menuNameField.SetEnabled(_hasAvatar);
            _handSelectors.SetEnabled(_hasAvatar);
            _menuEntries.SetEnabled(_hasAvatar);
            _addMenu.SetEnabled(_hasAvatar);
            Refresh();
        }

        private VisualElement CreateHand(string title, bool left, Dictionary<FaceGesture, UiButton> buttons,
            out UiButton all)
        {
            var column = new VisualElement();
            column.AddToClassList("ee4v-gesture-picker__hand-column");
            column.EnableInClassList("ee4v-gesture-picker__hand-column--right", !left);
            column.Add(new SectionHeader(title));
            all = new UiButton(_text.AllGestures ?? I18N.Get("assignments.allGestures"),
                () => _session.SelectHand(left, null));
            all.AddToClassList("ee4v-gesture-picker__hand-all");
            column.Add(all);
            var row = new VisualElement();
            row.AddToClassList("ee4v-gesture-picker__hands");
            foreach (FaceGesture gesture in Enum.GetValues(typeof(FaceGesture)))
            {
                var button = new UiButton(_text.GestureName?.Invoke(gesture) ?? gesture.ToString(),
                    () => _session.SelectHand(left, gesture));
                button.AddToClassList("ee4v-gesture-picker__hand");
                buttons.Add(gesture, button);
                row.Add(button);
            }
            column.Add(row);
            return column;
        }

        private void RefreshActions()
        {
            var activeTarget = _selectedTab == ExpressionTab && (_creatingMenu || _session.HasSelection);
            _assign.SetLabel(I18N.Get(_creatingMenu ? "assignments.registerMenu" :
                _session.IsMenuSelection ? "assignments.updateMenu" : "assignments.assign"));
            _assign.SetEnabled(_hasAvatar && activeTarget && _candidate != null);
        }

        private void Refresh()
        {
            var menuSelectionChanged = !ReferenceEquals(_observedMenuEntry, _session.SelectedMenuEntry);
            var selectionChanged = _observedHasSelection != _session.HasSelection ||
                !_observedCombination.Equals(_session.SelectedCombination) ||
                _observedAllLeft != _session.IsAllLeftSelected || _observedAllRight != _session.IsAllRightSelected ||
                !ReferenceEquals(_observedMenuEntry, _session.SelectedMenuEntry);
            _observedHasSelection = _session.HasSelection;
            _observedCombination = _session.SelectedCombination;
            _observedAllLeft = _session.IsAllLeftSelected;
            _observedAllRight = _session.IsAllRightSelected;
            _observedMenuEntry = _session.SelectedMenuEntry;
            if (selectionChanged)
            {
                if (_session.HasSelection)
                {
                    _creatingMenu = false;
                    _selectedTab = ExpressionTab;
                }
                if (menuSelectionChanged && _session.IsMenuSelection)
                {
                    _menuNameEdited = false;
                    CandidateChanged?.Invoke(_session.SelectedAssignment.Clip);
                }
            }
            RefreshCommonInputs();
            foreach (var pair in _left)
                pair.Value.EnableInClassList("ee4v-gesture-picker__hand--selected", _session.HasSelection && !_session.IsMenuSelection && !_session.IsAllLeftSelected && pair.Key == _session.SelectedCombination.Left);
            foreach (var pair in _right)
                pair.Value.EnableInClassList("ee4v-gesture-picker__hand--selected", _session.HasSelection && !_session.IsMenuSelection && !_session.IsAllRightSelected && pair.Key == _session.SelectedCombination.Right);
            _allLeft.EnableInClassList("ee4v-gesture-picker__hand--selected", _session.IsAllLeftSelected);
            _allRight.EnableInClassList("ee4v-gesture-picker__hand--selected", _session.IsAllRightSelected);
            _addMenu.EnableInClassList("ee4v-gesture-picker__hand--selected", _creatingMenu || _session.IsMenuSelection);
            var allText = _text.AllGestures ?? I18N.Get("assignments.allGestures");
            var target = _creatingMenu ? I18N.Get("assignments.newRegistration") :
                _session.IsMenuSelection ? I18N.Get("assignments.menuExpressions") + " · " + _session.SelectedMenuName :
                !_session.HasSelection ? _text.NoSelection :
                _text.LeftHand + " " + (_session.IsAllLeftSelected ? allText : _text.GestureName?.Invoke(_session.SelectedCombination.Left) ?? _session.SelectedCombination.Left.ToString()) + " + " +
                _text.RightHand + " " + (_session.IsAllRightSelected ? allText : _text.GestureName?.Invoke(_session.SelectedCombination.Right) ?? _session.SelectedCombination.Right.ToString());
            if (_session.SelectedCombinationCount > 1) { target += " " + I18N.Get("assignments.selectionCount", _session.SelectedCombinationCount); }
            var current = _creatingMenu ? I18N.Get("assignments.newRegistration") :
                _session.HasMixedSelection(assignment => assignment.Clip)
                ? I18N.Get("assignments.mixedClips")
                : _session.SelectedAssignment.Clip != null ? _session.SelectedAssignment.Clip.name : _text.Unassigned;
            _currentClip.SetText(current);
            _currentClip.tooltip = current;
            _candidateName.SetText(_candidate != null ? _candidate.name : _text.NoSelection);
            _candidateName.tooltip = _candidate != null ? _candidate.name : _text.NoSelection;
            Footer.tooltip = _session.IsMenuSelection ? _session.SelectedMenuName : target;
            _menuEntries.Clear();
            foreach (var entry in _session.MenuEntries)
            {
                var name = !string.IsNullOrEmpty(entry.Name) ? entry.Name :
                    entry.Assignment.Clip != null ? entry.Assignment.Clip.name : I18N.Get("assignments.unnamedMenuEntry");
                var row = new VisualElement();
                row.AddToClassList("ee4v-gesture-picker__menu-row");
                var button = new UiButton(name, () =>
                {
                    _menuNameEdited = false;
                    _session.Select(entry);
                }, name, variant: UiButtonVariant.Ghost);
                button.SetContentAlignment(Justify.FlexStart);
                button.SetLabelTextAlign(TextAnchor.MiddleLeft);
                button.AddToClassList("ee4v-gesture-picker__menu-entry");
                button.EnableInClassList("ee4v-gesture-picker__menu-entry--selected", ReferenceEquals(entry, _session.SelectedMenuEntry));
                row.Add(button);
                GestureAssignmentView.RegisterRemovalMenu(row, _text.Remove,
                    () => _hasAvatar && _session.MenuEntries.Contains(entry),
                    () =>
                    {
                        _session.Activate();
                        _session.Select(entry);
                        _session.RemoveSelectedMenuExpression();
                    });
                _menuEntries.Add(row);
            }
            _menuEntries.style.display = _session.MenuEntries.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshTabLayout();
            RefreshActions();
        }
    }

    internal sealed class GestureAssignmentSettingsView : VisualElement
    {
        private readonly GestureAssignmentViewText _text;
        private readonly GestureAssignmentSession _session;
        private readonly UiTextElement _selectionLabel;
        private readonly InputField _menuNameField;
        private readonly UiButton _removeMenuButton;
        private readonly Toggle _blinkToggle;
        private readonly Toggle _mouthToggle;
        private readonly Toggle _menuIconsToggle;
        private readonly ISettingsService _settings;
        private bool _menuDraft;
        private bool _rendering;
        private bool _subscribed;

        internal GestureAssignmentSettingsView(
            GestureAssignmentViewText text,
            ISettingsService settings = null,
            GestureAssignmentSession session = null,
            bool embedded = false)
        {
            _text = text ?? new GestureAssignmentViewText();
            _session = session ?? new GestureAssignmentSession();
            _settings = settings ?? CoreSettings.Current;
            _settings.Register(FaceExpressionSettings.MenuIconsDisabled);
            AddToClassList("ee4v-gesture-assignment-settings");
            EnableInClassList("ee4v-gesture-assignment-settings--embedded", embedded);

            var selectionHeader = new SectionHeader();
            _selectionLabel = selectionHeader.TitleText;
            _selectionLabel.AddToClassList("ee4v-gesture-assignment__selection");
            if (!embedded) { Add(selectionHeader); }
            var expressionHeader = new SectionHeader(_text.ExpressionSettings);
            expressionHeader.AddToClassList("ee4v-gesture-assignment__settings-section");
            Add(expressionHeader);
            var expressionControls = new VisualElement();
            expressionControls.AddToClassList("ee4v-gesture-assignment__controls");
            _blinkToggle = UiTextFactory.CreateToggle();
            _blinkToggle.RegisterValueChangedCallback(evt =>
            {
                if (_rendering) { return; }
                if (_menuDraft) { DraftEnableBlink = evt.newValue; }
                else { _session.SetSelectedBlink(evt.newValue); }
            });
            expressionControls.Add(CreateCheckboxRow(_text.EnableBlink, _blinkToggle));
            _mouthToggle = UiTextFactory.CreateToggle();
            _mouthToggle.RegisterValueChangedCallback(evt =>
            {
                if (_rendering) { return; }
                if (_menuDraft) { DraftFixMouth = evt.newValue; }
                else { _session.SetSelectedMouth(evt.newValue); }
            });
            expressionControls.Add(CreateCheckboxRow(_text.FixMouth, _mouthToggle));
            Add(expressionControls);

            _menuNameField = new InputField { IsDelayed = true };
            _menuNameField.AddToClassList("ee4v-gesture-assignment__extra-name");
            _menuNameField.ValueChanged += value =>
            {
                if (!_rendering && (value != _session.SelectedMenuName ||
                    _session.HasMixedSelection(assignment => assignment.MenuName)))
                {
                    _session.SetSelectedMenuName(value);
                }
            };
            _removeMenuButton = new UiButton(string.Empty, _session.RemoveSelectedMenuExpression, _text.Remove,
                FluentUiIcons.CreateState("dismiss.png", UiSizeTokens.Size12), UiButtonVariant.Ghost);
            _removeMenuButton.AddToClassList("ee4v-gesture-assignment__extra-remove");
            var menuControls = new FormInput(_text.MenuName, _menuNameField, _removeMenuButton);
            menuControls.AddToClassList("ee4v-gesture-assignment__extra-controls");
            if (!embedded) { Add(menuControls); }

            DisplayContent = new VisualElement();
            DisplayContent.AddToClassList("ee4v-gesture-assignment__display");
            var globalHeader = new SectionHeader(_text.GlobalSettings);
            globalHeader.AddToClassList("ee4v-gesture-assignment__settings-section");
            DisplayContent.Add(globalHeader);
            _menuIconsToggle = UiTextFactory.CreateToggle();
            _menuIconsToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering) { _settings.Set(FaceExpressionSettings.MenuIconsDisabled, !evt.newValue); }
            });
            DisplayContent.Add(CreateCheckboxRow(_text.EnableMenuIcons, _menuIconsToggle));
            Add(DisplayContent);
            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            RefreshState();
        }

        internal VisualElement DisplayContent { get; }

        internal bool DraftEnableBlink { get; private set; } = true;
        internal bool DraftFixMouth { get; private set; }

        internal void SetMenuDraft(bool menuDraft)
        {
            if (_menuDraft == menuDraft) { return; }
            _menuDraft = menuDraft;
            if (menuDraft)
            {
                DraftEnableBlink = true;
                DraftFixMouth = false;
            }
            RefreshState();
        }

        private static FormInput CreateCheckboxRow(string label, Toggle checkbox)
        {
            checkbox.AddToClassList("ee4v-gesture-assignment__checkbox");
            var row = new FormInput(label, checkbox);
            row.AddToClassList("ee4v-gesture-assignment__setting-row");
            row.LabelText.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button != 0 || !checkbox.enabledInHierarchy) { return; }
                checkbox.Focus();
                checkbox.value = !checkbox.value;
                evt.StopPropagation();
            });
            return row;
        }

        private void Subscribe()
        {
            if (_subscribed) { return; }
            _session.Changed += RefreshState;
            _settings.Changed += OnSettingChanged;
            _subscribed = true;
            RefreshState();
        }

        private void Unsubscribe()
        {
            if (!_subscribed) { return; }
            _session.Changed -= RefreshState;
            _settings.Changed -= OnSettingChanged;
            _subscribed = false;
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (args.Definition.Key == FaceExpressionSettings.MenuIconsDisabled.Key) { RefreshState(); }
        }

        private void RefreshState()
        {
            var assignment = _menuDraft
                ? new FaceExpressionAssignment(null, DraftEnableBlink, DraftFixMouth)
                : _session.SelectedAssignment;
            var isExtra = _session.IsMenuSelection;
            _rendering = true;
            _selectionLabel.SetText(!_session.HasSelection ? _text.Selection + ": " + _text.NoSelection : isExtra
                ? _text.Selection + ": " + _text.MenuOnly
                : _text.Selection + ": " +
                  (_session.IsAllLeftSelected ? _text.AllGestures ?? I18N.Get("assignments.allGestures") : GetGestureName(_session.SelectedCombination.Left)) +
                  " + " +
                  (_session.IsAllRightSelected ? _text.AllGestures ?? I18N.Get("assignments.allGestures") : GetGestureName(_session.SelectedCombination.Right)));
            var mixedName = _session.HasMixedSelection(value => value.MenuName);
            _menuNameField.SetValueWithoutNotify(mixedName ? string.Empty : _session.SelectedMenuName);
            _menuNameField.SetPlaceholder(mixedName ? I18N.Get("assignments.mixedValues") :
                assignment.Clip != null ? assignment.Clip.name : I18N.Get("assignments.clipNamePlaceholder"));
            _removeMenuButton.style.display = isExtra ? DisplayStyle.Flex : DisplayStyle.None;
            _blinkToggle.SetValueWithoutNotify(assignment.EnableBlink);
            _mouthToggle.SetValueWithoutNotify(assignment.FixMouth);
            _blinkToggle.showMixedValue = !_menuDraft && _session.HasMixedSelection(value => value.EnableBlink);
            _mouthToggle.showMixedValue = !_menuDraft && _session.HasMixedSelection(value => value.FixMouth);
            _menuNameField.SetEnabled(_session.HasSelection);
            _blinkToggle.SetEnabled(_menuDraft || _session.HasSelection);
            _mouthToggle.SetEnabled(_menuDraft || _session.HasSelection);
            _menuIconsToggle.SetValueWithoutNotify(!FaceExpressionSettings.GetMenuIconsDisabled(_settings));
            _rendering = false;
        }

        private string GetGestureName(FaceGesture gesture)
        {
            return _text.GestureName?.Invoke(gesture) ?? gesture.ToString();
        }
    }
}
