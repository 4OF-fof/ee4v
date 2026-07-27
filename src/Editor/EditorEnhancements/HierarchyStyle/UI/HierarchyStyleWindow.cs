using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Core.Injector;
using Ee4v.Core.Internal.EditorAPI;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.HierarchyStyle
{
    internal sealed class HierarchyStyleWindow : EditorWindow
    {
        private const float WindowWidth = 360f;
        private const float WindowHeight = 318f;
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName =
            "ee4v-hierarchy-style-window";
        private const string HeaderClassName =
            "ee4v-hierarchy-style-window__header";
        private const string PreviewClassName =
            "ee4v-hierarchy-style-window__preview";
        private const string PreviewImageClassName =
            "ee4v-hierarchy-style-window__preview-image";
        private const string HeaderTextClassName =
            "ee4v-hierarchy-style-window__header-text";
        private const string TitleClassName =
            "ee4v-hierarchy-style-window__title";
        private const string SubtitleClassName =
            "ee4v-hierarchy-style-window__subtitle";
        private const string CloseClassName =
            "ee4v-hierarchy-style-window__close";
        private const string HideButtonClassName =
            "ee4v-hierarchy-style-window__hide";
        private const string HideButtonTextClassName =
            "ee4v-hierarchy-style-window__hide-text";

        private IReadOnlyList<GameObject> _targets;
        private IReadOnlyList<string> _objectIds;
        private HierarchyStyleService _service;
        private HierarchyObjectIdentity _identity;
        private HierarchyStyleIconApplier _iconApplier;
        private IHierarchyObjectVisibility _visibility;
        private HierarchyStyleRecentIconSession
            _recentIconSession;
        private DecorationStyleEditor _editor;
        private VisualElement _preview;
        private Image _previewImage;
        private bool _watchingTransientFocus;

        internal static void ShowAt(
            IReadOnlyList<GameObject> targets,
            Vector2 screenPosition,
            HierarchyStyleService service,
            HierarchyObjectIdentity identity,
            HierarchyStyleIconApplier iconApplier,
            IHierarchyObjectVisibility visibility)
        {
            if (targets == null ||
                targets.Count == 0 ||
                service == null ||
                identity == null ||
                iconApplier == null ||
                visibility == null)
            {
                return;
            }

            var validTargets = targets
                .Where(target =>
                    target != null &&
                    target.scene.IsValid())
                .Distinct()
                .ToArray();
            if (validTargets.Length == 0)
            {
                return;
            }

            CloseExistingWindows();

            var window =
                CreateInstance<HierarchyStyleWindow>();
            window.Initialize(
                validTargets,
                service,
                identity,
                iconApplier,
                visibility);
            var size = new Vector2(
                WindowWidth,
                WindowHeight);
            window.position =
                EditorPopupWindow.TryGetDesktopBounds(
                    screenPosition,
                    out var desktopBounds)
                    ? HierarchyStyleWindowLayout
                        .CalculateWindowRect(
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
            IReadOnlyList<GameObject> targets,
            HierarchyStyleService service,
            HierarchyObjectIdentity identity,
            HierarchyStyleIconApplier iconApplier,
            IHierarchyObjectVisibility visibility)
        {
            _targets = new List<GameObject>(targets);
            _service = service;
            _identity = identity;
            _iconApplier = iconApplier;
            _visibility = visibility;
            _objectIds = _targets
                .Select(_identity.Get)
                .Where(objectId =>
                    !string.IsNullOrEmpty(objectId))
                .ToArray();
            _recentIconSession =
                new HierarchyStyleRecentIconSession(
                    _service.GetRecentIconGuids());
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
            if (_targets == null ||
                _targets.Count == 0 ||
                _objectIds == null ||
                _objectIds.Count == 0 ||
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
                "Editor/EditorEnhancements/HierarchyStyle/UI/hierarchy-style-window.uss");

            root.Add(CreateHeader());

            _editor = new DecorationStyleEditor(
                CreateEditorText(),
                CreateEditorState());
            _editor.ColorChanged += SetBackgroundColor;
            _editor.IconChanged += SetIcon;
            _editor.RemoveRecentIconRequested +=
                RemoveRecentIcon;
            _editor.ClearColorRequested +=
                ClearBackgroundColor;
            _editor.ClearIconRequested += ClearIcon;
            root.Add(_editor);
            root.Add(CreateHideButton());

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

            _preview = new VisualElement();
            _preview.AddToClassList(PreviewClassName);
            _previewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            _previewImage.AddToClassList(
                PreviewImageClassName);
            _preview.Add(_previewImage);
            header.Add(_preview);

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

        private VisualElement CreateHideButton()
        {
            var button = new Button(HideTargets)
            {
                tooltip = I18N.Get(
                    "editor.hide.tooltip")
            };
            button.AddToClassList(HideButtonClassName);
            button.Add(new Icon(
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.VisibilityHidden,
                    UiSizeTokens.Size16)));
            var text = UiTextFactory.Create(
                _targets.Count > 1
                    ? I18N.Get(
                        "editor.hide.multipleLabel",
                        _targets.Count)
                    : I18N.Get(
                        "editor.hide.singleLabel"));
            text.AddToClassList(
                HideButtonTextClassName);
            button.Add(text);
            return button;
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
            var first = _service.Get(_objectIds[0]);
            var colorMixed = false;
            var iconMixed = false;
            for (var i = 1;
                 i < _objectIds.Count;
                 i++)
            {
                var current =
                    _service.Get(_objectIds[i]);
                colorMixed |=
                    first.HasBackgroundColor !=
                    current.HasBackgroundColor ||
                    (first.HasBackgroundColor &&
                     first.BackgroundColor !=
                     current.BackgroundColor);
                iconMixed |= !string.Equals(
                    first.IconGuid,
                    current.IconGuid,
                    StringComparison.Ordinal);
            }

            var icon = !iconMixed && first.HasIcon
                ? LoadIcon(first.IconGuid)
                : null;
            return new DecorationStyleEditorState(
                first.HasBackgroundColor
                    ? first.BackgroundColor
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
                HierarchyStyleColorPresets.GetAll();
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
                var texture = LoadIcon(guid);
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
            return _targets.Count > 1
                ? I18N.Get(
                    "window.multipleTitle",
                    _targets.Count)
                : _targets[0].name;
        }

        private string CreateSubtitle()
        {
            return _targets.Count > 1
                ? I18N.Get("window.multipleSubtitle")
                : I18N.Get("window.singleSubtitle");
        }

        private string CreateTargetTooltip()
        {
            var paths = new List<string>();
            for (var i = 0; i < _targets.Count; i++)
            {
                paths.Add(GetHierarchyPath(
                    _targets[i].transform));
            }

            return string.Join("\n", paths);
        }

        private static string GetHierarchyPath(
            Transform transform)
        {
            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private void SetBackgroundColor(Color color)
        {
            _service.SetBackgroundColor(
                _objectIds,
                color);
            RefreshAfterChange();
        }

        private void ClearBackgroundColor()
        {
            SetBackgroundColor(Color.clear);
        }

        private void SetIcon(Texture texture)
        {
            if (texture != null &&
                !(texture is Texture2D))
            {
                _editor?.SetState(CreateEditorState());
                return;
            }

            var path = texture != null
                ? AssetDatabase.GetAssetPath(texture)
                : string.Empty;
            var iconGuid = string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
            _service.SetIcon(_objectIds, iconGuid);
            for (var i = 0; i < _targets.Count; i++)
            {
                _iconApplier.Apply(
                    _targets[i],
                    iconGuid);
            }

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

        private bool IsIconAppliedToTargets(
            string iconGuid)
        {
            if (string.IsNullOrEmpty(iconGuid))
            {
                return false;
            }

            for (var i = 0;
                 i < _objectIds.Count;
                 i++)
            {
                if (string.Equals(
                        _service.Get(_objectIds[i])
                            .IconGuid,
                        iconGuid,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshAfterChange()
        {
            _editor?.SetState(CreateEditorState());
            RefreshPreview();
            InjectorApi.Repaint(
                InjectionChannel.HierarchyItem);
        }

        private void RefreshPreview()
        {
            if (_preview == null ||
                _previewImage == null ||
                _targets == null ||
                _targets.Count == 0)
            {
                return;
            }

            var first = _service.Get(_objectIds[0]);
            var colorMixed = false;
            var iconMixed = false;
            for (var i = 1;
                 i < _objectIds.Count;
                 i++)
            {
                var current =
                    _service.Get(_objectIds[i]);
                colorMixed |=
                    first.HasBackgroundColor !=
                    current.HasBackgroundColor ||
                    (first.HasBackgroundColor &&
                     first.BackgroundColor !=
                     current.BackgroundColor);
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
                EditorGUIUtility.ObjectContent(
                    _targets[0],
                    typeof(GameObject)).image;
            if (!colorMixed &&
                first.HasBackgroundColor)
            {
                _preview.style.backgroundColor =
                    new StyleColor(
                        first.BackgroundColor);
            }
            else
            {
                _preview.style.backgroundColor =
                    StyleKeyword.Null;
            }
        }

        private static Texture2D LoadIcon(string iconGuid)
        {
            var path =
                AssetDatabase.GUIDToAssetPath(iconGuid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(
                    path);
        }

        private void HideTargets()
        {
            _visibility.Hide(
                _targets,
                I18N.Get("editor.hide.undo"));
            Close();
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
            EditorApplication.delayCall +=
                EvaluateFocusLoss;
        }

        private void EvaluateFocusLoss()
        {
            if (this == null)
            {
                return;
            }

            var focused = EditorWindow.focusedWindow;
            if (focused == this)
            {
                StopTransientFocusWatch();
                return;
            }

            if (EditorPopupWindow.IsTransientPicker(
                    focused) ||
                EditorPopupWindow.HasOpenTransientPicker() ||
                EditorPopupWindow.IsEyeDropperOpen() ||
                focused is ImageTooltipWindow)
            {
                StartTransientFocusWatch();
                return;
            }

            Close();
        }

        private void StartTransientFocusWatch()
        {
            if (_watchingTransientFocus)
            {
                return;
            }

            _watchingTransientFocus = true;
            EditorApplication.update +=
                WatchTransientFocus;
        }

        private void WatchTransientFocus()
        {
            if (this == null)
            {
                StopTransientFocusWatch();
                return;
            }

            var focused = EditorWindow.focusedWindow;
            if (focused == this)
            {
                StopTransientFocusWatch();
                return;
            }

            if (EditorPopupWindow.IsTransientPicker(
                    focused) ||
                EditorPopupWindow.HasOpenTransientPicker() ||
                EditorPopupWindow.IsEyeDropperOpen() ||
                focused is ImageTooltipWindow)
            {
                return;
            }

            if (focused == null)
            {
                StopTransientFocusWatch();
                Focus();
                return;
            }

            StopTransientFocusWatch();
            Focus();
        }

        private void StopTransientFocusWatch()
        {
            if (!_watchingTransientFocus)
            {
                return;
            }

            _watchingTransientFocus = false;
            EditorApplication.update -=
                WatchTransientFocus;
        }

        private void OnDisable()
        {
            StopTransientFocusWatch();
        }

        private static void CloseExistingWindows()
        {
            var windows =
                Resources.FindObjectsOfTypeAll<
                    HierarchyStyleWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                windows[i].Close();
            }
        }
    }

    internal static class HierarchyStyleWindowLayout
    {
        public static Rect CalculateWindowRect(
            Vector2 anchor,
            Vector2 size,
            Rect desktopBounds)
        {
            var maximumX = Mathf.Max(
                desktopBounds.xMin,
                desktopBounds.xMax - size.x);
            var maximumY = Mathf.Max(
                desktopBounds.yMin,
                desktopBounds.yMax - size.y);
            return new Rect(
                Mathf.Clamp(
                    anchor.x,
                    desktopBounds.xMin,
                    maximumX),
                Mathf.Clamp(
                    anchor.y,
                    desktopBounds.yMin,
                    maximumY),
                size.x,
                size.y);
        }
    }
}
