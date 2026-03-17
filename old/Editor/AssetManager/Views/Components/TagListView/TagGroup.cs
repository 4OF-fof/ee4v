using System;
using _4OF.ee4v.Core.i18n;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.TagListView {
    public class TagGroup : Foldout {
        public TagGroup(string groupName, int count, bool isOpen, Action<bool> onToggle) {
            text = I18N.Get("UI.AssetManager.TagListView.GroupNameFmt", groupName, count);
            value = isOpen;

            style.marginLeft = 0;
            style.marginTop = 8;
            style.marginBottom = 4;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            style.fontSize = 24;

            var toggle = this.Q<Toggle>();
            if (toggle != null) toggle.style.fontSize = 24;

            this.RegisterValueChangedCallback(evt => onToggle?.Invoke(evt.newValue));

            var container = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginBottom = 4
                }
            };
            contentContainer.Add(container);
        }

        public sealed override VisualElement contentContainer => base.contentContainer;
    }
}