using System;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.Navigation {
    public class NavigationFooter : VisualElement {
        public NavigationFooter() {
            var createBtn = new Button {
                text = I18N.Get("UI.AssetManager.Navigation.NewAsset"),
                style = {
                    marginTop = 15, marginBottom = 20, marginLeft = 4, marginRight = 4,
                    paddingLeft = 12, paddingRight = 12, paddingTop = 8, paddingBottom = 8,
                    borderTopLeftRadius = 16, borderTopRightRadius = 16,
                    borderBottomLeftRadius = 16, borderBottomRightRadius = 16,
                    backgroundColor = ColorPreset.InformationHover,
                    unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12,
                    borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0
                }
            };

            createBtn.RegisterCallback<PointerEnterEvent>(_ =>
                createBtn.style.backgroundColor = ColorPreset.InformationHover);
            createBtn.RegisterCallback<PointerLeaveEvent>(_ =>
                createBtn.style.backgroundColor = ColorPreset.Information);
            createBtn.clicked += () => OnCreateAssetRequested?.Invoke();

            Add(createBtn);
        }

        public event Action OnCreateAssetRequested;
    }
}