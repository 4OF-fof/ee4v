using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.AvatarEditing;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class FaceExpressionViewText
    {
        public string Avatar { get; set; }
        public string Clip { get; set; }
        public string UnsavedChanges { get; set; }
        public string Save { get; set; }
        public string SaveFailed { get; set; }
        public string NewClip { get; set; }
        public string CopyAndEdit { get; set; }
        public string Edit { get; set; }
        public string ResetView { get; set; }
        public string PreviewBackground { get; set; }
        public string BackToLibrary { get; set; }
        public string SearchPlaceholder { get; set; }
        public string SearchTooltip { get; set; }
        public string ClearSearchTooltip { get; set; }
        public string LibrarySearchPlaceholder { get; set; }
        public string LibrarySearchTooltip { get; set; }
        public string BlendShapes { get; set; }
        public string Library { get; set; }
        public string ClipOnly { get; set; }
        public string ClipOnlyTooltip { get; set; }
        public string NoBlendShapes { get; set; }
        public string Play { get; set; }
        public string Pause { get; set; }
        public string Loop { get; set; }
        public string Sequence { get; set; }
        public string Pose { get; set; }
        public string AddPose { get; set; }
        public string AddPoseTooltip { get; set; }
        public string AddPoseUnavailable { get; set; }
        public string InsertPose { get; set; }
        public string InsertPoseTooltip { get; set; }
        public string MovePoseEarlier { get; set; }
        public string MovePoseLater { get; set; }
        public string RenamePose { get; set; }
        public string ResetPoseName { get; set; }
        public string AddClip { get; set; }
        public string AddClipTooltip { get; set; }
        public string RemovePose { get; set; }
        public string Transition { get; set; }
        public string TimelineTooltip { get; set; }
    }

    internal sealed class FaceExpressionView : VisualElement
    {
        private const float LibraryItemLabelHeight = 30f;
        private const float LibraryFolderIconSize = 80f;

        private readonly ObjectField _avatarField;
        private readonly ActionBar _toolbar;
        private readonly bool _showAvatarField;
        private readonly ScenePreviewViewport _previewViewport;
        private readonly VisualElement _assignmentPane;
        private readonly VisualElement _rightPane;
        private readonly VisualElement _assignmentSettings;
        private readonly UiButton _editAssignment;
        private readonly ObjectField _clipField;
        private readonly MessagePanel _validation;
        private readonly MessagePanel _saveNotification;
        private readonly string _unsavedChanges;
        private readonly string _saveFailed;
        private bool _hasUnsavedChanges;
        private bool _saveHasFailed;
        private VisualElement _navigationOverlay;
        private Action _closeNavigationOverlay;
        private readonly UiButton _convert;
        private readonly VisualElement _editorContent;
        private readonly VisualElement _conversionPane;
        private readonly SearchField _search;
        private readonly SearchField _librarySearch;
        private IReadOnlyList<string> _libraryFolders;
        private IReadOnlyList<AnimationClip> _libraryClips;
        private Action<AnimationClip, Rect> _drawLibraryPreview;
        private readonly SectionHeader _sectionHeader;
        private readonly UiButton _backToLibrary;
        private readonly Toggle _clipOnly;
        private readonly FormInput _clipOnlyControl;
        private readonly VisualElement _animationControls;
        private readonly UiButton _playback;
        private readonly Slider _timeline;
        private readonly Toggle _loop;
        private readonly ScrollView _poseSequence;
        private readonly UiButton _addPose;
        private readonly Action<AnimationClip, float, Rect> _drawPosePreview;
        private readonly ListView _blendShapeList;
        private readonly ScrollView _library;
        private readonly EmptyState _empty;
        private readonly string _defaultSectionTitle;
        private readonly string _libraryTitle;
        private readonly string _newClip;
        private readonly string _copyAndEdit;
        private readonly string _edit;
        private readonly string _noBlendShapes;
        private readonly string _poseText;
        private readonly string _removePoseTooltip;
        private readonly string _transitionText;
        private readonly string _addPoseTooltip;
        private readonly string _addPoseUnavailableTooltip;
        private readonly string _insertPoseText;
        private readonly string _insertPoseTooltip;
        private readonly string _movePoseEarlierTooltip;
        private readonly string _movePoseLaterTooltip;
        private readonly string _renamePoseText;
        private readonly string _resetPoseNameText;
        private readonly string _poseSourceTooltip;
        private string _sectionTitle;
        private List<BlendShapeChannel> _channels = new List<BlendShapeChannel>();
        private readonly List<BlendShapeRowItem> _visibleItems = new List<BlendShapeRowItem>();
        private List<BlendShapeRowItem> _groupItems = new List<BlendShapeRowItem>();
        private List<BlendShapeChannel> _filteredChannels = new List<BlendShapeChannel>();
        private readonly Dictionary<BlendShapeChannel, string> _favoriteKeys = new Dictionary<BlendShapeChannel, string>();
        private IVisualElementScheduledItem _favoriteRefresh;
        private readonly HashSet<string> _collapsedBlendShapeGroups = new HashSet<string>(StringComparer.Ordinal);
        private BlendShapeNamingRule _namingRule;
        private bool _hideHeaders;
        private bool _rendering;
        private bool _hasClip;
        private bool _canNavigateLibraryBack;
        private float[] _poseTimes = Array.Empty<float>();
        private AnimationClip[] _poseSources = Array.Empty<AnimationClip>();
        private string[] _poseNames = Array.Empty<string>();
        private int _selectedPoseIndex;
        private bool _canAddPose;
        private bool _selectedPoseReadOnly;

        public FaceExpressionView(
            FaceExpressionViewText text,
            Action<Rect> drawPreview,
            Action<AnimationClip, float, Rect> drawPosePreview = null,
            bool showAvatarField = true)
        {
            text = text ?? new FaceExpressionViewText();
            _defaultSectionTitle = text.BlendShapes ?? string.Empty;
            _libraryTitle = text.Library ?? string.Empty;
            _newClip = text.NewClip ?? string.Empty;
            _copyAndEdit = text.CopyAndEdit ?? string.Empty;
            _edit = text.Edit ?? string.Empty;
            _noBlendShapes = text.NoBlendShapes ?? string.Empty;
            _poseText = text.Pose ?? string.Empty;
            _removePoseTooltip = text.RemovePose ?? string.Empty;
            _transitionText = text.Transition ?? string.Empty;
            _addPoseTooltip = text.AddPoseTooltip ?? string.Empty;
            _addPoseUnavailableTooltip = text.AddPoseUnavailable ?? string.Empty;
            _insertPoseText = text.InsertPose ?? "+";
            _insertPoseTooltip = text.InsertPoseTooltip ?? string.Empty;
            _movePoseEarlierTooltip = text.MovePoseEarlier ?? string.Empty;
            _movePoseLaterTooltip = text.MovePoseLater ?? string.Empty;
            _renamePoseText = text.RenamePose ?? string.Empty;
            _resetPoseNameText = text.ResetPoseName ?? string.Empty;
            _poseSourceTooltip = text.AddClipTooltip ?? string.Empty;
            _drawPosePreview = drawPosePreview;
            _unsavedChanges = text.UnsavedChanges;
            _saveFailed = text.SaveFailed;
            _sectionTitle = _defaultSectionTitle;
            AddToClassList("ee4v-face-expression");

            _showAvatarField = showAvatarField;
            _toolbar = new ActionBar();
            var toolbar = _toolbar;
            toolbar.AddToClassList("ee4v-face-expression__toolbar");
            _avatarField = UiTextFactory.CreateObjectField(
                text.Avatar,
                "ee4v-face-expression__asset-field");
            _avatarField.objectType = typeof(GameObject);
            _avatarField.allowSceneObjects = true;
            _avatarField.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    AvatarChanged?.Invoke(evt.newValue as GameObject);
                }
            });
            if (showAvatarField)
            {
                toolbar.Leading.Add(_avatarField);
            }

            Add(toolbar);

            _saveNotification = new MessagePanel();
            _saveNotification.AddToClassList("ee4v-face-expression__save-notification");
            var saveChangesButton = new UiButton(text.Save, () => SaveRequested?.Invoke());
            saveChangesButton.SetPrimaryActionEnabled(true);
            _saveNotification.Actions.Add(saveChangesButton);
            _saveNotification.Add(_saveNotification.Actions);
            Add(_saveNotification);

            var content = new VisualElement();
            content.AddToClassList("ee4v-face-expression__content");
            _previewViewport = new ScenePreviewViewport(
                drawPreview,
                () => ResetViewRequested?.Invoke(),
                text.PreviewBackground,
                text.ResetView);
            _previewViewport.AddToClassList("ee4v-face-expression__preview-pane");
            _previewViewport.RegisterCallback<DetachFromPanelEvent>(_ =>
                _previewViewport.Dispose());
            content.Add(_previewViewport);

            _assignmentPane = new VisualElement();
            _assignmentPane.AddToClassList("ee4v-face-expression__assignment-pane");
            _assignmentPane.style.display = DisplayStyle.None;
            content.Add(_assignmentPane);

            var rightPane = new VisualElement();
            _rightPane = rightPane;
            rightPane.AddToClassList("ee4v-face-expression__editor-pane");
            _editorContent = new VisualElement();
            _editorContent.AddToClassList("ee4v-face-expression__editor-content");
            var editorPane = _editorContent;
            rightPane.Add(editorPane);
            _conversionPane = new VisualElement();
            _conversionPane.AddToClassList("ee4v-face-expression__conversion-pane");
            _conversionPane.style.display = DisplayStyle.None;
            rightPane.Add(_conversionPane);
            _sectionHeader = new SectionHeader(_defaultSectionTitle);
            _sectionHeader.AddToClassList(
                "ee4v-face-expression__section-header");
            _backToLibrary = new UiButton(
                string.Empty,
                () => BackRequested?.Invoke(),
                text.BackToLibrary,
                FluentUiIcons.CreateState(
                    "arrow_left.png",
                    UiSizeTokens.Size20));
            _backToLibrary.AddToClassList(
                "ee4v-face-expression__back-to-library");
            _backToLibrary.style.display = DisplayStyle.None;
            _clipOnly = UiTextFactory.CreateToggle();
            _clipOnly.tooltip = text.ClipOnlyTooltip;
            _clipOnly.RegisterValueChangedCallback(_ => RefreshFilter());
            _clipOnlyControl = new FormInput(text.ClipOnly, _clipOnly);
            _clipOnlyControl.AddToClassList("ee4v-face-expression__clip-only");
            _clipOnlyControl.tooltip = text.ClipOnlyTooltip;
            _clipOnlyControl.LabelText.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button == 0 && _clipOnly.enabledInHierarchy)
                {
                    _clipOnly.Focus();
                    _clipOnly.value = !_clipOnly.value;
                    evt.StopPropagation();
                }
            });
            _sectionHeader.Actions.Add(_clipOnlyControl);
            editorPane.Add(_sectionHeader);

            _clipField = UiTextFactory.CreateObjectField(
                string.Empty,
                "ee4v-face-expression__asset-field");
            _clipField.objectType = typeof(AnimationClip);
            _clipField.allowSceneObjects = false;
            _clipField.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    ClipChanged?.Invoke(evt.newValue as AnimationClip);
                }
            });
            _editAssignment = new UiButton(_edit, () =>
                EditClipRequested?.Invoke(_clipField.value as AnimationClip));
            _editAssignment.AddToClassList("ee4v-face-expression__edit-assignment");
            _editAssignment.style.display = DisplayStyle.None;
            var clipControl = new FormInput(text.Clip, _clipField, _editAssignment);
            clipControl.AddToClassList("ee4v-face-expression__clip-field");
            editorPane.Add(clipControl);

            _validation = new MessagePanel();
            _validation.AddToClassList("ee4v-face-expression__validation");
            editorPane.Add(_validation);
            _convert = new UiButton(I18N.Get("conversion.title"), () => ConversionRequested?.Invoke());
            _convert.AddToClassList("ee4v-face-expression__convert");
            _convert.style.display = DisplayStyle.None;
            _validation.DetailsActions.style.display = DisplayStyle.None;
            _validation.DetailsActions.Add(_convert);

            _animationControls = new VisualElement();
            _animationControls.AddToClassList(
                "ee4v-face-expression__animation-controls");
            var sequenceHeader = new VisualElement();
            sequenceHeader.AddToClassList(
                "ee4v-face-expression__sequence-header");
            sequenceHeader.Add(UiTextFactory.Create(
                text.Sequence,
                UiClassNames.SectionTitle,
                "ee4v-face-expression__sequence-title"));
            _addPose = new UiButton(
                text.AddPose,
                () => PoseAddRequested?.Invoke(),
                text.AddPoseTooltip,
                variant: UiButtonVariant.Ghost);
            _addPose.AddToClassList("ee4v-face-expression__add-pose");
            sequenceHeader.Add(_addPose);
            _animationControls.Add(sequenceHeader);
            _poseSequence = new ScrollView(ScrollViewMode.Horizontal);
            _poseSequence.AddToClassList(
                "ee4v-face-expression__pose-sequence");
            _poseSequence.contentContainer.AddToClassList(
                "ee4v-face-expression__pose-sequence-content");
            _animationControls.Add(_poseSequence);
            var playbackRow = new VisualElement();
            playbackRow.AddToClassList(
                "ee4v-face-expression__playback-row");
            _playback = new UiButton(
                "▶",
                () => PlaybackChanged?.Invoke(),
                text.Play,
                variant: UiButtonVariant.Ghost);
            _playback.AddToClassList(
                "ee4v-face-expression__playback");
            playbackRow.Add(_playback);
            var timelineStack = new VisualElement();
            timelineStack.AddToClassList(
                "ee4v-face-expression__timeline-stack");
            _timeline = new Slider(0f, 1f);
            _timeline.AddToClassList(
                "ee4v-face-expression__timeline");
            _timeline.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    TimeChanged?.Invoke(evt.newValue);
                }
            });
            _timeline.tooltip = text.TimelineTooltip ?? string.Empty;
            timelineStack.Add(_timeline);
            playbackRow.Add(timelineStack);
            _loop = UiTextFactory.CreateToggle(
                text.Loop,
                "ee4v-face-expression__loop");
            _loop.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    LoopChanged?.Invoke(evt.newValue);
                }
            });
            playbackRow.Add(_loop);
            _animationControls.Add(playbackRow);
            editorPane.Add(_animationControls);

            _search = new SearchField(new SearchFieldState(
                placeholder: text.SearchPlaceholder,
                searchTooltip: text.SearchTooltip,
                clearTooltip: text.ClearSearchTooltip));
            _search.AddToClassList("ee4v-face-expression__blend-shape-search");
            _search.ValueChanged += _ => RefreshFilter();
            editorPane.Add(_search);
            _blendShapeList = new ListView
            {
                fixedItemHeight = 36f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                makeItem = CreateBlendShapeRow,
                bindItem = BindBlendShapeRow
            };
            _blendShapeList.AddToClassList("ee4v-face-expression__blend-shapes");
            var listScrollView = _blendShapeList.Q<ScrollView>();
            if (listScrollView != null)
            {
                listScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
                listScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            }
            editorPane.Add(_blendShapeList);
            _librarySearch = new SearchField(new SearchFieldState(
                placeholder: text.LibrarySearchPlaceholder,
                searchTooltip: text.LibrarySearchTooltip,
                clearTooltip: text.ClearSearchTooltip));
            _librarySearch.AddToClassList("ee4v-face-expression__library-search");
            _librarySearch.ValueChanged += _ => RenderLibraryItems();
            editorPane.Add(_librarySearch);
            _library = new ScrollView(ScrollViewMode.Vertical);
            _library.AddToClassList("ee4v-face-expression__library");
            _library.contentContainer.AddToClassList(
                "ee4v-face-expression__library-content");
            editorPane.Add(_library);
            _assignmentSettings = new VisualElement();
            _assignmentSettings.AddToClassList("ee4v-face-expression__assignment-settings");
            _assignmentSettings.style.display = DisplayStyle.None;
            editorPane.Add(_assignmentSettings);
            RegisterCallback<ClickEvent>(evt =>
            {
                if (_hasClip || evt.button != 0) { return; }
                for (var element = evt.target as VisualElement; element != null && element != this; element = element.parent)
                {
                    if (element is GestureAssignmentCell || element is Button || element is ObjectField ||
                        element is SearchField || element is InputField || element is FormInput ||
                        element is Toggle || element is Scroller)
                    {
                        return;
                    }
                }
                AssignmentSelectionCleared?.Invoke();
            });
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                BlendShapeFavorites.Changed += OnFavoritesChanged;
                OnFavoritesChanged();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                BlendShapeFavorites.Changed -= OnFavoritesChanged;
                _favoriteRefresh?.Pause();
            });
            _empty = new EmptyState();
            _empty.AddToClassList("ee4v-face-expression__empty");
            editorPane.Add(_empty);
            content.Add(rightPane);
            _previewViewport.FeatureOverlay.Add(_backToLibrary);
            Add(content);
        }

        public event Action<GameObject> AvatarChanged;
        public event Action SaveRequested;
        public event Action AssignmentSelectionCleared;

        public void SetUnsavedChanges(bool hasChanges, bool saveFailed = false)
        {
            _hasUnsavedChanges = hasChanges;
            _saveHasFailed = saveFailed;
            RefreshSaveNotification();
        }

        private void RefreshSaveNotification()
        {
            _saveNotification.SetState(_hasUnsavedChanges && !_hasClip
                ? new MessagePanelState(_saveHasFailed ? _saveFailed : _unsavedChanges,
                    severity: _saveHasFailed ? MessageSeverity.Error : MessageSeverity.Warning)
                : null);
        }

        public void ShowUnsavedChangesOverlay(VisualElement host, Func<bool> save, Func<bool> discard, Action continueNavigation)
        {
            if (_navigationOverlay != null || host == null) { return; }
            var previousFocus = host.panel?.focusController?.focusedElement as VisualElement;
            var background = host.Children().Select(element => (Element: element, Enabled: element.enabledSelf)).ToArray();
            var overlay = new VisualElement { focusable = true, tabIndex = -1 };
            _navigationOverlay = overlay;
            UiComposition.Prepare(overlay, "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");
            overlay.AddToClassList("ee4v-face-expression__unsaved-overlay");
            var notification = new MessagePanel(new MessagePanelState(_unsavedChanges,
                severity: MessageSeverity.Warning));
            notification.AddToClassList("ee4v-face-expression__unsaved-card");
            var closed = false;
            void Close()
            {
                if (closed) { return; }
                closed = true;
                foreach (var item in background) { item.Element.SetEnabled(item.Enabled); }
                overlay.RemoveFromHierarchy();
                _navigationOverlay = null;
                _closeNavigationOverlay = null;
                if (previousFocus?.panel != null && previousFocus.enabledInHierarchy) { previousFocus.Focus(); }
            }
            _closeNavigationOverlay = Close;
            var discardButton = new UiButton(I18N.Get("assignments.discard"), () =>
            {
                if (!discard())
                {
                    notification.SetState(new MessagePanelState(I18N.Get("assignments.discardFailed"),
                        severity: MessageSeverity.Error));
                    return;
                }
                Close();
                continueNavigation();
            });
            discardButton.AddToClassList("ee4v-face-expression__discard-changes");
            discardButton.SetLabelColor(UiColorTokens.TextOnState);
            notification.Actions.Add(discardButton);
            notification.Actions.Add(new UiButton(I18N.Get("assignments.continueEditing"), Close,
                variant: UiButtonVariant.Ghost));
            var saveButton = new UiButton(I18N.Get("assignments.save"), () =>
            {
                if (!save())
                {
                    notification.SetState(new MessagePanelState(_saveFailed, severity: MessageSeverity.Error));
                    return;
                }
                Close();
                continueNavigation();
            });
            saveButton.SetPrimaryActionEnabled(true);
            notification.Actions.Add(saveButton);
            notification.Add(notification.Actions);
            overlay.Add(notification);
            overlay.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            overlay.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
            overlay.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) { Close(); evt.PreventDefault(); }
                evt.StopPropagation();
            });
            overlay.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == overlay) { Close(); } });
            foreach (var item in background) { item.Element.SetEnabled(false); }
            host.Add(overlay);
            overlay.schedule.Execute(overlay.Focus);
        }

        public void CloseUnsavedChangesOverlay()
        {
            _closeNavigationOverlay?.Invoke();
        }

        public event Action<AnimationClip> ClipChanged;
        public event Action<AnimationClip> LibraryClipSelected;
        public event Action NewClipRequested;
        public event Action<AnimationClip> CopyClipRequested;
        public event Action<AnimationClip> EditClipRequested;
        public event Action BackRequested;
        public event Action<string> LibraryFolderRequested;
        public event Action<BlendShapeChannel> ChannelChanged;
        public event Action ConversionRequested;

        public void SetConversionAvailable(bool available)
        {
            _convert.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            _validation.DetailsActions.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowConversion(GameObject avatar, AnimationClip source, Action<AnimationClip> saved)
        {
            if (avatar == null || source == null) { return; }
            _conversionPane.Clear();
            _conversionPane.Add(new ExpressionConversionView(avatar, source, HideConversion, saved));
            _editorContent.style.display = DisplayStyle.None;
            _conversionPane.style.display = DisplayStyle.Flex;
        }

        private void HideConversion()
        {
            _conversionPane.Clear();
            _conversionPane.style.display = DisplayStyle.None;
            _editorContent.style.display = DisplayStyle.Flex;
        }
        public event Action<int> PoseSelected;
        public event Action PoseAddRequested;
        public event Action<int> PoseInsertRequested;
        public event Action<int, int> PoseMoveRequested;
        public event Action<int, string> PoseNameChanged;
        public event Action<int, AnimationClip> PoseSourceChanged;
        public event Action<int> PoseRemoveRequested;
        public event Action<int, float> TransitionDurationChanged;
        public event Action ResetViewRequested;
        public event Action PlaybackChanged;
        public event Action<float> TimeChanged;
        public event Action<bool> LoopChanged;

        public void SetAvatar(GameObject avatar)
        {
            HideConversion();
            if (_avatarField.value != avatar)
            {
                _collapsedBlendShapeGroups.Clear();
                _favoriteKeys.Clear();
            }
            _rendering = true;
            _avatarField.SetValueWithoutNotify(avatar);
            _previewViewport.SetPreviewAvailable(avatar != null);
            _rendering = false;
        }

        public void SetAvatarEditable(bool editable)
        {
            _avatarField.SetEnabled(editable);
        }

        public void SetAssignmentContent(VisualElement assignments, VisualElement settings)
        {
            _assignmentPane.Clear();
            _assignmentPane.Add(assignments);
            _assignmentSettings.Clear();
            _assignmentSettings.Add(settings);
            RefreshFilter();
        }

        public void SetSelectedAssignment(AnimationClip clip, bool editable)
        {
            if (_hasClip) { return; }
            _rendering = true;
            _clipField.SetValueWithoutNotify(clip);
            _rendering = false;
            _clipField.SetEnabled(editable);
            _editAssignment.SetEnabled(editable && clip != null);
        }

        public void SetClip(AnimationClip clip)
        {
            HideConversion();
            _hasClip = clip != null;
            RefreshSaveNotification();
            _clipField.SetEnabled(true);
            _rendering = true;
            _clipField.SetValueWithoutNotify(clip);
            _rendering = false;
            RefreshFilter();
        }

        public void SetAnimationState(
            float time,
            float duration,
            bool looping,
            bool playing,
            IReadOnlyList<float> poseTimes,
            IReadOnlyList<AnimationClip> poseSources,
            IReadOnlyList<string> poseNames,
            int selectedPoseIndex,
            bool canAddPose,
            string playTooltip,
            string pauseTooltip)
        {
            duration = Mathf.Max(1f / 60f, duration);
            time = Mathf.Clamp(time, 0f, duration);
            _rendering = true;
            _timeline.highValue = duration;
            _timeline.SetValueWithoutNotify(time);
            _loop.SetValueWithoutNotify(looping);
            _playback.SetLabel(playing ? "Ⅱ" : "▶");
            _playback.tooltip = playing
                ? pauseTooltip ?? string.Empty
                : playTooltip ?? string.Empty;
            _playback.SetEnabled((poseTimes?.Count ?? 0) > 1);
            _rendering = false;

            var nextPoseTimes = poseTimes?.ToArray() ?? Array.Empty<float>();
            var nextPoseSources = poseSources?.ToArray() ??
                                  new AnimationClip[nextPoseTimes.Length];
            var nextPoseNames = poseNames?.ToArray() ??
                                new string[nextPoseTimes.Length];
            var nextSelectedPoseReadOnly =
                selectedPoseIndex >= 0 &&
                selectedPoseIndex < nextPoseSources.Length &&
                nextPoseSources[selectedPoseIndex] != null;
            var sequenceChanged = !_poseTimes.SequenceEqual(nextPoseTimes) ||
                                  !_poseSources.SequenceEqual(nextPoseSources) ||
                                  !_poseNames.SequenceEqual(nextPoseNames) ||
                                  _selectedPoseIndex != selectedPoseIndex ||
                                  _canAddPose != canAddPose;
            _poseTimes = nextPoseTimes;
            _poseSources = nextPoseSources;
            _poseNames = nextPoseNames;
            _selectedPoseIndex = selectedPoseIndex;
            _canAddPose = canAddPose;
            var editingChanged = _selectedPoseReadOnly != nextSelectedPoseReadOnly;
            _selectedPoseReadOnly = nextSelectedPoseReadOnly;
            if (editingChanged) { _blendShapeList.RefreshItems(); }
            if (sequenceChanged)
            {
                RebuildPoseSequence();
            }
        }

        private void RebuildPoseSequence()
        {
            _poseSequence.Clear();
            for (var index = 0; index < _poseTimes.Length; index++)
            {
                var poseIndex = index;
                var poseTime = _poseTimes[index];
                var poseSource = index < _poseSources.Length
                    ? _poseSources[index]
                    : null;
                var poseName = index < _poseNames.Length
                    ? _poseNames[index] ?? string.Empty
                    : string.Empty;
                var poseDisplayName = string.IsNullOrWhiteSpace(poseName)
                    ? FormatPoseLabel(index)
                    : poseName;
                var card = new VisualElement();
                card.AddToClassList("ee4v-face-expression__pose-card");
                card.EnableInClassList(
                    "ee4v-face-expression__pose-card--selected",
                    index == _selectedPoseIndex);
                card.EnableInClassList(
                    "ee4v-face-expression__pose-card--referenced",
                    poseSource != null);

                var select = new UiButton(
                    string.Empty,
                    () => PoseSelected?.Invoke(poseIndex),
                    variant: UiButtonVariant.Ghost);
                select.AddToClassList("ee4v-face-expression__pose-select");
                select.tooltip = poseSource == null
                    ? poseDisplayName
                    : poseDisplayName + "\n" + poseSource.name;
                var previewArea = new PreviewContainer();
                previewArea.AddToClassList(
                    "ee4v-face-expression__pose-preview-area");
                IMGUIContainer posePreview = null;
                posePreview = new IMGUIContainer(() =>
                {
                    if (Event.current?.type != EventType.Repaint)
                    {
                        return;
                    }

                    if (_drawPosePreview == null)
                    {
                        EditorGUI.DrawRect(
                            posePreview.contentRect,
                            new Color(0.1f, 0.1f, 0.1f, 1f));
                        return;
                    }

                    _drawPosePreview(
                        poseSource,
                        poseTime,
                        posePreview.contentRect);
                });
                posePreview.AddToClassList(
                    "ee4v-face-expression__pose-preview");
                posePreview.pickingMode = PickingMode.Ignore;
                previewArea.Content.Add(posePreview);
                previewArea.Overlay.Add(UiTextFactory.Create(
                    poseDisplayName + " · " +
                    poseTime.ToString("0.00") + "s",
                    UiClassNames.SecondaryText,
                    "ee4v-face-expression__pose-caption"));
                previewArea.SetHasContent(true);
                select.Content.Add(previewArea);
                card.Add(select);
                ObjectField sourceField = null;
                select.RegisterCallback<ContextClickEvent>(evt =>
                {
                    var menu = new GenericMenu();
                    menu.AddItem(
                        UiTextFactory.CreateGuiContent(_renamePoseText),
                        false,
                        () => BeginPoseRename(
                            sourceField,
                            poseIndex,
                            poseName));
                    if (!string.IsNullOrWhiteSpace(poseName))
                    {
                        menu.AddItem(
                            UiTextFactory.CreateGuiContent(
                                _resetPoseNameText),
                            false,
                            () => PoseNameChanged?.Invoke(
                                poseIndex,
                                string.Empty));
                    }

                    menu.ShowAsContext();
                    evt.StopPropagation();
                });

                sourceField = UiTextFactory.CreateObjectField(
                    string.Empty,
                    "ee4v-face-expression__pose-source");
                sourceField.objectType = typeof(AnimationClip);
                sourceField.allowSceneObjects = false;
                sourceField.tooltip = _poseSourceTooltip;
                sourceField.SetValueWithoutNotify(poseSource);
                sourceField.RegisterValueChangedCallback(evt =>
                {
                    if (!_rendering)
                    {
                        PoseSourceChanged?.Invoke(
                            poseIndex,
                            evt.newValue as AnimationClip);
                        sourceField.SetValueWithoutNotify(poseSource);
                    }
                });
                card.Add(sourceField);
                card.RegisterCallback<DragUpdatedEvent>(evt =>
                {
                    if (GetDraggedClip() == null)
                    {
                        return;
                    }

                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    evt.StopPropagation();
                });
                card.RegisterCallback<DragPerformEvent>(evt =>
                {
                    var draggedClip = GetDraggedClip();
                    if (draggedClip == null)
                    {
                        return;
                    }

                    DragAndDrop.AcceptDrag();
                    sourceField.value = draggedClip;
                    evt.StopPropagation();
                });

                if (index == _selectedPoseIndex && index > 0)
                {
                    var moveEarlier = new UiButton(
                        string.Empty,
                        () => PoseMoveRequested?.Invoke(
                            poseIndex,
                            poseIndex - 1),
                        _movePoseEarlierTooltip,
                        FluentUiIcons.CreateState(
                            "arrow_left.png",
                            UiSizeTokens.Size16),
                        UiButtonVariant.Ghost);
                    moveEarlier.AddToClassList(
                        "ee4v-face-expression__move-pose");
                    moveEarlier.AddToClassList(
                        "ee4v-face-expression__move-pose--earlier");
                    card.Add(moveEarlier);
                }

                if (index == _selectedPoseIndex &&
                    index < _poseTimes.Length - 1)
                {
                    var moveLater = new UiButton(
                        string.Empty,
                        () => PoseMoveRequested?.Invoke(
                            poseIndex,
                            poseIndex + 1),
                        _movePoseLaterTooltip,
                        FluentUiIcons.CreateState(
                            "arrow_right.png",
                            UiSizeTokens.Size16),
                        UiButtonVariant.Ghost);
                    moveLater.AddToClassList(
                        "ee4v-face-expression__move-pose");
                    moveLater.AddToClassList(
                        "ee4v-face-expression__move-pose--later");
                    moveLater.EnableInClassList(
                        "ee4v-face-expression__move-pose--later-only",
                        index == 0);
                    card.Add(moveLater);
                }

                var remove = new UiButton(
                    string.Empty,
                    () => PoseRemoveRequested?.Invoke(poseIndex),
                    _removePoseTooltip,
                    FluentUiIcons.CreateState(
                        "dismiss.png",
                        UiSizeTokens.Size16),
                    UiButtonVariant.Ghost);
                remove.AddToClassList("ee4v-face-expression__remove-pose");
                remove.style.display = _poseTimes.Length > 1
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                card.Add(remove);
                _poseSequence.Add(card);

                if (index >= _poseTimes.Length - 1)
                {
                    continue;
                }

                var transition = new VisualElement();
                transition.AddToClassList(
                    "ee4v-face-expression__pose-transition");
                var insert = new UiButton(
                    _insertPoseText,
                    () => PoseInsertRequested?.Invoke(poseIndex),
                    _insertPoseTooltip,
                    variant: UiButtonVariant.Ghost);
                insert.AddToClassList(
                    "ee4v-face-expression__insert-pose");
                transition.Add(insert);
                transition.Add(UiTextFactory.Create(
                    _transitionText,
                    UiClassNames.SecondaryText,
                    "ee4v-face-expression__transition-label"));
                var duration = UiTextFactory.CreateFloatField(
                    string.Empty,
                    "ee4v-face-expression__transition-duration");
                duration.isDelayed = true;
                duration.SetValueWithoutNotify(
                    _poseTimes[index + 1] - _poseTimes[index]);
                duration.RegisterValueChangedCallback(evt =>
                {
                    if (!float.IsNaN(evt.newValue) &&
                        !float.IsInfinity(evt.newValue))
                    {
                        TransitionDurationChanged?.Invoke(
                            poseIndex,
                            evt.newValue);
                    }
                });
                transition.Add(duration);
                _poseSequence.Add(transition);
            }

            _addPose.SetEnabled(_canAddPose);
            _addPose.tooltip = _canAddPose
                ? _addPoseTooltip
                : _addPoseUnavailableTooltip;
        }

        private string FormatPoseLabel(int index)
        {
            try
            {
                return string.Format(_poseText, index + 1);
            }
            catch (FormatException)
            {
                return _poseText + " " + (index + 1);
            }
        }

        private void BeginPoseRename(
            VisualElement sourceField,
            int poseIndex,
            string poseName)
        {
            if (sourceField == null ||
                sourceField.panel == null ||
                sourceField.parent == null)
            {
                return;
            }

            var parent = sourceField.parent;
            var sourceIndex = parent.IndexOf(sourceField);
            var existing = parent.Q<InputField>(
                className: "ee4v-face-expression__pose-rename");
            existing?.RemoveFromHierarchy();

            var input = new InputField(new InputFieldState(
                poseName,
                placeholder: FormatPoseLabel(poseIndex)));
            input.AddToClassList("ee4v-face-expression__pose-rename");
            var completed = false;
            Action<bool> complete = commit =>
            {
                if (completed)
                {
                    return;
                }

                completed = true;
                var value = input.Value;
                var inputParent = input.parent;
                input.RemoveFromHierarchy();
                if (sourceField.parent == null &&
                    inputParent != null &&
                    inputParent.panel != null)
                {
                    inputParent.Insert(
                        Mathf.Min(sourceIndex, inputParent.childCount),
                        sourceField);
                }

                if (commit)
                {
                    PoseNameChanged?.Invoke(poseIndex, value);
                }
            };
            input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    complete(false);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Return ||
                         evt.keyCode == KeyCode.KeypadEnter)
                {
                    complete(true);
                    evt.StopPropagation();
                }
            });
            input.RegisterCallback<FocusOutEvent>(_ => complete(true));
            sourceField.RemoveFromHierarchy();
            parent.Insert(sourceIndex, input);
            input.schedule.Execute(input.FocusInput);
        }

        private static AnimationClip GetDraggedClip()
        {
            return DragAndDrop.objectReferences
                .OfType<AnimationClip>()
                .FirstOrDefault();
        }

        public void RefreshChannelValues()
        {
            _blendShapeList.RefreshItems();
        }

        public void SetChannels(
            IReadOnlyList<BlendShapeChannel> channels,
            string sectionTitle = null,
            BlendShapeNamingRule namingRule = null,
            bool hideHeaders = false)
        {
            _channels = channels?.ToList() ?? new List<BlendShapeChannel>();
            _favoriteKeys.Clear();
            _namingRule = namingRule;
            _hideHeaders = hideHeaders;
            _sectionTitle = string.IsNullOrEmpty(sectionTitle)
                ? _defaultSectionTitle
                : sectionTitle;
            RefreshFilter();
        }

        public void SetValidation(MessagePanelState state)
        {
            _validation.SetState(state);
        }

        public void SetLibrary(
            IReadOnlyList<string> folders,
            IReadOnlyList<AnimationClip> clips,
            bool canNavigateBack,
            Action<AnimationClip, Rect> drawPreview)
        {
            _canNavigateLibraryBack = canNavigateBack;
            _libraryFolders = folders;
            _libraryClips = clips;
            _drawLibraryPreview = drawPreview;
            RenderLibraryItems();
        }

        private void RenderLibraryItems()
        {
            _library.Clear();
            var folders = _libraryFolders;
            var clips = _libraryClips;
            var query = (_librarySearch.Value ?? string.Empty).Trim();
            var items = new List<VisualElement>();
            if (_canNavigateLibraryBack)
            {
                items.Add(CreateIconLibraryItem(
                    _backToLibrary.tooltip,
                    "arrow_left.png",
                    UiSizeTokens.Size28,
                    () => BackRequested?.Invoke()));
            }
            items.Add(CreateIconLibraryItem(
                _newClip,
                "add.png",
                UiSizeTokens.Size28,
                () => NewClipRequested?.Invoke()));
            if (folders != null)
            {
                for (var index = 0; index < folders.Count; index++)
                {
                    var folder = folders[index];
                    if (string.IsNullOrEmpty(folder))
                    {
                        continue;
                    }

                    var separatorIndex = folder.LastIndexOf('/');
                    var name = separatorIndex >= 0
                        ? folder.Substring(separatorIndex + 1)
                        : folder;
                    if (name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                    items.Add(CreateIconLibraryItem(
                        name,
                        "folder.png",
                        LibraryFolderIconSize,
                        () => LibraryFolderRequested?.Invoke(folder)));
                }
            }

            if (clips != null)
            {
                items.AddRange(clips
                    .Where(clip => clip != null && clip.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(clip => CreateClipLibraryItem(
                        clip,
                        _drawLibraryPreview)));
            }

            for (var firstIndex = 0;
                 firstIndex < items.Count;
                 firstIndex += 3)
            {
                var row = new VisualElement();
                row.AddToClassList("ee4v-face-expression__library-row");
                for (var column = 0; column < 3; column++)
                {
                    var slot = new VisualElement();
                    slot.AddToClassList(
                        "ee4v-face-expression__library-slot");
                    slot.style.marginRight = column < 2
                        ? UiSpacingTokens.Small
                        : 0f;
                    var clipIndex = firstIndex + column;
                    if (clipIndex < items.Count)
                    {
                        slot.Add(items[clipIndex]);
                    }
                    else
                    {
                        slot.style.visibility = Visibility.Hidden;
                    }

                    row.Add(slot);
                }

                _library.Add(row);
            }

            RefreshFilter();
        }

        private VisualElement CreateClipLibraryItem(
            AnimationClip clip,
            Action<AnimationClip, Rect> drawPreview)
        {
            IMGUIContainer preview = null;
            preview = new IMGUIContainer(() =>
            {
                if (Event.current?.type == EventType.Repaint)
                {
                    drawPreview?.Invoke(clip, preview.contentRect);
                }
            });
            preview.AddToClassList("ee4v-face-expression__library-preview");
            preview.pickingMode = PickingMode.Ignore;
            var dragging = false;
            var dragStart = Vector2.zero;
            var item = CreateLibraryItem(
                clip.name,
                () => EditClipRequested?.Invoke(clip),
                preview,
                () => { if (!dragging) { LibraryClipSelected?.Invoke(clip); } },
                () => !dragging);
            item.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) { return; }
                dragging = false;
                dragStart = evt.position;
            }, TrickleDown.TrickleDown);
            item.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if ((evt.pressedButtons & 1) == 0 || dragging ||
                    ((Vector2)evt.position - dragStart).sqrMagnitude < 36f) { return; }
                dragging = true;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new UnityEngine.Object[] { clip };
                if (item.HasPointerCapture(evt.pointerId)) { item.ReleasePointer(evt.pointerId); }
                DragAndDrop.StartDrag(clip.name);
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            item.RegisterCallback<ContextClickEvent>(evt =>
            {
                var menu = new GenericMenu();
                menu.AddItem(
                    UiTextFactory.CreateGuiContent(_edit),
                    false,
                    () => EditClipRequested?.Invoke(clip));
                menu.AddItem(
                    UiTextFactory.CreateGuiContent(_copyAndEdit),
                    false,
                    () => CopyClipRequested?.Invoke(clip));
                menu.ShowAsContext();
                evt.StopPropagation();
            });
            return item;
        }

        private VisualElement CreateIconLibraryItem(
            string name,
            string iconName,
            float iconSize,
            Action selected)
        {
            var preview = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            preview.AddToClassList("ee4v-face-expression__library-preview");
            preview.AddToClassList(
                "ee4v-face-expression__library-preview--icon");
            preview.Add(new Icon(FluentUiIcons.CreateState(
                iconName,
                iconSize)));
            return CreateLibraryItem(name, selected, preview);
        }

        private VisualElement CreateLibraryItem(
            string nameText,
            Action activated,
            VisualElement preview,
            Action selected = null,
            Func<bool> canActivate = null)
        {
            var item = new UiButton(
                string.Empty,
                selected,
                nameText,
                variant: UiButtonVariant.Ghost);
            item.AddToClassList("ee4v-face-expression__library-item");
            item.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button != 0 || evt.clickCount != 2 ||
                    (canActivate != null && !canActivate())) { return; }
                activated?.Invoke();
                evt.StopPropagation();
            });
            item.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter &&
                    evt.keyCode != KeyCode.Space) { return; }
                activated?.Invoke();
                evt.PreventDefault();
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            item.Content.Add(preview);

            var name = UiTextFactory.Create(
                nameText,
                "ee4v-face-expression__library-name");
            name.SetTextAlign(TextAnchor.MiddleCenter);
            name.style.alignItems = Align.Center;
            name.style.justifyContent = Justify.Center;
            name.pickingMode = PickingMode.Ignore;
            item.Content.Add(name);
            item.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var previewHeight = Mathf.Max(1f, evt.newRect.width - 2f);
                if (!Mathf.Approximately(
                        preview.resolvedStyle.height,
                        previewHeight))
                {
                    preview.style.height = previewHeight;
                    item.style.height =
                        previewHeight + LibraryItemLabelHeight;
                }
            });
            return item;
        }

        private VisualElement CreateBlendShapeRow()
        {
            var row = new BlendShapeRow(FavoriteKey);
            row.GroupToggled += key =>
            {
                if (!_collapsedBlendShapeGroups.Add(key)) { _collapsedBlendShapeGroups.Remove(key); }
                RefreshFavoriteRows();
            };
            row.Changed += channel =>
            {
                ChannelChanged?.Invoke(channel);
                if (_clipOnly.value && !channel.Animated)
                {
                    RefreshFilter();
                }
                else { _blendShapeList.RefreshItems(); }
            };
            var slot = new VisualElement();
            slot.AddToClassList("ee4v-face-expression-row-slot");
            slot.Add(row);
            return slot;
        }

        private void OnFavoritesChanged()
        {
            if (!_hasClip) { return; }
            if (_favoriteRefresh == null) { _favoriteRefresh = schedule.Execute(RefreshFavoriteRows); }
            else { _favoriteRefresh.ExecuteLater(0); }
        }

        private void RefreshBlendShapeList()
        {
            _blendShapeList.EnableInClassList("ee4v-face-expression__blend-shapes--with-headers",
                _visibleItems.Count > 0 && _visibleItems[0].IsHeader);
            if (!ReferenceEquals(_blendShapeList.itemsSource, _visibleItems))
            { _blendShapeList.itemsSource = _visibleItems; }
            else { _blendShapeList.RefreshItems(); }
        }

        private void BindBlendShapeRow(VisualElement element, int index)
        {
            var row = element.Q<BlendShapeRow>();
            if (row != null && index >= 0 && index < _visibleItems.Count)
            {
                row.SetItem(_visibleItems[index]);
                row.SetEditingEnabled(!_selectedPoseReadOnly);
            }
        }

        private void RefreshFilter()
        {
            var assigning = !_hasClip && _assignmentPane.childCount > 0;
            _previewViewport.style.display = assigning ? DisplayStyle.None : DisplayStyle.Flex;
            _assignmentPane.style.display = assigning ? DisplayStyle.Flex : DisplayStyle.None;
            _assignmentSettings.style.display = assigning ? DisplayStyle.Flex : DisplayStyle.None;
            _editAssignment.style.display = assigning ? DisplayStyle.Flex : DisplayStyle.None;
            _rightPane.EnableInClassList("ee4v-face-expression__editor-pane--assigning", assigning);
            _sectionHeader.SetTitle(_hasClip ? _sectionTitle : _libraryTitle);
            _toolbar.style.display = _showAvatarField ? DisplayStyle.Flex : DisplayStyle.None;
            _backToLibrary.style.display = _hasClip ? DisplayStyle.Flex : DisplayStyle.None;
            _clipOnlyControl.style.display = _hasClip
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _search.style.display = _hasClip
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _librarySearch.style.display = _hasClip ? DisplayStyle.None : DisplayStyle.Flex;
            _animationControls.style.display = _hasClip
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (!_hasClip)
            {
                _visibleItems.Clear();
                RefreshBlendShapeList();
                _blendShapeList.style.display = DisplayStyle.None;
                _library.style.display = DisplayStyle.Flex;
                _empty.style.display = DisplayStyle.None;
                return;
            }

            _library.style.display = DisplayStyle.None;

            var query = (_search?.Value ?? string.Empty).Trim();
            _filteredChannels = string.IsNullOrEmpty(query) && !_clipOnly.value
                ? _channels.ToList()
                : FilterWithHeaders(
                    _channels,
                    query,
                    _clipOnly.value);
            _groupItems = BlendShapeRowItem.Create(
                    _filteredChannels,
                    _namingRule,
                    _hideHeaders,
                    nestSides: !_clipOnly.value)
                .ToList();
            RefreshFavoriteRows();
        }

        private void RefreshFavoriteRows()
        {
            if (!_hasClip) { return; }
            var allItems = new List<BlendShapeRowItem>(_groupItems);
            var favorites = _filteredChannels.Where(channel => !channel.IsHeader &&
                BlendShapeFavorites.Contains(FavoriteKey(channel))).ToArray();
            if (favorites.Length > 0)
            {
                var pinned = BlendShapeRowItem.Create(new[] { new BlendShapeChannel(string.Empty, string.Empty,
                    0, false, I18N.Get("favorites.title")) }.Concat(favorites).ToArray(), _namingRule, false, nestSides: false);
                allItems.InsertRange(0, pinned);
            }
            _visibleItems.Clear();
            BlendShapeRowItem groupHeader = null;
            for (var index = 0; index < allItems.Count; index++)
            {
                var item = allItems[index];
                if (item.IsHeader)
                {
                    groupHeader = item;
                    item.GroupKey = item.Header.RendererPath + "\n" + item.Header.Name + "\n" +
                        item.Header.DisplayHeaderText + "\n" + _channels.IndexOf(item.Header);
                    item.IsCollapsed = _collapsedBlendShapeGroups.Contains(item.GroupKey);
                    item.IsGroupEnd = item.IsCollapsed || index == allItems.Count - 1 || allItems[index + 1].IsHeader ||
                        !allItems[index + 1].IsGrouped;
                }
                else if (!item.IsGrouped) { groupHeader = null; }
                if (item.IsHeader || groupHeader?.IsCollapsed != true) { _visibleItems.Add(item); }
            }
            RefreshBlendShapeList();
            var hasItems = _visibleItems.Count > 0;
            _blendShapeList.style.display = hasItems ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.SetState(new EmptyStateState(
                string.Empty,
                _noBlendShapes));
            _empty.style.display = hasItems ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static List<BlendShapeChannel> FilterWithHeaders(
            IReadOnlyList<BlendShapeChannel> channels,
            string query,
            bool clipOnly)
        {
            var result = new List<BlendShapeChannel>();
            BlendShapeChannel header = null;
            var headerAdded = false;
            var headerMatches = false;
            string rendererPath = null;
            for (var index = 0; index < channels.Count; index++)
            {
                var channel = channels[index];
                if (!string.Equals(rendererPath, channel.RendererPath, StringComparison.Ordinal))
                {
                    rendererPath = channel.RendererPath;
                    header = null;
                    headerAdded = false;
                    headerMatches = false;
                }

                if (channel.IsHeader)
                {
                    header = channel;
                    headerAdded = false;
                    headerMatches = !string.IsNullOrEmpty(query) &&
                                    channel.DisplayHeaderText.IndexOf(
                                        query,
                                        StringComparison.OrdinalIgnoreCase) >= 0;
                    continue;
                }

                if ((clipOnly && !channel.Animated) ||
                    (!string.IsNullOrEmpty(query) &&
                     !headerMatches &&
                     channel.DisplayName.IndexOf(
                         query,
                         StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                if (header != null && !headerAdded)
                {
                    result.Add(header);
                    headerAdded = true;
                }

                result.Add(channel);
            }

            return result;
        }

        private string FavoriteKey(BlendShapeChannel channel)
        {
            var avatar = _avatarField.value as GameObject;
            if (channel == null || avatar == null) { return null; }
            if (_favoriteKeys.TryGetValue(channel, out var key)) { return key; }
            var target = string.IsNullOrEmpty(channel.RendererPath) ? avatar.transform : avatar.transform.Find(channel.RendererPath);
            key = BlendShapeFavorites.Key(target?.GetComponent<SkinnedMeshRenderer>()?.sharedMesh, channel.Name);
            _favoriteKeys.Add(channel, key);
            return key;
        }
    }

    internal sealed class BlendShapeRowItem
    {
        private BlendShapeRowItem(
            BlendShapeChannel channel,
            BlendShapeName parsedName,
            bool showChannelName = false,
            string displayName = null,
            bool isChild = false)
        {
            if (channel.IsHeader)
            {
                Header = channel;
                DisplayName = channel.DisplayHeaderText;
                return;
            }

            DisplayName = displayName ?? (parsedName == null || showChannelName
                ? channel.DisplayName
                : string.IsNullOrEmpty(channel.RendererDisplayName)
                    ? parsedName.Role
                    : channel.RendererDisplayName + " / " + parsedName.Role);
            ActiveChannel = channel;
            IsChild = isChild;
        }

        internal BlendShapeChannel Header { get; }
        internal bool IsHeader => Header != null;
        internal string DisplayName { get; }
        internal BlendShapeChannel ActiveChannel { get; }
        internal bool IsChild { get; }
        internal bool IsGrouped { get; private set; }
        internal bool IsGroupEnd { get; set; }
        internal string GroupKey { get; set; }
        internal bool IsCollapsed { get; set; }
        internal string Tooltip => IsHeader
            ? Header.DisplayHeaderText
            : ActiveChannel.DisplayName;

        internal static IReadOnlyList<BlendShapeRowItem> Create(
            IReadOnlyList<BlendShapeChannel> channels,
            BlendShapeNamingRule namingRule,
            bool hideHeaders,
            bool nestSides = true)
        {
            var result = new List<BlendShapeRowItem>();
            var segment = new List<BlendShapeChannel>();
            for (var index = 0; index < (channels?.Count ?? 0); index++)
            {
                var channel = channels[index];
                if (channel.IsHeader)
                {
                    AppendSegment(
                        result,
                        segment,
                        namingRule,
                        nestSides);
                    segment.Clear();
                    if (!hideHeaders)
                    {
                        result.Add(new BlendShapeRowItem(channel, null));
                    }

                    continue;
                }

                segment.Add(channel);
            }

            AppendSegment(result, segment, namingRule, nestSides);

            BlendShapeRowItem header = null;
            BlendShapeRowItem previous = null;
            foreach (var item in result)
            {
                if (item.IsHeader || (header != null && !string.IsNullOrEmpty(header.Header.Name) &&
                    item.ActiveChannel.RendererPath != header.Header.RendererPath))
                {
                    if (previous?.IsGrouped == true) { previous.IsGroupEnd = true; }
                    header = item.IsHeader ? item : null;
                }
                item.IsGrouped = header != null;
                previous = item;
            }
            if (previous?.IsGrouped == true) { previous.IsGroupEnd = true; }

            if (!hideHeaders)
            {
                var ungrouped = result.Where(item => !item.IsGrouped).ToArray();
                if (ungrouped.Length > 0)
                {
                    result.RemoveAll(item => !item.IsGrouped);
                    result.Add(new BlendShapeRowItem(new BlendShapeChannel(string.Empty, string.Empty,
                        0f, false, I18N.Get("group.ungrouped")), null) { IsGrouped = true });
                    foreach (var item in ungrouped)
                    {
                        item.IsGrouped = true;
                        result.Add(item);
                    }
                    ungrouped[ungrouped.Length - 1].IsGroupEnd = true;
                }
            }

            return result;
        }

        private static void AppendSegment(
            ICollection<BlendShapeRowItem> result,
            IReadOnlyList<BlendShapeChannel> channels,
            BlendShapeNamingRule namingRule,
            bool nestSides)
        {
            var orderedGroups = new List<BlendShapeChannelGroup>();
            var groupsByRole =
                new Dictionary<string, List<BlendShapeChannelGroup>>(
                    StringComparer.Ordinal);
            for (var index = 0; index < channels.Count; index++)
            {
                var channel = channels[index];
                BlendShapeName parsedName = null;
                if (namingRule != null)
                {
                    namingRule.TryParse(channel, out parsedName);
                }

                if (parsedName == null ||
                    !nestSides ||
                    !BlendShapeChannelGroup.IsSupportedSide(parsedName.Side))
                {
                    orderedGroups.Add(
                        BlendShapeChannelGroup.CreateStandalone(
                            channel,
                            parsedName,
                            showChannelName: parsedName != null));
                    continue;
                }

                var key = channel.RendererPath + "\n" + parsedName.Role;
                if (!groupsByRole.TryGetValue(key, out var groups))
                {
                    groups = new List<BlendShapeChannelGroup>();
                    groupsByRole.Add(key, groups);
                }

                var group = groups.FirstOrDefault(candidate =>
                    candidate.CanAdd(parsedName.Side));
                if (group == null)
                {
                    group = new BlendShapeChannelGroup();
                    groups.Add(group);
                    orderedGroups.Add(group);
                }

                group.Add(channel, parsedName);
            }

            for (var index = 0; index < orderedGroups.Count; index++)
            {
                orderedGroups[index].AppendTo(result);
            }
        }

        private sealed class BlendShapeChannelGroup
        {
            private BlendShapeChannel _standalone;
            private BlendShapeName _standaloneName;
            private bool _showStandaloneChannelName;
            private BlendShapeChannel _normal;
            private BlendShapeName _normalName;
            private BlendShapeChannel _left;
            private BlendShapeName _leftName;
            private BlendShapeChannel _right;
            private BlendShapeName _rightName;

            internal static BlendShapeChannelGroup CreateStandalone(
                BlendShapeChannel channel,
                BlendShapeName parsedName,
                bool showChannelName)
            {
                return new BlendShapeChannelGroup
                {
                    _standalone = channel,
                    _standaloneName = parsedName,
                    _showStandaloneChannelName = showChannelName
                };
            }

            internal static bool IsSupportedSide(string side)
            {
                return string.IsNullOrEmpty(side) ||
                       string.Equals(side, "L", StringComparison.Ordinal) ||
                       string.Equals(side, "R", StringComparison.Ordinal);
            }

            internal bool CanAdd(string side)
            {
                if (string.IsNullOrEmpty(side))
                {
                    return _normal == null;
                }

                return string.Equals(side, "L", StringComparison.Ordinal)
                    ? _left == null
                    : _right == null;
            }

            internal void Add(
                BlendShapeChannel channel,
                BlendShapeName parsedName)
            {
                if (string.IsNullOrEmpty(parsedName.Side))
                {
                    _normal = channel;
                    _normalName = parsedName;
                    return;
                }

                if (string.Equals(
                        parsedName.Side,
                        "L",
                        StringComparison.Ordinal))
                {
                    _left = channel;
                    _leftName = parsedName;
                    return;
                }

                _right = channel;
                _rightName = parsedName;
            }

            internal void AppendTo(ICollection<BlendShapeRowItem> result)
            {
                if (_standalone != null)
                {
                    result.Add(new BlendShapeRowItem(
                        _standalone,
                        _standaloneName,
                        _showStandaloneChannelName));
                    return;
                }

                if (_normal == null)
                {
                    AppendStandaloneSide(result, _left, _leftName);
                    AppendStandaloneSide(result, _right, _rightName);
                    return;
                }

                result.Add(new BlendShapeRowItem(_normal, _normalName));
                AppendChild(result, _left, _leftName, "L");
                AppendChild(result, _right, _rightName, "R");
            }

            private static void AppendStandaloneSide(
                ICollection<BlendShapeRowItem> result,
                BlendShapeChannel channel,
                BlendShapeName parsedName)
            {
                if (channel != null)
                {
                    result.Add(new BlendShapeRowItem(
                        channel,
                        parsedName,
                        showChannelName: true));
                }
            }

            private static void AppendChild(
                ICollection<BlendShapeRowItem> result,
                BlendShapeChannel channel,
                BlendShapeName parsedName,
                string side)
            {
                if (channel != null)
                {
                    result.Add(new BlendShapeRowItem(
                        channel,
                        parsedName,
                        displayName: side,
                        isChild: true));
                }
            }
        }
    }

    internal sealed class BlendShapeRow : VisualElement
    {
        private readonly UiButton _favorite;
        private readonly UiButton _groupToggle;
        private readonly Func<BlendShapeChannel, string> _favoriteKey;
        private readonly UiTextElement _name;
        private readonly VisualElement _controls;
        private readonly Slider _slider;
        private readonly FloatField _value;
        private readonly UiButton _reset;
        private BlendShapeRowItem _item;
        private bool _rendering;
        private bool _editingEnabled = true;

        public BlendShapeRow(Func<BlendShapeChannel, string> favoriteKey = null)
        {
            _favoriteKey = favoriteKey ?? (channel => string.IsNullOrEmpty(channel?.SourceAssetGuid) ? null
                : BlendShapeFavorites.Key(channel.SourceAssetGuid, channel.SourceMeshLocalId, channel.Name));
            AddToClassList("ee4v-face-expression-row");
            _groupToggle = new UiButton(string.Empty, () =>
            {
                if (_item?.IsHeader == true && _item.GroupKey != null) { GroupToggled?.Invoke(_item.GroupKey); }
            }, variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SectionTitle);
            _groupToggle.AddToClassList("ee4v-face-expression-row__group-toggle");
            _groupToggle.SetContentAlignment(Justify.FlexStart);
            Add(_groupToggle);
            _favorite = new UiButton(string.Empty, () =>
            {
                var channel = _item?.ActiveChannel;
                if (_editingEnabled && channel != null)
                {
                    BlendShapeFavorites.Toggle(_favoriteKey(channel));
                    if (panel == null) { RefreshFavoriteIcon(); }
                }
            }, icon: AvatarEditingUi.CreateBlendShapeFavoriteIcon(false), variant: UiButtonVariant.Ghost);
            _favorite.AddToClassList("ee4v-face-expression-row__favorite");
            Add(_favorite);
            RegisterCallback<AttachToPanelEvent>(_ => BlendShapeFavorites.KeyChanged += OnFavoriteKeyChanged);
            RegisterCallback<DetachFromPanelEvent>(_ => BlendShapeFavorites.KeyChanged -= OnFavoriteKeyChanged);
            RegisterCallback<ContextClickEvent>(evt =>
            {
                var channel = _item?.ActiveChannel;
                if (!_editingEnabled || channel?.Animated != true) { return; }
                var menu = new GenericMenu();
                menu.AddItem(UiTextFactory.CreateGuiContent(I18N.Get("favorites.removeCurve")), false, () =>
                {
                    channel.Animated = false;
                    channel.Value = channel.InitialValue;
                    Changed?.Invoke(channel);
                });
                menu.ShowAsContext();
                evt.StopPropagation();
            });
            _name = UiTextFactory.Create(string.Empty, "ee4v-face-expression-row__name");
            Add(_name);
            _controls = new VisualElement();
            _controls.AddToClassList("ee4v-face-expression-row__controls");
            Add(_controls);
            _slider = new Slider(0f, 100f);
            _slider.AddToClassList("ee4v-face-expression-row__slider");
            _slider.RegisterValueChangedCallback(evt => SetValue(evt.newValue));
            _controls.Add(_slider);
            _value = UiTextFactory.CreateFloatField();
            _value.AddToClassList("ee4v-face-expression-row__value");
            _value.RegisterValueChangedCallback(evt => SetValue(evt.newValue));
            _controls.Add(_value);
            _reset = new UiButton(I18N.Get("action.resetBlendShape"),
                () =>
                {
                    var channel = _item?.ActiveChannel;
                    if (channel != null) { SetValue(channel.InitialValue); }
                }, variant: UiButtonVariant.Ghost);
            _reset.AddToClassList("ee4v-face-expression-row__reset");
            _controls.Add(_reset);
        }

        public event Action<BlendShapeChannel> Changed;
        public event Action<string> GroupToggled;

        public void SetEditingEnabled(bool enabled)
        {
            _editingEnabled = enabled;
            _controls.SetEnabled(enabled);
            _favorite.SetEnabled(enabled && _favoriteKey(_item?.ActiveChannel) != null);
        }

        public void SetItem(BlendShapeRowItem item)
        {
            _item = item;
            var isHeader = item?.IsHeader == true;
            EnableInClassList("ee4v-face-expression-row--header", isHeader);
            EnableInClassList("ee4v-face-expression-row--grouped", item?.IsGrouped == true);
            EnableInClassList("ee4v-face-expression-row--group-end", item?.IsGroupEnd == true);
            EnableInClassList(
                "ee4v-face-expression-row--child",
                item?.IsChild == true);
            _name.SetText(item?.DisplayName ?? string.Empty);
            _name.tooltip = item?.Tooltip ?? string.Empty;
            _name.style.display = isHeader ? DisplayStyle.None : DisplayStyle.Flex;
            _groupToggle.style.display = isHeader ? DisplayStyle.Flex : DisplayStyle.None;
            if (isHeader)
            {
                _groupToggle.SetLabel(item.DisplayName);
                _groupToggle.SetIcon(FluentUiIcons.CreateState(item.IsCollapsed
                    ? "chevron_right.png" : "chevron_down.png", UiSizeTokens.Size12));
                _groupToggle.tooltip = item.Tooltip;
            }
            _favorite.style.display = isHeader || item?.IsChild == true ? DisplayStyle.None : DisplayStyle.Flex;
            _controls.style.display = isHeader ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshControls();
        }

        private void RefreshControls()
        {
            var channel = _item?.ActiveChannel;
            _rendering = true;
            var key = _favoriteKey(channel);
            _favorite.SetEnabled(_editingEnabled && key != null);
            RefreshFavoriteIcon();
            _slider.SetValueWithoutNotify(channel?.Value ?? 0f);
            _value.SetValueWithoutNotify(channel?.Value ?? 0f);
            RefreshReset(channel);
            _rendering = false;
        }

        private void OnFavoriteKeyChanged(string key)
        {
            if (string.Equals(_favoriteKey(_item?.ActiveChannel), key, StringComparison.Ordinal)) { RefreshFavoriteIcon(); }
        }

        private void RefreshFavoriteIcon()
        {
            var key = _favoriteKey(_item?.ActiveChannel);
            var favorite = BlendShapeFavorites.Contains(key);
            _favorite.SetIcon(AvatarEditingUi.CreateBlendShapeFavoriteIcon(favorite));
            _favorite.tooltip = I18N.Get(favorite ? "favorites.remove" : "favorites.add");
        }

        private void RefreshReset(BlendShapeChannel channel)
        {
            _reset.SetEnabled(channel != null && !Mathf.Approximately(channel.Value, channel.InitialValue));
            _reset.tooltip = channel == null ? string.Empty
                : I18N.Get("action.resetBlendShapeTooltip", channel.InitialValue);
        }

        private void SetValue(float value)
        {
            var channel = _item?.ActiveChannel;
            if (!_editingEnabled || _rendering || channel == null)
            {
                return;
            }

            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return;
            }

            _rendering = true;
            channel.Value = value;
            channel.Animated = true;
            _slider.SetValueWithoutNotify(channel.Value);
            _value.SetValueWithoutNotify(channel.Value);
            RefreshReset(channel);
            _rendering = false;
            Changed?.Invoke(channel);
        }
    }
}
