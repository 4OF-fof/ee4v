using System;
using System.Collections.Generic;
using System.IO;
using Ee4v.Core.EditorIntegration;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.ItemStyle
{
    public sealed class ItemStyleWindowRequest
    {
        public ItemStyleWindowRequest(
            ItemStyleService service,
            IReadOnlyList<string> identities,
            Vector2 screenPosition,
            string title,
            string subtitle,
            string targetTooltip,
            IReadOnlyList<Color> colorPresets,
            Type iconType,
            Texture defaultIcon,
            Action repaint)
        {
            Service = service ??
                throw new ArgumentNullException(nameof(service));
            Identities = identities ??
                throw new ArgumentNullException(nameof(identities));
            ScreenPosition = screenPosition;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            TargetTooltip = targetTooltip ?? string.Empty;
            ColorPresets = colorPresets ?? Array.Empty<Color>();
            IconType = iconType ?? typeof(Texture);
            DefaultIcon = defaultIcon;
            Repaint = repaint;
        }

        public ItemStyleService Service { get; }
        public IReadOnlyList<string> Identities { get; }
        public Vector2 ScreenPosition { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string TargetTooltip { get; }
        public IReadOnlyList<Color> ColorPresets { get; }
        public Type IconType { get; }
        public Texture DefaultIcon { get; }
        public Action Repaint { get; }
        public string ColorLabel { get; set; } = "Color";
        public string ClearColorLabel { get; set; } = "Clear Color";
        public string IconLabel { get; set; } = "Icon";
        public string ClearIconLabel { get; set; } = "Clear Icon";
        public string RecentIconsLabel { get; set; } = "Recent Icons";
        public string RemoveRecentIconTooltip { get; set; } =
            "Right-click to remove";
        public string ActionLabel { get; set; }
        public string ActionTooltip { get; set; }
        public Action Action { get; set; }
        public Action<Texture> IconApplied { get; set; }
    }

    public sealed class ItemStyleWindow : EditorWindow
    {
        private const float WindowWidth = 360f;
        private const float WindowHeight = 300f;
        private ItemStyleWindowRequest _request;
        private List<string> _recentIconGuids;
        private ColorField _colorField;
        private ObjectField _iconField;
        private Image _preview;

        public static void ShowAt(ItemStyleWindowRequest request)
        {
            if (request == null || request.Identities.Count == 0)
            {
                return;
            }

            CloseExistingWindows();
            var window = CreateInstance<ItemStyleWindow>();
            window.Initialize(request);
            var size = new Vector2(WindowWidth, WindowHeight);
            window.position = EditorPopupApi.TryGetDesktopBounds(
                    request.ScreenPosition,
                    out var desktopBounds)
                ? ClampToDesktop(
                    request.ScreenPosition,
                    size,
                    desktopBounds)
                : new Rect(request.ScreenPosition, size);
            window.ShowPopup();
            window.Focus();
            EditorPopupApi.TrySetBackgroundColor(
                window,
                UiColorTokens.SurfaceRaised);
        }

        private void Initialize(ItemStyleWindowRequest request)
        {
            _request = request;
            _recentIconGuids = new List<string>(
                request.Service.GetRecentIconGuids());
            minSize = new Vector2(WindowWidth, WindowHeight);
            maxSize = minSize;
            titleContent = UiTextFactory.CreateGuiContent(request.Title);
        }

        private void CreateGUI()
        {
            BuildContent();
        }

        private void BuildContent()
        {
            if (_request == null)
            {
                return;
            }

            var root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = UiSpacingTokens.Large;
            root.style.paddingRight = UiSpacingTokens.Large;
            root.style.paddingTop = UiSpacingTokens.Large;
            root.style.paddingBottom = UiSpacingTokens.Large;
            root.style.backgroundColor =
                (Color)UiColorTokens.SurfaceRaised;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            var heading = UiTextFactory.Create(
                _request.Title,
                UiClassNames.SectionTitle);
            heading.tooltip = _request.TargetTooltip;
            root.Add(heading);
            if (!string.IsNullOrWhiteSpace(_request.Subtitle))
            {
                var subtitle = UiTextFactory.Create(
                    _request.Subtitle,
                    UiClassNames.SecondaryText);
                subtitle.tooltip = _request.TargetTooltip;
                subtitle.style.marginBottom = UiSpacingTokens.Medium;
                root.Add(subtitle);
            }

            var style = ResolveStyle();
            _preview = new Image
            {
                image = LoadIcon(style.IconGuid) ?? _request.DefaultIcon,
                scaleMode = ScaleMode.ScaleToFit
            };
            _preview.style.height = 42f;
            _preview.style.marginBottom = UiSpacingTokens.Medium;
            root.Add(_preview);

            AddColorControls(root, style);
            AddIconControls(root, style);
            AddRecentIcons(root);
            AddAction(root);
            root.focusable = true;
            root.Focus();
        }

        private void AddColorControls(
            VisualElement root,
            ItemStyleValue style)
        {
            var row = CreateRow();
            _colorField = UiTextFactory.CreateColorField(
                _request.ColorLabel);
            _colorField.value = style.HasColor
                ? style.Color
                : Color.clear;
            _colorField.showMixedValue = HasMixedColor(style);
            _colorField.style.flexGrow = 1f;
            _colorField.RegisterValueChangedCallback(evt =>
                SetColor(evt.newValue));
            row.Add(_colorField);
            row.Add(UiTextFactory.CreateButton(
                _request.ClearColorLabel,
                () => SetColor(Color.clear)));
            root.Add(row);

            var presets = CreateRow();
            presets.style.marginLeft = 120f;
            for (var i = 0; i < _request.ColorPresets.Count; i++)
            {
                var color = _request.ColorPresets[i];
                var button = UiTextFactory.CreateButton(
                    string.Empty,
                    () => SetColor(color));
                button.tooltip = "#" +
                    ColorUtility.ToHtmlStringRGBA(color);
                button.style.width = 18f;
                button.style.height = 18f;
                button.style.backgroundColor = color;
                presets.Add(button);
            }

            root.Add(presets);
        }

        private void AddIconControls(
            VisualElement root,
            ItemStyleValue style)
        {
            var row = CreateRow();
            _iconField = UiTextFactory.CreateObjectField(
                _request.IconLabel);
            _iconField.objectType = _request.IconType;
            _iconField.value = LoadIcon(style.IconGuid);
            _iconField.showMixedValue = HasMixedIcon(style);
            _iconField.style.flexGrow = 1f;
            _iconField.RegisterValueChangedCallback(evt =>
                SetIcon(evt.newValue as Texture));
            row.Add(_iconField);
            row.Add(UiTextFactory.CreateButton(
                _request.ClearIconLabel,
                () => SetIcon(null)));
            root.Add(row);
        }

        private void AddRecentIcons(VisualElement root)
        {
            var label = UiTextFactory.Create(
                _request.RecentIconsLabel,
                UiClassNames.SecondaryText);
            label.style.marginTop = UiSpacingTokens.Small;
            root.Add(label);

            var row = CreateRow();
            row.style.height = 32f;
            for (var i = 0; i < _recentIconGuids.Count; i++)
            {
                var iconGuid = _recentIconGuids[i];
                var texture = LoadIcon(iconGuid);
                if (texture == null)
                {
                    continue;
                }

                var button = UiTextFactory.CreateButton(
                    string.Empty,
                    () => SetIcon(texture));
                button.tooltip = AssetDatabase.GUIDToAssetPath(iconGuid);
                button.style.width = 30f;
                button.Add(new Image
                {
                    image = texture,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                });
                button.RegisterCallback<ContextClickEvent>(evt =>
                {
                    RemoveRecentIcon(iconGuid);
                    evt.StopPropagation();
                });
                row.Add(button);
            }

            root.Add(row);
        }

        private void AddAction(VisualElement root)
        {
            if (_request.Action == null ||
                string.IsNullOrWhiteSpace(_request.ActionLabel))
            {
                return;
            }

            var button = UiTextFactory.CreateButton(
                _request.ActionLabel,
                () =>
                {
                    _request.Action();
                    Close();
                });
            button.tooltip = _request.ActionTooltip;
            button.style.marginTop = UiSpacingTokens.Medium;
            root.Add(button);
        }

        private ItemStyleValue ResolveStyle()
        {
            return _request.Service.Get(_request.Identities[0]);
        }

        private bool HasMixedColor(ItemStyleValue first)
        {
            for (var i = 1; i < _request.Identities.Count; i++)
            {
                var current = _request.Service.Get(
                    _request.Identities[i]);
                if (first.HasColor != current.HasColor ||
                    (first.HasColor && first.Color != current.Color))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasMixedIcon(ItemStyleValue first)
        {
            for (var i = 1; i < _request.Identities.Count; i++)
            {
                var current = _request.Service.Get(
                    _request.Identities[i]);
                if (!string.Equals(
                        first.IconGuid,
                        current.IconGuid,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetColor(Color color)
        {
            _request.Service.SetColor(_request.Identities, color);
            if (_colorField != null)
            {
                _colorField.showMixedValue = false;
            }
            _colorField?.SetValueWithoutNotify(color);
            _request.Repaint?.Invoke();
        }

        private void SetIcon(Texture texture)
        {
            if (texture != null &&
                !_request.IconType.IsInstanceOfType(texture))
            {
                return;
            }

            var path = texture != null
                ? AssetDatabase.GetAssetPath(texture)
                : string.Empty;
            var iconGuid = string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
            _request.Service.SetIcon(
                _request.Identities,
                iconGuid);
            if (_iconField != null)
            {
                _iconField.showMixedValue = false;
            }
            _iconField?.SetValueWithoutNotify(texture);
            if (_preview != null)
            {
                _preview.image = texture ?? _request.DefaultIcon;
            }

            _request.IconApplied?.Invoke(texture);
            _request.Repaint?.Invoke();
        }

        private void RemoveRecentIcon(string iconGuid)
        {
            if (IsIconApplied(iconGuid) ||
                !_request.Service.RemoveRecentIcon(iconGuid))
            {
                return;
            }

            _recentIconGuids.Remove(iconGuid);
            BuildContent();
        }

        private bool IsIconApplied(string iconGuid)
        {
            for (var i = 0; i < _request.Identities.Count; i++)
            {
                if (string.Equals(
                        _request.Service.Get(_request.Identities[i]).IconGuid,
                        iconGuid,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static Texture LoadIcon(string iconGuid)
        {
            var path = AssetDatabase.GUIDToAssetPath(iconGuid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture>(path);
        }

        private static VisualElement CreateRow()
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = UiSpacingTokens.Small
                }
            };
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
            {
                return;
            }

            evt.StopPropagation();
            Close();
        }

        private void OnLostFocus()
        {
            EditorApplication.delayCall += CloseIfFocusLeft;
        }

        private void CloseIfFocusLeft()
        {
            if (this == null || EditorWindow.focusedWindow == this)
            {
                return;
            }

            if (EditorPopupApi.IsTransientPicker(
                    EditorWindow.focusedWindow) ||
                EditorPopupApi.HasOpenTransientPicker() ||
                EditorPopupApi.IsEyeDropperOpen())
            {
                return;
            }

            Close();
        }

        private static Rect ClampToDesktop(
            Vector2 position,
            Vector2 size,
            Rect desktop)
        {
            return new Rect(
                Mathf.Clamp(
                    position.x,
                    desktop.xMin,
                    Mathf.Max(desktop.xMin, desktop.xMax - size.x)),
                Mathf.Clamp(
                    position.y,
                    desktop.yMin,
                    Mathf.Max(desktop.yMin, desktop.yMax - size.y)),
                size.x,
                size.y);
        }

        private static void CloseExistingWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<ItemStyleWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                windows[i].Close();
            }
        }
    }
}
