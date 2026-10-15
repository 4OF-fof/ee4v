using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ObjectPickerWindow
        : CustomPopupWindow
    {
        private static readonly Vector2 PopupSize =
            new Vector2(520f, 420f);

        private IReadOnlyList<Object> _candidates;
        private Object _selected;
        private Action<Object> _select;
        private bool _showAsGrid;
        private SearchField _search;
        private VisualElement _options;
        private IReadOnlyList<Object> _filtered =
            Array.Empty<Object>();

        internal static void Show(
            VisualElement anchor,
            IReadOnlyList<Object> candidates,
            Object selected,
            Action<Object> select,
            bool showAsGrid = false)
        {
            if (anchor == null ||
                candidates == null ||
                candidates.Count == 0 ||
                select == null)
            {
                return;
            }

            foreach (var existing in Resources
                         .FindObjectsOfTypeAll<
                             ObjectPickerWindow>())
            {
                existing.Close();
            }

            var window = CreateInstance<
                ObjectPickerWindow>();
            window._candidates = candidates;
            window._selected = selected;
            window._select = select;
            window._showAsGrid = showAsGrid;
            window.ShowAsPopup(anchor, PopupSize);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            UiComposition.Prepare(root);
            ConfigureCloseAndSubmitKeys(root, SelectFirst);

            var popup = new CustomPopup(
                UiLocalization.Get("ui.objectPicker.title"),
                closeTooltip: UiLocalization.Get(
                    "ui.objectPicker.close"));

            var body = new VisualElement();
            body.AddToClassList(
                "ee4v-ui-object-picker__body");
            _search = new SearchField(new SearchFieldState(
                placeholder: UiLocalization.Get(
                    "ui.objectPicker.search"),
                searchIconState:
                    FluentUiIcons.CreateState(
                        "search.png",
                        UiSizeTokens.Size14),
                clearIconState:
                    FluentUiIcons.CreateState(
                        "dismiss.png",
                        UiSizeTokens.Size10)));
            _search.AddToClassList(
                "ee4v-ui-object-picker__search");
            _search.ValueChanged += _ => RebuildOptions();
            body.Add(_search);

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList(
                "ee4v-ui-object-picker__content");
            _options = scroll.contentContainer;
            _options.AddToClassList(
                "ee4v-ui-object-picker__options");
            _options.EnableInClassList(
                "ee4v-ui-object-picker__options--grid",
                _showAsGrid);
            body.Add(scroll);
            popup.Content.Add(body);
            SetPopup(popup);
            RebuildOptions();

            _search.schedule.Execute(() =>
                _search.Q<TextField>()?.Focus());
        }

        private void RebuildOptions()
        {
            if (_options == null)
            {
                return;
            }

            _options.Clear();
            var query = (_search?.Value ?? string.Empty).Trim();
            _filtered = (_candidates ?? Array.Empty<Object>())
                .Where(candidate => Matches(candidate, query))
                .ToArray();
            foreach (var candidate in _filtered)
            {
                var selectedObject = candidate;
                var path = AssetDatabase.GetAssetPath(candidate);
                var option = new NavigationItem(
                    new NavigationItemState(
                        candidate.name,
                        _showAsGrid ? string.Empty : path,
                        selected: candidate == _selected),
                    () => Select(selectedObject));
                option.tooltip = path;
                option.AddToClassList(
                    "ee4v-ui-object-picker__option");
                option.EnableInClassList(
                    "ee4v-ui-object-picker__option--grid",
                    _showAsGrid);
                var preview = new ObjectThumbnail(candidate);
                preview.EnableInClassList(
                    "ee4v-ui-object-picker__preview--grid",
                    _showAsGrid);
                option.Leading.Add(preview);
                _options.Add(option);
            }

            if (_filtered.Count == 0)
            {
                var empty = new EmptyState(new EmptyStateState(
                    string.Empty,
                    UiLocalization.Get("ui.objectPicker.noMatch")));
                empty.AddToClassList(
                    "ee4v-ui-object-picker__empty");
                _options.Add(empty);
            }
        }

        private static bool Matches(Object candidate, string query)
        {
            if (candidate == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            return candidate.name.IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   AssetDatabase.GetAssetPath(candidate).IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Select(Object candidate)
        {
            if (candidate == null)
            {
                return;
            }

            _select?.Invoke(candidate);
            Close();
        }

        private void SelectFirst()
        {
            if (_filtered.Count > 0)
            {
                Select(_filtered[0]);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _select = null;
            _candidates = null;
            _selected = null;
            _filtered = Array.Empty<Object>();
        }
    }
}
