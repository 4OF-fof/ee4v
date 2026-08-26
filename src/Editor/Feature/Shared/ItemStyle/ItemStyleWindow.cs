using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEditor;
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

    public sealed class ItemStyleWindow : CustomPopupWindow
    {
        private const float WindowWidth = 360f;
        private const float WindowHeight = 268f;
        private const float ActionWindowHeight = 318f;
        private readonly ItemStyleIconCache _iconCache =
            new ItemStyleIconCache();
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
            window.ShowAsPopup(request.ScreenPosition, size);
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
            UiComposition.Prepare(
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
                var texture = _iconCache.Get(guid);
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
                    ? _iconCache.Get(first.IconGuid)
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

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
            {
                return;
            }

            evt.StopPropagation();
            Close();
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
