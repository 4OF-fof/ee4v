using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectsToolbar : VisualElement
    {
        private const string RootClassName =
            "ee4v-hidden-objects-toolbar";
        private const string SearchClassName =
            "ee4v-hidden-objects-toolbar__search";
        private const string SceneClassName =
            "ee4v-hidden-objects-toolbar__scene";
        private const string RefreshClassName =
            "ee4v-hidden-objects-toolbar__refresh";

        private readonly HiddenObjectsViewText _text;
        private readonly SearchField _searchField;
        private readonly PopupField<HiddenObjectSceneOptionViewState>
            _scenePopup;

        public HiddenObjectsToolbar(HiddenObjectsViewText text)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            AddToClassList(RootClassName);

            _searchField = new SearchField(new SearchFieldState(
                placeholder: _text.SearchPlaceholder,
                searchTooltip: _text.SearchTooltip,
                clearTooltip: _text.ClearSearchTooltip));
            _searchField.AddToClassList(SearchClassName);
            _searchField.ValueChanged += value =>
                QueryChanged?.Invoke(value);

            var initialOptions = new List<HiddenObjectSceneOptionViewState>
            {
                new HiddenObjectSceneOptionViewState(0, string.Empty)
            };
            _scenePopup =
                new PopupField<HiddenObjectSceneOptionViewState>(
                    initialOptions,
                    0,
                    FormatSceneOption,
                    FormatSceneOption)
                {
                    tooltip = _text.SceneTooltip
                };
            _scenePopup.AddToClassList(SceneClassName);
            _scenePopup.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != null)
                {
                    SceneChanged?.Invoke(evt.newValue.SceneHandle);
                }
            });

            var refreshButton = new Button(
                () => RefreshRequested?.Invoke())
            {
                text = _text.RefreshText,
                tooltip = _text.RefreshTooltip
            };
            refreshButton.AddToClassList(RefreshClassName);

            Add(_searchField);
            Add(_scenePopup);
            Add(refreshButton);
        }

        public event Action<string> QueryChanged;

        public event Action<int> SceneChanged;

        public event Action RefreshRequested;

        public void SetState(
            string query,
            IReadOnlyList<HiddenObjectSceneOptionViewState> options,
            int selectedSceneHandle)
        {
            _searchField.SetState(new SearchFieldState(
                query,
                _text.SearchPlaceholder,
                _text.SearchTooltip,
                _text.ClearSearchTooltip));

            var choices = options == null || options.Count == 0
                ? new List<HiddenObjectSceneOptionViewState>
                {
                    new HiddenObjectSceneOptionViewState(0, string.Empty)
                }
                : options.ToList();
            _scenePopup.choices = choices;

            var selected = choices.FirstOrDefault(
                option => option.SceneHandle == selectedSceneHandle) ??
                choices[0];
            _scenePopup.SetValueWithoutNotify(selected);
        }

        private static string FormatSceneOption(
            HiddenObjectSceneOptionViewState option)
        {
            return option != null ? option.Label : string.Empty;
        }
    }
}
