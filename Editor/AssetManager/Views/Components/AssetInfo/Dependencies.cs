using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Presenter;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.AssetInfo {
    public class Dependencies : VisualElement {
        private readonly VisualElement _container;

        public Dependencies() {
            var header = new Label(I18N.Get("UI.AssetManager.AssetInfo.Dependencies"))
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            Add(header);

            _container = new VisualElement
                { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginBottom = 10 } };
            Add(_container);

            var addButton = new Button(() => OnAddRequested?.Invoke()) {
                text = I18N.Get("UI.AssetManager.AssetInfo.AddDependency"),
                style = {
                    backgroundColor = ColorPreset.TagPillBackgroundStyle,
                    borderTopLeftRadius = 10, borderTopRightRadius = 10, borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    paddingLeft = 10, paddingRight = 10, paddingTop = 4, paddingBottom = 4, height = 24,
                    borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                    marginBottom = 10, width = Length.Percent(100)
                }
            };

            addButton.RegisterCallback<MouseEnterEvent>(_ =>
            {
                addButton.style.backgroundColor = ColorPreset.TagPillHoverStyle;
            });
            addButton.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                addButton.style.backgroundColor = ColorPreset.TagPillBackgroundStyle;
            });

            Add(addButton);
        }

        public event Action OnAddRequested;
        public event Action<Ulid> OnRemoveRequested;
        public event Action<Ulid> OnClicked;

        public void SetVisible(bool dependenciesVisible) {
            style.display = dependenciesVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetDependencies(IReadOnlyList<DependencyDisplayData> dependencies) {
            _container.Clear();
            if (dependencies == null) return;

            foreach (var dep in dependencies) _container.Add(CreatePill(dep));
        }

        private VisualElement CreatePill(DependencyDisplayData dep) {
            var pill = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row, backgroundColor = ColorPreset.DropFolderArea,
                    borderTopLeftRadius = 10, borderTopRightRadius = 10, borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    paddingLeft = 8, paddingRight = 4, paddingTop = 2, paddingBottom = 2,
                    marginRight = 4, marginBottom = 4, alignItems = Align.Center, maxWidth = Length.Percent(100),
                    overflow = Overflow.Hidden
                }
            };

            pill.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                OnClicked?.Invoke(dep.Id);
                evt.StopPropagation();
            });

            pill.RegisterCallback<MouseEnterEvent>(_ =>
            {
                pill.style.backgroundColor = ColorPreset.SMouseOverBackground;
            });
            pill.RegisterCallback<MouseLeaveEvent>(_ => { pill.style.backgroundColor = ColorPreset.DropFolderArea; });

            pill.Add(new Label(dep.Name) {
                style = {
                    marginRight = 4, flexShrink = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap
                }
            });

            var removeBtn = new Button(() => OnRemoveRequested?.Invoke(dep.Id)) {
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
                removeBtn.style.backgroundColor = ColorPreset.TabCloseButtonHover;
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
    }
}