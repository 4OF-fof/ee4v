using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class GroupedResultGroupState
    {
        public GroupedResultGroupState(
            string title,
            IReadOnlyList<ReferenceRowState> rows,
            string description = "")
        {
            Title = title ?? string.Empty;
            Rows = rows ?? new ReferenceRowState[0];
            Description = description ?? string.Empty;
        }

        public string Title { get; }

        public IReadOnlyList<ReferenceRowState> Rows { get; }

        public string Description { get; }
    }

    internal sealed class GroupedResultListState
    {
        public GroupedResultListState(IReadOnlyList<GroupedResultGroupState> groups)
        {
            Groups = groups ?? new GroupedResultGroupState[0];
        }

        public IReadOnlyList<GroupedResultGroupState> Groups { get; }
    }

    internal sealed class GroupedResultList : VisualElement
    {
        public GroupedResultList(GroupedResultListState state = null)
        {
            AddToClassList(UiClassNames.GroupedList);
            SetState(state ?? new GroupedResultListState(new GroupedResultGroupState[0]));
        }

        public void SetState(GroupedResultListState state)
        {
            state = state ?? new GroupedResultListState(new GroupedResultGroupState[0]);
            Clear();

            for (var groupIndex = 0; groupIndex < state.Groups.Count; groupIndex++)
            {
                var groupState = state.Groups[groupIndex];

                var group = new VisualElement();
                group.AddToClassList(UiClassNames.Group);

                var title = UiTextFactory.Create(groupState.Title, UiClassNames.GroupTitle);
                group.Add(title);

                if (!string.IsNullOrWhiteSpace(groupState.Description))
                {
                    var description = UiTextFactory.Create(groupState.Description, UiClassNames.GroupDescription);
                    group.Add(description);
                }

                var rows = new VisualElement();
                rows.AddToClassList(UiClassNames.GroupRows);
                for (var rowIndex = 0; rowIndex < groupState.Rows.Count; rowIndex++)
                {
                    rows.Add(new ReferenceRow(groupState.Rows[rowIndex]));
                }

                group.Add(rows);
                Add(group);
            }
        }

        public static int CountRows(GroupedResultListState state)
        {
            if (state == null || state.Groups == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < state.Groups.Count; i++)
            {
                count += state.Groups[i].Rows != null ? state.Groups[i].Rows.Count : 0;
            }

            return count;
        }
    }
}
