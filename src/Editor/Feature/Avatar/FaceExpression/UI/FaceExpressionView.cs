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
        public string SearchPlaceholder { get; set; }
        public string SearchTooltip { get; set; }
        public string ClearSearchTooltip { get; set; }
        public string BlendShapes { get; set; }
        public string ClipOnly { get; set; }
        public string ClipOnlyTooltip { get; set; }
        public string NoBlendShapes { get; set; }
        public string ClipRequired { get; set; }
    }

    internal sealed class FaceExpressionView : VisualElement
    {
        private readonly ObjectField _avatarField;
        private readonly ObjectField _clipField;
        private readonly SearchField _search;
        private readonly UiTextElement _sectionTitle;
        private readonly Toggle _clipOnly;
        private readonly ListView _blendShapeList;
        private readonly ScrollView _blendShapeScrollView;
        private readonly UiTextElement _empty;
        private readonly string _defaultSectionTitle;
        private readonly string _noBlendShapes;
        private readonly string _clipRequired;
        private List<BlendShapeChannel> _channels = new List<BlendShapeChannel>();
        private List<BlendShapeRowItem> _visibleItems = new List<BlendShapeRowItem>();
        private BlendShapeNamingRule _namingRule;
        private bool _hideHeaders;
        private bool _rendering;
        private bool _hasClip;
        private int _animationDragPointerId = -1;
        private bool _animationDragValue;
        private float _animationDragPointerY;
        private int _animationDragLastIndex = -1;

        public FaceExpressionView(FaceExpressionViewText text, Action<Rect> drawPreview)
        {
            text = text ?? new FaceExpressionViewText();
            _defaultSectionTitle = text.BlendShapes ?? string.Empty;
            _noBlendShapes = text.NoBlendShapes ?? string.Empty;
            _clipRequired = text.ClipRequired ?? string.Empty;
            AddToClassList("ee4v-face-expression");

            var toolbar = new VisualElement();
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
            toolbar.Add(_avatarField);

            _clipField = UiTextFactory.CreateObjectField(
                text.Clip,
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
            toolbar.Add(_clipField);
            var create = UiTextFactory.CreateButton(
                text.NewClip,
                () => NewClipRequested?.Invoke());
            create.AddToClassList("ee4v-face-expression__new-clip");
            toolbar.Add(create);
            Add(toolbar);

            var content = new VisualElement();
            content.AddToClassList("ee4v-face-expression__content");
            var previewPane = new VisualElement();
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
            previewPane.Add(preview);
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
            previewPane.Add(resetView);
            content.Add(previewPane);

            var editorPane = new VisualElement();
            editorPane.AddToClassList("ee4v-face-expression__editor-pane");
            var sectionHeader = new VisualElement();
            sectionHeader.AddToClassList("ee4v-face-expression__section-header");
            _sectionTitle = UiTextFactory.Create(
                _defaultSectionTitle,
                UiClassNames.SectionTitle);
            sectionHeader.Add(_sectionTitle);
            _clipOnly = UiTextFactory.CreateToggle(
                text.ClipOnly,
                "ee4v-face-expression__clip-only");
            _clipOnly.tooltip = text.ClipOnlyTooltip;
            _clipOnly.RegisterValueChangedCallback(_ => RefreshFilter());
            sectionHeader.Add(_clipOnly);
            editorPane.Add(sectionHeader);
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
                _blendShapeScrollView.verticalScroller.AddToClassList(
                    "ee4v-face-expression__vertical-scroller");
                _blendShapeScrollView.verticalScroller.valueChanged += _ =>
                {
                    if (_animationDragPointerId >= 0)
                    {
                        ApplyAnimationDragAtPointer();
                    }
                };
            }
            editorPane.Add(_blendShapeList);
            RegisterCallback<PointerMoveEvent>(OnAnimationDragPointerMove);
            RegisterCallback<PointerUpEvent>(OnAnimationDragPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(
                OnAnimationDragPointerCaptureOut);
            _empty = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-face-expression__empty");
            editorPane.Add(_empty);
            content.Add(editorPane);
            Add(content);
        }

        public event Action<GameObject> AvatarChanged;
        public event Action<AnimationClip> ClipChanged;
        public event Action NewClipRequested;
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
            _sectionTitle.SetText(string.IsNullOrEmpty(sectionTitle)
                ? _defaultSectionTitle
                : sectionTitle);
            RefreshFilter();
        }

        private VisualElement CreateBlendShapeRow()
        {
            var row = new BlendShapeRow();
            row.Changed += channel => ChannelChanged?.Invoke(channel);
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
            if (!_hasClip)
            {
                _visibleItems.Clear();
                _blendShapeList.itemsSource = (IList)_visibleItems;
                _blendShapeList.Rebuild();
                _blendShapeList.style.display = DisplayStyle.None;
                _empty.SetText(_clipRequired);
                _empty.style.display = DisplayStyle.Flex;
                return;
            }

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
                    groupOptions: !_clipOnly.value)
                .ToList();
            _blendShapeList.itemsSource = (IList)_visibleItems;
            _blendShapeList.Rebuild();
            var hasItems = _visibleItems.Count > 0;
            _blendShapeList.style.display = hasItems ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.SetText(_noBlendShapes);
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
            BlendShapeName parsedName)
        {
            if (channel.IsHeader)
            {
                Header = channel;
                DisplayName = channel.DisplayHeaderText;
                return;
            }

            DisplayName = parsedName == null
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
        internal string SelectedVariation => FindActive()?.Variation ?? string.Empty;
        internal string SelectedSide => FindActive()?.Side ?? string.Empty;
        internal IReadOnlyList<string> Variations => _options
            .Select(option => option.Variation)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        internal string Tooltip => IsHeader
            ? Header.DisplayHeaderText
            : string.Join(", ", _options
                .Select(option => option.Channel.DisplayName)
                .Distinct(StringComparer.Ordinal));

        internal static IReadOnlyList<BlendShapeRowItem> Create(
            IReadOnlyList<BlendShapeChannel> channels,
            BlendShapeNamingRule namingRule,
            bool hideHeaders,
            bool groupOptions = true)
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

                if (!groupOptions)
                {
                    result.Add(new BlendShapeRowItem(channel, parsedName));
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
            return _options.Any(option =>
                string.Equals(
                    option.Variation,
                    SelectedVariation,
                    StringComparison.Ordinal) &&
                string.Equals(option.Side, side, StringComparison.Ordinal));
        }

        internal void SelectVariation(string variation)
        {
            var selectedSide = SelectedSide;
            ActiveChannel = FindChannel(variation, selectedSide) ??
                            FindChannel(variation, string.Empty) ??
                            _options.FirstOrDefault(option => string.Equals(
                                option.Variation,
                                variation,
                                StringComparison.Ordinal))?.Channel ??
                            ActiveChannel;
        }

        internal void ToggleSide(string side)
        {
            var nextSide = string.Equals(
                SelectedSide,
                side,
                StringComparison.Ordinal)
                ? string.Empty
                : side;
            ActiveChannel = FindChannel(SelectedVariation, nextSide) ??
                            ActiveChannel;
        }

        private bool AddChannel(
            BlendShapeChannel channel,
            BlendShapeName parsedName)
        {
            var variation = parsedName?.Variation ?? string.Empty;
            var side = parsedName?.Side ?? string.Empty;
            if (_options.Any(option =>
                    string.Equals(
                        option.Variation,
                        variation,
                        StringComparison.Ordinal) &&
                    string.Equals(option.Side, side, StringComparison.Ordinal)))
            {
                return false;
            }

            _options.Add(new BlendShapeOption(channel, variation, side));
            ActiveChannel = ActiveChannel ?? channel;
            return true;
        }

        private void SelectDefault()
        {
            if (IsHeader || _options.Count == 0)
            {
                return;
            }

            var firstVariation = _options[0].Variation;
            ActiveChannel = FindChannel(firstVariation, string.Empty) ??
                            _options[0].Channel;
        }

        private BlendShapeChannel FindChannel(string variation, string side)
        {
            return _options.FirstOrDefault(option =>
                string.Equals(
                    option.Variation,
                    variation,
                    StringComparison.Ordinal) &&
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
                string variation,
                string side)
            {
                Channel = channel;
                Variation = variation;
                Side = side;
            }

            internal BlendShapeChannel Channel { get; }
            internal string Variation { get; }
            internal string Side { get; }
        }
    }

    internal sealed class BlendShapeRow : VisualElement
    {
        private readonly Toggle _toggle;
        private readonly UiTextElement _name;
        private readonly VisualElement _controls;
        private readonly VisualElement _options;
        private readonly PopupField<string> _variation;
        private readonly UiTextElement _variationText;
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
            _variation = UiTextFactory.CreatePopupField(
                string.Empty,
                new List<string> { string.Empty },
                0,
                FormatVariation,
                FormatVariation,
                "ee4v-face-expression-row__variation");
            _variationText = UiTextFactory.Create(
                string.Empty,
                "ee4v-face-expression-row__variation-text");
            _variationText.pickingMode = PickingMode.Ignore;
            var variationInput = _variation.Q<VisualElement>(
                className: "unity-base-field__input");
            (variationInput ?? _variation).Insert(0, _variationText);
            _variation.RegisterValueChangedCallback(evt =>
            {
                if (_rendering || _item == null)
                {
                    return;
                }

                _item.SelectVariation(evt.newValue);
                RefreshControls();
            });
            _options.Add(_variation);
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
            var variations = _item?.Variations.ToList() ?? new List<string>();
            if (variations.Count == 0)
            {
                variations.Add(string.Empty);
            }

            _rendering = true;
            _variation.choices = variations;
            var selectedVariation =
                _item?.SelectedVariation ?? variations[0];
            _variation.SetValueWithoutNotify(selectedVariation);
            _variationText.SetText(FormatVariation(selectedVariation));
            var showVariation = variations.Count > 1 ||
                                (_optionsReadOnly &&
                                 !string.IsNullOrEmpty(selectedVariation));
            _variation.style.visibility = showVariation
                ? Visibility.Visible
                : Visibility.Hidden;
            _variation.SetEnabled(showVariation && !_optionsReadOnly);
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

        private static string FormatVariation(string variation)
        {
            return string.IsNullOrEmpty(variation) ? "-" : variation;
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
            _toggle.SetValueWithoutNotify(true);
            _slider.SetValueWithoutNotify(channel.Value);
            _value.SetValueWithoutNotify(channel.Value);
            _rendering = false;
            Changed?.Invoke(channel);
        }
    }
}
