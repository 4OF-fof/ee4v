using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public enum PathFieldKind
    {
        Folder,
        File
    }

    public sealed class PathField : VisualElement
    {
        private readonly InputField _input;
        private readonly PathFieldKind _kind;
        private readonly string _extension;
        private readonly TextElement _measureText;
        private readonly VisualElement _fieldContainer;
        private readonly UiButton _browse;
        private string _displayValue;
        private VisualElement _sizingParent;

        public PathField(PathFieldKind kind = PathFieldKind.Folder,
            string value = null, string extension = "")
        {
            _kind = kind;
            _extension = extension ?? string.Empty;
            _displayValue = value ?? string.Empty;
            AddToClassList("ee4v-ui-path-field");
            _input = new InputField(new InputFieldState(value))
            {
                IsDelayed = true
            };
            _input.AddToClassList("ee4v-ui-path-field__input");
            _fieldContainer = _input.Q<VisualElement>(
                className: "ee4v-ui-input-field__field-container");
            _input.ValueChanged += path =>
            {
                _displayValue = path;
                RefreshWidth();
                ValueChanged?.Invoke(path);
            };
            _input.RegisterCallback<InputEvent>(evt =>
            {
                _displayValue = evt.newData ?? string.Empty;
                RefreshWidth();
            });
            _browse = new UiButton(string.Empty, Browse,
                UiLocalization.Get(kind == PathFieldKind.Folder
                    ? "ui.pathField.selectFolder" : "ui.pathField.selectFile"),
                FluentUiIcons.CreateState("folder.png", UiSizeTokens.Size16),
                UiButtonVariant.Ghost);
            _browse.AddToClassList("ee4v-ui-path-field__browse");
            Add(_input);
            Add(_browse);
            var measure = UiTextFactory.Create("M", "ee4v-ui-path-field__measurement");
            measure.pickingMode = PickingMode.Ignore;
            _measureText = measure.Q<Label>();
            Add(measure);
            RegisterCallback<FocusInEvent>(_ =>
                AddToClassList("ee4v-ui-path-field--focused"));
            RegisterCallback<FocusOutEvent>(_ =>
                RemoveFromClassList("ee4v-ui-path-field--focused"));
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _sizingParent = parent;
                _sizingParent?.RegisterCallback<GeometryChangedEvent>(OnParentGeometryChanged);
                schedule.Execute(RefreshWidth);
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                _sizingParent?.UnregisterCallback<GeometryChangedEvent>(OnParentGeometryChanged);
                _sizingParent = null;
            });
            RegisterCallback<GeometryChangedEvent>(_ => RefreshWidth());
        }

        public event Action<string> ValueChanged;

        public string Value => _input.Value;

        public void SetValueWithoutNotify(string value)
        {
            _input.SetValueWithoutNotify(value);
            _displayValue = value ?? string.Empty;
            RefreshWidth();
        }

        private void OnParentGeometryChanged(GeometryChangedEvent evt)
        {
            RefreshWidth();
        }

        private void RefreshWidth()
        {
            if (panel == null || parent == null || parent.contentRect.width <= 0f) { return; }
            var textWidth = _measureText.MeasureTextSize(_displayValue,
                0f, MeasureMode.Undefined, 0f, MeasureMode.Undefined).x;
            var preferredWidth = textWidth + _fieldContainer.resolvedStyle.paddingLeft +
                _fieldContainer.resolvedStyle.paddingRight + _browse.resolvedStyle.width +
                resolvedStyle.paddingLeft + resolvedStyle.paddingRight + UiSpacingTokens.Xs;
            var maximumWidth = parent.contentRect.width;
            var minimumWidth = resolvedStyle.minWidth.value;
            if (parent is FormInput form)
            {
                var remainingWidth = maximumWidth - GetOccupiedWidth(form.LabelText) -
                    GetOccupiedWidth(form.Button);
                if (remainingWidth >= minimumWidth) { maximumWidth = remainingWidth; }
            }
            var width = Mathf.Clamp(Mathf.Ceil(preferredWidth), minimumWidth, maximumWidth);
            if (!float.IsNaN(width) && !float.IsInfinity(width) &&
                (style.width.keyword != StyleKeyword.Undefined ||
                 !Mathf.Approximately(style.width.value.value, width)))
            {
                style.width = width;
            }
        }

        private static float GetOccupiedWidth(VisualElement element)
        {
            return element == null || element.resolvedStyle.display == DisplayStyle.None
                ? 0f : element.resolvedStyle.width + element.resolvedStyle.marginLeft +
                    element.resolvedStyle.marginRight;
        }

        private void Browse()
        {
            var directory = GetInitialDirectory();
            var selected = _kind == PathFieldKind.Folder
                ? EditorUtility.OpenFolderPanel(
                    UiLocalization.Get("ui.pathField.selectFolder"), directory, string.Empty)
                : EditorUtility.OpenFilePanel(
                    UiLocalization.Get("ui.pathField.selectFile"), directory, _extension);
            if (string.IsNullOrEmpty(selected) || string.Equals(selected, Value, StringComparison.Ordinal))
            {
                return;
            }
            SetValueWithoutNotify(selected);
            ValueChanged?.Invoke(selected);
        }

        private string GetInitialDirectory()
        {
            if (!string.IsNullOrWhiteSpace(Value))
            {
                try
                {
                    var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(Value));
                    if (_kind == PathFieldKind.File || !Directory.Exists(path))
                    {
                        path = Path.GetDirectoryName(path);
                    }
                    while (!string.IsNullOrEmpty(path))
                    {
                        if (Directory.Exists(path)) { return path; }
                        path = Path.GetDirectoryName(path);
                    }
                }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }
}
