using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        public string NewClip { get; set; }
        public string CopyAndEdit { get; set; }
        public string ResetView { get; set; }
        public string BackToLibrary { get; set; }
        public string SearchPlaceholder { get; set; }
        public string SearchTooltip { get; set; }
        public string ClearSearchTooltip { get; set; }
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
        private readonly ObjectField _clipField;
        private readonly HelpBox _validation;
        private readonly SearchField _search;
        private readonly SectionHeader _sectionHeader;
        private readonly UiButton _backToLibrary;
        private readonly Toggle _clipOnly;
        private readonly VisualElement _animationControls;
        private readonly UiButton _playback;
        private readonly Slider _timeline;
        private readonly Toggle _loop;
        private readonly ScrollView _poseSequence;
        private readonly UiButton _addPose;
        private readonly Action<AnimationClip, float, Rect> _drawPosePreview;
        private readonly ListView _blendShapeList;
        private readonly ScrollView _blendShapeScrollView;
        private readonly ScrollView _library;
        private readonly EmptyState _empty;
        private readonly string _defaultSectionTitle;
        private readonly string _libraryTitle;
        private readonly string _newClip;
        private readonly string _copyAndEdit;
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
        private List<BlendShapeRowItem> _visibleItems = new List<BlendShapeRowItem>();
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
        private int _animationDragPointerId = -1;
        private bool _animationDragValue;
        private float _animationDragPointerY;
        private int _animationDragLastIndex = -1;

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
            _sectionTitle = _defaultSectionTitle;
            AddToClassList("ee4v-face-expression");

            var toolbar = new ActionBar();
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

            if (showAvatarField)
            {
                Add(toolbar);
            }

            _validation = UiTextFactory.CreateHelpBox(
                string.Empty,
                HelpBoxMessageType.Info,
                "ee4v-face-expression__validation");
            _validation.style.display = DisplayStyle.None;
            Add(_validation);

            var content = new VisualElement();
            content.AddToClassList("ee4v-face-expression__content");
            var previewPane = new PreviewContainer();
            previewPane.AddToClassList("ee4v-face-expression__preview-pane");
            var preview = new IMGUIContainer(() =>
            {
                var rect = GUILayoutUtility.GetRect(
                    1f,
                    10000f,
                    1f,
                    10000f,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
                drawPreview?.Invoke(rect);
            });
            preview.AddToClassList("ee4v-face-expression__preview");
            previewPane.Content.Add(preview);
            var resetView = new UiButton(
                string.Empty,
                () => ResetViewRequested?.Invoke(),
                text.ResetView,
                FluentUiIcons.CreateState(
                    "arrow_clockwise.png",
                    UiSizeTokens.Size28),
                UiButtonVariant.Ghost);
            resetView.AddToClassList("ee4v-face-expression__reset-view");
            previewPane.Overlay.Add(resetView);
            previewPane.SetHasContent(true);
            content.Add(previewPane);

            var editorPane = new VisualElement();
            editorPane.AddToClassList("ee4v-face-expression__editor-pane");
            _sectionHeader = new SectionHeader(_defaultSectionTitle);
            _sectionHeader.AddToClassList(
                "ee4v-face-expression__section-header");
            _backToLibrary = new UiButton(
                string.Empty,
                () => BackRequested?.Invoke(),
                text.BackToLibrary,
                FluentUiIcons.CreateState(
                    "arrow_left.png",
                    UiSizeTokens.Size16),
                UiButtonVariant.Ghost);
            _backToLibrary.AddToClassList(
                "ee4v-face-expression__back-to-library");
            _backToLibrary.style.display = DisplayStyle.None;
            _sectionHeader.Insert(0, _backToLibrary);
            _clipOnly = UiTextFactory.CreateToggle(
                text.ClipOnly,
                "ee4v-face-expression__clip-only");
            _clipOnly.tooltip = text.ClipOnlyTooltip;
            _clipOnly.RegisterValueChangedCallback(_ => RefreshFilter());
            _sectionHeader.Actions.Add(_clipOnly);
            editorPane.Add(_sectionHeader);

            _clipField = UiTextFactory.CreateObjectField(
                text.Clip,
                "ee4v-face-expression__asset-field",
                "ee4v-face-expression__clip-field");
            _clipField.objectType = typeof(AnimationClip);
            _clipField.allowSceneObjects = false;
            _clipField.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    ClipChanged?.Invoke(evt.newValue as AnimationClip);
                }
            });
            editorPane.Add(_clipField);

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
            _search.ValueChanged += _ => RefreshFilter();
            editorPane.Add(_search);
            _blendShapeList = new ListView
            {
                fixedItemHeight = 28f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                makeItem = CreateBlendShapeRow,
                bindItem = BindBlendShapeRow
            };
            _blendShapeList.AddToClassList("ee4v-face-expression__blend-shapes");
            _blendShapeScrollView = _blendShapeList.Q<ScrollView>();
            if (_blendShapeScrollView != null)
            {
                _blendShapeScrollView.verticalScroller.valueChanged += _ =>
                {
                    if (_animationDragPointerId >= 0)
                    {
                        ApplyAnimationDragAtPointer();
                    }
                };
            }
            editorPane.Add(_blendShapeList);
            _library = new ScrollView(ScrollViewMode.Vertical);
            _library.AddToClassList("ee4v-face-expression__library");
            _library.contentContainer.AddToClassList(
                "ee4v-face-expression__library-content");
            editorPane.Add(_library);
            RegisterCallback<PointerMoveEvent>(OnAnimationDragPointerMove);
            RegisterCallback<PointerUpEvent>(OnAnimationDragPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(
                OnAnimationDragPointerCaptureOut);
            _empty = new EmptyState();
            _empty.AddToClassList("ee4v-face-expression__empty");
            editorPane.Add(_empty);
            content.Add(editorPane);
            Add(content);
        }

        public event Action<GameObject> AvatarChanged;
        public event Action<AnimationClip> ClipChanged;
        public event Action NewClipRequested;
        public event Action<AnimationClip> CopyClipRequested;
        public event Action BackRequested;
        public event Action<string> LibraryFolderRequested;
        public event Action<BlendShapeChannel> ChannelChanged;
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
            _rendering = true;
            _avatarField.SetValueWithoutNotify(avatar);
            _rendering = false;
        }

        public void SetAvatarEditable(bool editable)
        {
            _avatarField.SetEnabled(editable);
        }

        public void SetClip(AnimationClip clip)
        {
            _hasClip = clip != null;
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
            _selectedPoseReadOnly = nextSelectedPoseReadOnly;
            _blendShapeList.SetEnabled(!_selectedPoseReadOnly);
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
            _namingRule = namingRule;
            _hideHeaders = hideHeaders;
            _sectionTitle = string.IsNullOrEmpty(sectionTitle)
                ? _defaultSectionTitle
                : sectionTitle;
            RefreshFilter();
        }

        public void SetValidation(
            string message,
            HelpBoxMessageType messageType)
        {
            UiTextFactory.SetText(_validation, message);
            _validation.messageType = messageType;
            _validation.style.display = string.IsNullOrWhiteSpace(message)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        public void SetLibrary(
            IReadOnlyList<string> folders,
            IReadOnlyList<AnimationClip> clips,
            bool canNavigateBack,
            Action<AnimationClip, Rect> drawPreview)
        {
            _library.Clear();
            _canNavigateLibraryBack = canNavigateBack;
            var items = new List<VisualElement>
            {
                CreateIconLibraryItem(
                    _newClip,
                    "add.png",
                    UiSizeTokens.Size28,
                    () => NewClipRequested?.Invoke())
            };
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
                    .Where(clip => clip != null)
                    .Select(clip => CreateClipLibraryItem(
                        clip,
                        drawPreview)));
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
            var item = CreateLibraryItem(
                clip.name,
                () => ClipChanged?.Invoke(clip),
                preview);
            item.RegisterCallback<ContextClickEvent>(evt =>
            {
                var menu = new GenericMenu();
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
            Action selected,
            VisualElement preview)
        {
            var item = new UiButton(
                string.Empty,
                selected,
                nameText,
                variant: UiButtonVariant.Ghost);
            item.AddToClassList("ee4v-face-expression__library-item");
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
            var row = new BlendShapeRow();
            row.Changed += channel =>
            {
                ChannelChanged?.Invoke(channel);
                if (_clipOnly.value && !channel.Animated)
                {
                    RefreshFilter();
                }
            };
            row.AnimationDragStarted += BeginAnimationDrag;
            row.AnimationChanged += ChangeAnimation;
            return row;
        }

        private void ChangeAnimation(BlendShapeChannel channel, bool animated)
        {
            if (SetAnimated(channel, animated))
            {
                RefreshAfterAnimationChanges();
            }
        }

        private void BeginAnimationDrag(
            BlendShapeChannel channel,
            int pointerId,
            bool value,
            float pointerY)
        {
            _animationDragPointerId = pointerId;
            _animationDragValue = value;
            _animationDragLastIndex = _visibleItems.FindIndex(item =>
                ReferenceEquals(item.ActiveChannel, channel));
            _animationDragPointerY = pointerY;
            this.CapturePointer(pointerId);
            ApplyAnimationRange(_animationDragLastIndex);
        }

        private void OnAnimationDragPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _animationDragPointerId ||
                !this.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            _animationDragPointerY = evt.position.y;
            ApplyAnimationDragAtPointer();
            evt.StopPropagation();
        }

        private void ApplyAnimationDragAtPointer()
        {
            if (_animationDragPointerId < 0 ||
                _visibleItems.Count == 0 ||
                _blendShapeScrollView == null)
            {
                return;
            }

            var viewport = _blendShapeScrollView.contentViewport.worldBound;
            var pointerY = Mathf.Clamp(
                _animationDragPointerY,
                viewport.yMin,
                Mathf.Max(viewport.yMin, viewport.yMax - 0.01f));
            var contentY = _blendShapeScrollView.verticalScroller.value +
                           pointerY - viewport.yMin;
            var targetIndex = Mathf.Clamp(
                Mathf.FloorToInt(contentY / _blendShapeList.fixedItemHeight),
                0,
                _visibleItems.Count - 1);
            ApplyAnimationRange(targetIndex);
        }

        private void ApplyAnimationRange(int targetIndex)
        {
            if (targetIndex < 0)
            {
                return;
            }

            if (_animationDragLastIndex < 0)
            {
                _animationDragLastIndex = targetIndex;
            }

            var firstIndex = Mathf.Min(_animationDragLastIndex, targetIndex);
            var lastIndex = Mathf.Max(_animationDragLastIndex, targetIndex);
            var changed = false;
            for (var index = firstIndex; index <= lastIndex; index++)
            {
                changed |= SetAnimated(
                    _visibleItems[index].ActiveChannel,
                    _animationDragValue);
            }

            _animationDragLastIndex = targetIndex;
            if (changed)
            {
                RefreshAfterAnimationChanges();
            }
        }

        private bool SetAnimated(BlendShapeChannel channel, bool animated)
        {
            if (channel == null ||
                channel.IsHeader ||
                channel.Animated == animated)
            {
                return false;
            }

            channel.Animated = animated;
            ChannelChanged?.Invoke(channel);
            return true;
        }

        private void RefreshAfterAnimationChanges()
        {
            if (_clipOnly.value)
            {
                if (_animationDragPointerId < 0)
                {
                    RefreshFilter();
                    return;
                }
            }

            _blendShapeList.RefreshItems();
        }

        private void OnAnimationDragPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId == _animationDragPointerId)
            {
                EndAnimationDrag();
                evt.StopPropagation();
            }
        }

        private void OnAnimationDragPointerCaptureOut(
            PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == _animationDragPointerId)
            {
                ApplyAnimationDragAtPointer();
                _animationDragPointerId = -1;
                _animationDragLastIndex = -1;
                CompleteAnimationDrag();
            }
        }

        private void EndAnimationDrag()
        {
            ApplyAnimationDragAtPointer();
            var pointerId = _animationDragPointerId;
            _animationDragPointerId = -1;
            _animationDragLastIndex = -1;
            if (pointerId >= 0 && this.HasPointerCapture(pointerId))
            {
                this.ReleasePointer(pointerId);
            }

            CompleteAnimationDrag();
        }

        private void CompleteAnimationDrag()
        {
            if (_clipOnly.value)
            {
                RefreshFilter();
            }
        }

        private void BindBlendShapeRow(VisualElement element, int index)
        {
            if (element is BlendShapeRow row && index >= 0 && index < _visibleItems.Count)
            {
                row.SetItem(_visibleItems[index]);
            }
        }

        private void RefreshFilter()
        {
            _sectionHeader.SetTitle(_hasClip ? _sectionTitle : _libraryTitle);
            _backToLibrary.style.display =
                _hasClip || _canNavigateLibraryBack
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _clipOnly.style.display = _hasClip
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _search.style.display = _hasClip
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _animationControls.style.display = _hasClip
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (!_hasClip)
            {
                _visibleItems.Clear();
                _blendShapeList.itemsSource = (IList)_visibleItems;
                _blendShapeList.Rebuild();
                _blendShapeList.style.display = DisplayStyle.None;
                _library.style.display = DisplayStyle.Flex;
                _empty.style.display = DisplayStyle.None;
                return;
            }

            _library.style.display = DisplayStyle.None;

            var query = (_search?.Value ?? string.Empty).Trim();
            var visibleChannels = string.IsNullOrEmpty(query) && !_clipOnly.value
                ? _channels.ToList()
                : FilterWithHeaders(
                    _channels,
                    query,
                    _clipOnly.value);
            _visibleItems = BlendShapeRowItem.Create(
                    visibleChannels,
                    _namingRule,
                    _hideHeaders,
                    nestSides: !_clipOnly.value)
                .ToList();
            _blendShapeList.itemsSource = (IList)_visibleItems;
            _blendShapeList.Rebuild();
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
        private readonly Toggle _toggle;
        private readonly UiTextElement _name;
        private readonly VisualElement _controls;
        private readonly Slider _slider;
        private readonly FloatField _value;
        private BlendShapeRowItem _item;
        private bool _rendering;

        public BlendShapeRow()
        {
            AddToClassList("ee4v-face-expression-row");
            _toggle = UiTextFactory.CreateToggle();
            _toggle.AddToClassList(
                "ee4v-face-expression-row__animation-toggle");
            _toggle.RegisterValueChangedCallback(evt =>
            {
                var channel = _item?.ActiveChannel;
                if (!_rendering && channel != null)
                {
                    AnimationChanged?.Invoke(channel, evt.newValue);
                }
            });
            RegisterCallback<PointerDownEvent>(
                OnTogglePointerDown,
                TrickleDown.TrickleDown);
            Add(_toggle);
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
        }

        public event Action<BlendShapeChannel> Changed;
        public event Action<BlendShapeChannel, bool> AnimationChanged;
        public event Action<BlendShapeChannel, int, bool, float>
            AnimationDragStarted;

        public void SetItem(BlendShapeRowItem item)
        {
            _item = item;
            var isHeader = item?.IsHeader == true;
            EnableInClassList("ee4v-face-expression-row--header", isHeader);
            EnableInClassList(
                "ee4v-face-expression-row--child",
                item?.IsChild == true);
            _name.SetText(item?.DisplayName ?? string.Empty);
            _name.tooltip = item?.Tooltip ?? string.Empty;
            _toggle.style.display = isHeader ? DisplayStyle.None : DisplayStyle.Flex;
            _controls.style.display = isHeader ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshControls();
        }

        private void RefreshControls()
        {
            var channel = _item?.ActiveChannel;
            _rendering = true;
            _toggle.SetValueWithoutNotify(channel?.Animated == true);
            _slider.SetValueWithoutNotify(channel?.Value ?? 0f);
            _value.SetValueWithoutNotify(channel?.Value ?? 0f);
            _rendering = false;
        }

        private void OnTogglePointerDown(PointerDownEvent evt)
        {
            if (evt.button != (int)MouseButton.LeftMouse ||
                _item?.ActiveChannel == null ||
                _item.IsHeader ||
                !(evt.target is VisualElement target) ||
                !_toggle.Contains(target))
            {
                return;
            }

            _toggle.Focus();
            var channel = _item.ActiveChannel;
            AnimationDragStarted?.Invoke(
                channel,
                evt.pointerId,
                !channel.Animated,
                evt.position.y);
            evt.PreventDefault();
            evt.StopPropagation();
        }

        private void SetValue(float value)
        {
            var channel = _item?.ActiveChannel;
            if (_rendering || channel == null)
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
            _toggle.SetValueWithoutNotify(channel.Animated);
            _slider.SetValueWithoutNotify(channel.Value);
            _value.SetValueWithoutNotify(channel.Value);
            _rendering = false;
            Changed?.Invoke(channel);
        }
    }
}
