using System;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetCollectionCreationPopup : CustomPopupWindow
    {
        private static readonly Vector2 PopupSize =
            new Vector2(620f, 520f);

        private Func<string, AssetFilterNode, bool> _save;
        private AssetCollection _initialCollection;
        private AssetManagerTextField _name;
        private AssetFilterEditor _filterEditor;
        private UiTextElement _error;

        public static void Show(
            VisualElement anchor,
            AssetCollection initialCollection,
            Func<string, AssetFilterNode, bool> save)
        {
            if (anchor == null || save == null)
            {
                return;
            }

            var window = CreateInstance<AssetCollectionCreationPopup>();
            window._initialCollection = initialCollection;
            window._save = save;
            window.ShowAsPopup(anchor, PopupSize);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            AssetManagerWindowSession.PrepareRoot(root);
            root.AddToClassList("ee4v-asset-manager");
            ConfigureCloseAndSubmitKeys(root, Submit);

            var popup = new CustomPopup(
                _initialCollection == null
                    ? I18N.Get("common.newCollection")
                    : _initialCollection.Name,
                showFooter: true,
                closeTooltip: I18N.Get("action.cancel"));
            var form = new ScrollView(ScrollViewMode.Vertical);
            form.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            form.AddToClassList(
                "ee4v-asset-manager__collection-popup-form");

            _name = AssetManagerControls.CreateTextField(
                I18N.Get("field.name"));
            _name.value = _initialCollection?.Name ?? string.Empty;
            form.Add(_name);

            form.Add(UiTextFactory.Create(
                I18N.Get("filterEditor.conditions"),
                UiClassNames.FormLabel,
                "ee4v-asset-manager__collection-popup-conditions-title"));
            _filterEditor = new AssetFilterEditor(
                _initialCollection?.Root);
            form.Add(_filterEditor);

            _error = UiTextFactory.Create(
                string.Empty,
                UiClassNames.FormError,
                "ee4v-asset-manager__collection-popup-error");
            _error.SetWhiteSpace(WhiteSpace.Normal);
            form.Add(_error);
            popup.Content.Add(form);

            popup.Footer.Add(AssetManagerControls.CreateButton(
                I18N.Get("action.cancel"),
                Close));
            popup.Footer.Add(AssetManagerControls.CreateButton(
                I18N.Get(
                    _initialCollection == null
                        ? "action.createCollection"
                        : "action.saveCollection"),
                Submit,
                "ee4v-asset-manager__primary-action"));
            SetPopup(popup);
            root.schedule.Execute(_name.FocusInput);
        }

        private void Submit()
        {
            var name = (_name.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                SetError(I18N.Get("notice.collectionNameRequired"));
                return;
            }

            if (!_filterEditor.TryCreateNode(
                    out var root,
                    out var errorKey))
            {
                SetError(I18N.Get(errorKey));
                return;
            }

            if (_save(name, root))
            {
                Close();
                return;
            }

            SetError(I18N.Get("notice.collectionSaveFailed"));
        }

        private void SetError(string message)
        {
            _error.SetText(message ?? string.Empty);
        }
    }
}
