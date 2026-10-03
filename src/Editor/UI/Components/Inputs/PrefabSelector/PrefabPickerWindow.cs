using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class PrefabPickerWindow
        : CustomPopupWindow
    {
        private static readonly Vector2 PopupSize =
            new Vector2(520f, 420f);

        private IReadOnlyList<GameObject> _candidates;
        private GameObject _selected;
        private Action<GameObject> _select;
        private bool _showAsGrid;
        private SearchField _search;
        private VisualElement _options;
        private IReadOnlyList<GameObject> _filtered =
            Array.Empty<GameObject>();

        internal static void Show(
            VisualElement anchor,
            IReadOnlyList<GameObject> candidates,
            GameObject selected,
            Action<GameObject> select,
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
                             PrefabPickerWindow>())
            {
                existing.Close();
            }

            var window = CreateInstance<
                PrefabPickerWindow>();
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
                UiLocalization.Get("ui.prefabPicker.title"),
                closeTooltip: UiLocalization.Get(
                    "ui.prefabPicker.close"));

            var body = new VisualElement();
            body.AddToClassList(
                "ee4v-ui-prefab-picker__body");
            _search = new SearchField(new SearchFieldState(
                placeholder: UiLocalization.Get(
                    "ui.prefabPicker.search"),
                searchIconState:
                    FluentUiIcons.CreateState(
                        "search.png",
                        UiSizeTokens.Size14),
                clearIconState:
                    FluentUiIcons.CreateState(
                        "dismiss.png",
                        UiSizeTokens.Size10)));
            _search.AddToClassList(
                "ee4v-ui-prefab-picker__search");
            _search.ValueChanged += _ => RebuildOptions();
            body.Add(_search);

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList(
                "ee4v-ui-prefab-picker__content");
            _options = scroll.contentContainer;
            _options.AddToClassList(
                "ee4v-ui-prefab-picker__options");
            _options.EnableInClassList(
                "ee4v-ui-prefab-picker__options--grid",
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
            _filtered = (_candidates ?? Array.Empty<GameObject>())
                .Where(prefab => Matches(prefab, query))
                .ToArray();
            foreach (var prefab in _filtered)
            {
                var selectedPrefab = prefab;
                var path = AssetDatabase.GetAssetPath(prefab);
                var option = new NavigationItem(
                    new NavigationItemState(
                        prefab.name,
                        _showAsGrid ? string.Empty : path,
                        selected: prefab == _selected),
                    () => Select(selectedPrefab));
                option.tooltip = path;
                option.AddToClassList(
                    "ee4v-ui-prefab-picker__option");
                option.EnableInClassList(
                    "ee4v-ui-prefab-picker__option--grid",
                    _showAsGrid);
                var preview = new PrefabThumbnail(prefab);
                preview.EnableInClassList(
                    "ee4v-ui-prefab-picker__preview--grid",
                    _showAsGrid);
                option.Leading.Add(preview);
                _options.Add(option);
            }

            if (_filtered.Count == 0)
            {
                var empty = new EmptyState(new EmptyStateState(
                    string.Empty,
                    UiLocalization.Get("ui.prefabPicker.noMatch")));
                empty.AddToClassList(
                    "ee4v-ui-prefab-picker__empty");
                _options.Add(empty);
            }
        }

        private static bool Matches(GameObject prefab, string query)
        {
            if (prefab == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            return prefab.name.IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   AssetDatabase.GetAssetPath(prefab).IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Select(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            _select?.Invoke(prefab);
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
            _filtered = Array.Empty<GameObject>();
        }
    }
}
