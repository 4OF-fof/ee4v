using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class FaceExpressionWindow : EditorWindow
    {
        private const StringComparison AssetPathComparison =
            StringComparison.OrdinalIgnoreCase;

        private FaceExpressionPreview _preview;
        private FaceExpressionView _view;
        private ISettingsService _settings;
        private IBlendShapePresetStore _presetStore;
        private GameObject _avatar;
        private AnimationClip _clip;
        private IReadOnlyList<BlendShapeChannel> _channels = Array.Empty<BlendShapeChannel>();
        private IReadOnlyList<AnimationClip> _libraryClips =
            Array.Empty<AnimationClip>();
        private IReadOnlyList<string> _libraryFolders = Array.Empty<string>();
        private string _libraryRootFolder = string.Empty;
        private string _libraryFolder = string.Empty;
        private IReadOnlyList<string> _previewRendererPaths =
            Array.Empty<string>();
        private bool _changingClip;
        private readonly AnimationClipThumbnailCache _thumbnails =
            new AnimationClipThumbnailCache();

        private bool HasClipReference => !ReferenceEquals(_clip, null);
        private bool IsClipMissing => HasClipReference && _clip == null;

        [MenuItem("ee4v/Window/Face Expression/Face Expression Editor")]
        private static void Open()
        {
            ShowWindow();
            FaceExpressionGroupWindow.ShowWindow();
        }

        internal static void ShowWindow()
        {
            var window = GetWindow<FaceExpressionWindow>();
            window.titleContent = UiTextFactory.CreateGuiContent(I18N.Get("window.title"));
            window.minSize = new Vector2(720f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = CoreSettings.Current;
            _settings.Changed += OnSettingChanged;
            _presetStore = BlendShapePresetStorage.Shared;
            _presetStore.Changed += RefreshClip;
            FaceExpressionGroupSession.Changed += ApplyGroupFilter;
            FaceExpressionGroupSession.MeshesChanged += RefreshClip;
            _preview = new FaceExpressionPreview(Repaint);
            Undo.undoRedoPerformed += RefreshClip;
            EditorApplication.projectChanged += RefreshLibrary;
            I18N.Reloaded += Rebuild;
        }

        private void OnDisable()
        {
            if (_settings != null)
            {
                _settings.Changed -= OnSettingChanged;
            }

            if (_presetStore != null)
            {
                _presetStore.Changed -= RefreshClip;
                _presetStore = null;
            }

            Undo.undoRedoPerformed -= RefreshClip;
            EditorApplication.projectChanged -= RefreshLibrary;
            I18N.Reloaded -= Rebuild;
            FaceExpressionGroupSession.Changed -= ApplyGroupFilter;
            FaceExpressionGroupSession.MeshesChanged -= RefreshClip;
            ClearThumbnails();
            _preview?.Dispose();
            _preview = null;
        }

        private void CreateGUI()
        {
            BuildContent();
            RefreshLibrary();
            if (_avatar == null && Selection.activeGameObject != null)
            {
                SetAvatar(Selection.activeGameObject);
            }
            else
            {
                RenderState();
            }
        }

        private void Rebuild()
        {
            if (rootVisualElement.panel == null)
            {
                return;
            }

            BuildContent();
            RefreshLibrary();
            RenderState();
        }

        private void BuildContent()
        {
            titleContent = UiTextFactory.CreateGuiContent(I18N.Get("window.title"));
            var root = rootVisualElement;
            root.Clear();
            UiComposition.Prepare(
                root,
                "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");

            _view = new FaceExpressionView(CreateText(), rect => _preview?.Draw(rect));
            _view.AvatarChanged += SetAvatar;
            _view.ClipChanged += SetClip;
            _view.NewClipRequested += CreateClip;
            _view.CopyClipRequested += CopyClip;
            _view.BackRequested += GoBack;
            _view.LibraryFolderRequested += OpenLibraryFolder;
            _view.ChannelChanged += ChangeChannel;
            _view.ResetViewRequested += () => _preview?.ResetView();
            RenderLibrary();
            root.Add(_view);
        }

        private void SetAvatar(GameObject avatar)
        {
            _avatar = avatar;
            _clip = null;
            _preview?.SetAvatar(avatar);
            _previewRendererPaths = FaceExpressionClipEditor.GetRendererPaths(avatar);
            ClearThumbnails();
            RenderLibrary();
            FaceExpressionGroupSession.SetAvatar(avatar);
            _view?.SetAvatar(avatar);
            FaceExpressionSettings.EnsureNamePreset(
                avatar,
                _settings,
                _presetStore);
            RefreshValidation();
        }

        private void SetClip(AnimationClip clip)
        {
            _changingClip = true;
            try
            {
                _clip = clip;
                RefreshClip();
            }
            finally
            {
                _changingClip = false;
            }
        }

        private void RefreshClip()
        {
            _channels = FaceExpressionClipEditor.Read(
                _avatar,
                _clip,
                FaceExpressionSettings.GetSeparators(_settings),
                FaceExpressionGroupSession.RendererPaths);
            _view?.SetClip(_clip);
            RefreshValidation();
            FaceExpressionGroupSession.UpdateChannels(_channels);
            _preview?.SetChannels(_channels);
        }

        private void ChangeChannel(BlendShapeChannel channel)
        {
            if (_changingClip ||
                _clip == null ||
                !_channels.Any(current => ReferenceEquals(current, channel)))
            {
                return;
            }

            FaceExpressionClipEditor.Write(_clip, channel);
            _preview?.SetChannels(_channels);
            RefreshValidation();
        }

        private void RefreshValidation()
        {
            if (_view == null)
            {
                return;
            }

            if (_avatar == null || _clip == null)
            {
                _view.SetValidation(string.Empty, HelpBoxMessageType.Info);
                return;
            }

            var findings = FaceExpressionApi.ValidateClip(_avatar, _clip);
            if (findings.Count == 0)
            {
                _view.SetValidation(
                    I18N.Get("validation.valid"),
                    HelpBoxMessageType.Info);
                return;
            }

            var type = findings.Any(finding => string.Equals(
                finding.Severity,
                "error",
                StringComparison.OrdinalIgnoreCase))
                ? HelpBoxMessageType.Error
                : HelpBoxMessageType.Warning;
            _view.SetValidation(
                string.Join("\n", findings.Select(FormatValidationFinding)),
                type);
        }

        private static string FormatValidationFinding(
            FaceExpressionValidationFinding finding)
        {
            var message = I18N.Get("validation." + finding.Code);
            var location = string.Join(
                " / ",
                new[] { finding.RendererPath, finding.ShapeName }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrEmpty(location)
                ? "• " + message
                : "• " + message + " (" + location + ")";
        }

        private void CreateClip()
        {
            var rootFolder = NormalizeAssetPath(
                ProjectAssetSettings.EnsureAssetFolder(
                    "Animation/Facial",
                    _settings));
            if (!string.Equals(
                    _libraryRootFolder,
                    rootFolder,
                    AssetPathComparison))
            {
                _libraryRootFolder = rootFolder;
                _libraryFolder = rootFolder;
            }

            var folder = AssetDatabase.IsValidFolder(_libraryFolder) &&
                         IsAtOrBelow(_libraryFolder, rootFolder)
                ? _libraryFolder
                : rootFolder;
            var path = EditorUtility.SaveFilePanelInProject(
                I18N.Get("dialog.createTitle"),
                I18N.Get("dialog.defaultName"),
                "anim",
                I18N.Get("dialog.createMessage"),
                folder);
            var clip = FaceExpressionClipEditor.Create(path);
            if (clip == null)
            {
                return;
            }

            Selection.activeObject = clip;
            SetClip(clip);
        }

        private void CopyClip(AnimationClip source)
        {
            if (source == null ||
                !AssetDatabase.IsValidFolder(_libraryFolder) ||
                !IsAtOrBelow(_libraryFolder, _libraryRootFolder))
            {
                return;
            }

            var fileName = SanitizeFileName(source.name);
            var path = AssetDatabase.GenerateUniqueAssetPath(
                _libraryFolder + "/" + fileName + ".anim");
            var clip = FaceExpressionClipEditor.Copy(source, path);
            if (clip == null)
            {
                return;
            }

            Selection.activeObject = clip;
            SetClip(clip);
        }

        private void RefreshLibrary()
        {
            if (IsClipMissing)
            {
                SetClip(null);
            }

            ClearThumbnails();
            var rootFolder = NormalizeAssetPath(
                ProjectAssetSettings.GetAssetFolder(
                    "Animation/Facial",
                    _settings));
            if (!string.Equals(
                    _libraryRootFolder,
                    rootFolder,
                    AssetPathComparison))
            {
                _libraryRootFolder = rootFolder;
                _libraryFolder = rootFolder;
            }

            if (!IsAtOrBelow(_libraryFolder, rootFolder) ||
                !AssetDatabase.IsValidFolder(_libraryFolder))
            {
                _libraryFolder = rootFolder;
            }

            if (!AssetDatabase.IsValidFolder(_libraryFolder))
            {
                _libraryFolders = Array.Empty<string>();
                _libraryClips = Array.Empty<AnimationClip>();
                RenderLibrary();
                return;
            }

            _libraryFolders = AssetDatabase.GetSubFolders(_libraryFolder)
                .Select(NormalizeAssetPath)
                .OrderBy(GetAssetName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(folder => folder, StringComparer.Ordinal)
                .ToArray();
            _libraryClips = AssetDatabase
                .FindAssets("t:AnimationClip", new[] { _libraryFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => string.Equals(
                    GetAssetDirectory(path),
                    _libraryFolder,
                    AssetPathComparison))
                .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
                .Where(clip => clip != null)
                .OrderBy(clip => clip.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    clip => AssetDatabase.GetAssetPath(clip),
                    StringComparer.Ordinal)
                .ToArray();
            RenderLibrary();
        }

        private void RenderLibrary()
        {
            _view?.SetLibrary(
                _libraryFolders,
                _libraryClips,
                !string.Equals(
                    _libraryFolder,
                    _libraryRootFolder,
                    AssetPathComparison),
                DrawLibraryThumbnail);
        }

        private void OpenLibraryFolder(string folder)
        {
            folder = NormalizeAssetPath(folder);
            if (!AssetDatabase.IsValidFolder(folder) ||
                !IsAtOrBelow(folder, _libraryRootFolder))
            {
                return;
            }

            _libraryFolder = folder;
            RefreshLibrary();
        }

        private void GoBack()
        {
            if (HasClipReference)
            {
                SetClip(null);
                return;
            }

            if (string.Equals(
                    _libraryFolder,
                    _libraryRootFolder,
                    AssetPathComparison))
            {
                return;
            }

            var parent = GetAssetDirectory(_libraryFolder);
            _libraryFolder = IsAtOrBelow(parent, _libraryRootFolder)
                ? parent
                : _libraryRootFolder;
            RefreshLibrary();
        }

        private static bool IsAtOrBelow(string folder, string rootFolder)
        {
            return !string.IsNullOrEmpty(folder) &&
                   !string.IsNullOrEmpty(rootFolder) &&
                   (string.Equals(
                        folder,
                        rootFolder,
                        AssetPathComparison) ||
                    folder.StartsWith(
                        rootFolder + "/",
                        AssetPathComparison));
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        private static string GetAssetDirectory(string path)
        {
            return NormalizeAssetPath(Path.GetDirectoryName(path));
        }

        private static string GetAssetName(string path)
        {
            var separatorIndex = path.LastIndexOf('/');
            return separatorIndex >= 0
                ? path.Substring(separatorIndex + 1)
                : path;
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var name = new string((value ?? string.Empty)
                    .Select(character =>
                        invalid.Contains(character) ||
                        character == '/' ||
                        character == '\\'
                            ? '_'
                            : character)
                    .ToArray())
                .Trim()
                .TrimEnd('.');
            return string.IsNullOrWhiteSpace(name) ||
                   string.Equals(name, ".", StringComparison.Ordinal) ||
                   string.Equals(name, "..", StringComparison.Ordinal)
                ? "Expression"
                : name;
        }

        private void DrawLibraryThumbnail(AnimationClip clip, Rect rect)
        {
            _thumbnails.Draw(
                clip,
                rect,
                _preview,
                _avatar,
                _previewRendererPaths,
                () => _preview.SetChannels(_channels, false));
        }

        private void ClearThumbnails()
        {
            _thumbnails.Clear();
            _view?.MarkDirtyRepaint();
        }

        private void OnSettingChanged(
            object sender,
            SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    FaceExpressionSettings.BlendShapeSeparators))
            {
                RefreshClip();
                return;
            }

            if (ProjectAssetSettings.IsAssetRootDefinition(
                    args.Definition))
            {
                RefreshLibrary();
            }
        }

        private void RenderState()
        {
            _view?.SetAvatar(_avatar);
            _view?.SetClip(_clip);
            ApplyGroupFilter();
            _preview?.SetChannels(_channels);
        }

        private void ApplyGroupFilter()
        {
            var selectedGroupName =
                FaceExpressionGroupSession.SelectedGroupName;
            _view?.SetChannels(
                FaceExpressionGroupSession.Filter(_channels),
                string.IsNullOrEmpty(selectedGroupName)
                    ? I18N.Get("group.all")
                    : selectedGroupName,
                FaceExpressionSettings.GetNameRule(_presetStore),
                !string.IsNullOrEmpty(
                    FaceExpressionGroupSession.SelectedGroupKey));
        }

        private static FaceExpressionViewText CreateText()
        {
            return new FaceExpressionViewText
            {
                Avatar = I18N.Get("field.avatar"),
                Clip = I18N.Get("field.clip"),
                NewClip = I18N.Get("action.newClip"),
                CopyAndEdit = I18N.Get("action.copyAndEdit"),
                ResetView = I18N.Get("action.resetView"),
                BackToLibrary = I18N.Get("action.backToLibrary"),
                SearchPlaceholder = I18N.Get("search.placeholder"),
                SearchTooltip = I18N.Get("search.tooltip"),
                ClearSearchTooltip = I18N.Get("search.clearTooltip"),
                BlendShapes = I18N.Get("section.blendShapes"),
                Library = I18N.Get("section.library"),
                ClipOnly = I18N.Get("filter.clipOnly"),
                ClipOnlyTooltip = I18N.Get("filter.clipOnlyTooltip"),
                NoBlendShapes = I18N.Get("empty.blendShapes")
            };
        }
    }
}
