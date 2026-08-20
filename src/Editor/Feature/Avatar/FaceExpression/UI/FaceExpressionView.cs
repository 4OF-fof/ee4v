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
        private readonly Toggle _clipOnly;
        private readonly ListView _blendShapeList;
        private readonly UiTextElement _empty;
        private readonly string _noBlendShapes;
        private readonly string _clipRequired;
        private List<BlendShapeChannel> _channels = new List<BlendShapeChannel>();
        private List<BlendShapeChannel> _visibleChannels = new List<BlendShapeChannel>();
        private bool _rendering;
        private bool _hasClip;

        public FaceExpressionView(FaceExpressionViewText text, Action<Rect> drawPreview)
        {
            text = text ?? new FaceExpressionViewText();
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
            sectionHeader.Add(UiTextFactory.Create(
                text.BlendShapes,
                UiClassNames.SectionTitle));
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
            _blendShapeList.Q<ScrollView>()?.verticalScroller.AddToClassList(
                "ee4v-face-expression__vertical-scroller");
            editorPane.Add(_blendShapeList);
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

        public void SetChannels(IReadOnlyList<BlendShapeChannel> channels)
        {
            _channels = channels?.ToList() ?? new List<BlendShapeChannel>();
            RefreshFilter();
        }

        private VisualElement CreateBlendShapeRow()
        {
            var row = new BlendShapeRow();
            row.Changed += channel => ChannelChanged?.Invoke(channel);
            row.AnimationChanged += () =>
            {
                if (_clipOnly.value)
                {
                    RefreshFilter();
                }
            };
            return row;
        }

        private void BindBlendShapeRow(VisualElement element, int index)
        {
            if (element is BlendShapeRow row && index >= 0 && index < _visibleChannels.Count)
            {
                row.SetChannel(_visibleChannels[index]);
            }
        }

        private void RefreshFilter()
        {
            if (!_hasClip)
            {
                _visibleChannels.Clear();
                _blendShapeList.itemsSource = (IList)_visibleChannels;
                _blendShapeList.Rebuild();
                _blendShapeList.style.display = DisplayStyle.None;
                _empty.SetText(_clipRequired);
                _empty.style.display = DisplayStyle.Flex;
                return;
            }

            var query = (_search?.Value ?? string.Empty).Trim();
            _visibleChannels = string.IsNullOrEmpty(query) && !_clipOnly.value
                ? _channels.ToList()
                : FilterWithHeaders(
                    _channels,
                    query,
                    _clipOnly.value);
            _blendShapeList.itemsSource = (IList)_visibleChannels;
            _blendShapeList.Rebuild();
            var hasItems = _visibleChannels.Count > 0;
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
            for (var index = 0; index < channels.Count; index++)
            {
                var channel = channels[index];
                if (channel.IsHeader)
                {
                    header = channel;
                    headerAdded = false;
                    headerMatches = !string.IsNullOrEmpty(query) &&
                                    channel.HeaderText.IndexOf(
                                        query,
                                        StringComparison.OrdinalIgnoreCase) >= 0;
                    continue;
                }

                if ((clipOnly && !channel.Animated) ||
                    (!string.IsNullOrEmpty(query) &&
                     !headerMatches &&
                     channel.Name.IndexOf(
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

    internal sealed class BlendShapeRow : VisualElement
    {
        private readonly Toggle _toggle;
        private readonly UiTextElement _name;
        private readonly VisualElement _controls;
        private readonly Slider _slider;
        private readonly FloatField _value;
        private BlendShapeChannel _channel;
        private bool _rendering;

        public BlendShapeRow()
        {
            AddToClassList("ee4v-face-expression-row");
            _toggle = UiTextFactory.CreateToggle();
            _toggle.AddToClassList(
                "ee4v-face-expression-row__animation-toggle");
            _toggle.RegisterValueChangedCallback(evt =>
            {
                if (_rendering || _channel == null)
                {
                    return;
                }

                _channel.Animated = evt.newValue;
                Changed?.Invoke(_channel);
                AnimationChanged?.Invoke();
            });
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
        public event Action AnimationChanged;

        public void SetChannel(BlendShapeChannel channel)
        {
            _channel = channel;
            var isHeader = channel?.IsHeader == true;
            _rendering = true;
            EnableInClassList("ee4v-face-expression-row--header", isHeader);
            _name.SetText(isHeader
                ? channel.HeaderText
                : channel?.Name ?? string.Empty);
            _toggle.SetValueWithoutNotify(channel?.Animated == true);
            _slider.SetValueWithoutNotify(channel?.Value ?? 0f);
            _value.SetValueWithoutNotify(channel?.Value ?? 0f);
            _toggle.style.display = isHeader ? DisplayStyle.None : DisplayStyle.Flex;
            _controls.style.display = isHeader ? DisplayStyle.None : DisplayStyle.Flex;
            _rendering = false;
        }

        private void SetValue(float value)
        {
            if (_rendering || _channel == null)
            {
                return;
            }

            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return;
            }

            _rendering = true;
            _channel.Value = value;
            _channel.Animated = true;
            _toggle.SetValueWithoutNotify(true);
            _slider.SetValueWithoutNotify(_channel.Value);
            _value.SetValueWithoutNotify(_channel.Value);
            _rendering = false;
            Changed?.Invoke(_channel);
        }
    }
}
