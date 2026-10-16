using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private const float OverviewHeaderHeight = 40f;
        private const float OverviewHeaderWidth = 112f;
        private const int OverviewMenuColumns = 2;
        private const float OverviewMenuGap = 8f;

        private readonly ObjectField _avatarField;
        private readonly VisualElement _matrix;
        private readonly VisualElement _overviewGrid;
        private readonly VisualElement _menuColumn;
        private readonly VisualElement _menuHeader;
        private readonly ScrollView _menuScroll;
        private readonly VisualElement _extraRow;
        private readonly Dictionary<GestureCombination, GestureAssignmentCell> _cells =
            new Dictionary<GestureCombination, GestureAssignmentCell>();
        private readonly GestureAssignmentViewText _text;
        private readonly GestureAssignmentSession _session;
        private readonly Action<AnimationClip, Rect> _drawPreview;
        private readonly bool _readOnly;
        private FaceExpressionConfiguration _menuSnapshot;
        private FaceExpressionMenuEntry[] _menuSourceSnapshot = Array.Empty<FaceExpressionMenuEntry>();
        private IReadOnlyList<GestureMatrixControllerWriter.EffectiveMenuEntry> _effectiveMenuSnapshot;
        private readonly Dictionary<GestureAssignmentCell, Func<bool>> _menuSelectionChecks =
            new Dictionary<GestureAssignmentCell, Func<bool>>();
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
                _text.LeftHand + (_readOnly ? " ↓\n" : " ↓ / ") + _text.RightHand + " →",
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
                        () => SelectAssignment(combination),
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
            if (_readOnly)
            {
                _menuColumn = new VisualElement();
                _menuColumn.AddToClassList("ee4v-gesture-assignment__menu-column");
                _menuHeader = CreateHeader(I18N.Get("assignments.menuBadge"), false);
                _menuHeader.tooltip = I18N.Get("assignments.menuOverviewHint");
                _menuColumn.Add(_menuHeader);
                _menuScroll = new ScrollView(ScrollViewMode.Vertical)
                {
                    horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                    verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible
                };
                _menuScroll.AddToClassList("ee4v-gesture-assignment__menu-scroll");
                _menuScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => RefreshMenuLayout());
                _extraRow.AddToClassList("ee4v-gesture-assignment__menu-grid");
                _menuScroll.Add(_extraRow);
                _menuColumn.Add(_menuScroll);
                _matrix.Add(_menuColumn);
            }
            else { grid.Add(_extraRow); }
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
        internal event Action<AnimationClip> AssignedClipSelected;

        private void SelectAssignment(GestureCombination combination, FaceExpressionMenuEntry menuEntry = null)
        {
            _session.Activate();
            if (menuEntry != null) { _session.Select(menuEntry); }
            else { _session.Select(combination); }
            var clip = _session.SelectedAssignment.Clip;
            if (_readOnly && clip != null) { AssignedClipSelected?.Invoke(clip); }
        }

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
            if (_readOnly)
            {
                var configuration = _session.CreateConfiguration();
                var menuEntries = GestureMatrixControllerWriter.GetEffectiveMenuEntries(
                    configuration.Assignments, configuration.MenuEntries)
                    .Where(entry => entry.IsExplicit).ToList();
                if (!MenuContentsMatch(configuration, menuEntries))
                {
                    _extraRow.Clear();
                    _menuSelectionChecks.Clear();
                    var menuCells = new List<VisualElement>();
                    RenderEffectiveMenuCells(menuCells, menuEntries);
                    foreach (var cell in menuCells) { _extraRow.Add(cell); }
                    _menuSnapshot = configuration;
                    _menuSourceSnapshot = _session.MenuEntries.ToArray();
                    _effectiveMenuSnapshot = menuEntries;
                }
                foreach (var pair in _menuSelectionChecks)
                {
                    pair.Key.EnableInClassList("ee4v-gesture-assignment__cell--selected", pair.Value());
                }
                _menuColumn.style.display = menuEntries.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                RefreshOverviewLayout();
                return;
            }
            _extraRow.Clear();
            var items = new List<VisualElement>();
            foreach (var entry in _session.MenuEntries)
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
                    header = CreateHeader(_text.MenuOnly, true);
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

                header.tooltip = _text.MenuOnlyHint;
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

        private bool MenuContentsMatch(FaceExpressionConfiguration configuration,
            IReadOnlyList<GestureMatrixControllerWriter.EffectiveMenuEntry> menuEntries)
        {
            if (_menuSnapshot == null || !_menuSourceSnapshot.SequenceEqual(_session.MenuEntries) ||
                configuration.Assignments.Count != _menuSnapshot.Assignments.Count ||
                configuration.MenuEntries.Count != _menuSnapshot.MenuEntries.Count ||
                menuEntries.Count != _effectiveMenuSnapshot.Count) { return false; }
            if (configuration.Assignments.Any(pair =>
                !_menuSnapshot.Assignments.TryGetValue(pair.Key, out var previous) ||
                !EqualityComparer<FaceExpressionAssignment>.Default.Equals(pair.Value, previous))) { return false; }
            for (var index = 0; index < configuration.MenuEntries.Count; index++)
            {
                var current = configuration.MenuEntries[index];
                var previous = _menuSnapshot.MenuEntries[index];
                if (current.Name != previous.Name ||
                    !EqualityComparer<FaceExpressionAssignment>.Default.Equals(current.Assignment, previous.Assignment))
                { return false; }
            }
            for (var index = 0; index < menuEntries.Count; index++)
            {
                var current = menuEntries[index];
                var previous = _effectiveMenuSnapshot[index];
                if (current.Name != previous.Name || current.LeftGesture != previous.LeftGesture ||
                    current.IsExplicit != previous.IsExplicit) { return false; }
            }
            return true;
        }

        private void RenderEffectiveMenuCells(List<VisualElement> items,
            IReadOnlyList<GestureMatrixControllerWriter.EffectiveMenuEntry> menuEntries)
        {
            var explicitEntries = _session.MenuEntries.Where(entry => entry.Assignment.Clip != null).ToArray();
            var explicitIndex = 0;
            foreach (var item in menuEntries)
            {
                var explicitEntry = explicitEntries[explicitIndex++];
                var cell = new GestureAssignmentCell(_text.Clip, _text.Unassigned,
                    () => SelectAssignment(default, explicitEntry),
                    null, rect => _drawPreview?.Invoke(item.Assignment.Clip, rect));
                cell.SetAssignment(item.Assignment);
                cell.SetReadOnly(true);
                cell.SetCaption(item.Name);
                var sourceName = I18N.Get("assignments.menuSelection");
                cell.tooltip = item.Name + "\n" + sourceName + "\n" + item.Assignment.Clip.name;
                _menuSelectionChecks.Add(cell, () => ReferenceEquals(explicitEntry, _session.SelectedMenuEntry));
                RegisterRemovalMenu(cell, _text.Remove,
                    () => _hasAvatar && _session.MenuEntries.Contains(explicitEntry),
                    () =>
                    {
                        _session.Activate();
                        _session.Select(explicitEntry);
                        _session.RemoveSelectedMenuExpression();
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
            var menuColumns = _extraRow.childCount > 0 ? OverviewMenuColumns : 0;
            var gap = menuColumns > 0 ? Mathf.Min(OverviewMenuGap, width * 0.02f) : 0f;
            var availableWidth = Mathf.Max(0f, width - gap);
            var headerWidth = Mathf.Min(OverviewHeaderWidth, availableWidth / 2f);
            var headerHeight = Mathf.Min(OverviewHeaderHeight, height);
            var cellSize = Mathf.Max(0f, Mathf.Min((availableWidth - headerWidth) / (ColumnCount + menuColumns),
                (height - headerHeight) / ColumnCount));
            var gridWidth = headerWidth + ColumnCount * cellSize;
            var menuWidth = menuColumns * cellSize;
            _overviewGrid.style.width = gridWidth;
            _overviewGrid.style.height = headerHeight + ColumnCount * cellSize;
            for (var index = 0; index <= ColumnCount; index++)
            {
                SetOverviewRowLayout(_overviewGrid[index], index == 0 ? headerHeight : cellSize, headerWidth, cellSize);
            }
            if (menuColumns == 0) { return; }
            _menuColumn.style.width = menuWidth;
            _menuColumn.style.height = height;
            _menuColumn.style.marginLeft = Mathf.Max(gap, width - gridWidth - menuWidth);
            _menuHeader.style.height = headerHeight;
            _menuHeader.style.flexBasis = headerHeight;
            RefreshMenuLayout();
        }

        private void RefreshMenuLayout()
        {
            if (!_readOnly || _menuScroll == null || _menuScroll.contentViewport.contentRect.width <= 0f) { return; }
            var menuCellSize = Mathf.Floor(_menuScroll.contentViewport.contentRect.width / OverviewMenuColumns);
            _extraRow.style.height = Mathf.CeilToInt((float)_extraRow.childCount / OverviewMenuColumns) * menuCellSize;
            foreach (var cell in _extraRow.Children())
            {
                cell.style.width = menuCellSize;
                cell.style.height = menuCellSize;
                cell.style.flexBasis = menuCellSize;
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

        private sealed class Condition
        {
            internal readonly VisualElement Root = new VisualElement();
            internal readonly Dictionary<FaceGesture, UiButton> LeftButtons = new Dictionary<FaceGesture, UiButton>();
            internal readonly Dictionary<FaceGesture, UiButton> RightButtons = new Dictionary<FaceGesture, UiButton>();
            internal SectionHeader Heading;
            internal UiButton AllLeft;
            internal UiButton AllRight;
            internal UiButton MenuButton;
            internal FaceGesture? Left;
            internal FaceGesture? Right;
            internal bool HasLeft;
            internal bool HasRight;
            internal bool IsMenu;
            internal FaceExpressionMenuEntry MenuEntry;
            internal GestureCombination[] AssignedCombinations = Array.Empty<GestureCombination>();

            internal IEnumerable<GestureCombination> GetCombinations()
            {
                if (IsMenu) { return Enumerable.Empty<GestureCombination>(); }
                if (!HasLeft || !HasRight) { return AssignedCombinations; }
                return Expand(Left, Right);
            }

            internal static IEnumerable<GestureCombination> Expand(FaceGesture? leftGesture, FaceGesture? rightGesture)
            {
                return from FaceGesture left in Enum.GetValues(typeof(FaceGesture))
                       from FaceGesture right in Enum.GetValues(typeof(FaceGesture))
                       where (!leftGesture.HasValue || leftGesture.Value == left) &&
                             (!rightGesture.HasValue || rightGesture.Value == right)
                       select new GestureCombination(left, right);
            }

            internal bool KeepsMenu => IsMenu || ((!HasLeft || !HasRight) && MenuEntry != null);
        }

        private readonly GestureAssignmentSession _session;
        private readonly GestureAssignmentViewText _text;
        private readonly ObjectField _candidateField;
        private readonly InputField _menuNameField;
        private readonly UiButton _addCondition;
        private readonly VisualElement _conditionList;
        private readonly VisualElement _expressionPanel;
        private readonly VisualElement _settingsPanel;
        private readonly GestureAssignmentSettingsView _settingsView;
        private readonly ExclusiveButtonGroup<int> _tabs;
        private readonly List<Condition> _conditions = new List<Condition>();
        private HashSet<GestureCombination> _observedCombinations;
        private FaceExpressionMenuEntry[] _observedMenuEntries = Array.Empty<FaceExpressionMenuEntry>();
        private AnimationClip _candidate;
        private bool _hasAvatar;
        private bool _updatingConditions;
        private int _candidateGeneration;
        private bool _updatingCommonInputs;
        private bool _refreshing;
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
            Header = new VisualElement();
            Header.AddToClassList("ee4v-gesture-picker__header");
            Header.Add(new SectionHeader(I18N.Get("assignments.expressionInputs")));
            Add(Header);
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
                if (_hasAvatar && _candidate != null)
                {
                    _session.Activate();
                    _session.SetClipMenuName(_candidate, value);
                }
            };
            var menuName = new FormInput(_text.MenuName, _menuNameField);
            menuName.AddToClassList("ee4v-gesture-picker__name-input");
            inputs.Add(menuName);
            Header.Add(inputs);

            _tabs = new ExclusiveButtonGroup<int>(new[]
            {
                new ExclusiveButtonOption<int>(ExpressionTab, I18N.Get("assignments.expressionTab")),
                new ExclusiveButtonOption<int>(SettingsTab, I18N.Get("assignments.settingsTab"))
            }, _selectedTab, SelectTab, tabVariant: SelectionTabVariant.Underline);
            _tabs.AddToClassList("ee4v-gesture-picker__tabs");
            foreach (var tab in _tabs.Children()) { tab.AddToClassList("ee4v-gesture-picker__tab"); }
            Header.Add(_tabs);
            _expressionPanel = CreatePanel();
            _settingsPanel = CreatePanel();
            _conditionList = new VisualElement();
            _conditionList.AddToClassList("ee4v-gesture-picker__conditions");
            _expressionPanel.Add(_conditionList);
            _settingsView = new GestureAssignmentSettingsView(_text, settings, _session, embedded: true);
            _settingsPanel.Add(_settingsView);

            Footer = new VisualElement();
            Footer.AddToClassList("ee4v-gesture-picker__footer");
            _addCondition = new UiButton(I18N.Get("assignments.addCondition"), () => AddCondition());
            _addCondition.AddToClassList("ee4v-gesture-picker__add-condition");
            Footer.Add(_addCondition);
            Add(Footer);
            RegisterCallback<AttachToPanelEvent>(_ => { _session.Changed += Refresh; Refresh(); });
            RegisterCallback<DetachFromPanelEvent>(_ => _session.Changed -= Refresh);
            Refresh();
        }

        internal VisualElement Header { get; }
        internal VisualElement Footer { get; }
        internal event Action<AnimationClip> CandidateChanged;
        internal Func<AnimationClip, Task<AnimationClip>> PrepareCandidate { get; set; }
        internal event Action<Exception> AssignmentFailed;

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

        private Condition AddCondition(FaceGesture? left = null, FaceGesture? right = null,
            bool hasHands = false, FaceExpressionMenuEntry menuEntry = null, bool refreshButtons = true)
        {
            var condition = new Condition
            {
                Left = left, Right = right, HasLeft = hasHands, HasRight = hasHands,
                IsMenu = menuEntry != null, MenuEntry = menuEntry
            };
            condition.Root.AddToClassList("ee4v-gesture-picker__condition");
            var heading = condition.Heading = new SectionHeader();
            heading.AddToClassList("ee4v-gesture-picker__condition-heading");
            var remove = new UiButton(string.Empty, () => RemoveCondition(condition),
                I18N.Get("assignments.removeCondition"),
                FluentUiIcons.CreateState("dismiss.png", UiSizeTokens.Size12), UiButtonVariant.Ghost);
            remove.AddToClassList("ee4v-gesture-picker__condition-remove");
            heading.Actions.Add(remove);
            condition.Root.Add(heading);
            var hands = new VisualElement();
            hands.AddToClassList("ee4v-gesture-picker__hand-selectors");
            hands.Add(CreateHand(condition, _text.LeftHand, true, condition.LeftButtons, out condition.AllLeft));
            hands.Add(CreateHand(condition, _text.RightHand, false, condition.RightButtons, out condition.AllRight));
            condition.Root.Add(hands);
            condition.MenuButton = new UiButton(I18N.Get("assignments.menuSelection"), () => SelectMenu(condition));
            condition.MenuButton.AddToClassList("ee4v-gesture-picker__menu-selection");
            condition.Root.Add(condition.MenuButton);
            condition.AssignedCombinations = condition.GetCombinations().ToArray();
            _conditions.Add(condition);
            _conditionList.Add(condition.Root);
            GestureAssignmentView.RegisterRemovalMenu(condition.Root, I18N.Get("assignments.removeCondition"),
                () => _hasAvatar && _candidate != null, () => RemoveCondition(condition));
            if (refreshButtons) RefreshConditionButtons();
            return condition;
        }

        private VisualElement CreateHand(Condition condition, string title, bool left,
            Dictionary<FaceGesture, UiButton> buttons, out UiButton all)
        {
            var column = new VisualElement();
            column.AddToClassList("ee4v-gesture-picker__hand-column");
            column.EnableInClassList("ee4v-gesture-picker__hand-column--right", !left);
            column.Add(new SectionHeader(title));
            all = new UiButton(_text.AllGestures ?? I18N.Get("assignments.allGestures"),
                () => SelectHand(condition, left, null));
            all.AddToClassList("ee4v-gesture-picker__hand-all");
            column.Add(all);
            var row = new VisualElement();
            row.AddToClassList("ee4v-gesture-picker__hands");
            foreach (FaceGesture gesture in Enum.GetValues(typeof(FaceGesture)))
            {
                var button = new UiButton(_text.GestureName?.Invoke(gesture) ?? gesture.ToString(),
                    () => SelectHand(condition, left, gesture));
                button.AddToClassList("ee4v-gesture-picker__hand");
                buttons.Add(gesture, button);
                row.Add(button);
            }
            column.Add(row);
            return column;
        }

        private void SelectHand(Condition condition, bool left, FaceGesture? gesture)
        {
            if (!CanSelectHand(condition, left, gesture)) { return; }
            condition.IsMenu = false;
            if (left) { condition.Left = gesture; condition.HasLeft = true; }
            else { condition.Right = gesture; condition.HasRight = true; }
            if (condition.HasLeft && condition.HasRight)
            {
                condition.MenuEntry = null;
                ApplyConditions(condition);
                return;
            }
            RefreshConditionButtons();
        }

        private void SelectMenu(Condition condition)
        {
            if (!CanSelectMenu(condition)) { return; }
            condition.IsMenu = true;
            condition.HasLeft = false;
            condition.HasRight = false;
            if (condition.MenuEntry == null)
            {
                var assignment = _session.GetClipAssignments(_candidate).FirstOrDefault();
                var hasAssignment = assignment.Clip != null;
                condition.MenuEntry = new FaceExpressionMenuEntry(_draftMenuName,
                    new FaceExpressionAssignment(_candidate,
                        hasAssignment ? assignment.EnableBlink : _settingsView.DraftEnableBlink,
                        hasAssignment ? assignment.FixMouth : _settingsView.DraftFixMouth));
            }
            ApplyConditions(condition);
        }

        private void RemoveCondition(Condition condition)
        {
            _conditions.Remove(condition);
            condition.Root.RemoveFromHierarchy();
            ApplyConditions(null);
            if (_conditions.Count == 0) { AddCondition(); }
            RefreshConditionButtons();
        }

        private async void ApplyConditions(Condition selected)
        {
            if (!_hasAvatar || _candidate == null || _updatingConditions) { return; }
            var generation = _candidateGeneration;
            _updatingConditions = true;
            SetEnabled(false);
            Header.SetEnabled(false);
            Footer.SetEnabled(false);
            try
            {
                if (PrepareCandidate != null)
                {
                    var prepared = await PrepareCandidate(_candidate);
                    if (generation != _candidateGeneration || !_hasAvatar || prepared == null) return;
                    _candidate = prepared;
                    _candidateField.SetValueWithoutNotify(prepared);
                    foreach (var condition in _conditions.Where(condition => condition.MenuEntry != null))
                    {
                        var previous = condition.MenuEntry;
                        condition.MenuEntry = new FaceExpressionMenuEntry(previous.Name,
                            new FaceExpressionAssignment(prepared, previous.Assignment.EnableBlink, previous.Assignment.FixMouth));
                    }
                }
                var assignment = _session.GetClipAssignments(_candidate).FirstOrDefault();
                var hasAssignment = assignment.Clip != null;
                _session.Activate();
                var applied = _session.SetClipConditions(_candidate,
                    _conditions.SelectMany(condition => condition.GetCombinations()),
                    _conditions.Where(condition => condition.KeepsMenu).Select(condition => condition.MenuEntry).ToArray(),
                    _draftMenuName,
                    hasAssignment ? assignment.EnableBlink : _settingsView.DraftEnableBlink,
                    hasAssignment ? assignment.FixMouth : _settingsView.DraftFixMouth);
                if (!applied) { _observedCombinations = null; }
                else
                {
                    foreach (var condition in _conditions)
                    {
                        condition.AssignedCombinations = condition.GetCombinations().ToArray();
                    }
                    CaptureConditions();
                    if (selected != null)
                    {
                        if (selected.IsMenu) { _session.Select(selected.MenuEntry); }
                        else
                        {
                            _session.SelectHands(selected.Left, selected.Right);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { _observedCombinations = null; }
            catch (Exception exception) { _observedCombinations = null; AssignmentFailed?.Invoke(exception); }
            finally
            {
                _updatingConditions = false;
                SetEnabled(true);
                Header.SetEnabled(true);
                Footer.SetEnabled(true);
                Refresh();
            }
        }

        private HashSet<GestureCombination> GetClipCombinations()
        {
            return new HashSet<GestureCombination>(_session.CreateConfiguration().Assignments
                .Where(pair => _candidate != null && pair.Value.Clip == _candidate).Select(pair => pair.Key));
        }

        private FaceExpressionMenuEntry[] GetClipMenuEntries()
        {
            return _session.MenuEntries.Where(entry => _candidate != null && entry.Assignment.Clip == _candidate).ToArray();
        }

        private void CaptureConditions()
        {
            _observedCombinations = GetClipCombinations();
            _observedMenuEntries = GetClipMenuEntries();
        }

        private void RefreshConditions()
        {
            if (_updatingConditions) { return; }
            var combinations = GetClipCombinations();
            var menuEntries = GetClipMenuEntries();
            if (_observedCombinations != null && _observedCombinations.SetEquals(combinations) &&
                _observedMenuEntries.SequenceEqual(menuEntries)) { return; }
            _conditions.Clear();
            _conditionList.Clear();
            var remaining = new HashSet<GestureCombination>(combinations);
            var gestures = Enum.GetValues(typeof(FaceGesture)).Cast<FaceGesture>().ToArray();
            if (remaining.Count == gestures.Length * gestures.Length)
            {
                AddCondition(null, null, hasHands: true, refreshButtons: false);
                remaining.Clear();
            }
            foreach (var left in gestures)
            {
                var row = gestures.Select(right => new GestureCombination(left, right)).ToArray();
                if (!row.All(combinations.Contains) || !row.Any(remaining.Contains)) { continue; }
                AddCondition(left, null, hasHands: true, refreshButtons: false);
                remaining.ExceptWith(row);
            }
            foreach (var right in gestures)
            {
                var column = gestures.Select(left => new GestureCombination(left, right)).ToArray();
                if (!column.All(combinations.Contains) || !column.Any(remaining.Contains)) { continue; }
                AddCondition(null, right, hasHands: true, refreshButtons: false);
                remaining.ExceptWith(column);
            }
            foreach (var combination in remaining.OrderBy(value => value.Left).ThenBy(value => value.Right))
            {
                AddCondition(combination.Left, combination.Right, hasHands: true, refreshButtons: false);
            }
            foreach (var entry in menuEntries) { AddCondition(menuEntry: entry, refreshButtons: false); }
            if (_conditions.Count == 0) { AddCondition(refreshButtons: false); }
            _observedCombinations = combinations;
            _observedMenuEntries = menuEntries;
        }

        private void RefreshConditionButtons()
        {
            for (var index = 0; index < _conditions.Count; index++)
            {
                var condition = _conditions[index];
                var own = new HashSet<GestureCombination>(condition.AssignedCombinations);
                var others = new HashSet<GestureCombination>(_conditions.Where(other => other != condition)
                    .SelectMany(other => other.GetCombinations()));
                condition.Heading.SetTitle(I18N.Get("assignments.conditionTitle", index + 1));
                condition.Heading.style.display = _conditions.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
                condition.Root.SetEnabled(_hasAvatar && _candidate != null);
                foreach (var pair in condition.LeftButtons)
                {
                    pair.Value.EnableInClassList("ee4v-gesture-picker__hand--selected",
                        !condition.IsMenu && condition.HasLeft && condition.Left == pair.Key);
                    pair.Value.SetEnabled(CanSelectHand(condition, true, pair.Key, own, others));
                }
                foreach (var pair in condition.RightButtons)
                {
                    pair.Value.EnableInClassList("ee4v-gesture-picker__hand--selected",
                        !condition.IsMenu && condition.HasRight && condition.Right == pair.Key);
                    pair.Value.SetEnabled(CanSelectHand(condition, false, pair.Key, own, others));
                }
                condition.AllLeft.EnableInClassList("ee4v-gesture-picker__hand--selected",
                    !condition.IsMenu && condition.HasLeft && !condition.Left.HasValue);
                condition.AllRight.EnableInClassList("ee4v-gesture-picker__hand--selected",
                    !condition.IsMenu && condition.HasRight && !condition.Right.HasValue);
                condition.AllLeft.SetEnabled(CanSelectHand(condition, true, null, own, others));
                condition.AllRight.SetEnabled(CanSelectHand(condition, false, null, own, others));
                condition.MenuButton.EnableInClassList("ee4v-gesture-picker__hand--selected", condition.IsMenu);
                condition.MenuButton.SetEnabled(CanSelectMenu(condition));
            }
        }

        private bool CanSelectHand(Condition condition, bool left, FaceGesture? gesture,
            ISet<GestureCombination> own = null, ISet<GestureCombination> others = null)
        {
            if (!_hasAvatar || _candidate == null || _updatingConditions) { return false; }
            if (left ? !condition.HasRight : !condition.HasLeft) { return true; }
            var targets = Condition.Expand(left ? gesture : condition.Left, left ? condition.Right : gesture);
            own ??= new HashSet<GestureCombination>(condition.AssignedCombinations);
            others ??= new HashSet<GestureCombination>(_conditions.Where(other => other != condition)
                .SelectMany(other => other.GetCombinations()));
            return targets.All(combination => !others.Contains(combination) &&
                (_session.GetAssignment(combination).Clip == null ||
                 (_session.GetAssignment(combination).Clip == _candidate && own.Contains(combination))));
        }

        private bool CanSelectMenu(Condition condition)
        {
            return _hasAvatar && _candidate != null && !_updatingConditions &&
                !_session.MenuEntries.Any(entry => entry.Assignment.Clip == _candidate) &&
                !_conditions.Any(other => other != condition && other.KeepsMenu);
        }

        private void RefreshCommonInputs()
        {
            var names = _session.CreateConfiguration().Assignments.Values
                .Where(value => _candidate != null && value.Clip == _candidate).Select(value => value.MenuName)
                .Concat(GetClipMenuEntries().Select(entry => entry.Name)).Distinct().ToArray();
            var mixed = names.Length > 1;
            if (names.Length > 0) { _draftMenuName = mixed ? string.Empty : names[0]; }
            _updatingCommonInputs = true;
            _menuNameField.SetValueWithoutNotify(_draftMenuName);
            _menuNameField.SetPlaceholder(mixed ? I18N.Get("assignments.mixedValues") :
                _candidate != null ? _candidate.name : I18N.Get("assignments.clipNamePlaceholder"));
            _menuNameField.SetEnabled(_hasAvatar);
            _updatingCommonInputs = false;
        }

        internal void SetCandidate(AnimationClip clip)
        {
            if (_candidate != clip)
            {
                _candidateGeneration++;
                _draftMenuName = string.Empty;
                _observedCombinations = null;
            }
            _candidate = clip;
            _candidateField.SetValueWithoutNotify(clip);
            Refresh();
        }

        internal void SetAvatar(GameObject avatar)
        {
            _candidateGeneration++;
            _hasAvatar = avatar != null;
            _candidateField.SetEnabled(_hasAvatar);
            _observedCombinations = null;
            Refresh();
        }

        private void Refresh()
        {
            if (_refreshing || _updatingConditions) { return; }
            _refreshing = true;
            try
            {
                var menuSelectionChanged = !ReferenceEquals(_observedMenuEntry, _session.SelectedMenuEntry);
                var selectionChanged = _observedHasSelection != _session.HasSelection ||
                    !_observedCombination.Equals(_session.SelectedCombination) ||
                    _observedAllLeft != _session.IsAllLeftSelected || _observedAllRight != _session.IsAllRightSelected ||
                    menuSelectionChanged;
                _observedHasSelection = _session.HasSelection;
                _observedCombination = _session.SelectedCombination;
                _observedAllLeft = _session.IsAllLeftSelected;
                _observedAllRight = _session.IsAllRightSelected;
                _observedMenuEntry = _session.SelectedMenuEntry;
                if (selectionChanged && _session.HasSelection)
                {
                    _selectedTab = ExpressionTab;
                    if (!_updatingConditions && menuSelectionChanged && _session.IsMenuSelection)
                    {
                        CandidateChanged?.Invoke(_session.SelectedAssignment.Clip);
                    }
                }
                RefreshConditions();
                RefreshCommonInputs();
                RefreshConditionButtons();
                _tabs.SetSelected(_selectedTab);
                _expressionPanel.style.display = _selectedTab == ExpressionTab ? DisplayStyle.Flex : DisplayStyle.None;
                _settingsPanel.style.display = _selectedTab == SettingsTab ? DisplayStyle.Flex : DisplayStyle.None;
                _settingsView.SetClipTarget(_hasAvatar ? _candidate : null);
                _settingsView.SetExpressionDraft(_hasAvatar && _candidate != null &&
                    !_session.GetClipAssignments(_candidate).Any());
                _addCondition.SetEnabled(_hasAvatar && _candidate != null);
                Footer.style.display = _selectedTab == SettingsTab ? DisplayStyle.None : DisplayStyle.Flex;
            }
            finally { _refreshing = false; }
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
        private AnimationClip _clipTarget;
        private bool _expressionDraft;
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
                if (_expressionDraft) { DraftEnableBlink = evt.newValue; }
                if (ClipTarget != null) { _session.SetClipBlink(ClipTarget, evt.newValue); }
                else if (!_expressionDraft) { _session.SetSelectedBlink(evt.newValue); }
            });
            expressionControls.Add(CreateCheckboxRow(_text.EnableBlink, _blinkToggle));
            _mouthToggle = UiTextFactory.CreateToggle();
            _mouthToggle.RegisterValueChangedCallback(evt =>
            {
                if (_rendering) { return; }
                if (_expressionDraft) { DraftFixMouth = evt.newValue; }
                if (ClipTarget != null) { _session.SetClipMouth(ClipTarget, evt.newValue); }
                else if (!_expressionDraft) { _session.SetSelectedMouth(evt.newValue); }
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

        private AnimationClip ClipTarget => _clipTarget != null ? _clipTarget : _session.SelectedAssignment.Clip;

        internal void SetClipTarget(AnimationClip clip)
        {
            if (_clipTarget != clip) { _expressionDraft = false; }
            _clipTarget = clip;
            RefreshState();
        }

        internal void SetExpressionDraft(bool expressionDraft)
        {
            if (_expressionDraft == expressionDraft) { return; }
            _expressionDraft = expressionDraft;
            if (expressionDraft)
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
            row.AddToClassList("ee4v-gesture-assignment__property-row");
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
            var clipAssignments = _session.GetClipAssignments(ClipTarget).ToArray();
            var assignment = clipAssignments.Length > 0 ? clipAssignments[0] : _expressionDraft
                ? new FaceExpressionAssignment(null, DraftEnableBlink, DraftFixMouth)
                : _session.SelectedAssignment;
            if (_expressionDraft && clipAssignments.Length > 0)
            {
                DraftEnableBlink = assignment.EnableBlink;
                DraftFixMouth = assignment.FixMouth;
            }
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
            _blinkToggle.showMixedValue = clipAssignments.Length > 0
                ? clipAssignments.Any(value => value.EnableBlink != assignment.EnableBlink)
                : !_expressionDraft && _session.HasMixedSelection(value => value.EnableBlink);
            _mouthToggle.showMixedValue = clipAssignments.Length > 0
                ? clipAssignments.Any(value => value.FixMouth != assignment.FixMouth)
                : !_expressionDraft && _session.HasMixedSelection(value => value.FixMouth);
            _menuNameField.SetEnabled(_session.HasSelection);
            var canEditExpression = _expressionDraft || clipAssignments.Length > 0 || (ClipTarget == null && _session.HasSelection);
            _blinkToggle.SetEnabled(canEditExpression);
            _mouthToggle.SetEnabled(canEditExpression);
            _menuIconsToggle.SetValueWithoutNotify(!FaceExpressionSettings.GetMenuIconsDisabled(_settings));
            _rendering = false;
        }

        private string GetGestureName(FaceGesture gesture)
        {
            return _text.GestureName?.Invoke(gesture) ?? gesture.ToString();
        }
    }
}
