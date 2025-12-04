using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.AssetInfo {
    public class Tags : VisualElement {
        private readonly VisualElement _tagsContainer;
        private IAssetRepository _repository;

        public Tags() {
            Add(new Label(I18N.Get("UI.AssetManager.AssetInfo.Tags"))
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } });

            _tagsContainer = new VisualElement
                { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginBottom = 10 } };
            Add(_tagsContainer);

            var addButton = CreateButton(I18N.Get("UI.AssetManager.AssetInfo.AddTag"), () =>
            {
                if (_repository == null) return;
                OnTagAdded?.Invoke("");
            });
            Add(addButton);
        }

        public event Action<string> OnTagAdded;
        public event Action<string> OnTagRemoved;
        public event Action<string> OnTagClicked;

        public void SetRepository(IAssetRepository repo) {
            _repository = repo;
        }

        public void SetTags(IReadOnlyList<string> tags) {
            _tagsContainer.Clear();
            if (tags == null) return;

            foreach (var tag in tags) _tagsContainer.Add(CreateTagPill(tag));
        }

        private VisualElement CreateTagPill(string tag) {
            var pill = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row, backgroundColor = ColorPreset.TagPillBackground,
                    borderTopLeftRadius = 10, borderTopRightRadius = 10, borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    paddingLeft = 8, paddingRight = 4, paddingTop = 2, paddingBottom = 2, marginRight = 4,
                    marginBottom = 4,
                    alignItems = Align.Center, maxWidth = Length.Percent(100), overflow = Overflow.Hidden
                }
            };

            pill.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                OnTagClicked?.Invoke(tag);
                evt.StopPropagation();
            });

            pill.RegisterCallback<MouseEnterEvent>(_ =>
            {
                pill.style.backgroundColor = ColorPreset.TagPillHover;
            });
            pill.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                pill.style.backgroundColor = ColorPreset.TagPillBackground;
            });

            pill.Add(new Label(tag) {
                style = {
                    marginRight = 4, flexShrink = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap
                }
            });

            var removeBtn = new Button(() => OnTagRemoved?.Invoke(tag)) {
                text = "×",
                style = {
                    width = 16, height = 16, fontSize = 10, backgroundColor = Color.clear,
                    borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                    paddingLeft = 0, paddingRight = 0, flexShrink = 0
                }
            };

            removeBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            removeBtn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                removeBtn.style.backgroundColor = ColorPreset.CloseIcon;
                removeBtn.style.color = ColorPreset.TextColor;
            });
            removeBtn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                removeBtn.style.backgroundColor = Color.clear;
                removeBtn.style.color = new StyleColor(StyleKeyword.Null);
            });

            pill.Add(removeBtn);
            return pill;
        }

        private static Button CreateButton(string text, Action onClick) {
            var btn = new Button(onClick) {
                text = text,
                style = {
                    backgroundColor = ColorPreset.TagPillBackground,
                    borderTopLeftRadius = 10, borderTopRightRadius = 10, borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    paddingLeft = 10, paddingRight = 10, paddingTop = 4, paddingBottom = 4, height = 24,
                    borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                    marginBottom = 10, width = Length.Percent(100)
                }
            };

            btn.RegisterCallback<MouseEnterEvent>(_ => { btn.style.backgroundColor = ColorPreset.TagPillHover; });
            btn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                btn.style.backgroundColor = ColorPreset.TagPillBackground;
            });

            return btn;
        }
    }
}