using System;
using System.Linq;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class GestureAssignmentCell : VisualElement
    {
        private readonly ObjectField _clipField;
        private readonly UiTextElement _empty;
        private readonly PreviewContainer _previewArea;
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

            _previewArea = new PreviewContainer();
            _previewArea.AddToClassList(
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
            _previewArea.Content.Add(_preview);
            _empty = UiTextFactory.Create(
                unassignedText,
                UiClassNames.SecondaryText,
                "ee4v-gesture-assignment__cell-empty");
            _empty.SetTextAlign(TextAnchor.MiddleCenter);
            _empty.pickingMode = PickingMode.Ignore;
            _previewArea.Placeholder.Add(_empty);
            Add(_previewArea);

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
                if (!_clipField.enabledSelf || GetDraggedClip() == null)
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                evt.StopPropagation();
            });
            RegisterCallback<DragPerformEvent>(evt =>
            {
                var clip = GetDraggedClip();
                if (!_clipField.enabledSelf || clip == null)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                _clipField.value = clip;
                evt.StopPropagation();
            });
        }

        internal void SetClipEnabled(bool enabled)
        {
            _clipField.SetEnabled(enabled);
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
            _empty.SetText(clip == null ? _unassignedText : string.Empty);
            _previewArea.SetHasContent(clip != null);
            tooltip = clip == null ? _unassignedText : clip.name;
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
