using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetCollectionCreationPopup : CustomPopupWindow
    {
        private static readonly Vector2 PopupSize =
            new Vector2(620f, 520f);

        private Func<string, AssetCollectionIcon, AssetFilterNode, bool> _save;
        private AssetCollectionIcon _icon;
        private readonly Dictionary<AssetCollectionIcon, UiButton> _iconButtons =
            new Dictionary<AssetCollectionIcon, UiButton>();
        private AssetCollection _initialCollection;
        private AssetFilterNode _initialRoot;
        private IReadOnlyList<AssetTagOption> _tagOptions;
        private AssetManagerTextField _name;
        private AssetFilterEditor _filterEditor;
        private UiTextElement _error;

        public static void Show(
            VisualElement anchor,
            AssetCollection initialCollection,
            Func<string, AssetCollectionIcon, AssetFilterNode, bool> save,
            AssetFilterNode initialRoot = null,
            IReadOnlyList<AssetTagOption> tagOptions = null)
        {
            if (anchor == null || save == null)
            {
                return;
            }

            var window = CreateInstance<AssetCollectionCreationPopup>();
            window._initialCollection = initialCollection;
            window._initialRoot = initialRoot;
            window._tagOptions = tagOptions;
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
                I18N.Get("field.icon"), UiClassNames.FormLabel));
            _iconButtons.Clear();
            var icons = new VisualElement();
            icons.AddToClassList("ee4v-asset-manager__collection-icon-choices");
            foreach (AssetCollectionIcon icon in Enum.GetValues(typeof(AssetCollectionIcon)))
            {
                var choice = icon;
                var button = AssetManagerControls.CreateIconButton(
                    I18N.Get("collectionIcon." + choice.ToString().ToLowerInvariant()),
                    AssetManagerControls.GetCollectionIconFileName(choice),
                    UiSizeTokens.Size14,
                    UiButtonVariant.Ghost,
                    () => SelectIcon(choice),
                    "ee4v-asset-manager__collection-icon-choice");
                _iconButtons.Add(choice, button);
                icons.Add(button);
            }
            SelectIcon(_initialCollection?.Icon ?? AssetCollectionIcon.Folder);
            form.Add(icons);

            form.Add(UiTextFactory.Create(
                I18N.Get("filterEditor.conditions"),
                UiClassNames.FormLabel,
                "ee4v-asset-manager__collection-popup-conditions-title"));
            _filterEditor = new AssetFilterEditor(
                _initialCollection?.Root ?? _initialRoot, tagOptions: _tagOptions);
            _filterEditor.TagPickerClosed += OnTagPickerClosed;
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

        private void OnTagPickerClosed()
        {
            if (this != null) { Focus(); }
        }

        protected override void OnLostFocus()
        {
            if (_filterEditor?.IsTagPickerOpen == true) { return; }
            base.OnLostFocus();
        }

        protected override void OnDisable()
        {
            if (_filterEditor != null)
            {
                _filterEditor.TagPickerClosed -= OnTagPickerClosed;
                _filterEditor.CloseTagPicker();
            }
            base.OnDisable();
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

            if (_save(name, _icon, root))
            {
                Close();
                return;
            }

            SetError(I18N.Get("notice.collectionSaveFailed"));
        }

        private void SelectIcon(AssetCollectionIcon icon)
        {
            _icon = icon;
            foreach (var choice in _iconButtons)
            {
                choice.Value.EnableInClassList(
                    "ee4v-asset-manager__collection-icon-choice--selected",
                    choice.Key == icon);
            }
        }

        private void SetError(string message)
        {
            _error.SetText(message ?? string.Empty);
        }
    }

    internal sealed class AssetSearchFilterPopup : CustomPopupWindow
    {
        private IReadOnlyList<AssetTagOption> _options;
        private AssetFilterNode _initialRoot;
        private Action<AssetFilterNode> _apply;
        private Action<AssetFilterNode> _save;
        private AssetFilterEditor _editor;
        private UiTextElement _error;

        internal static void Show(VisualElement anchor, IReadOnlyList<AssetTagOption> options,
            AssetFilterNode root,
            Action<AssetFilterNode> apply,
            Action<AssetFilterNode> save)
        {
            var window = CreateInstance<AssetSearchFilterPopup>();
            window._options = options;
            window._initialRoot = root;
            window._apply = apply;
            window._save = save;
            var size = new Vector2(640f, 420f);
            var bounds = anchor.worldBound;
            window.ShowAsPopup(anchor,
                new Vector2(bounds.xMax - size.x, bounds.yMax), size);
        }

        private void CreateGUI()
        {
            AssetManagerWindowSession.PrepareRoot(rootVisualElement);
            rootVisualElement.AddToClassList("ee4v-asset-manager");
            ConfigureCloseAndSubmitKeys(rootVisualElement, () => Submit(false));
            var popup = new CustomPopup(I18N.Get("searchFilter.open"),
                showFooter: true, closeTooltip: I18N.Get("action.cancel"));
            popup.AddToClassList("ee4v-asset-manager__search-filter-popup");
            var form = new ScrollView(ScrollViewMode.Vertical);
            form.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            form.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            form.AddToClassList("ee4v-asset-manager__collection-popup-form");
            _editor = new AssetFilterEditor(_initialRoot, allowEmptyRoot: true, tagOptions: _options);
            _editor.TagPickerClosed += OnTagPickerClosed;
            form.Add(_editor);
            _error = UiTextFactory.Create(string.Empty, UiClassNames.FormError);
            _error.SetWhiteSpace(WhiteSpace.Normal);
            _error.style.display = DisplayStyle.None;
            form.Add(_error);
            popup.Content.Add(form);
            popup.Footer.Add(new UiButton(I18N.Get("searchFilter.clear"), () =>
            {
                Close();
                _apply(null);
            }, variant: UiButtonVariant.Ghost));
            popup.Footer.Add(new UiButton(I18N.Get("searchFilter.save"), () => Submit(true),
                variant: UiButtonVariant.Ghost));
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            popup.Footer.Add(spacer);
            popup.Footer.Add(new UiButton(I18N.Get("action.cancel"), Close,
                variant: UiButtonVariant.Ghost));
            popup.Footer.Add(AssetManagerControls.CreateButton(I18N.Get("searchFilter.apply"), () => Submit(false),
                "ee4v-asset-manager__primary-action"));
            SetPopup(popup);
        }

        private void OnTagPickerClosed()
        {
            if (this != null) { Focus(); }
        }

        protected override void OnLostFocus()
        {
            if (_editor?.IsTagPickerOpen == true) { return; }
            base.OnLostFocus();
        }

        protected override void OnDisable()
        {
            if (_editor != null)
            {
                _editor.TagPickerClosed -= OnTagPickerClosed;
                _editor.CloseTagPicker();
            }
            base.OnDisable();
        }

        private void Submit(bool save)
        {
            if (!_editor.TryCreateNode(out var root, out var errorKey))
            {
                _error.SetText(I18N.Get(errorKey));
                _error.style.display = DisplayStyle.Flex;
                return;
            }
            Close();
            (save ? _save : _apply)(root);
        }
    }
}
