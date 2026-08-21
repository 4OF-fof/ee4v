using System;
using System.Collections.Generic;
using System.Linq;
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
        public string Apply { get; set; }
        public string LeftHand { get; set; }
        public string RightHand { get; set; }
        public string Selection { get; set; }
        public string Clip { get; set; }
        public string EnableBlink { get; set; }
        public string FixMouth { get; set; }
        public string Unassigned { get; set; }
        public string MenuOnly { get; set; }
        public string MenuOnlyHint { get; set; }
        public string MenuName { get; set; }
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
        private readonly UiTextButton _applyButton;
        private readonly VisualElement _extraRow;
        private readonly Dictionary<GestureCombination, GestureAssignmentCell> _cells =
            new Dictionary<GestureCombination, GestureAssignmentCell>();
        private readonly GestureAssignmentViewText _text;
        private readonly Action<AnimationClip, Rect> _drawPreview;
        private bool _rendering;
        private bool _subscribed;

        public GestureAssignmentView(
            GestureAssignmentViewText text,
            Action<AnimationClip, Rect> drawPreview = null)
        {
            _text = text ?? new GestureAssignmentViewText();
            _drawPreview = drawPreview;
            AddToClassList("ee4v-gesture-assignment");

            var toolbar = new VisualElement();
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
            toolbar.Add(_avatarField);
            _applyButton = UiTextFactory.CreateButton(
                _text.Apply,
                () => ApplyRequested?.Invoke());
            _applyButton.AddToClassList("ee4v-ui-button");
            _applyButton.AddToClassList("ee4v-gesture-assignment__apply");
            toolbar.Add(_applyButton);
            Add(toolbar);

            var matrix = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            matrix.AddToClassList("ee4v-gesture-assignment__matrix");
            matrix.verticalScroller.AddToClassList(
                "ee4v-gesture-assignment__vertical-scroller");
            matrix.horizontalScroller.AddToClassList(
                "ee4v-gesture-assignment__horizontal-scroller");
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
                        () => GestureAssignmentSession.Select(combination),
                        clip => GestureAssignmentSession.SetClip(combination, clip),
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
            _rendering = true;
            _avatarField.SetValueWithoutNotify(avatar);
            _rendering = false;
        }

        public void SetConfiguration(FaceExpressionConfiguration configuration)
        {
            GestureAssignmentSession.SetConfiguration(configuration);
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

            GestureAssignmentSession.Changed += RefreshState;
            _subscribed = true;
            RefreshState();
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GestureAssignmentSession.Changed -= RefreshState;
            _subscribed = false;
        }

        private void RefreshState()
        {
            foreach (var pair in _cells)
            {
                pair.Value.SetAssignment(
                    GestureAssignmentSession.GetAssignment(pair.Key));
                pair.Value.EnableInClassList(
                    "ee4v-gesture-assignment__cell--selected",
                    !GestureAssignmentSession.IsMenuSelection &&
                    pair.Key.Equals(GestureAssignmentSession.SelectedCombination));
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
                GestureAssignmentSession.GetAssignment(combination).Clip,
                rect);
        }

        private void RenderMenuEntries()
        {
            _extraRow.Clear();
            var items = new List<VisualElement>();
            foreach (var entry in GestureAssignmentSession.MenuEntries)
            {
                var cell = new GestureAssignmentCell(
                    _text.Clip,
                    _text.Unassigned,
                    () => GestureAssignmentSession.Select(entry),
                    clip => GestureAssignmentSession.SetClip(entry, clip),
                    rect => _drawPreview?.Invoke(entry.Assignment.Clip, rect));
                cell.SetAssignment(entry.Assignment);
                cell.EnableInClassList(
                    "ee4v-gesture-assignment__cell--selected",
                    ReferenceEquals(
                        entry,
                        GestureAssignmentSession.SelectedMenuEntry));
                items.Add(cell);
            }

            var add = UiTextFactory.CreateButton(
                "+",
                GestureAssignmentSession.AddMenuExpression,
                UiClassNames.NavigationItemLabel);
            add.AddToClassList("ee4v-gesture-assignment__cell");
            add.AddToClassList("ee4v-gesture-assignment__extra-add");
            add.tooltip = _text.AddMenuExpression;
            add.TextElement.SetTextAlign(TextAnchor.MiddleCenter);
            add.TextElement.SetFontSize(28);
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

        private sealed class GestureAssignmentCell : VisualElement
        {
            private readonly ObjectField _clipField;
            private readonly UiTextElement _empty;
            private readonly IMGUIContainer _preview;
            private readonly string _unassignedText;

            internal GestureAssignmentCell(
                string clipLabel,
                string unassignedText,
                Action selected,
                Action<AnimationClip> clipChanged,
                Action<Rect> drawPreview)
            {
                _unassignedText = unassignedText;
                AddToClassList("ee4v-gesture-assignment__cell");
                RegisterCallback<MouseDownEvent>(_ => selected?.Invoke());

                var previewArea = new VisualElement();
                previewArea.AddToClassList(
                    "ee4v-gesture-assignment__cell-preview-area");
                _preview = new IMGUIContainer(() =>
                {
                    if (Event.current?.type == EventType.Repaint)
                    {
                        drawPreview?.Invoke(_preview.contentRect);
                    }
                });
                _preview.AddToClassList(
                    "ee4v-gesture-assignment__cell-preview");
                _preview.pickingMode = PickingMode.Ignore;
                previewArea.Add(_preview);
                _empty = UiTextFactory.Create(
                    unassignedText,
                    UiClassNames.SecondaryText,
                    "ee4v-gesture-assignment__cell-empty");
                _empty.pickingMode = PickingMode.Ignore;
                previewArea.Add(_empty);
                Add(previewArea);

                _clipField = UiTextFactory.CreateObjectField(
                    string.Empty,
                    "ee4v-gesture-assignment__cell-input");
                _clipField.objectType = typeof(AnimationClip);
                _clipField.allowSceneObjects = false;
                _clipField.tooltip = clipLabel;
                _clipField.RegisterValueChangedCallback(evt =>
                {
                    var clip = evt.newValue as AnimationClip;
                    SetClip(clip);
                    clipChanged?.Invoke(clip);
                });
                Add(_clipField);

                RegisterCallback<DragUpdatedEvent>(evt =>
                {
                    if (GetDraggedClip() == null)
                    {
                        return;
                    }

                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    evt.StopPropagation();
                });
                RegisterCallback<DragPerformEvent>(evt =>
                {
                    var clip = GetDraggedClip();
                    if (clip == null)
                    {
                        return;
                    }

                    DragAndDrop.AcceptDrag();
                    _clipField.value = clip;
                    evt.StopPropagation();
                });
            }

            internal void SetAssignment(FaceExpressionAssignment assignment)
            {
                SetClip(assignment.Clip);
                EnableInClassList(
                    "ee4v-gesture-assignment__cell--configured",
                    !assignment.IsDefault);
            }

            private void SetClip(AnimationClip clip)
            {
                _clipField.SetValueWithoutNotify(clip);
                _empty.SetText(clip == null
                    ? _unassignedText
                    : string.Empty);
                tooltip = clip == null
                    ? _unassignedText
                    : clip.name;
                _preview.MarkDirtyRepaint();
            }

            private static AnimationClip GetDraggedClip()
            {
                return DragAndDrop.objectReferences
                    .OfType<AnimationClip>()
                    .FirstOrDefault();
            }
        }
    }

    internal sealed class GestureAssignmentSettingsView : VisualElement
    {
        private readonly GestureAssignmentViewText _text;
        private readonly UiTextElement _selectionLabel;
        private readonly TextField _menuNameField;
        private readonly UiTextButton _removeMenuButton;
        private readonly Toggle _blinkToggle;
        private readonly Toggle _mouthToggle;
        private bool _rendering;
        private bool _subscribed;

        internal GestureAssignmentSettingsView(GestureAssignmentViewText text)
        {
            _text = text ?? new GestureAssignmentViewText();
            AddToClassList("ee4v-gesture-assignment-settings");

            _selectionLabel = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SectionTitle,
                "ee4v-gesture-assignment__selection");
            Add(_selectionLabel);

            var menuControls = new VisualElement();
            menuControls.AddToClassList(
                "ee4v-gesture-assignment__extra-controls");
            _menuNameField = UiTextFactory.CreateTextField(_text.MenuName);
            _menuNameField.AddToClassList(
                "ee4v-gesture-assignment__extra-name");
            _menuNameField.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    GestureAssignmentSession.SetSelectedMenuName(evt.newValue);
                }
            });
            menuControls.Add(_menuNameField);
            _removeMenuButton = UiTextFactory.CreateButton(
                _text.Remove,
                GestureAssignmentSession.RemoveSelectedMenuExpression);
            _removeMenuButton.AddToClassList("ee4v-ui-button");
            _removeMenuButton.AddToClassList(
                "ee4v-gesture-assignment__extra-remove");
            menuControls.Add(_removeMenuButton);
            Add(menuControls);

            var controls = new VisualElement();
            controls.AddToClassList("ee4v-gesture-assignment__controls");
            _blinkToggle = UiTextFactory.CreateToggle(
                _text.EnableBlink,
                "ee4v-gesture-assignment__toggle");
            _blinkToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    GestureAssignmentSession.UpdateSelection(
                        evt.newValue,
                        _mouthToggle.value);
                }
            });
            controls.Add(_blinkToggle);

            _mouthToggle = UiTextFactory.CreateToggle(
                _text.FixMouth,
                "ee4v-gesture-assignment__toggle");
            _mouthToggle.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    GestureAssignmentSession.UpdateSelection(
                        _blinkToggle.value,
                        evt.newValue);
                }
            });
            controls.Add(_mouthToggle);
            Add(controls);

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

            GestureAssignmentSession.Changed += RefreshState;
            _subscribed = true;
            RefreshState();
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GestureAssignmentSession.Changed -= RefreshState;
            _subscribed = false;
        }

        private void RefreshState()
        {
            var assignment = GestureAssignmentSession.SelectedAssignment;
            var isExtra = GestureAssignmentSession.IsMenuSelection;
            _rendering = true;
            _selectionLabel.SetText(isExtra
                ? _text.Selection + ": " + _text.MenuOnly
                : _text.Selection + ": " +
                  GetGestureName(GestureAssignmentSession.SelectedCombination.Left) +
                  " + " +
                  GetGestureName(GestureAssignmentSession.SelectedCombination.Right));
            _menuNameField.SetValueWithoutNotify(
                GestureAssignmentSession.SelectedMenuEntry?.Name ?? string.Empty);
            _menuNameField.style.display = isExtra
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _removeMenuButton.style.display = isExtra
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _blinkToggle.SetValueWithoutNotify(assignment.EnableBlink);
            _mouthToggle.SetValueWithoutNotify(assignment.FixMouth);
            _rendering = false;
        }

        private string GetGestureName(FaceGesture gesture)
        {
            return _text.GestureName?.Invoke(gesture) ?? gesture.ToString();
        }
    }
}
