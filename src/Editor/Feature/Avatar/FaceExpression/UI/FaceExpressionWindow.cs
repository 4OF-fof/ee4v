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
        private FaceExpressionEditor _editor;

        [MenuItem("ee4v/Window/Avatar/Face Expression/Editor", false, 220)]
        private static void Open()
        {
            ShowWindow();
            FaceExpressionGroupWindow.ShowWindow();
        }

        internal static void ShowWindow()
        {
            var window = GetWindow<FaceExpressionWindow>();
            window.RefreshTitle();
            window.minSize = new Vector2(720f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshTitle();
            I18N.Reloaded += RefreshTitle;
        }

        private void OnDisable()
        {
            I18N.Reloaded -= RefreshTitle;
            _editor?.Dispose();
            _editor = null;
        }

        private void CreateGUI()
        {
            if (_editor == null)
            {
                _editor = new FaceExpressionEditor(rootVisualElement, Repaint, false);
            }

            _editor.Initialize(Selection.activeGameObject);
        }

        private void RefreshTitle()
        {
            titleContent = UiTextFactory.CreateGuiContent(I18N.Get("window.title"));
        }
    }

    public sealed class FaceExpressionEmbeddedView : IDisposable
    {
        private readonly FaceExpressionEditor _editor;

        public FaceExpressionEmbeddedView(
            VisualElement root,
            Action repaint)
        {
            _editor = new FaceExpressionEditor(root, repaint, true);
        }

        public void Initialize(GameObject avatar)
        {
            _editor.Initialize(avatar);
        }

        public void StopPlayback()
        {
            _editor.StopPlayback();
        }

        public void Dispose()
        {
            _editor.Dispose();
        }
    }

    internal sealed class FaceExpressionEditor : IDisposable
    {
        private const StringComparison AssetPathComparison =
            StringComparison.OrdinalIgnoreCase;
        private const float DefaultTransitionDuration = 0.2f;

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
        private float _currentTime;
        private float _timelineDuration = 1f;
        private bool _playing;
        private double _lastPlaybackTime;
        private IReadOnlyList<float> _poseTimes = Array.Empty<float>();
        private IReadOnlyList<AnimationClip> _poseSources =
            Array.Empty<AnimationClip>();
        private IReadOnlyList<string> _poseNames = Array.Empty<string>();
        private int _selectedPoseIndex;
        private readonly AnimationClipThumbnailCache _thumbnails =
            new AnimationClipThumbnailCache();
        private readonly AnimationClipThumbnailCache _poseThumbnails =
            new AnimationClipThumbnailCache();
        private double _poseThumbnailRefreshAt = -1d;
        private double _validationRefreshAt = -1d;
        private readonly VisualElement _root;
        private readonly Action _repaint;
        private readonly bool _avatarLocked;
        private bool _disposed;

        private bool HasClipReference => !ReferenceEquals(_clip, null);
        private bool IsClipMissing => HasClipReference && _clip == null;

        internal FaceExpressionEditor(
            VisualElement root,
            Action repaint,
            bool avatarLocked)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _repaint = repaint;
            _avatarLocked = avatarLocked;

            _settings = CoreSettings.Current;
            _settings.Changed += OnSettingChanged;
            _presetStore = BlendShapePresetStorage.Shared;
            _presetStore.Changed += RefreshClip;
            FaceExpressionGroupSession.Changed += ApplyGroupFilter;
            FaceExpressionGroupSession.MeshesChanged += RefreshClip;
            _preview = new FaceExpressionPreview(RequestRepaint);
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged += RefreshLibrary;
            EditorApplication.update += UpdatePlayback;
            I18N.Reloaded += Rebuild;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopPlayback();
            if (_settings != null)
            {
                _settings.Changed -= OnSettingChanged;
            }

            if (_presetStore != null)
            {
                _presetStore.Changed -= RefreshClip;
                _presetStore = null;
            }

            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= RefreshLibrary;
            EditorApplication.update -= UpdatePlayback;
            I18N.Reloaded -= Rebuild;
            FaceExpressionGroupSession.Changed -= ApplyGroupFilter;
            FaceExpressionGroupSession.MeshesChanged -= RefreshClip;
            ClearThumbnails();
            _preview?.Dispose();
            _preview = null;
        }

        internal void Initialize(GameObject avatar)
        {
            BuildContent();
            RefreshLibrary();
            if (_avatarLocked || (_avatar == null && avatar != null))
            {
                SetAvatar(avatar);
            }
            else
            {
                RenderState();
            }
        }

        private void Rebuild()
        {
            if (_disposed)
            {
                return;
            }

            if (_root.panel == null)
            {
                return;
            }

            BuildContent();
            RefreshLibrary();
            RenderState();
        }

        private void BuildContent()
        {
            var root = _root;
            root.Clear();
            UiComposition.Prepare(
                root,
                "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");

            _view = new FaceExpressionView(
                CreateText(),
                rect => _preview?.Draw(rect),
                DrawPoseThumbnail,
                !_avatarLocked);
            _view.SetAvatarEditable(!_avatarLocked);
            _view.AvatarChanged += SetAvatar;
            _view.ClipChanged += SetClip;
            _view.NewClipRequested += CreateClip;
            _view.CopyClipRequested += CopyClip;
            _view.BackRequested += GoBack;
            _view.LibraryFolderRequested += OpenLibraryFolder;
            _view.ChannelChanged += ChangeChannel;
            _view.PoseSelected += SelectPose;
            _view.PoseAddRequested += AddPose;
            _view.PoseInsertRequested += InsertPose;
            _view.PoseMoveRequested += MovePose;
            _view.PoseNameChanged += SetPoseName;
            _view.PoseSourceChanged += SetPoseSource;
            _view.PoseRemoveRequested += RemovePose;
            _view.TransitionDurationChanged += SetTransitionDuration;
            _view.ResetViewRequested += () => _preview?.ResetView();
            _view.PlaybackChanged += TogglePlayback;
            _view.TimeChanged += SetCurrentTime;
            _view.LoopChanged += SetLooping;
            RenderLibrary();
            root.Add(_view);
        }

        private void SetAvatar(GameObject avatar)
        {
            _avatar = avatar;
            _clip = null;
            _poseTimes = Array.Empty<float>();
            _poseSources = Array.Empty<AnimationClip>();
            _poseNames = Array.Empty<string>();
            _selectedPoseIndex = 0;
            StopPlayback();
            _currentTime = 0f;
            _timelineDuration = 1f;
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
                StopPlayback();
                _poseThumbnails.Clear();
                _clip = clip;
                _selectedPoseIndex = 0;
                _currentTime = 0f;
                _timelineDuration = clip == null || clip.length <= 0f
                    ? 1f
                    : clip.length;
                RefreshClip();
            }
            finally
            {
                _changingClip = false;
            }
        }

        private void RefreshClip()
        {
            _poseTimes = FaceExpressionClipEditor.GetPoseTimes(_clip);
            _poseSources = FaceExpressionClipEditor.GetPoseSources(
                _clip,
                _poseTimes);
            _poseNames = FaceExpressionClipEditor.GetPoseNames(
                _clip,
                _poseTimes);
            _selectedPoseIndex = Mathf.Clamp(
                _selectedPoseIndex,
                0,
                Mathf.Max(0, _poseTimes.Count - 1));
            var selectedPoseTime = SelectedPoseTime;
            _channels = FaceExpressionClipEditor.Read(
                _avatar,
                _clip,
                FaceExpressionSettings.GetSeparators(_settings),
                FaceExpressionGroupSession.RendererPaths,
                selectedPoseTime);
            if (_clip != null)
            {
                _timelineDuration = Mathf.Max(
                    1f / 60f,
                    _poseTimes.Count == 0
                        ? 0f
                        : _poseTimes[_poseTimes.Count - 1]);
                _currentTime = Mathf.Clamp(
                    FaceExpressionClipEditor.SnapTime(_clip, _currentTime),
                    0f,
                    _timelineDuration);
            }
            _view?.SetClip(_clip);
            UpdateAnimationView();
            RefreshValidation();
            FaceExpressionGroupSession.UpdateChannels(_channels);
            _preview?.SetChannels(_channels);
        }

        private void ChangeChannel(BlendShapeChannel channel)
        {
            if (_changingClip ||
                _clip == null ||
                SelectedPoseSource != null ||
                !_channels.Any(current => ReferenceEquals(current, channel)))
            {
                return;
            }

            _playing = false;
            _currentTime = SelectedPoseTime;
            var previousPoseTimes = _poseTimes;
            FaceExpressionClipEditor.WritePose(
                _clip,
                channel,
                SelectedPoseTime,
                _poseTimes);
            _poseTimes = FaceExpressionClipEditor.GetPoseTimes(_clip);
            var sequenceChanged = !previousPoseTimes.SequenceEqual(_poseTimes);
            _selectedPoseIndex = Mathf.Clamp(
                _selectedPoseIndex,
                0,
                Mathf.Max(0, _poseTimes.Count - 1));
            _timelineDuration = Mathf.Max(
                1f / 60f,
                _poseTimes[_poseTimes.Count - 1]);
            if (sequenceChanged)
            {
                _currentTime = SelectedPoseTime;
                FaceExpressionClipEditor.Sample(
                    _clip,
                    _channels,
                    _currentTime);
                _view?.RefreshChannelValues();
            }

            _preview?.SetChannels(_channels);
            UpdateAnimationView();
            if (sequenceChanged)
            {
                InvalidatePoseThumbnails();
                _validationRefreshAt =
                    EditorApplication.timeSinceStartup + 0.15d;
            }
            else
            {
                ScheduleDeferredRefresh();
            }
        }

        private float SelectedPoseTime =>
            _poseTimes.Count == 0
                ? 0f
                : _poseTimes[Mathf.Clamp(
                    _selectedPoseIndex,
                    0,
                    _poseTimes.Count - 1)];

        private AnimationClip SelectedPoseSource =>
            _poseSources.Count == 0
                ? null
                : _poseSources[Mathf.Clamp(
                    _selectedPoseIndex,
                    0,
                    _poseSources.Count - 1)];

        private void SelectPose(int poseIndex)
        {
            if (_clip == null || poseIndex < 0 || poseIndex >= _poseTimes.Count)
            {
                return;
            }

            StopPlayback();
            _selectedPoseIndex = poseIndex;
            _currentTime = SelectedPoseTime;
            RefreshClip();
        }

        private void AddPose()
        {
            InsertPose(_selectedPoseIndex);
        }

        private void InsertPose(int afterPoseIndex)
        {
            if (_clip == null ||
                afterPoseIndex < 0 ||
                afterPoseIndex >= _poseTimes.Count)
            {
                return;
            }

            StopPlayback();
            var newTime = FaceExpressionClipEditor.AddPose(
                _clip,
                _poseTimes[afterPoseIndex],
                DefaultTransitionDuration);
            if (newTime < 0f)
            {
                return;
            }

            _selectedPoseIndex = afterPoseIndex + 1;
            _currentTime = newTime;
            RefreshClip();
            InvalidatePoseThumbnails();
        }

        private void MovePose(int poseIndex, int targetIndex)
        {
            if (_clip == null ||
                poseIndex < 0 ||
                targetIndex < 0 ||
                poseIndex >= _poseTimes.Count ||
                targetIndex >= _poseTimes.Count)
            {
                return;
            }

            StopPlayback();
            if (!FaceExpressionClipEditor.MovePose(
                    _clip,
                    poseIndex,
                    targetIndex))
            {
                return;
            }

            _selectedPoseIndex = targetIndex;
            _currentTime = _poseTimes[targetIndex];
            RefreshClip();
            InvalidatePoseThumbnails();
        }

        private void SetPoseSource(int poseIndex, AnimationClip source)
        {
            if (_clip == null ||
                poseIndex < 0 ||
                poseIndex >= _poseTimes.Count)
            {
                return;
            }

            StopPlayback();
            if (!FaceExpressionClipEditor.SetPoseSource(
                _clip,
                _poseTimes[poseIndex],
                source,
                _channels))
            {
                return;
            }

            _selectedPoseIndex = poseIndex;
            _currentTime = _poseTimes[poseIndex];
            RefreshClip();
            InvalidatePoseThumbnails();
        }

        private void SetPoseName(int poseIndex, string name)
        {
            if (_clip == null ||
                poseIndex < 0 ||
                poseIndex >= _poseTimes.Count)
            {
                return;
            }

            StopPlayback();
            if (!FaceExpressionClipEditor.SetPoseName(
                    _clip,
                    _poseTimes[poseIndex],
                    name))
            {
                return;
            }

            RefreshClip();
        }

        private void RemovePose(int poseIndex)
        {
            if (_clip == null || poseIndex < 0 || poseIndex >= _poseTimes.Count)
            {
                return;
            }

            StopPlayback();
            if (!FaceExpressionClipEditor.RemovePose(
                    _clip,
                    _poseTimes[poseIndex]))
            {
                return;
            }

            _selectedPoseIndex = Mathf.Max(0, poseIndex - 1);
            _currentTime = 0f;
            RefreshClip();
            _currentTime = SelectedPoseTime;
            SampleCurrentTime();
            InvalidatePoseThumbnails();
        }

        private void SetTransitionDuration(int poseIndex, float duration)
        {
            if (_clip == null || poseIndex < 0 || poseIndex >= _poseTimes.Count - 1)
            {
                return;
            }

            StopPlayback();
            if (!FaceExpressionClipEditor.SetTransitionDuration(
                    _clip,
                    _poseTimes[poseIndex],
                    duration))
            {
                UpdateAnimationView();
                return;
            }

            _currentTime = 0f;
            RefreshClip();
            _currentTime = SelectedPoseTime;
            SampleCurrentTime();
            InvalidatePoseThumbnails();
        }

        private void TogglePlayback()
        {
            if (_clip == null || _poseTimes.Count < 2)
            {
                return;
            }

            _playing = !_playing;
            if (_playing)
            {
                if (_currentTime >= _timelineDuration)
                {
                    _currentTime = 0f;
                }

                _lastPlaybackTime = EditorApplication.timeSinceStartup;
            }
            else
            {
                SampleCurrentTime();
                return;
            }

            UpdateAnimationView();
        }

        internal void StopPlayback()
        {
            _playing = false;
            UpdateAnimationView();
        }

        private void SetCurrentTime(float time)
        {
            if (_clip == null)
            {
                return;
            }

            _playing = false;
            _currentTime = Mathf.Clamp(
                FaceExpressionClipEditor.SnapTime(_clip, time),
                0f,
                _timelineDuration);
            SampleCurrentTime();
        }

        private void SetLooping(bool looping)
        {
            FaceExpressionClipEditor.SetLooping(_clip, looping);
            UpdateAnimationView();
        }

        private void UpdatePlayback()
        {
            var now = EditorApplication.timeSinceStartup;
            if (_poseThumbnailRefreshAt >= 0d && now >= _poseThumbnailRefreshAt)
            {
                _poseThumbnailRefreshAt = -1d;
                _poseThumbnails.Invalidate(_clip);
                RequestRepaint();
            }

            if (_validationRefreshAt >= 0d && now >= _validationRefreshAt)
            {
                _validationRefreshAt = -1d;
                RefreshValidation();
            }

            if (!_playing || _clip == null)
            {
                return;
            }

            var delta = Mathf.Max(0f, (float)(now - _lastPlaybackTime));
            _lastPlaybackTime = now;
            var nextTime = _currentTime + delta;
            if (nextTime > _timelineDuration)
            {
                if (FaceExpressionClipEditor.IsLooping(_clip))
                {
                    nextTime %= _timelineDuration;
                }
                else
                {
                    nextTime = _timelineDuration;
                    _playing = false;
                }
            }

            _currentTime = nextTime;
            SampleCurrentTime();
        }

        private void SampleCurrentTime()
        {
            FaceExpressionClipEditor.Sample(_clip, _channels, _currentTime);
            _view?.RefreshChannelValues();
            _preview?.SetChannels(_channels);
            UpdateAnimationView();
        }

        private void ScheduleDeferredRefresh()
        {
            var refreshAt = EditorApplication.timeSinceStartup + 0.15d;
            _poseThumbnailRefreshAt = refreshAt;
            _validationRefreshAt = refreshAt;
        }

        private void InvalidatePoseThumbnails()
        {
            _poseThumbnailRefreshAt = -1d;
            _poseThumbnails.Invalidate(_clip);
            RequestRepaint();
        }

        private void OnUndoRedo()
        {
            RefreshClip();
            InvalidatePoseThumbnails();
        }

        private void UpdateAnimationView()
        {
            _view?.SetAnimationState(
                _currentTime,
                _timelineDuration,
                FaceExpressionClipEditor.IsLooping(_clip),
                _playing,
                _poseTimes,
                _poseSources,
                _poseNames,
                _selectedPoseIndex,
                FaceExpressionClipEditor.CanAddPose(_clip),
                I18N.Get("animation.play"),
                I18N.Get("animation.pause"));
        }

        private void RefreshValidation()
        {
            if (_view == null)
            {
                return;
            }

            if (_avatar == null || _clip == null)
            {
                _view.SetValidation(null);
                return;
            }

            var findings = FaceExpressionApi.ValidateClip(_avatar, _clip);
            if (findings.Count == 0)
            {
                _view.SetValidation(null);
                return;
            }

            var type = findings.Any(finding => string.Equals(
                finding.Severity,
                "error",
                StringComparison.OrdinalIgnoreCase))
                ? MessageSeverity.Error
                : MessageSeverity.Warning;
            var reasons = findings.Select(finding => I18N.Get("validation." + finding.Code)).Distinct().ToArray();
            _view.SetValidation(new MessagePanelState(
                I18N.Get("validation.title", findings.Count),
                string.Join("\n", reasons),
                type,
                findings.Select(finding => FormatValidationFinding(finding, reasons.Length > 1)).Distinct().ToArray()));
        }

        private static string FormatValidationFinding(
            FaceExpressionValidationFinding finding, bool includeReason)
        {
            var message = I18N.Get("validation." + finding.Code);
            var location = string.Join(
                " / ",
                new[] { finding.RendererPath, finding.ShapeName }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrEmpty(location)) { return string.Empty; }
            return includeReason ? message + " (" + location + ")" : location;
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

        private void DrawPoseThumbnail(
            AnimationClip source,
            float time,
            Rect rect)
        {
            var previewClip = source == null ? _clip : source;
            var previewTime = source == null ? time : 0f;
            _poseThumbnails.DrawAtTime(
                previewClip,
                previewTime,
                rect,
                _preview,
                _avatar,
                _previewRendererPaths,
                () => _preview.SetChannels(_channels, false),
                refreshWhenDirty: source != null);
        }

        private void ClearThumbnails()
        {
            _thumbnails.Clear();
            _poseThumbnails.Clear();
            _poseThumbnailRefreshAt = -1d;
            _validationRefreshAt = -1d;
            _view?.MarkDirtyRepaint();
        }

        private void RequestRepaint()
        {
            _repaint?.Invoke();
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
            UpdateAnimationView();
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
            UpdateAnimationView();
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
                NoBlendShapes = I18N.Get("empty.blendShapes"),
                Play = I18N.Get("animation.play"),
                Pause = I18N.Get("animation.pause"),
                Loop = I18N.Get("animation.loop"),
                Sequence = I18N.Get("animation.sequence"),
                Pose = I18N.Get("animation.pose"),
                AddPose = I18N.Get("animation.addPose"),
                AddPoseTooltip = I18N.Get("animation.addPoseTooltip"),
                AddPoseUnavailable = I18N.Get("animation.addPoseUnavailable"),
                InsertPose = I18N.Get("animation.insertPose"),
                InsertPoseTooltip = I18N.Get("animation.insertPoseTooltip"),
                MovePoseEarlier = I18N.Get("animation.movePoseEarlier"),
                MovePoseLater = I18N.Get("animation.movePoseLater"),
                RenamePose = I18N.Get("animation.renamePose"),
                ResetPoseName = I18N.Get("animation.resetPoseName"),
                AddClip = I18N.Get("animation.addClip"),
                AddClipTooltip = I18N.Get("animation.addClipTooltip"),
                RemovePose = I18N.Get("animation.removePose"),
                Transition = I18N.Get("animation.transition"),
                TimelineTooltip = I18N.Get("animation.timelineTooltip")
            };
        }
    }
}
