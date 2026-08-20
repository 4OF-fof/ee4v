using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class FaceExpressionGroupViewText
    {
        internal string Groups { get; set; }
        internal string All { get; set; }
    }

    internal sealed class FaceExpressionGroupView : VisualElement
    {
        private readonly string _allText;
        private readonly ListView _list;
        private List<GroupOption> _options = new List<GroupOption>();
        private bool _rendering;

        internal FaceExpressionGroupView(FaceExpressionGroupViewText text)
        {
            text = text ?? new FaceExpressionGroupViewText();
            _allText = text.All;
            AddToClassList("ee4v-face-expression-groups");
            Add(UiTextFactory.Create(
                text.Groups,
                UiClassNames.SectionTitle,
                "ee4v-face-expression-groups__title"));

            _list = new ListView
            {
                fixedItemHeight = 28f,
                virtualizationMethod =
                    CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = () => new GroupRow(),
                bindItem = BindRow
            };
            _list.AddToClassList("ee4v-face-expression-groups__list");
            _list.selectionChanged += selected =>
            {
                if (_rendering)
                {
                    return;
                }

                var option = selected.OfType<GroupOption>().FirstOrDefault();
                GroupSelected?.Invoke(option?.GroupName);
            };
            Add(_list);
        }

        internal event Action<string> GroupSelected;

        internal void SetGroups(
            IReadOnlyList<BlendShapeGroup> groups,
            int totalCount,
            string selectedGroupName)
        {
            _options = new List<GroupOption>
            {
                new GroupOption(null, _allText, totalCount)
            };
            if (groups != null)
            {
                for (var index = 0; index < groups.Count; index++)
                {
                    _options.Add(new GroupOption(
                        groups[index].Name,
                        groups[index].Name,
                        groups[index].Count));
                }
            }

            _rendering = true;
            _list.itemsSource = (IList)_options;
            _list.Rebuild();
            var selectedIndex = string.IsNullOrEmpty(selectedGroupName)
                ? 0
                : _options.FindIndex(option => string.Equals(
                    option.GroupName,
                    selectedGroupName,
                    StringComparison.OrdinalIgnoreCase));
            _list.selectedIndex = Mathf.Max(0, selectedIndex);
            _rendering = false;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (element is GroupRow row &&
                index >= 0 &&
                index < _options.Count)
            {
                row.SetOption(_options[index]);
            }
        }

        private sealed class GroupOption
        {
            internal GroupOption(
                string groupName,
                string displayName,
                int count)
            {
                GroupName = groupName;
                DisplayName = displayName;
                Count = count;
            }

            internal string GroupName { get; }
            internal string DisplayName { get; }
            internal int Count { get; }
        }

        private sealed class GroupRow : VisualElement
        {
            private readonly UiTextElement _name;
            private readonly UiTextElement _count;

            internal GroupRow()
            {
                AddToClassList("ee4v-face-expression-group-row");
                _name = UiTextFactory.Create(
                    string.Empty,
                    "ee4v-face-expression-group-row__name");
                _count = UiTextFactory.Create(
                    string.Empty,
                    UiClassNames.SecondaryText,
                    "ee4v-face-expression-group-row__count");
                Add(_name);
                Add(_count);
            }

            internal void SetOption(GroupOption option)
            {
                _name.SetText(option?.DisplayName ?? string.Empty);
                _count.SetText((option?.Count ?? 0).ToString());
            }
        }
    }
}
