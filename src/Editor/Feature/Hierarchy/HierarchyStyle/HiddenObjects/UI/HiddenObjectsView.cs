using System;
using UnityEngine.UIElements;

namespace Ee4v.HiddenObjects
{
    internal sealed class HiddenObjectsView : VisualElement
    {
        private const string RootClassName =
            "ee4v-hidden-objects-view";

        private readonly HiddenObjectsToolbar _toolbar;
        private readonly HiddenObjectTreeView _tree;
        private readonly HiddenObjectsFooter _footer;

        public HiddenObjectsView(HiddenObjectsViewText text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            AddToClassList(RootClassName);
            _toolbar = new HiddenObjectsToolbar(text);
            _tree = new HiddenObjectTreeView();
            _footer = new HiddenObjectsFooter(text);

            _toolbar.QueryChanged += value =>
                QueryChanged?.Invoke(value);
            _toolbar.SceneChanged += sceneHandle =>
                SceneChanged?.Invoke(sceneHandle);
            _toolbar.RefreshRequested += () =>
                RefreshRequested?.Invoke();
            _tree.SelectionChanged += (instanceId, selected) =>
                SelectionChanged?.Invoke(instanceId, selected);
            _tree.FocusRequested += instanceId =>
                FocusRequested?.Invoke(instanceId);
            _footer.SelectAllRequested += () =>
                SelectAllRequested?.Invoke();
            _footer.ClearSelectionRequested += () =>
                ClearSelectionRequested?.Invoke();
            _footer.RevealRequested += () =>
                RevealRequested?.Invoke();

            Add(_toolbar);
            Add(_tree);
            Add(_footer);
        }

        public event Action<string> QueryChanged;

        public event Action<int> SceneChanged;

        public event Action RefreshRequested;

        public event Action<int, bool> SelectionChanged;

        public event Action<int> FocusRequested;

        public event Action SelectAllRequested;

        public event Action ClearSelectionRequested;

        public event Action RevealRequested;

        public void SetState(HiddenObjectsViewState state)
        {
            if (state == null)
            {
                return;
            }

            _toolbar.SetState(
                state.Query,
                state.SceneOptions,
                state.SelectedSceneHandle);
            _tree.SetState(state);
            _footer.SetState(
                state.SummaryText,
                state.VisibleHiddenCount,
                state.SelectedCount);
        }
    }
}
