using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.State;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.Navigation {
    public class SystemFolderList : VisualElement {
        private readonly List<Label> _navLabels = new();
        private Label _selectedLabel;

        public event Action<NavigationMode, string, Func<AssetMetadata, bool>> OnNavigationRequested;
        public event Action OnTagListRequested;

        public SystemFolderList() {
            style.flexDirection = FlexDirection.Column;
            style.marginBottom = 10;

            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.AllItems"),
                () => FireNav(NavigationMode.AllItems, I18N.Get("UI.AssetManager.Navigation.AllItemsContext"), a => !a.IsDeleted));
            
            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.BoothItems"), () => {
                FireNav(NavigationMode.BoothItems, I18N.Get("UI.AssetManager.Navigation.BoothItemsContext"), a => !a.IsDeleted);
            });

            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.Backups"),
                () => FireNav(NavigationMode.Backups, I18N.Get("UI.AssetManager.Navigation.BackupsContext"), a => !a.IsDeleted));

            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.Uncategorized"),
                () => FireNav(NavigationMode.Uncategorized, I18N.Get("UI.AssetManager.Navigation.UncategorizedContext"), 
                    a => !a.IsDeleted && a.Folder == Ulid.Empty && (a.Tags == null || a.Tags.Count == 0)));

            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.TagList"), () => OnTagListRequested?.Invoke());

            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.Trash"),
                () => FireNav(NavigationMode.Trash, I18N.Get("UI.AssetManager.Navigation.TrashContext"), a => a.IsDeleted));
        }

        public void SelectByIndex(int index) {
            if (index >= 0 && index < _navLabels.Count) {
                SetSelected(_navLabels[index]);
            }
        }

        public void ClearSelection() {
            if (_selectedLabel == null) return;
            _selectedLabel.RemoveFromClassList("selected");
            _selectedLabel.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            _selectedLabel.style.color = new StyleColor(StyleKeyword.Null);
            _selectedLabel = null;
        }

        private void CreateNavLabel(string text, Action onClick) {
            var label = new Label(text) {
                userData = onClick,
                style = {
                    paddingLeft = 8, paddingRight = 8, paddingTop = 4, paddingBottom = 4,
                    marginBottom = 2, unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            label.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button != 0) return;
                SetSelected(label);
                onClick?.Invoke();
                evt.StopPropagation();
            });
            _navLabels.Add(label);
            Add(label);
        }

        private void SetSelected(Label label) {
            ClearSelection();
            _selectedLabel = label;
            if (_selectedLabel == null) return;
            _selectedLabel.AddToClassList("selected");
            _selectedLabel.style.backgroundColor = ColorPreset.AccentBlue40Style;
            _selectedLabel.style.color = ColorPreset.AccentBlue;
        }

        private void FireNav(NavigationMode mode, string context, Func<AssetMetadata, bool> filter) {
            OnNavigationRequested?.Invoke(mode, context, filter);
        }
    }
}