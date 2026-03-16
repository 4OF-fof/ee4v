using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class AnalyzerResultSectionState
    {
        public AnalyzerResultSectionState(
            string title,
            string description,
            GroupedResultListState results,
            string emptyTitle,
            string emptyMessage)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Results = results ?? new GroupedResultListState(new GroupedResultGroupState[0]);
            EmptyTitle = emptyTitle ?? string.Empty;
            EmptyMessage = emptyMessage ?? string.Empty;
        }

        public string Title { get; }

        public string Description { get; }

        public GroupedResultListState Results { get; }

        public string EmptyTitle { get; }

        public string EmptyMessage { get; }
    }

    internal sealed class AnalyzerResultSection : VisualElement
    {
        private readonly UiSection _section;
        private readonly GroupedResultList _resultList;
        private readonly UiEmptyState _emptyState;

        public AnalyzerResultSection(AnalyzerResultSectionState state = null)
        {
            _section = new UiSection();
            _resultList = new GroupedResultList();
            _emptyState = new UiEmptyState();

            Add(_section);
            _section.Body.Add(_resultList);
            _section.Body.Add(_emptyState);

            SetState(state ?? new AnalyzerResultSectionState(string.Empty, string.Empty, null, string.Empty, string.Empty));
        }

        public void SetState(AnalyzerResultSectionState state)
        {
            state = state ?? new AnalyzerResultSectionState(string.Empty, string.Empty, null, string.Empty, string.Empty);

            var rowCount = GroupedResultList.CountRows(state.Results);
            _section.SetState(new UiSectionState(state.Title, state.Description, rowCount.ToString()));
            _resultList.SetState(state.Results);
            _emptyState.SetState(new UiEmptyStateState(state.EmptyTitle, state.EmptyMessage));

            _resultList.style.display = rowCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyState.style.display = rowCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
