using System;
using System.Collections.Generic;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class GestureAssignmentViewText
    {
        public string Avatar { get; set; }
        public string Apply { get; set; }
        public string LibraryHint { get; set; }
        public string LeftHand { get; set; }
        public string RightHand { get; set; }
        public string Selection { get; set; }
        public string ExpressionSettings { get; set; }
        public string Synchronization { get; set; }
        public string SynchronizeLeft { get; set; }
        public string SynchronizeRight { get; set; }
        public string GlobalSettings { get; set; }
        public string Clip { get; set; }
        public string EnableBlink { get; set; }
        public string FixMouth { get; set; }
        public string Unassigned { get; set; }
        public string MenuOnly { get; set; }
        public string MenuOnlyHint { get; set; }
        public string MenuName { get; set; }
        public string DisableMenuIcons { get; set; }
        public string AddMenuExpression { get; set; }
        public string Remove { get; set; }
        public Func<FaceGesture, string> GestureName { get; set; }
    }

    internal sealed class GestureAssignmentView : VisualElement
    {
        private const int ColumnCount = 8;
        private const float RowHeaderWidth = 112f;
        private const float CellWidth = 112f;
        private const float CellHeight = 128f;

        private readonly ObjectField _avatarField;
        private readonly UiButton _applyButton;
        private readonly VisualElement _extraRow;
        private readonly Dictionary<GestureCombination, GestureAssignmentCell> _cells =
            new Dictionary<GestureCombination, GestureAssignmentCell>();
        private readonly GestureAssignmentViewText _text;
        private readonly GestureAssignmentSession _session;
        private readonly Action<AnimationClip, Rect> _drawPreview;
        private bool _hasAvatar;
        private bool _rendering;
        private bool _subscribed;

        public GestureAssignmentView(
            GestureAssignmentViewText text,
            Action<AnimationClip, Rect> drawPreview = null,
            bool showAvatarField = true,
            GestureAssignmentSession session = null)
        {
            _text = text ?? new GestureAssignmentViewText();
            _session = session ?? new GestureAssignmentSession();
            _drawPreview = drawPreview;
            AddToClassList("ee4v-gesture-assignment");

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
            }
            else
            {
                var hint = UiTextFactory.Create(_text.LibraryHint);
                hint.SetWhiteSpace(WhiteSpace.Normal);
                toolbar.Leading.Add(hint);
            }
            _applyButton = new UiButton(
                _text.Apply,
                () => ApplyRequested?.Invoke());
            _applyButton.AddToClassList("ee4v-gesture-assignment__apply");
            toolbar.Actions.Add(_applyButton);
            Add(toolbar);

            var matrix = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            matrix.AddToClassList("ee4v-gesture-assignment__matrix");
            var header = new VisualElement();
            header.AddToClassList("ee4v-gesture-assignment__matrix-row");
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

            matrix.Add(header);
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
                    row.Add(cell);
                }

                matrix.Add(row);
            }

            _extraRow = new VisualElement();
            _extraRow.AddToClassList("ee4v-gesture-assignment__extra-row");
            matrix.Add(_extraRow);
            Add(matrix);

            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            RefreshState();
        }

        public event Action<GameObject> AvatarChanged;
        public event Action ApplyRequested;

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

        public void SetApplyEnabled(bool enabled, string tooltip = null)
        {
            _applyButton.SetEnabled(enabled);
            _applyButton.tooltip = tooltip ?? string.Empty;
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
                pair.Value.SetClipEnabled(_hasAvatar);
                pair.Value.EnableInClassList(
                    "ee4v-gesture-assignment__cell--selected",
                    !_session.IsMenuSelection &&
                    pair.Key.Equals(_session.SelectedCombination));
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

        private static VisualElement CreateCellRow()
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-gesture-assignment__matrix-row");
            row.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var dataWidth = evt.newRect.width - RowHeaderWidth;
                if (dataWidth <= 0f)
                {
                    return;
                }

                var height = Mathf.Max(
                    CellHeight,
                    dataWidth / ColumnCount * CellHeight / CellWidth);
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
                cell.SetClipEnabled(_hasAvatar);
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
            items.Add(add);

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
        }

    }

    internal sealed class GestureAssignmentSettingsView : ScrollView
    {
        private readonly GestureAssignmentViewText _text;
        private readonly GestureAssignmentSession _session;
        private readonly UiTextElement _selectionLabel;
        private readonly InputField _menuNameField;
        private readonly UiButton _removeMenuButton;
        private readonly Toggle _blinkToggle;
        private readonly Toggle _mouthToggle;
        private readonly Toggle _synchronizeLeftToggle;
        private readonly Toggle _synchronizeRightToggle;
        private readonly Toggle _menuIconsToggle;
        private readonly ISettingsService _settings;
        private bool _rendering;
        private bool _subscribed;

        internal GestureAssignmentSettingsView(
            GestureAssignmentViewText text,
            ISettingsService settings = null,
            GestureAssignmentSession session = null)
            : base(ScrollViewMode.Vertical)
        {
            _text = text ?? new GestureAssignmentViewText();
            _session = session ?? new GestureAssignmentSession();
            _settings = settings ?? CoreSettings.Current;
            _settings.Register(FaceExpressionSettings.MenuIconsDisabled);
            AddToClassList("ee4v-gesture-assignment-settings");

            var selectionHeader = new SectionHeader();
            _selectionLabel = selectionHeader.TitleText;
            _selectionLabel.AddToClassList(
                "ee4v-gesture-assignment__selection");
            Add(selectionHeader);

            _menuNameField = new InputField
            {
                IsDelayed = true
            };
            _menuNameField.AddToClassList(
                "ee4v-gesture-assignment__extra-name");
            _menuNameField.ValueChanged += value =>
            {
                if (!_rendering &&
                    value != _session.SelectedMenuName)
                {
                    _session.SetSelectedMenuName(value);
                }
            };
            _removeMenuButton = new UiButton(
                _text.Remove,
                _session.RemoveSelectedMenuExpression);
            _removeMenuButton.AddToClassList(
                "ee4v-gesture-assignment__extra-remove");
            var menuControls = new FormInput(
                _text.MenuName,
                _menuNameField,
                _removeMenuButton);
            menuControls.AddToClassList(
                "ee4v-gesture-assignment__extra-controls");
            Add(menuControls);

            var expressionHeader = new SectionHeader(
                _text.ExpressionSettings);
            expressionHeader.AddToClassList(
                "ee4v-gesture-assignment__settings-section");
            Add(expressionHeader);

            var expressionControls = new VisualElement();
            expressionControls.AddToClassList(
                "ee4v-gesture-assignment__controls");
            _blinkToggle = UiTextFactory.CreateToggle(
                _text.EnableBlink,
                "ee4v-gesture-assignment__toggle");
            _blinkToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    _session.UpdateSelection(
                        evt.newValue,
                        _mouthToggle.value);
                }
            });
            expressionControls.Add(_blinkToggle);

            _mouthToggle = UiTextFactory.CreateToggle(
                _text.FixMouth,
                "ee4v-gesture-assignment__toggle");
            _mouthToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    _session.UpdateSelection(
                        _blinkToggle.value,
                        evt.newValue);
                }
            });
            expressionControls.Add(_mouthToggle);
            Add(expressionControls);

            var synchronizationHeader = new SectionHeader(
                _text.Synchronization);
            synchronizationHeader.AddToClassList(
                "ee4v-gesture-assignment__settings-section");
            Add(synchronizationHeader);

            var synchronizationControls = new VisualElement();
            synchronizationControls.AddToClassList(
                "ee4v-gesture-assignment__controls");
            _synchronizeLeftToggle = UiTextFactory.CreateToggle(
                _text.SynchronizeLeft,
                "ee4v-gesture-assignment__toggle");
            _synchronizeLeftToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    _session.SetSelectedLeftSynchronized(
                        evt.newValue);
                }
            });
            synchronizationControls.Add(_synchronizeLeftToggle);
            _synchronizeRightToggle = UiTextFactory.CreateToggle(
                _text.SynchronizeRight,
                "ee4v-gesture-assignment__toggle");
            _synchronizeRightToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    _session.SetSelectedRightSynchronized(
                        evt.newValue);
                }
            });
            synchronizationControls.Add(_synchronizeRightToggle);
            Add(synchronizationControls);

            var globalHeader = new SectionHeader(
                _text.GlobalSettings);
            globalHeader.AddToClassList(
                "ee4v-gesture-assignment__settings-section");
            Add(globalHeader);

            var globalControls = new VisualElement();
            globalControls.AddToClassList(
                "ee4v-gesture-assignment__controls");
            _menuIconsToggle = UiTextFactory.CreateToggle(
                _text.DisableMenuIcons,
                "ee4v-gesture-assignment__toggle");
            _menuIconsToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    _settings.Set(
                        FaceExpressionSettings.MenuIconsDisabled,
                        evt.newValue);
                }
            });
            globalControls.Add(_menuIconsToggle);
            Add(globalControls);

            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            RefreshState();
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _session.Changed += RefreshState;
            _settings.Changed += OnSettingChanged;
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
            _settings.Changed -= OnSettingChanged;
            _subscribed = false;
        }

        private void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (args.Definition.Key == FaceExpressionSettings.MenuIconsDisabled.Key)
            {
                RefreshState();
            }
        }

        private void RefreshState()
        {
            var assignment = _session.SelectedAssignment;
            var isExtra = _session.IsMenuSelection;
            _rendering = true;
            _selectionLabel.SetText(isExtra
                ? _text.Selection + ": " + _text.MenuOnly
                : _text.Selection + ": " +
                  GetGestureName(_session.SelectedCombination.Left) +
                  " + " +
                  GetGestureName(_session.SelectedCombination.Right));
            _menuNameField.SetValueWithoutNotify(
                _session.SelectedMenuName);
            _menuNameField.style.display = DisplayStyle.Flex;
            _removeMenuButton.style.display = isExtra
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _blinkToggle.SetValueWithoutNotify(assignment.EnableBlink);
            _mouthToggle.SetValueWithoutNotify(assignment.FixMouth);
            _synchronizeLeftToggle.SetValueWithoutNotify(
                _session.IsSelectedLeftSynced);
            _synchronizeRightToggle.SetValueWithoutNotify(
                _session.IsSelectedRightSynced);
            _synchronizeLeftToggle.SetEnabled(!isExtra);
            _synchronizeRightToggle.SetEnabled(!isExtra);
            _menuIconsToggle.SetValueWithoutNotify(
                FaceExpressionSettings.GetMenuIconsDisabled(_settings));
            _rendering = false;
        }

        private string GetGestureName(FaceGesture gesture)
        {
            return _text.GestureName?.Invoke(gesture) ?? gesture.ToString();
        }
    }
}
