using System;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.TagListView {
    public sealed class TagItem : Button {
        public TagItem(string displayName, string fullPath, int count, Action<string> onSelect,
            Action<string, VisualElement> onRightClick) {
            text = I18N.Get("UI.AssetManager.TagListView.TagWithCountFmt", displayName, count);
            tooltip = fullPath;

            style.height = 24;
            style.borderTopLeftRadius = 12;
            style.borderTopRightRadius = 12;
            style.borderBottomLeftRadius = 12;
            style.borderBottomRightRadius = 12;
            style.paddingLeft = 10;
            style.paddingRight = 10;
            style.marginRight = 4;
            style.marginBottom = 4;
            style.backgroundColor = ColorPreset.TagPillBackground;
            style.borderTopWidth = 0;
            style.borderBottomWidth = 0;
            style.borderLeftWidth = 0;
            style.borderRightWidth = 0;
            style.color = ColorPreset.TextColor;
            style.fontSize = 11;

            clicked += () => onSelect?.Invoke(fullPath);

            RegisterCallback<MouseEnterEvent>(_ =>
                style.backgroundColor = ColorPreset.TagPillHover);
            RegisterCallback<MouseLeaveEvent>(_ =>
                style.backgroundColor = ColorPreset.TagPillBackground);

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 1) return;
                evt.StopPropagation();
                onRightClick?.Invoke(fullPath, this);
            });
        }
    }
}