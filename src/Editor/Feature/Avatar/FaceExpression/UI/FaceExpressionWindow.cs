using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.Core;
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
        }

        internal static void ShowWindow()
        {
            var window = GetWindow<FaceExpressionWindow>();
            window.RefreshTitle();
            window.minSize = new Vector2(1000f, 680f);
            window.Show();
        }

        internal static void ShowFor(GameObject avatar)
        {
            ShowWindow();
            GetWindow<FaceExpressionWindow>()._editor?.Initialize(avatar);
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

        public bool IsAssignmentView => _editor.IsAssignmentView;

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

        public bool TryConfirmNavigation(VisualElement host, Action continueNavigation)
        {
            return _editor.TryConfirmNavigation(host, continueNavigation);
        }

        public void Dispose()
        {
            _editor.Dispose();
        }
    }

    internal sealed partial class FaceExpressionEditor : IDisposable
    {
        private const StringComparison AssetPathComparison =
            StringComparison.OrdinalIgnoreCase;
        private const float DefaultTransitionDuration = 0.2f;

        private FaceExpressionPreview _preview;
        private FaceExpressionView _view;
        private FaceExpressionView _libraryAssignmentsView;
        private GameObject _libraryAssignmentsAvatar;
        private int _libraryAssignmentsRevision = -1;
        private GestureAssignmentView _assignmentView;
        private GestureAssignmentPickerView _assignmentPicker;
        private AnimationClip _assignmentCandidateClip;
        private readonly GestureAssignmentSession _assignments = new GestureAssignmentSession(true);
        private bool _initiallyInstalled;
        private readonly VrchatFaceExpressionGateway _gateway = new VrchatFaceExpressionGateway();
        private ISettingsService _settings;
        private IBlendShapePresetStore _presetStore;
        private GameObject _avatar;
        private AnimationClip _clip;
        private IReadOnlyList<BlendShapeChannel> _channels = Array.Empty<BlendShapeChannel>();
        private IReadOnlyList<AnimationClip> _libraryClips =
            Array.Empty<AnimationClip>();
        private string _libraryRootFolder = string.Empty;
        private string _libraryFolder = string.Empty;
        private string _observedLibraryFolder;
        private string _observedLibraryFolderGuid;
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
        private readonly HashSet<AnimationClip> _editedClips = new HashSet<AnimationClip>();
        private readonly Dictionary<AnimationClip, ClipEditSnapshot> _clipSnapshots =
            new Dictionary<AnimationClip, ClipEditSnapshot>();
        private FaceExpressionConfiguration _savedConfiguration;
        private bool _savedInstallationPresent;
        private int _savedAssignmentRevision;
        private bool _assignmentAssetsDirty;
        private double _assignmentApplyAt = -1d;
        private double _menuIconsRefreshAt = -1d;
        private bool _saveFailed;
        private bool _saveAttemptNeedsRollback;
        private readonly VisualElement _root;
        private readonly Action _repaint;
        private readonly bool _avatarLocked;
        private bool _disposed;
        private bool _initialized;

        private bool HasClipReference => !ReferenceEquals(_clip, null);
        internal bool IsAssignmentView => !_disposed && _view?.IsAssigning == true;
        private bool IsClipMissing => HasClipReference && _clip == null;
        private bool HasUnsavedChanges => _assignmentAssetsDirty ||
            _assignments.Revision != _savedAssignmentRevision || _editedClips.Count > 0 || _saveAttemptNeedsRollback || _saveFailed;

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
            _assignments.ConfigurationChanged += OnAssignmentConfigurationChanged;
            _assignments.Changed += RefreshLibraryAssignmentSelection;
            _preview = new FaceExpressionPreview(RequestRepaint);
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged += RefreshLibrary;
            EditorApplication.update += UpdatePlayback;
            EditorApplication.update += RegisterNextLocalExpression;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += SavePendingChanges;
            I18N.Reloaded += Rebuild;
            GlobalDataSettings.PathChanged += OnLibraryPathChanged;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            SavePendingChanges();
            DisposeSharedLibrary();
            _view?.CloseUnsavedChangesOverlay();
            ReleaseClipSnapshots();
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
            EditorApplication.update -= RegisterNextLocalExpression;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= SavePendingChanges;
            I18N.Reloaded -= Rebuild;
            GlobalDataSettings.PathChanged -= OnLibraryPathChanged;
            FaceExpressionGroupSession.Changed -= ApplyGroupFilter;
            FaceExpressionGroupSession.MeshesChanged -= RefreshClip;
            _assignments.ConfigurationChanged -= OnAssignmentConfigurationChanged;
            _assignments.Changed -= RefreshLibraryAssignmentSelection;
            _assignments.Dispose();
            ClearThumbnails();
            _preview?.Dispose();
            _preview = null;
        }

        internal void Initialize(GameObject avatar)
        {
            BuildContent();
            if (!_initialized || _avatar != avatar)
            {
                _initialized = true;
                SetAvatar(avatar);
            }
            else
            {
                RefreshLibrary();
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
            _view?.CloseUnsavedChangesOverlay();
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
            _view.RegisterCallback<MouseDownEvent>(_ => _assignments.Activate());
            _view.RegisterCallback<FocusInEvent>(_ => _assignments.Activate());
            _view.SetAvatarEditable(!_avatarLocked);
            _view.AvatarChanged += SetAvatar;
            _view.SaveRequested += SavePendingChanges;
            _view.AssignmentSelectionCleared += _assignments.ClearSelection;
            _view.ClipChanged += ChooseClip;
            _view.LibraryClipSelected += SelectAssignmentCandidate;
            _view.EditClipRequested += EditClip;
            _view.NewClipRequested += CreateClip;
            _view.CopyClipRequested += CopyClip;
            _view.ClearClipAssignmentsRequested += ClearClipAssignments;
            _view.ConversionRequested += () => ShowClipConversion(_clip);
            _view.BackRequested += GoBack;
            _view.LibraryFolderRequested += OpenLibraryFolder;
            _view.LibraryProjectFolderRequested += ShowLibraryFolderInProject;
            _view.SharedClipSelected += SelectSharedCandidate;
            _view.SharedClipEditRequested += entry => EditSharedClip(entry, false);
            _view.SharedClipCopyRequested += entry => EditSharedClip(entry, true);
            _view.SharedClipImportRequested += AddSharedClipToProject;
            _view.DeleteClipRequested += DeleteLibraryClip;
            _view.SharedClipDeleteRequested += entry => DeleteLibraryClip(
                FaceExpressionLibraryApi.GetProjectClip(entry), entry);
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
            _assignmentView = new GestureAssignmentView(
                GestureAssignmentWindow.CreateText(), DrawLibraryThumbnail, false, _assignments, readOnly: true);
            _assignmentView.SetAvatar(_avatar);
            _assignmentView.AssignedClipSelected += SelectAssignmentCandidate;
            _assignmentPicker = new GestureAssignmentPickerView(GestureAssignmentWindow.CreateText(), _assignments, _settings);
            _assignmentPicker.CandidateChanged += SelectAssignmentCandidate;
            _assignmentPicker.PrepareCandidate = PrepareLibraryCandidate;
            _assignmentPicker.AssignmentFailed += ReportLibraryFailure;
            _assignmentPicker.SetAvatar(_avatar);
            _assignmentPicker.SetCandidate(_assignmentCandidateClip);
            _view.SetAssignmentContent(_assignmentView, _assignmentPicker);
            _view.SetLibrarySelection(_assignmentCandidateClip);
            RefreshLibraryAssignmentSelection();
            RenderLibrary();
            RefreshSaveNotification();
            root.Add(_view);
        }

        private void SetAvatar(GameObject avatar)
        {
            if (!TrySavePendingChanges())
            {
                _view?.SetAvatar(_avatar);
                return;
            }
            _assignments.ResetSynchronization();
            CancelLibraryOperation();
            _sharedCandidateEntry = null;
            ReleaseClipSnapshots();
            _avatar = avatar;
            _localRegistrations.Clear();
            _localRegistrationFailures.Clear();
            _showOtherLibraryTargets = false;
            _sharedFolder = string.Empty;
            _libraryFolder = _libraryRootFolder;
            _initiallyInstalled = avatar != null && avatar.transform.Find(
                FaceExpressionGenerationPaths.Create(avatar).RootName) != null;
            _clip = null;
            _assignmentCandidateClip = null;
            _assignmentPicker?.SetCandidate(null);
            _view?.SetLibrarySelection(null);
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
            RefreshLibrary();
            FaceExpressionGroupSession.SetAvatar(avatar);
            _view?.SetAvatar(avatar);
            _assignmentView?.SetAvatar(avatar);
            _assignmentPicker?.SetAvatar(avatar);
            _gateway.TryRead(avatar, out var configuration);
            _assignments.SetConfiguration(configuration);
            _savedConfiguration = _assignments.CreateConfiguration();
            _savedInstallationPresent = _initiallyInstalled;
            _saveAttemptNeedsRollback = false;
            _savedAssignmentRevision = _assignments.Revision;
            _assignmentAssetsDirty = false;
            _assignmentApplyAt = -1d;
            _menuIconsRefreshAt = -1d;
            _editedClips.Clear();
            _saveFailed = false;
            RefreshSaveNotification();
            _assignments.Activate();
            FaceExpressionSettings.EnsureNamePreset(
                avatar,
                _settings,
                _presetStore);
            RefreshValidation();
            RefreshClip();
        }

        private void ChooseClip(AnimationClip clip)
        {
            if (_clip != null || !_assignments.HasSelection) { SetClip(clip); }
            else { AssignClip(clip); }
        }

        private void SelectAssignmentCandidate(AnimationClip clip)
        {
            _libraryInteractionAt = EditorApplication.timeSinceStartup;
            if (_avatar == null) { return; }
            CancelLibraryOperation();
            _sharedCandidateEntry = null;
            _assignmentCandidateClip = clip;
            _assignmentPicker?.SetCandidate(clip);
            _view?.SetLibrarySelection(clip);
            if (_clip == null) { RefreshClip(); }
        }

        private void AssignClip(AnimationClip clip)
        {
            if (_avatar == null || !_assignments.HasSelection) { return; }
            _assignments.Activate();
            _assignments.SetSelectedClip(clip);
        }

        private void ClearClipAssignments(AnimationClip clip)
        {
            if (_disposed || _avatar == null || clip == null) return;
            _assignments.Activate();
            _assignments.SetClipConditions(clip, Array.Empty<GestureCombination>(),
                Array.Empty<FaceExpressionMenuEntry>(), string.Empty, true, false);
        }

        private void RefreshLibraryAssignmentSelection()
        {
            _view?.SetAssignedLibraryClips(_assignments.SelectedClips);
            if (_view == null) { return; }
            if (_libraryAssignmentsView == _view && _libraryAssignmentsAvatar == _avatar &&
                _libraryAssignmentsRevision == _assignments.Revision) return;
            _libraryAssignmentsView = _view;
            _libraryAssignmentsAvatar = _avatar;
            _libraryAssignmentsRevision = _assignments.Revision;
            var labels = new Dictionary<AnimationClip, List<string>>();
            void AddLabel(AnimationClip clip, string label)
            {
                if (clip == null) { return; }
                if (!labels.TryGetValue(clip, out var values))
                {
                    values = new List<string>();
                    labels.Add(clip, values);
                }
                if (!values.Contains(label)) { values.Add(label); }
            }
            foreach (FaceGesture left in Enum.GetValues(typeof(FaceGesture)))
            {
                foreach (FaceGesture right in Enum.GetValues(typeof(FaceGesture)))
                {
                    AddLabel(_assignments.GetAssignment(new GestureCombination(left, right)).Clip,
                        I18N.Get("gesture." + left) + "+" + I18N.Get("gesture." + right));
                }
            }
            foreach (var entry in _assignments.MenuEntries)
            {
                AddLabel(entry.Assignment.Clip, I18N.Get("assignments.menuBadge"));
            }
            _view.SetLibraryAssignments(labels);
        }

        private void EditClip(AnimationClip clip)
        {
            if (_avatar == null || clip == null) { return; }
            SelectAssignmentCandidate(clip);
            if (NeedsClipConversion(clip))
            {
                ShowClipConversion(clip);
                return;
            }
            SetClip(clip);
        }

        private bool NeedsClipConversion(AnimationClip clip) => _avatar != null && clip != null &&
            FaceExpressionApi.ValidateClip(_avatar, clip).Any(finding => finding.Code == "binding_not_found");

        private void ShowClipConversion(AnimationClip sourceClip)
        {
            if (_disposed || _avatar == null || sourceClip == null) return;
            StopPlayback();
            var targetAvatar = _avatar;
            if (_clip != sourceClip) SetClip(sourceClip);
            _view.ShowConversion(targetAvatar, sourceClip, converted =>
            {
                if (_disposed || _avatar != targetAvatar || _clip != sourceClip) return;
                _showOtherLibraryTargets = false;
                SelectAssignmentCandidate(converted);
                SetClip(converted);
                ScheduleAssignmentRefresh();
                TrySavePendingChanges();
                RenderLibrary();
            });
        }

        private void RefreshSaveNotification()
        {
            _view?.SetUnsavedChanges(HasUnsavedChanges, _saveFailed);
        }

        private void OnAssignmentConfigurationChanged()
        {
            if (_disposed || _avatar == null) return;
            _assignmentApplyAt = EditorApplication.timeSinceStartup;
            _menuIconsRefreshAt = -1d;
            _saveFailed = false;
            RefreshSaveNotification();
        }

        private bool TryApplyAssignments()
        {
            if (_assignmentApplyAt < 0d) return true;
            _assignmentApplyAt = -1d;
            using (new FaceExpressionGenerationUndo())
            {
                if (_assignments.IsInitialConfiguration && !_initiallyInstalled)
                    VrchatFaceExpressionGateway.RemoveGeneratedInstallation(_avatar);
                else if (!_gateway.TryApply(_avatar, _assignments.CreateConfiguration(), out _, out var error,
                             deferSave: true, generateIcons: false, saveScene: true))
                {
                    _saveFailed = true;
                    _saveAttemptNeedsRollback = true;
                    _view?.SetValidation(new MessagePanelState(I18N.Get("status." + (error ?? "applyFailed")),
                        severity: MessageSeverity.Error));
                    RefreshSaveNotification();
                    return false;
                }
            }
            _savedAssignmentRevision = _assignments.Revision;
            _savedConfiguration = _assignments.CreateConfiguration();
            _savedInstallationPresent = _avatar != null && _avatar.transform.Find(
                FaceExpressionGenerationPaths.Create(_avatar).RootName) != null;
            _assignmentAssetsDirty = false;
            _saveFailed = false;
            _menuIconsRefreshAt = _savedInstallationPresent ? EditorApplication.timeSinceStartup + 0.3d : -1d;
            RefreshSaveNotification();
            return true;
        }

        private void SavePendingChanges()
        {
            TrySavePendingChanges();
        }

        internal bool TryConfirmNavigation(VisualElement host, Action continueNavigation)
        {
            if (!TryApplyAssignments()) return false;
            if (!HasUnsavedChanges || _view == null) { return true; }
            StopPlayback();
            _view.ShowUnsavedChangesOverlay(host, TrySavePendingChanges, () =>
            {
                if (!TryDiscardPendingChanges()) { return false; }
                SetClip(null);
                return true;
            }, continueNavigation);
            return false;
        }

        private bool TrySavePendingChanges()
        {
            if (_disposed) return true;
            try
            {
                if (_assignmentAssetsDirty || _assignments.Revision != _savedAssignmentRevision)
                    _assignmentApplyAt = EditorApplication.timeSinceStartup;
                if (!TryApplyAssignments()) return false;
                RefreshPendingMenuIcons(complete: true);
                DeferredAssetSave.Flush();
                if (!HasUnsavedChanges) return true;
                _saveAttemptNeedsRollback = true;
                foreach (var clip in _editedClips)
                {
                    if (clip == null) { continue; }
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(clip)))
                    {
                        AssetDatabase.SaveAssetIfDirty(asset);
                    }
                }
                foreach (var clip in _editedClips.Where(clip => clip != null).ToArray())
                    if (!TryPublishLibraryClip(clip)) return false;
                _savedAssignmentRevision = _assignments.Revision;
                _savedConfiguration = _assignments.CreateConfiguration();
                _savedInstallationPresent = _avatar != null && _avatar.transform.Find(
                    FaceExpressionGenerationPaths.Create(_avatar).RootName) != null;
                ReleaseClipSnapshots();
                CaptureClipSnapshot(_clip);
                _saveAttemptNeedsRollback = false;
                _assignmentAssetsDirty = false;
                _editedClips.Clear();
                _saveFailed = false;
                RefreshSaveNotification();
                RefreshValidation();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _saveFailed = true;
                RefreshSaveNotification();
                return false;
            }
        }

        private bool TryDiscardPendingChanges()
        {
            if (_disposed || !HasUnsavedChanges) { return true; }
            try
            {
                StopPlayback();
                foreach (var clip in _editedClips)
                {
                    if (clip == null) { continue; }
                    if (!_clipSnapshots.TryGetValue(clip, out var snapshot))
                    {
                        throw new InvalidOperationException("The expression clip has no saved edit snapshot.");
                    }
                    snapshot.Restore(_saveAttemptNeedsRollback);
                }
                if (_saveAttemptNeedsRollback || _editedClips.Count > 0)
                {
                    using (new FaceExpressionGenerationUndo())
                    {
                        if (!_savedInstallationPresent) { VrchatFaceExpressionGateway.RemoveGeneratedInstallation(_avatar); }
                        else if (!_gateway.TryApply(_avatar, _savedConfiguration, out _, out _, saveScene: true)) { return false; }
                    }
                }
                if (_assignments.Revision != _savedAssignmentRevision)
                {
                    _assignments.ResetSynchronization();
                    _assignments.SetConfiguration(_savedConfiguration);
                    _savedAssignmentRevision = _assignments.Revision;
                    _initiallyInstalled = _savedInstallationPresent;
                }
                _assignmentAssetsDirty = false;
                _assignmentApplyAt = -1d;
                _menuIconsRefreshAt = -1d;
                _editedClips.Clear();
                _saveFailed = false;
                ReleaseClipSnapshots();
                CaptureClipSnapshot(_clip);
                _saveAttemptNeedsRollback = false;
                ClearThumbnails();
                RefreshClip();
                RefreshSaveNotification();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private void CaptureClipSnapshot(AnimationClip clip)
        {
            if (clip != null && !_clipSnapshots.ContainsKey(clip))
            {
                _clipSnapshots.Add(clip, new ClipEditSnapshot(clip));
            }
        }

        private void ReleaseClipSnapshots()
        {
            foreach (var snapshot in _clipSnapshots.Values) { snapshot.Dispose(); }
            _clipSnapshots.Clear();
        }

        private sealed class ClipEditSnapshot : IDisposable
        {
            private readonly AnimationClip _clip;
            private readonly AnimationClip _savedClip;
            private readonly bool _clipWasDirty;
            private readonly (FaceExpressionSequenceData Original, FaceExpressionSequenceData Saved, bool WasDirty)[] _sequence;

            internal ClipEditSnapshot(AnimationClip clip)
            {
                _clip = clip;
                _clipWasDirty = EditorUtility.IsDirty(clip);
                _savedClip = UnityEngine.Object.Instantiate(clip);
                _savedClip.name = clip.name;
                _savedClip.hideFlags = HideFlags.HideAndDontSave;
                _sequence = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(clip))
                    .OfType<FaceExpressionSequenceData>().Select(data =>
                    {
                        var saved = UnityEngine.Object.Instantiate(data);
                        saved.name = data.name;
                        saved.hideFlags = HideFlags.HideAndDontSave;
                        return (Original: data, Saved: saved, WasDirty: EditorUtility.IsDirty(data));
                    }).ToArray();
            }

            internal void Restore(bool writeToDisk)
            {
                var path = AssetDatabase.GetAssetPath(_clip);
                foreach (var data in AssetDatabase.LoadAllAssetsAtPath(path).OfType<FaceExpressionSequenceData>())
                {
                    if (_sequence.Any(item => item.Original == data)) { continue; }
                    Undo.ClearUndo(data);
                    AssetDatabase.RemoveObjectFromAsset(data);
                    UnityEngine.Object.DestroyImmediate(data, true);
                }
                for (var index = 0; index < _sequence.Length; index++)
                {
                    var item = _sequence[index];
                    var original = item.Original;
                    if (original == null)
                    {
                        original = ScriptableObject.CreateInstance<FaceExpressionSequenceData>();
                        original.hideFlags = HideFlags.HideInHierarchy;
                        AssetDatabase.AddObjectToAsset(original, _clip);
                        _sequence[index] = (original, item.Saved, item.WasDirty);
                    }
                    RestoreObject(item.Saved, original, writeToDisk || item.WasDirty);
                }
                RestoreObject(_savedClip, _clip, writeToDisk || _clipWasDirty);
                if (writeToDisk)
                {
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) { AssetDatabase.SaveAssetIfDirty(asset); }
                }
            }

            private static void RestoreObject(UnityEngine.Object saved, UnityEngine.Object original, bool keepDirty)
            {
                var flags = original.hideFlags;
                Undo.ClearUndo(original);
                EditorUtility.CopySerialized(saved, original);
                original.hideFlags = flags;
                if (keepDirty) { EditorUtility.SetDirty(original); }
                else { EditorUtility.ClearDirty(original); }
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_savedClip);
                foreach (var item in _sequence) { UnityEngine.Object.DestroyImmediate(item.Saved); }
            }
        }

        private void MarkAssignmentAssetsDirty()
        {
            if (_disposed || _avatar == null) { return; }
            _assignmentAssetsDirty = true;
            _assignmentApplyAt = EditorApplication.timeSinceStartup + 0.15d;
            _menuIconsRefreshAt = -1d;
            _saveFailed = false;
            RefreshSaveNotification();
        }

        private void SetClip(AnimationClip clip)
        {
            _changingClip = true;
            try
            {
                StopPlayback();
                _poseThumbnails.Clear();
                _clip = clip;
                CaptureClipSnapshot(clip);
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
                _clip != null ? _clip : _assignmentCandidateClip,
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
            ScheduleAssignmentRefresh();
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
            ScheduleAssignmentRefresh();
        }

        private void UpdatePlayback()
        {
            var now = EditorApplication.timeSinceStartup;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating)
            {
                if (_assignmentApplyAt >= 0d && now >= _assignmentApplyAt) TryApplyAssignments();
                if (_menuIconsRefreshAt >= 0d && now >= _menuIconsRefreshAt)
                {
                    try { RefreshPendingMenuIcons(); }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        _menuIconsRefreshAt = double.PositiveInfinity;
                        _saveFailed = true;
                        _saveAttemptNeedsRollback = true;
                        RefreshSaveNotification();
                    }
                }
            }
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

        private void RefreshPendingMenuIcons(bool complete = false)
        {
            if (_menuIconsRefreshAt < 0d || _avatar == null) return;
            _menuIconsRefreshAt = -1d;
            try
            {
                using (new FaceExpressionGenerationUndo())
                    if (_gateway.RefreshMenuIcons(_avatar, _assignments.CreateConfiguration(), complete, _preview))
                        _menuIconsRefreshAt = EditorApplication.timeSinceStartup;
            }
            catch
            {
                _menuIconsRefreshAt = double.PositiveInfinity;
                throw;
            }
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
            ScheduleAssignmentRefresh();
        }

        private void ScheduleAssignmentRefresh()
        {
            if (_clip == null) { return; }
            _editedClips.Add(_clip);
            if (_initiallyInstalled || !_assignments.IsInitialConfiguration)
            {
                MarkAssignmentAssetsDirty();
            }
            RefreshSaveNotification();
        }

        private void InvalidatePoseThumbnails(bool refreshAssignments = true)
        {
            _poseThumbnailRefreshAt = -1d;
            _poseThumbnails.Invalidate(_clip);
            RequestRepaint();
            if (refreshAssignments) { ScheduleAssignmentRefresh(); }
        }

        private void OnUndoRedo()
        {
            if (_assignments.RestoreUndo())
            {
                OnAssignmentConfigurationChanged();
            }
            else if (_clip != null)
            {
                ScheduleAssignmentRefresh();
            }
            RefreshClip();
            InvalidatePoseThumbnails(false);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && !TrySavePendingChanges())
            {
                EditorApplication.isPlaying = false;
            }
            StopPlayback();
            if (state != PlayModeStateChange.EnteredEditMode &&
                state != PlayModeStateChange.EnteredPlayMode) { return; }
            ClearThumbnails();
            _preview?.SetAvatar(_avatar);
            _previewRendererPaths = FaceExpressionClipEditor.GetRendererPaths(_avatar);
            _preview?.SetChannels(_channels);
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
                _view.SetConversionAvailable(false);
                _view.SetValidation(null);
                return;
            }

            var findings = FaceExpressionApi.ValidateClip(_avatar, _clip);
            _view.SetConversionAvailable(findings.Any(finding => finding.Code == "binding_not_found"));
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
            SelectAssignmentCandidate(clip);
            SetClip(clip);
            ScheduleAssignmentRefresh();
            TrySavePendingChanges();
        }

        private void CopyClip(AnimationClip source)
        {
            if (source == null)
            {
                return;
            }

            if (NeedsClipConversion(source))
            {
                SelectAssignmentCandidate(source);
                ShowClipConversion(source);
                return;
            }

            var fileName = SanitizeFileName(source.name);
            if (!AssetDatabase.IsValidFolder(_libraryFolder))
                _libraryFolder = ProjectAssetSettings.EnsureAssetFolder("Animation/Facial", _settings);
            var path = AssetDatabase.GenerateUniqueAssetPath(
                _libraryFolder + "/" + fileName + ".anim");
            var clip = FaceExpressionClipEditor.Copy(source, path);
            if (clip == null)
            {
                return;
            }

            Selection.activeObject = clip;
            SelectAssignmentCandidate(clip);
            SetClip(clip);
            ScheduleAssignmentRefresh();
            TrySavePendingChanges();
        }

        private void RefreshLibrary()
        {
            var libraryReady = SynchronizeSharedLibrary();
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

            if (_observedLibraryFolder == _libraryFolder && !string.IsNullOrEmpty(_observedLibraryFolderGuid))
            {
                var currentFolder = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(_observedLibraryFolderGuid));
                if (AssetDatabase.IsValidFolder(currentFolder) && IsAtOrBelow(currentFolder, rootFolder))
                {
                    _libraryFolder = currentFolder;
                    _sharedFolder = currentFolder == rootFolder ? string.Empty : currentFolder.Substring(rootFolder.Length + 1);
                }
            }
            _observedLibraryFolder = _libraryFolder;
            _observedLibraryFolderGuid = AssetDatabase.IsValidFolder(_libraryFolder) ? AssetDatabase.AssetPathToGUID(_libraryFolder) : null;
            _currentLibraryClips.Clear();
            if (!AssetDatabase.IsValidFolder(rootFolder))
            {
                _libraryClips = Array.Empty<AnimationClip>();
                QueueLocalRegistrations(libraryReady);
                RenderLibrary();
                return;
            }

            _libraryClips = AssetDatabase
                .FindAssets("t:AnimationClip", new[] { rootFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
                .Where(clip => clip != null)
                .OrderBy(clip => clip.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    clip => AssetDatabase.GetAssetPath(clip),
                    StringComparer.Ordinal)
                .ToArray();
            if (_avatar != null)
            {
                var sharedGuids = _sharedEntries.SelectMany(entry => entry.Assets.Select(asset => asset.Guid)).ToHashSet();
                foreach (var clip in _libraryClips)
                    if (!sharedGuids.Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip))) &&
                        !FaceExpressionApi.ValidateClip(_avatar, clip).Any(finding => finding.Code == "binding_not_found" ||
                            finding.Code == "non_blendshape_curve" || finding.Code == "object_reference_curves" ||
                            finding.Code == "animation_events" || finding.Severity == "error"))
                        _currentLibraryClips.Add(clip);
            }
            QueueLocalRegistrations(libraryReady);
            RenderLibrary();
        }

        private void RenderLibrary()
        {
            RenderSharedLibrary();
        }

        private void OpenLibraryFolder(string folder)
        {
            _libraryInteractionAt = EditorApplication.timeSinceStartup;
            if (folder == FaceExpressionView.CurrentAvatarFolderPath)
            {
                _showOtherLibraryTargets = false;
                _sharedFolder = string.Empty;
                _libraryFolder = _libraryRootFolder;
                RefreshLibrary();
                return;
            }
            if (folder == FaceExpressionView.OtherAvatarsFolderPath)
            {
                _showOtherLibraryTargets = true;
                _sharedFolder = string.Empty;
                _libraryFolder = _libraryRootFolder;
                RefreshLibrary();
                return;
            }
            if (folder.StartsWith("shared:/", StringComparison.Ordinal))
            {
                _sharedFolder = folder.Substring("shared:/".Length);
                _libraryFolder = _sharedFolder.Length == 0 ? _libraryRootFolder : _libraryRootFolder + "/" + _sharedFolder;
                RefreshLibrary();
                return;
            }
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
            _libraryInteractionAt = EditorApplication.timeSinceStartup;
            if (HasClipReference)
            {
                SetClip(null);
                RefreshLibrary();
                return;
            }

            if (_sharedFolder.Length == 0) _showOtherLibraryTargets = false;
            var index = _sharedFolder.LastIndexOf('/');
            _sharedFolder = index < 0 ? string.Empty : _sharedFolder.Substring(0, index);
            _libraryFolder = _sharedFolder.Length == 0 ? _libraryRootFolder : _libraryRootFolder + "/" + _sharedFolder;
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
            if (ReferenceEquals(args.Definition, FaceExpressionSettings.MenuIconsDisabled))
            {
                if (_initiallyInstalled || !_assignments.IsInitialConfiguration) { MarkAssignmentAssetsDirty(); }
                return;
            }
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
                UnsavedChanges = I18N.Get("assignments.unsavedChanges"),
                Save = I18N.Get("assignments.save"),
                SaveFailed = I18N.Get("assignments.saveFailed"),
                NewClip = I18N.Get("action.newClip"),
                CopyAndEdit = I18N.Get("action.copyAndEdit"),
                Edit = I18N.Get("action.edit"),
                ResetView = I18N.Get("action.resetView"),
                PreviewBackground = I18N.Get("action.previewBackground"),
                BackToLibrary = I18N.Get("action.backToLibrary"),
                BackToAssignments = I18N.Get("action.backToAssignments"),
                SearchPlaceholder = I18N.Get("search.placeholder"),
                SearchTooltip = I18N.Get("search.tooltip"),
                ClearSearchTooltip = I18N.Get("search.clearTooltip"),
                LibrarySearchPlaceholder = I18N.Get("search.libraryPlaceholder"),
                LibrarySearchTooltip = I18N.Get("search.libraryTooltip"),
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
