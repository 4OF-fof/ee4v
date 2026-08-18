using System;
using System.Collections.Generic;
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
        public string TargetTooltip { get; }
        public IReadOnlyList<Color> ColorPresets { get; }
        public Type IconType { get; }
        public Texture DefaultIcon { get; }
        public Action Repaint { get; }
        public string CloseTooltip { get; set; } = "Close";
        public string ColorLabel { get; set; } = "Color";
        public string ColorTooltip { get; set; } = "Choose a color";
        public string CustomColorLabel { get; set; } = "Custom color";
        public string ClearColorLabel { get; set; } = "Clear color";
        public string IconLabel { get; set; } = "Icon";
        public string IconTooltip { get; set; } = "Choose an icon";
        public string ChooseIconLabel { get; set; } = "Choose icon";
        public string ClearIconLabel { get; set; } = "Clear icon";
        public string RecentIconsLabel { get; set; } = "Recently used";
        public string RemoveRecentIconTooltip { get; set; } =
            "Right-click to remove";
        public bool PreviewColorAsBackground { get; set; }
        public string ActionLabel { get; set; }
        public string ActionTooltip { get; set; }
        public Action Action { get; set; }
        public Action<Texture> IconApplied { get; set; }
    }

    public sealed class ItemStyleWindow : EditorWindow
    {
        private const float WindowWidth = 360f;
        private const float WindowHeight = 268f;
        private const float ActionWindowHeight = 318f;
        private ItemStyleWindowRequest _request;
        private List<string> _recentIconGuids;
        private ItemStyleEditor _editor;

        public static void ShowAt(ItemStyleWindowRequest request)
        {
            if (request == null || request.Identities.Count == 0)
            {
                return;
            }

            CloseExistingWindows();
            var window = CreateInstance<ItemStyleWindow>();
            window.Initialize(request);
            var size = new Vector2(
                WindowWidth,
                request.Action == null
                    ? WindowHeight
                    : ActionWindowHeight);
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
            var height = request.Action == null
                ? WindowHeight
                : ActionWindowHeight;
            minSize = new Vector2(WindowWidth, height);
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
            root.AddToClassList(UiClassNames.PopupSurface);
            UiComposition.Prepare(root);
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/ui-button.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/Feature/Shared/ItemStyle/item-style-window.uss");

            _editor = new ItemStyleEditor(
                CreateText(),
                Close,
                _request.Action == null
                    ? null
                    : new Action(() =>
                    {
                        _request.Action();
                        Close();
                    }));
            _editor.ColorChanged += SetColor;
            _editor.IconChanged += SetIcon;
            _editor.ClearColorRequested +=
                () => SetColor(Color.clear);
            _editor.ClearIconRequested += () => SetIcon(null);
            _editor.RemoveRecentIconRequested += RemoveRecentIcon;
            root.Add(_editor);
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            root.focusable = true;
            root.Focus();
            Render();
        }

        private ItemStyleEditorText CreateText()
        {
            return new ItemStyleEditorText
            {
                Title = _request.Title,
                TargetTooltip = _request.TargetTooltip,
                CloseTooltip = _request.CloseTooltip,
                ColorTitle = _request.ColorLabel,
                ColorTooltip = _request.ColorTooltip,
                CustomColorLabel = _request.CustomColorLabel,
                ClearColorLabel = _request.ClearColorLabel,
                IconTitle = _request.IconLabel,
                IconTooltip = _request.IconTooltip,
                RecentIconsLabel = _request.RecentIconsLabel,
                ChooseIconLabel = _request.ChooseIconLabel,
                ClearIconLabel = _request.ClearIconLabel,
                ActionLabel = _request.ActionLabel,
                ActionTooltip = _request.ActionTooltip
            };
        }

        private void Render()
        {
            _editor?.SetState(CreateState());
        }

        private ItemStyleEditorState CreateState()
        {
            var first = _request.Service.Get(_request.Identities[0]);
            var colorMixed = false;
            var iconMixed = false;
            for (var i = 1; i < _request.Identities.Count; i++)
            {
                var current = _request.Service.Get(
                    _request.Identities[i]);
                colorMixed |= first.HasColor != current.HasColor ||
                    (first.HasColor && first.Color != current.Color);
                iconMixed |= !string.Equals(
                    first.IconGuid,
                    current.IconGuid,
                    StringComparison.Ordinal);
            }

            var recentIcons = new List<ItemStyleIconCandidate>();
            for (var i = 0; i < _recentIconGuids.Count; i++)
            {
                var guid = _recentIconGuids[i];
                var texture = LoadIcon(guid);
                if (texture == null)
                {
                    continue;
                }

                var path = AssetDatabase.GUIDToAssetPath(guid);
                recentIcons.Add(new ItemStyleIconCandidate
                {
                    Texture = texture,
                    Tooltip =
                        (string.IsNullOrEmpty(path) ? texture.name : path) +
                        "\n" + _request.RemoveRecentIconTooltip,
                    IsApplied = IsIconApplied(guid)
                });
            }

            return new ItemStyleEditorState
            {
                Color = first.HasColor ? first.Color : Color.clear,
                ColorIsMixed = colorMixed,
                Icon = !iconMixed && first.HasIcon
                    ? LoadIcon(first.IconGuid)
                    : null,
                IconIsMixed = iconMixed,
                DefaultIcon = _request.DefaultIcon,
                ColorPresets = _request.ColorPresets,
                RecentIcons = recentIcons,
                PreviewColorAsBackground =
                    _request.PreviewColorAsBackground,
                IconType = _request.IconType
            };
        }

        private void SetColor(Color color)
        {
            _request.Service.SetColor(_request.Identities, color);
            Render();
            _request.Repaint?.Invoke();
        }

        private void SetIcon(Texture texture)
        {
            if (texture != null &&
                !_request.IconType.IsInstanceOfType(texture))
            {
                Render();
                return;
            }

            var path = texture != null
                ? AssetDatabase.GetAssetPath(texture)
                : string.Empty;
            var iconGuid = string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
            _request.Service.SetIcon(_request.Identities, iconGuid);
            _recentIconGuids = new List<string>(
                _request.Service.GetRecentIconGuids());
            _request.IconApplied?.Invoke(texture);
            Render();
            _request.Repaint?.Invoke();
        }

        private void RemoveRecentIcon(Texture texture)
        {
            var path = texture != null
                ? AssetDatabase.GetAssetPath(texture)
                : string.Empty;
            var iconGuid = string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
            if (IsIconApplied(iconGuid) ||
                !_request.Service.RemoveRecentIcon(iconGuid))
            {
                return;
            }

            _recentIconGuids.Remove(iconGuid);
            Render();
        }

        private bool IsIconApplied(string iconGuid)
        {
            if (string.IsNullOrEmpty(iconGuid))
            {
                return false;
            }

            for (var i = 0; i < _request.Identities.Count; i++)
            {
                if (string.Equals(
                        _request.Service.Get(
                            _request.Identities[i]).IconGuid,
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

    internal sealed class ItemStyleEditorText
    {
        public string Title;
        public string TargetTooltip;
        public string CloseTooltip;
        public string ColorTitle;
        public string ColorTooltip;
        public string CustomColorLabel;
        public string ClearColorLabel;
        public string IconTitle;
        public string IconTooltip;
        public string RecentIconsLabel;
        public string ChooseIconLabel;
        public string ClearIconLabel;
        public string ActionLabel;
        public string ActionTooltip;
    }

    internal sealed class ItemStyleIconCandidate
    {
        public Texture Texture;
        public string Tooltip;
        public bool IsApplied;
    }

    internal sealed class ItemStyleEditorState
    {
        public Color Color;
        public bool ColorIsMixed;
        public Texture Icon;
        public bool IconIsMixed;
        public Texture DefaultIcon;
        public IReadOnlyList<Color> ColorPresets = Array.Empty<Color>();
        public IReadOnlyList<ItemStyleIconCandidate> RecentIcons =
            Array.Empty<ItemStyleIconCandidate>();
        public bool PreviewColorAsBackground;
        public Type IconType = typeof(Texture);
    }

    internal sealed class ItemStyleEditor : VisualElement
    {
        private const string RootClassName = "ee4v-item-style";
        private const string SelectedClassName =
            "ee4v-item-style__choice--selected";
        private readonly ItemStyleEditorText _text;
        private readonly VisualElement _preview;
        private readonly Image _previewImage;
        private readonly VisualElement _palette;
        private readonly VisualElement _recentIcons;
        private readonly ColorField _colorField;
        private readonly ObjectField _iconField;

        public ItemStyleEditor(
            ItemStyleEditorText text,
            Action closeRequested,
            Action actionRequested)
        {
            _text = text ??
                throw new ArgumentNullException(nameof(text));
            AddToClassList(RootClassName);

            var header = new VisualElement();
            header.AddToClassList("ee4v-item-style__header");
            _preview = new VisualElement();
            _preview.AddToClassList("ee4v-item-style__preview");
            _previewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            _previewImage.AddToClassList(
                "ee4v-item-style__preview-image");
            _preview.Add(_previewImage);
            header.Add(_preview);

            var heading = new VisualElement();
            heading.AddToClassList("ee4v-item-style__heading");
            var title = UiTextFactory.Create(
                text.Title,
                UiClassNames.WindowTitle,
                "ee4v-item-style__title");
            title.tooltip = text.TargetTooltip;
            heading.Add(title);
            header.Add(heading);
            var close = new UiButton(
                string.Empty,
                closeRequested,
                text.CloseTooltip,
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Close,
                    UiSizeTokens.Size14),
                UiButtonVariant.Ghost,
                compact: true);
            close.AddToClassList("ee4v-item-style__close");
            header.Add(close);
            Add(header);

            var colorSection = CreateSection(
                text.ColorTitle,
                text.ColorTooltip);
            _palette = new VisualElement();
            _palette.AddToClassList("ee4v-item-style__palette");
            colorSection.Add(_palette);
            _colorField = UiTextFactory.CreateColorField();
            _colorField.showAlpha = true;
            _colorField.hdr = false;
            _colorField.tooltip = text.ColorTooltip;
            _colorField.AddToClassList("ee4v-item-style__color-field");
            _colorField.RegisterValueChangedCallback(
                evt => ColorChanged?.Invoke(evt.newValue));
            colorSection.Add(CreateFieldRow(
                text.CustomColorLabel,
                text.ColorTooltip,
                _colorField));
            Add(colorSection);

            var iconSection = CreateSection(
                text.IconTitle,
                text.IconTooltip);
            iconSection.AddToClassList(
                "ee4v-item-style__section--last");
            iconSection.Add(UiTextFactory.Create(
                text.RecentIconsLabel,
                UiClassNames.SecondaryText,
                "ee4v-item-style__caption"));
            _recentIcons = new VisualElement();
            _recentIcons.AddToClassList(
                "ee4v-item-style__recent-icons");
            iconSection.Add(_recentIcons);
            _iconField = UiTextFactory.CreateObjectField();
            _iconField.allowSceneObjects = false;
            _iconField.tooltip = text.IconTooltip;
            _iconField.AddToClassList("ee4v-item-style__object-field");
            _iconField.RegisterValueChangedCallback(
                evt => IconChanged?.Invoke(evt.newValue as Texture));
            iconSection.Add(CreateFieldRow(
                text.ChooseIconLabel,
                text.IconTooltip,
                _iconField));
            Add(iconSection);

            if (actionRequested != null &&
                !string.IsNullOrWhiteSpace(text.ActionLabel))
            {
                var action = new UiButton(
                    text.ActionLabel,
                    actionRequested,
                    text.ActionTooltip,
                    IconState.FromBuiltinIcon(
                        UiBuiltinIcon.VisibilityHidden,
                        UiSizeTokens.Size16));
                action.AddToClassList("ee4v-item-style__action");
                Add(action);
            }
        }

        public event Action<Color> ColorChanged;
        public event Action<Texture> IconChanged;
        public event Action<Texture> RemoveRecentIconRequested;
        public event Action ClearColorRequested;
        public event Action ClearIconRequested;

        public void SetState(ItemStyleEditorState state)
        {
            if (state == null)
            {
                return;
            }

            _colorField.showMixedValue = state.ColorIsMixed;
            _colorField.SetValueWithoutNotify(
                state.Color == Color.clear
                    ? new Color(1f, 1f, 1f, 0.7f)
                    : state.Color);
            _iconField.objectType = state.IconType;
            _iconField.showMixedValue = state.IconIsMixed;
            _iconField.SetValueWithoutNotify(state.Icon);
            RebuildPalette(state);
            RebuildRecentIcons(state);

            _previewImage.image = state.Icon ?? state.DefaultIcon;
            _previewImage.tintColor =
                !state.PreviewColorAsBackground &&
                state.Icon == null &&
                !state.ColorIsMixed &&
                state.Color != Color.clear
                    ? state.Color
                    : Color.white;
            _preview.style.backgroundColor =
                state.PreviewColorAsBackground &&
                !state.ColorIsMixed &&
                state.Color != Color.clear
                    ? new StyleColor(state.Color)
                    : StyleKeyword.Null;
        }

        private void RebuildPalette(ItemStyleEditorState state)
        {
            _palette.Clear();
            var clear = CreateChoice(
                _text.ClearColorLabel,
                () => ClearColorRequested?.Invoke());
            clear.EnableInClassList(
                SelectedClassName,
                !state.ColorIsMixed && state.Color == Color.clear);
            clear.Add(new Icon(IconState.FromBuiltinIcon(
                UiBuiltinIcon.Close,
                UiSizeTokens.Size12)));
            _palette.Add(clear);
            for (var i = 0; i < state.ColorPresets.Count; i++)
            {
                var color = state.ColorPresets[i];
                var captured = color;
                var button = CreateChoice(
                    _text.ColorTooltip + " #" +
                    ColorUtility.ToHtmlStringRGB(color),
                    () => ColorChanged?.Invoke(captured));
                button.EnableInClassList(
                    SelectedClassName,
                    !state.ColorIsMixed && state.Color == color);
                var swatch = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                swatch.AddToClassList("ee4v-item-style__swatch-color");
                swatch.style.backgroundColor = color;
                button.Add(swatch);
                _palette.Add(button);
            }
        }

        private void RebuildRecentIcons(ItemStyleEditorState state)
        {
            _recentIcons.Clear();
            var clear = CreateChoice(
                _text.ClearIconLabel,
                () => ClearIconRequested?.Invoke());
            clear.EnableInClassList(
                SelectedClassName,
                !state.IconIsMixed && state.Icon == null);
            clear.Add(new Icon(IconState.FromBuiltinIcon(
                UiBuiltinIcon.Close,
                UiSizeTokens.Size12)));
            _recentIcons.Add(clear);
            for (var i = 0; i < state.RecentIcons.Count; i++)
            {
                var candidate = state.RecentIcons[i];
                if (candidate?.Texture == null)
                {
                    continue;
                }

                var captured = candidate;
                var button = UiTextFactory.CreateButton(
                    string.Empty,
                    () => IconChanged?.Invoke(captured.Texture));
                button.tooltip = candidate.Tooltip;
                button.AddToClassList("ee4v-item-style__icon-choice");
                button.EnableInClassList(
                    SelectedClassName,
                    candidate.IsApplied);
                button.Add(new Image
                {
                    image = candidate.Texture,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                });
                button.RegisterCallback<ContextClickEvent>(evt =>
                {
                    if (!captured.IsApplied)
                    {
                        RemoveRecentIconRequested?.Invoke(
                            captured.Texture);
                    }

                    evt.StopPropagation();
                });
                _recentIcons.Add(button);
            }
        }

        private static VisualElement CreateSection(
            string title,
            string tooltip)
        {
            var section = new VisualElement();
            section.AddToClassList("ee4v-item-style__section");
            var heading = UiTextFactory.Create(
                title,
                UiClassNames.SectionTitle,
                "ee4v-item-style__section-title");
            heading.tooltip = tooltip;
            section.Add(heading);
            return section;
        }

        private static VisualElement CreateFieldRow(
            string label,
            string tooltip,
            VisualElement field)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-item-style__field-row");
            var text = UiTextFactory.Create(
                label,
                UiClassNames.FormLabel,
                "ee4v-item-style__field-label");
            text.tooltip = tooltip;
            row.Add(text);
            row.Add(field);
            return row;
        }

        private static UiTextButton CreateChoice(
            string tooltip,
            Action onClick)
        {
            var button = UiTextFactory.CreateButton(
                string.Empty,
                onClick);
            button.tooltip = tooltip;
            button.AddToClassList("ee4v-item-style__choice");
            return button;
        }
    }
}
