using System;
using System.Collections.Generic;
using System.IO;
using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal.EditorAPI;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FolderStyle
{
    internal sealed class FolderStyleWindow : EditorWindow
    {
        private const float WindowWidth = 360f;
        private const float WindowHeight = 268f;
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName =
            "ee4v-folder-style-window";
        private const string HeaderClassName =
            "ee4v-folder-style-window__header";
        private const string PreviewClassName =
            "ee4v-folder-style-window__preview";
        private const string PreviewImageClassName =
            "ee4v-folder-style-window__preview-image";
        private const string HeaderTextClassName =
            "ee4v-folder-style-window__header-text";
        private const string TitleClassName =
            "ee4v-folder-style-window__title";
        private const string SubtitleClassName =
            "ee4v-folder-style-window__subtitle";
        private const string CloseClassName =
            "ee4v-folder-style-window__close";

        private IReadOnlyList<string> _folderGuids;
        private FolderStyleService _service;
        private DecorationRecentIconSession
            _recentIconSession;
        private DecorationStyleEditor _editor;
        private Image _previewImage;
        private TransientPopupFocusController _focusController;

        internal static void ShowAt(
            IReadOnlyList<string> folderGuids,
            Vector2 screenPosition,
            FolderStyleService service)
        {
            if (folderGuids == null ||
                folderGuids.Count == 0 ||
                service == null)
            {
                return;
            }

            CloseExistingWindows();

            var window =
                CreateInstance<FolderStyleWindow>();
            window.Initialize(folderGuids, service);
            var size = new Vector2(
                WindowWidth,
                WindowHeight);
            window.position =
                EditorPopupWindow.TryGetDesktopBounds(
                    screenPosition,
                    out var desktopBounds)
                    ? PopupWindowLayout.ClampToDesktop(
                            screenPosition,
                            size,
                            desktopBounds)
                    : new Rect(screenPosition, size);
            window.ShowPopup();
            window.Focus();
            EditorPopupWindow.TrySetBackgroundColor(
                window,
                UiColorTokens.SurfaceRaised);
        }

        private void Initialize(
            IReadOnlyList<string> folderGuids,
            FolderStyleService service)
        {
            _folderGuids = new List<string>(folderGuids);
            _service = service;
            _recentIconSession =
                new DecorationRecentIconSession(
                    _service.GetRecentIconGuids());
            _focusController =
                new TransientPopupFocusController(this);
            minSize = new Vector2(
                WindowWidth,
                WindowHeight);
            maxSize = minSize;
            titleContent = new GUIContent(
                I18N.Get("window.title"));
        }

        private void CreateGUI()
        {
            BuildContent();
        }

        private void BuildContent()
        {
            if (_folderGuids == null ||
                _service == null)
            {
                return;
            }

            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/DecorationStyleEditor/decoration-style-editor.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/EditorEnhancements/FolderStyle/UI/folder-style-window.uss");

            root.Add(CreateHeader());

            _editor = new DecorationStyleEditor(
                CreateEditorText(),
                CreateEditorState());
            _editor.ColorChanged += SetColor;
            _editor.IconChanged += SetIcon;
            _editor.RemoveRecentIconRequested +=
                RemoveRecentIcon;
            _editor.ClearColorRequested += ClearColor;
            _editor.ClearIconRequested += ClearIcon;
            root.Add(_editor);

            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(
                OnKeyDown);
            root.Focus();
            RefreshPreview();
        }

        private VisualElement CreateHeader()
        {
            var header = new VisualElement();
            header.AddToClassList(HeaderClassName);

            var preview = new VisualElement();
            preview.AddToClassList(PreviewClassName);
            _previewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            _previewImage.AddToClassList(
                PreviewImageClassName);
            preview.Add(_previewImage);
            header.Add(preview);

            var headerText = new VisualElement();
            headerText.AddToClassList(
                HeaderTextClassName);
            var title = UiTextFactory.Create(
                CreateTitle(),
                UiClassNames.WindowTitle);
            title.AddToClassList(TitleClassName);
            title.tooltip = CreateTargetTooltip();
            headerText.Add(title);

            var subtitle = UiTextFactory.Create(
                CreateSubtitle(),
                UiClassNames.SecondaryText);
            subtitle.AddToClassList(
                SubtitleClassName);
            headerText.Add(subtitle);
            header.Add(headerText);

            var closeButton = new Button(Close)
            {
                tooltip = I18N.Get(
                    "window.closeTooltip")
            };
            closeButton.AddToClassList(CloseClassName);
            closeButton.Add(new Icon(
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Close,
                    UiSizeTokens.Size14)));
            header.Add(closeButton);
            return header;
        }

        private DecorationStyleEditorText CreateEditorText()
        {
            return new DecorationStyleEditorText(
                I18N.Get("editor.color.label"),
                I18N.Get("editor.color.tooltip"),
                I18N.Get(
                    "editor.color.customLabel"),
                I18N.Get(
                    "editor.color.clearLabel"),
                I18N.Get("editor.icon.label"),
                I18N.Get("editor.icon.tooltip"),
                I18N.Get(
                    "editor.icon.recentLabel"),
                I18N.Get(
                    "editor.icon.chooseLabel"),
                I18N.Get(
                    "editor.icon.clearLabel"));
        }

        private DecorationStyleEditorState CreateEditorState()
        {
            var first = _service.Get(
                _folderGuids[0]);
            var colorMixed = false;
            var iconMixed = false;
            for (var i = 1;
                 i < _folderGuids.Count;
                 i++)
            {
                var current =
                    _service.Get(_folderGuids[i]);
                colorMixed |=
                    first.HasColor != current.HasColor ||
                    (first.HasColor &&
                     first.Color != current.Color);
                iconMixed |= !string.Equals(
                    first.IconGuid,
                    current.IconGuid,
                    StringComparison.Ordinal);
            }

            var icon = !iconMixed && first.HasIcon
                ? LoadIcon(first.IconGuid)
                : null;
            return new DecorationStyleEditorState(
                first.HasColor
                    ? first.Color
                    : Color.clear,
                icon,
                CreateColorPresets(),
                CreateRecentIconCandidates(),
                colorMixed,
                iconMixed);
        }

        private IReadOnlyList<DecorationColorPresetState>
            CreateColorPresets()
        {
            var colors =
                FolderStyleColorPresets.GetAll();
            var presets =
                new List<DecorationColorPresetState>(
                    colors.Count);
            for (var i = 0; i < colors.Count; i++)
            {
                var color = colors[i];
                presets.Add(
                    new DecorationColorPresetState(
                        color,
                        I18N.Get(
                            "editor.color.presetTooltip",
                            "#" +
                            ColorUtility.ToHtmlStringRGB(
                                color))));
            }

            return presets;
        }

        private IReadOnlyList<DecorationIconCandidateState>
            CreateRecentIconCandidates()
        {
            var recentGuids =
                _recentIconSession.IconGuids;
            var candidates =
                new List<DecorationIconCandidateState>();
            for (var i = 0;
                 i < recentGuids.Count;
                 i++)
            {
                var guid = recentGuids[i];
                var path =
                    AssetDatabase.GUIDToAssetPath(guid);
                var texture =
                    LoadIcon(guid);
                if (texture == null)
                {
                    continue;
                }

                var isApplied =
                    IsIconAppliedToTargets(guid);
                candidates.Add(
                    new DecorationIconCandidateState(
                        texture,
                        string.IsNullOrEmpty(path)
                            ? texture.name
                            : Path.GetFileName(path),
                        isApplied,
                        !isApplied));
            }

            return candidates;
        }

        private string CreateTitle()
        {
            if (_folderGuids.Count > 1)
            {
                return I18N.Get(
                    "window.multipleTitle",
                    _folderGuids.Count);
            }

            var path = AssetDatabase.GUIDToAssetPath(
                _folderGuids[0]);
            var folderName = string.IsNullOrEmpty(path)
                ? I18N.Get("window.unknownFolder")
                : Path.GetFileName(path);
            return I18N.Get(
                "window.singleTitle",
                folderName);
        }

        private string CreateSubtitle()
        {
            return _folderGuids.Count > 1
                ? I18N.Get("window.multipleSubtitle")
                : I18N.Get("window.singleSubtitle");
        }

        private string CreateTargetTooltip()
        {
            var paths = new List<string>();
            for (var i = 0;
                 i < _folderGuids.Count;
                 i++)
            {
                var path =
                    AssetDatabase.GUIDToAssetPath(
                        _folderGuids[i]);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            return string.Join("\n", paths);
        }

        private void SetColor(Color color)
        {
            _service.SetColor(_folderGuids, color);
            RefreshAfterChange();
        }

        private void ClearColor()
        {
            SetColor(Color.clear);
        }

        private void SetIcon(Texture texture)
        {
            var path = texture != null
                ? AssetDatabase.GetAssetPath(texture)
                : string.Empty;
            var iconGuid = string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
            _service.SetIcon(_folderGuids, iconGuid);
            RefreshAfterChange();
        }

        private void ClearIcon()
        {
            SetIcon(null);
        }

        private void RemoveRecentIcon(Texture texture)
        {
            var path = texture != null
                ? AssetDatabase.GetAssetPath(texture)
                : string.Empty;
            var iconGuid = string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
            if (IsIconAppliedToTargets(iconGuid))
            {
                return;
            }

            _service.RemoveRecentIcon(iconGuid);
            _recentIconSession.Remove(iconGuid);
            _editor?.SetState(CreateEditorState());
        }

        private void RefreshAfterChange()
        {
            _editor?.SetState(CreateEditorState());
            RefreshPreview();
            InjectorApi.Repaint(
                InjectionChannel.ProjectItem);
        }

        private bool IsIconAppliedToTargets(
            string iconGuid)
        {
            if (string.IsNullOrEmpty(iconGuid))
            {
                return false;
            }

            for (var i = 0;
                 i < _folderGuids.Count;
                 i++)
            {
                if (string.Equals(
                        _service.Get(_folderGuids[i])
                            .IconGuid,
                        iconGuid,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshPreview()
        {
            if (_previewImage == null ||
                _folderGuids == null ||
                _folderGuids.Count == 0)
            {
                return;
            }

            var first =
                _service.Get(_folderGuids[0]);
            var colorMixed = false;
            var iconMixed = false;
            for (var i = 1;
                 i < _folderGuids.Count;
                 i++)
            {
                var current =
                    _service.Get(_folderGuids[i]);
                colorMixed |=
                    first.HasColor != current.HasColor ||
                    (first.HasColor &&
                     first.Color != current.Color);
                iconMixed |= !string.Equals(
                    first.IconGuid,
                    current.IconGuid,
                    StringComparison.Ordinal);
            }

            var customIcon =
                !iconMixed && first.HasIcon
                    ? LoadIcon(first.IconGuid)
                    : null;
            _previewImage.image =
                customIcon ??
                EditorGUIUtility.IconContent(
                    "Folder Icon").image;
            _previewImage.tintColor =
                customIcon != null ||
                colorMixed ||
                !first.HasColor
                    ? Color.white
                    : first.Color;
        }

        private static Texture LoadIcon(string iconGuid)
        {
            var path =
                AssetDatabase.GUIDToAssetPath(iconGuid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture>(
                    path);
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
            _focusController?.OnLostFocus();
        }

        private void OnDisable()
        {
            _focusController?.Dispose();
            _focusController = null;
        }

        private static void CloseExistingWindows()
        {
            var windows =
                Resources.FindObjectsOfTypeAll<
                    FolderStyleWindow>();
            for (var i = 0;
                 i < windows.Length;
                 i++)
            {
                windows[i].Close();
            }
        }
    }

}
