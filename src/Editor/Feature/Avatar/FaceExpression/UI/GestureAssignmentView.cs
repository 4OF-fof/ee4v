using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class GestureAssignmentViewText
    {
        public string Avatar { get; set; }
        public string Hint { get; set; }
        public string Apply { get; set; }
        public Func<FaceGesture, string> GestureName { get; set; }
    }

    internal sealed class GestureAssignmentView : VisualElement
    {
        private readonly ObjectField _avatarField;
        private readonly UiTextElement _status;
        private readonly Dictionary<FaceGesture, ObjectField> _fields =
            new Dictionary<FaceGesture, ObjectField>();
        private readonly Dictionary<FaceGesture, AnimationClip> _assignments =
            new Dictionary<FaceGesture, AnimationClip>();
        private bool _rendering;

        public GestureAssignmentView(GestureAssignmentViewText text)
        {
            text = text ?? new GestureAssignmentViewText();
            AddToClassList("ee4v-gesture-assignment");

            _avatarField = UiTextFactory.CreateObjectField(text.Avatar);
            _avatarField.objectType = typeof(GameObject);
            _avatarField.allowSceneObjects = true;
            _avatarField.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    AvatarChanged?.Invoke(evt.newValue as GameObject);
                }
            });
            Add(_avatarField);
            Add(UiTextFactory.Create(
                text.Hint,
                UiClassNames.SecondaryText,
                "ee4v-gesture-assignment__hint"));

            var grid = new VisualElement();
            grid.AddToClassList("ee4v-gesture-assignment__grid");
            foreach (FaceGesture gesture in Enum.GetValues(typeof(FaceGesture)))
            {
                var captured = gesture;
                var field = UiTextFactory.CreateObjectField(
                    text.GestureName?.Invoke(gesture) ?? gesture.ToString());
                field.objectType = typeof(AnimationClip);
                field.allowSceneObjects = false;
                field.RegisterValueChangedCallback(evt =>
                {
                    if (!_rendering)
                    {
                        _assignments[captured] = evt.newValue as AnimationClip;
                    }
                });
                _fields.Add(gesture, field);
                grid.Add(field);
            }

            Add(grid);
            var footer = new VisualElement();
            footer.AddToClassList("ee4v-gesture-assignment__footer");
            _status = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-gesture-assignment__status");
            footer.Add(_status);
            footer.Add(UiTextFactory.CreateButton(
                text.Apply,
                () => ApplyRequested?.Invoke(
                    new Dictionary<FaceGesture, AnimationClip>(_assignments))));
            Add(footer);
        }

        public event Action<GameObject> AvatarChanged;
        public event Action<IReadOnlyDictionary<FaceGesture, AnimationClip>> ApplyRequested;

        public void SetAvatar(GameObject avatar)
        {
            _rendering = true;
            _avatarField.SetValueWithoutNotify(avatar);
            _rendering = false;
        }

        public void SetAssignments(
            IReadOnlyDictionary<FaceGesture, AnimationClip> assignments)
        {
            _assignments.Clear();
            _rendering = true;
            foreach (FaceGesture gesture in Enum.GetValues(typeof(FaceGesture)))
            {
                var clip = assignments != null &&
                           assignments.TryGetValue(gesture, out var value)
                    ? value
                    : null;
                _assignments[gesture] = clip;
                _fields[gesture].SetValueWithoutNotify(clip);
            }

            _rendering = false;
        }

        public void SetStatus(string status)
        {
            _status.SetText(status ?? string.Empty);
        }
    }
}
