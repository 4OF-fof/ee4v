using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class FaceExpressionGroupViewText
    {
        internal string Groups { get; set; }
        internal string All { get; set; }
        internal string AddMesh { get; set; }
        internal string RemoveMesh { get; set; }
        internal string CopyBlendShapes { get; set; }
        internal string MeshGroupSection { get; set; }
        internal string BodySection { get; set; }
    }

    internal sealed class FaceExpressionGroupView : VisualElement
    {
        private readonly string _allText;
        private readonly string _removeMeshText;
        private readonly string _copyBlendShapesText;
        private readonly string _meshGroupSectionText;
        private readonly string _bodySectionText;
        private readonly ListView _list;
        private List<GroupOption> _options = new List<GroupOption>();
        private string _selectedGroupKey;
        private bool _rendering;

        internal FaceExpressionGroupView(FaceExpressionGroupViewText text)
        {
            text = text ?? new FaceExpressionGroupViewText();
            _allText = text.All;
            _removeMeshText = text.RemoveMesh;
            _copyBlendShapesText = text.CopyBlendShapes;
            _meshGroupSectionText = text.MeshGroupSection;
            _bodySectionText = text.BodySection;
            AddToClassList("ee4v-face-expression-groups");

            var header = new SectionHeader(text.Groups);
            header.AddToClassList("ee4v-face-expression-groups__header");
            header.TitleText.AddToClassList(
                "ee4v-face-expression-groups__title");
            header.Actions.Add(UiTextFactory.CreateButton(
                text.AddMesh,
                () => AddMeshRequested?.Invoke(),
                "ee4v-face-expression-groups__add-mesh"));
            Add(header);

            _list = new ListView
            {
                fixedItemHeight = 28f,
                virtualizationMethod =
                    CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = () => new GroupRow(ShowContextMenu),
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
                if (option?.IsSectionHeader == true)
                {
                    RestoreSelection();
                    return;
                }

                GroupSelected?.Invoke(option?.GroupKey);
            };
            Add(_list);
        }

        internal event Action<string> GroupSelected;
        internal event Action AddMeshRequested;
        internal event Action<string> RemoveMeshRequested;
        internal event Action CopyBodyBlendShapesRequested;

        internal void SetGroups(
            IReadOnlyList<BlendShapeGroup> groups,
            int totalCount,
            string selectedGroupKey)
        {
            _options = new List<GroupOption>
            {
                new GroupOption(null, _allText, totalCount, false)
            };
            if (groups != null)
            {
                AddSection(
                    groups.Where(group => string.IsNullOrEmpty(group.RendererPath)),
                    _bodySectionText,
                    isBodySection: true);
                AddSection(
                    groups.Where(group => !string.IsNullOrEmpty(group.RendererPath)),
                    _meshGroupSectionText);
            }

            _selectedGroupKey = selectedGroupKey;
            _rendering = true;
            _list.itemsSource = (IList)_options;
            _list.Rebuild();
            var selectedIndex = string.IsNullOrEmpty(selectedGroupKey)
                ? 0
                : _options.FindIndex(option => string.Equals(
                    option.GroupKey,
                    selectedGroupKey,
                    StringComparison.Ordinal));
            _list.selectedIndex = Mathf.Max(0, selectedIndex);
            _rendering = false;
        }

        private void ShowContextMenu(GroupOption option)
        {
            var menu = new GenericMenu();
            if (option.IsBodySection)
            {
                menu.AddItem(
                    UiTextFactory.CreateGuiContent(_copyBlendShapesText),
                    false,
                    () => CopyBodyBlendShapesRequested?.Invoke());
            }
            else
            {
                menu.AddItem(
                    UiTextFactory.CreateGuiContent(_removeMeshText),
                    false,
                    () => RemoveMeshRequested?.Invoke(option.RendererPath));
            }

            menu.ShowAsContext();
        }

        private void AddSection(
            IEnumerable<BlendShapeGroup> groups,
            string sectionName,
            bool isBodySection = false)
        {
            var sectionGroups = groups.ToArray();
            if (sectionGroups.Length == 0)
            {
                return;
            }

            _options.Add(new GroupOption(
                null,
                sectionName,
                0,
                true,
                isBodySection: isBodySection));
            for (var index = 0; index < sectionGroups.Length; index++)
            {
                var group = sectionGroups[index];
                _options.Add(new GroupOption(
                    group.Key,
                    group.Name,
                    group.Count,
                    false,
                    group.RendererPath));
            }
        }

        private void RestoreSelection()
        {
            _rendering = true;
            var selectedIndex = string.IsNullOrEmpty(_selectedGroupKey)
                ? 0
                : _options.FindIndex(option => string.Equals(
                    option.GroupKey,
                    _selectedGroupKey,
                    StringComparison.Ordinal));
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
                string groupKey,
                string displayName,
                int count,
                bool isSectionHeader,
                string rendererPath = null,
                bool isBodySection = false)
            {
                GroupKey = groupKey;
                DisplayName = displayName;
                Count = count;
                IsSectionHeader = isSectionHeader;
                RendererPath = rendererPath;
                IsBodySection = isBodySection;
            }

            internal string GroupKey { get; }
            internal string DisplayName { get; }
            internal int Count { get; }
            internal bool IsSectionHeader { get; }
            internal string RendererPath { get; }
            internal bool IsBodySection { get; }
        }

        private sealed class GroupRow : ItemRow
        {
            private readonly Badge _count;
            private readonly Action<GroupOption> _showContextMenu;
            private GroupOption _option;

            internal GroupRow(Action<GroupOption> showContextMenu)
            {
                _showContextMenu = showContextMenu;
                AddToClassList("ee4v-face-expression-group-row");
                TitleText.AddToClassList(
                    "ee4v-face-expression-group-row__name");
                _count = new Badge();
                _count.AddToClassList(
                    "ee4v-face-expression-group-row__count");
                Trailing.Add(_count);
                RegisterCallback<ContextClickEvent>(OnContextClick);
            }

            internal void SetOption(GroupOption option)
            {
                _option = option;
                base.SetState(new ItemRowState(
                    option?.DisplayName ?? string.Empty));
                EnableInClassList(
                    "ee4v-face-expression-group-row--section",
                    option?.IsSectionHeader == true);
                _count.SetText(option?.IsSectionHeader == true
                    ? string.Empty
                    : (option?.Count ?? 0).ToString());
            }

            private void OnContextClick(ContextClickEvent evt)
            {
                if (_option == null ||
                    (!_option.IsBodySection &&
                     string.IsNullOrEmpty(_option.RendererPath)))
                {
                    return;
                }

                _showContextMenu?.Invoke(_option);
                evt.StopPropagation();
            }
        }
    }
}
