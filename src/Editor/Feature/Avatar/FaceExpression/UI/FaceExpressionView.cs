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
    }

    internal sealed class FaceExpressionView : VisualElement
    {
        private const float LibraryItemLabelHeight = 30f;
        private const float LibraryFolderIconSize = 80f;

        private readonly ObjectField _avatarField;
        private readonly ObjectField _clipField;
        private readonly SearchField _search;
        private readonly SectionHeader _sectionHeader;
        private readonly UiButton _backToLibrary;
        private readonly Toggle _clipOnly;
        private readonly ListView _blendShapeList;
        private readonly ScrollView _blendShapeScrollView;
        private readonly ScrollView _library;
        private readonly EmptyState _empty;
        private readonly string _defaultSectionTitle;
        private readonly string _libraryTitle;
        private readonly string _newClip;
        private readonly string _noBlendShapes;
        private string _sectionTitle;
        private List<BlendShapeChannel> _channels = new List<BlendShapeChannel>();
        private List<BlendShapeRowItem> _visibleItems = new List<BlendShapeRowItem>();
        private BlendShapeNamingRule _namingRule;
        private bool _hideHeaders;
        private bool _rendering;
        private bool _hasClip;
        private bool _canNavigateLibraryBack;
        private int _animationDragPointerId = -1;
        private bool _animationDragValue;
        private float _animationDragPointerY;
        private int _animationDragLastIndex = -1;

        public FaceExpressionView(FaceExpressionViewText text, Action<Rect> drawPreview)
        {
            text = text ?? new FaceExpressionViewText();
            _defaultSectionTitle = text.BlendShapes ?? string.Empty;
            _libraryTitle = text.Library ?? string.Empty;
            _newClip = text.NewClip ?? string.Empty;
            _noBlendShapes = text.NoBlendShapes ?? string.Empty;
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
            toolbar.Leading.Add(_avatarField);

            _clipField = UiTextFactory.CreateObjectField(
                text.Clip,
                "ee4v-face-expression__asset-field");
            _clipField.AddToClassList(
                "ee4v-face-expression__asset-field--last");
            _clipField.objectType = typeof(AnimationClip);
            _clipField.allowSceneObjects = false;
            _clipField.RegisterValueChangedCallback(evt =>
            {
                if (!_rendering)
                {
                    ClipChanged?.Invoke(evt.newValue as AnimationClip);
                }
            });
            toolbar.Leading.Add(_clipField);
            Add(toolbar);

            var content = new VisualElement();
            content.AddToClassList("ee4v-face-expression__content");
            var previewPane = new PreviewSurface();
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
                UiButtonVariant.Ghost,
                compact: true);
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
                UiButtonVariant.Ghost,
                compact: true);
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
        public event Action BackRequested;
        public event Action<string> LibraryFolderRequested;
        public event Action<BlendShapeChannel> ChannelChanged;
        public event Action ResetViewRequested;

        public void SetAvatar(GameObject avatar)
        {
            _rendering = true;
            _avatarField.SetValueWithoutNotify(avatar);
            _rendering = false;
        }

        public void SetClip(AnimationClip clip)
        {
            _hasClip = clip != null;
            _rendering = true;
            _clipField.SetValueWithoutNotify(clip);
            _rendering = false;
            RefreshFilter();
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
            return CreateLibraryItem(
                clip.name,
                () => ClipChanged?.Invoke(clip),
                preview);
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
            var item = UiTextFactory.CreateButton(onClick: selected);
            item.AddToClassList("ee4v-face-expression__library-item");
            item.tooltip = nameText;
            item.Add(preview);

            var name = UiTextFactory.Create(
                nameText,
                "ee4v-face-expression__library-name");
            name.SetTextAlign(TextAnchor.MiddleCenter);
            name.style.alignItems = Align.Center;
            name.style.justifyContent = Justify.Center;
            name.pickingMode = PickingMode.Ignore;
            item.Add(name);
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
                row.SetItem(_visibleItems[index], _clipOnly.value);
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
                    groupSides: !_clipOnly.value)
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
        private readonly List<BlendShapeOption> _options =
            new List<BlendShapeOption>();

        private BlendShapeRowItem(
            BlendShapeChannel channel,
            BlendShapeName parsedName,
            bool showChannelName = false)
        {
            if (channel.IsHeader)
            {
                Header = channel;
                DisplayName = channel.DisplayHeaderText;
                return;
            }

            DisplayName = parsedName == null || showChannelName
                ? channel.DisplayName
                : string.IsNullOrEmpty(channel.RendererDisplayName)
                    ? parsedName.Role
                    : channel.RendererDisplayName + " / " + parsedName.Role;
            AddChannel(channel, parsedName);
        }

        internal BlendShapeChannel Header { get; }
        internal bool IsHeader => Header != null;
        internal string DisplayName { get; }
        internal BlendShapeChannel ActiveChannel { get; private set; }
        internal string SelectedSide => FindActive()?.Side ?? string.Empty;
        internal string Tooltip => IsHeader
            ? Header.DisplayHeaderText
            : string.Join(", ", _options
                .Select(option => option.Channel.DisplayName)
                .Distinct(StringComparer.Ordinal));

        internal static IReadOnlyList<BlendShapeRowItem> Create(
            IReadOnlyList<BlendShapeChannel> channels,
            BlendShapeNamingRule namingRule,
            bool hideHeaders,
            bool groupSides = true)
        {
            var result = new List<BlendShapeRowItem>();
            var grouped = new Dictionary<string, List<BlendShapeRowItem>>(
                StringComparer.Ordinal);
            for (var index = 0; index < (channels?.Count ?? 0); index++)
            {
                var channel = channels[index];
                if (channel.IsHeader)
                {
                    grouped.Clear();
                    if (!hideHeaders)
                    {
                        result.Add(new BlendShapeRowItem(channel, null));
                    }

                    continue;
                }

                if (namingRule == null ||
                    !namingRule.TryParse(channel, out var parsedName))
                {
                    result.Add(new BlendShapeRowItem(channel, null));
                    continue;
                }

                if (!groupSides)
                {
                    result.Add(new BlendShapeRowItem(
                        channel,
                        parsedName,
                        showChannelName: true));
                    continue;
                }

                var key = channel.RendererPath + "\n" + parsedName.Role;
                if (!grouped.TryGetValue(key, out var rows))
                {
                    rows = new List<BlendShapeRowItem>();
                    grouped.Add(key, rows);
                }

                var added = false;
                for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    if (rows[rowIndex].AddChannel(channel, parsedName))
                    {
                        added = true;
                        break;
                    }
                }

                if (added)
                {
                    continue;
                }

                var row = new BlendShapeRowItem(channel, parsedName);
                rows.Add(row);
                result.Add(row);
            }

            for (var index = 0; index < result.Count; index++)
            {
                result[index].SelectDefault();
            }

            return result;
        }

        internal bool HasSide(string side)
        {
            return _options.Any(option => string.Equals(
                option.Side,
                side,
                StringComparison.Ordinal));
        }

        internal void ToggleSide(string side)
        {
            var nextSide = string.Equals(
                SelectedSide,
                side,
                StringComparison.Ordinal)
                ? string.Empty
                : side;
            ActiveChannel = FindChannel(nextSide) ??
                            ActiveChannel;
        }

        private bool AddChannel(
            BlendShapeChannel channel,
            BlendShapeName parsedName)
        {
            var side = parsedName?.Side ?? string.Empty;
            if (_options.Any(option =>
                    string.Equals(option.Side, side, StringComparison.Ordinal)))
            {
                return false;
            }

            _options.Add(new BlendShapeOption(channel, side));
            ActiveChannel = ActiveChannel ?? channel;
            return true;
        }

        private void SelectDefault()
        {
            if (IsHeader || _options.Count == 0)
            {
                return;
            }

            ActiveChannel = FindChannel(string.Empty) ??
                            _options[0].Channel;
        }

        private BlendShapeChannel FindChannel(string side)
        {
            return _options.FirstOrDefault(option =>
                string.Equals(option.Side, side, StringComparison.Ordinal))
                ?.Channel;
        }

        private BlendShapeOption FindActive()
        {
            return _options.FirstOrDefault(option =>
                ReferenceEquals(option.Channel, ActiveChannel));
        }

        private sealed class BlendShapeOption
        {
            internal BlendShapeOption(
                BlendShapeChannel channel,
                string side)
            {
                Channel = channel;
                Side = side;
            }

            internal BlendShapeChannel Channel { get; }
            internal string Side { get; }
        }
    }

    internal sealed class BlendShapeRow : VisualElement
    {
        private readonly Toggle _toggle;
        private readonly UiTextElement _name;
        private readonly VisualElement _controls;
        private readonly VisualElement _options;
        private readonly Toggle _left;
        private readonly Toggle _right;
        private readonly Slider _slider;
        private readonly FloatField _value;
        private BlendShapeRowItem _item;
        private bool _rendering;
        private bool _optionsReadOnly;

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
            _options = new VisualElement();
            _options.AddToClassList("ee4v-face-expression-row__options");
            _controls.Add(_options);
            _right = CreateSideToggle("R");
            _options.Add(_right);
            _left = CreateSideToggle("L");
            _options.Add(_left);
        }

        public event Action<BlendShapeChannel> Changed;
        public event Action<BlendShapeChannel, bool> AnimationChanged;
        public event Action<BlendShapeChannel, int, bool, float>
            AnimationDragStarted;

        public void SetItem(BlendShapeRowItem item, bool optionsReadOnly)
        {
            _item = item;
            _optionsReadOnly = optionsReadOnly;
            var isHeader = item?.IsHeader == true;
            EnableInClassList("ee4v-face-expression-row--header", isHeader);
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
            SetSideToggle(_left, "L");
            SetSideToggle(_right, "R");
            _toggle.SetValueWithoutNotify(channel?.Animated == true);
            _slider.SetValueWithoutNotify(channel?.Value ?? 0f);
            _value.SetValueWithoutNotify(channel?.Value ?? 0f);
            _rendering = false;
        }

        private Toggle CreateSideToggle(string side)
        {
            var toggle = UiTextFactory.CreateToggle(
                side,
                "ee4v-face-expression-row__side");
            toggle.RegisterValueChangedCallback(_ =>
            {
                if (_rendering || _item == null)
                {
                    return;
                }

                _item.ToggleSide(side);
                RefreshControls();
            });
            return toggle;
        }

        private void SetSideToggle(Toggle toggle, string side)
        {
            var visible = _item?.HasSide(side) == true;
            toggle.style.visibility = visible
                ? Visibility.Visible
                : Visibility.Hidden;
            toggle.SetEnabled(visible && !_optionsReadOnly);
            toggle.SetValueWithoutNotify(visible && string.Equals(
                _item.SelectedSide,
                side,
                StringComparison.Ordinal));
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
            channel.Animated = !Mathf.Approximately(
                channel.Value,
                channel.InitialValue);
            _toggle.SetValueWithoutNotify(channel.Animated);
            _slider.SetValueWithoutNotify(channel.Value);
            _value.SetValueWithoutNotify(channel.Value);
            _rendering = false;
            Changed?.Invoke(channel);
        }
    }
}
