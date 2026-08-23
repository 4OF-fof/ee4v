using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

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
        private readonly Dictionary<AnimationClip, ThumbnailEntry> _thumbnails =
            new Dictionary<AnimationClip, ThumbnailEntry>();

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
                "Editor/UI/Components/Content/Icon/icon.uss",
                "Editor/UI/Components/Inputs/ui-button.uss",
                "Editor/UI/Components/Inputs/SearchField/search-field.uss",
                "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");

            _view = new FaceExpressionView(CreateText(), rect => _preview?.Draw(rect));
            _view.AvatarChanged += SetAvatar;
            _view.ClipChanged += SetClip;
            _view.NewClipRequested += CreateClip;
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

        private void RefreshLibrary()
        {
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
            if (_clip != null)
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

        private void DrawLibraryThumbnail(AnimationClip clip, Rect rect)
        {
            if (clip == null || _preview == null || _avatar == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
                return;
            }

            var dirtyCount = EditorUtility.GetDirtyCount(clip);
            if (!_thumbnails.TryGetValue(clip, out var entry) ||
                entry.Texture == null ||
                entry.DirtyCount != dirtyCount)
            {
                if (entry.Texture != null)
                {
                    DestroyImmediate(entry.Texture);
                }

                var channels = FaceExpressionClipEditor.Read(
                    _avatar,
                    clip,
                    Array.Empty<string>(),
                    _previewRendererPaths);
                var texture = _preview.RenderThumbnail(channels, 160, 160);
                _preview.SetChannels(_channels, false);
                if (texture != null)
                {
                    texture.hideFlags = HideFlags.HideAndDontSave;
                }

                entry = new ThumbnailEntry(texture, dirtyCount);
                _thumbnails[clip] = entry;
            }

            if (entry.Texture == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
                return;
            }

            GUI.DrawTexture(rect, entry.Texture, ScaleMode.ScaleAndCrop, false);
        }

        private void ClearThumbnails()
        {
            foreach (var entry in _thumbnails.Values)
            {
                if (entry.Texture != null)
                {
                    DestroyImmediate(entry.Texture);
                }
            }

            _thumbnails.Clear();
            _view?.MarkDirtyRepaint();
        }

        private readonly struct ThumbnailEntry
        {
            internal ThumbnailEntry(Texture2D texture, int dirtyCount)
            {
                Texture = texture;
                DirtyCount = dirtyCount;
            }

            internal Texture2D Texture { get; }
            internal int DirtyCount { get; }
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

            if (ReferenceEquals(
                    args.Definition,
                    ProjectAssetSettings.RootFolderName) ||
                ReferenceEquals(
                    args.Definition,
                    ProjectAssetSettings.UseProjectRootFolderName) ||
                ReferenceEquals(
                    args.Definition,
                    ProjectAssetSettings.ProjectRootFolderName))
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
