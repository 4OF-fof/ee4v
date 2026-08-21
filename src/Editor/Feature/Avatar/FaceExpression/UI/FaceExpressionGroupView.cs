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
        internal string Meshes { get; set; }
        internal string AddMesh { get; set; }
        internal string MeshGroupSection { get; set; }
        internal string BlendShapeGroupSection { get; set; }
    }

    internal sealed class FaceExpressionGroupView : VisualElement
    {
        private readonly string _allText;
        private readonly string _meshGroupSectionText;
        private readonly string _blendShapeGroupSectionText;
        private readonly ListView _list;
        private readonly VisualElement _meshList;
        private List<GroupOption> _options = new List<GroupOption>();
        private string _selectedGroupKey;
        private bool _rendering;

        internal FaceExpressionGroupView(FaceExpressionGroupViewText text)
        {
            text = text ?? new FaceExpressionGroupViewText();
            _allText = text.All;
            _meshGroupSectionText = text.MeshGroupSection;
            _blendShapeGroupSectionText = text.BlendShapeGroupSection;
            AddToClassList("ee4v-face-expression-groups");

            var meshHeader = new VisualElement();
            meshHeader.AddToClassList("ee4v-face-expression-groups__mesh-header");
            meshHeader.Add(UiTextFactory.Create(
                text.Meshes,
                UiClassNames.SectionTitle,
                "ee4v-face-expression-groups__mesh-title"));
            meshHeader.Add(UiTextFactory.CreateButton(
                text.AddMesh,
                () => AddMeshRequested?.Invoke(),
                "ee4v-face-expression-groups__add-mesh"));
            Add(meshHeader);

            _meshList = new VisualElement();
            _meshList.AddToClassList("ee4v-face-expression-groups__meshes");
            Add(_meshList);

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

        internal void SetMeshes(
            IReadOnlyList<FaceMeshOption> meshes,
            string removeText)
        {
            _meshList.Clear();
            for (var index = 0; index < (meshes?.Count ?? 0); index++)
            {
                var mesh = meshes[index];
                var row = new VisualElement();
                row.AddToClassList("ee4v-face-expression-groups__mesh-row");
                row.Add(UiTextFactory.Create(
                    mesh.DisplayName,
                    "ee4v-face-expression-groups__mesh-name"));
                if (!mesh.IsBody)
                {
                    row.Add(UiTextFactory.CreateButton(
                        removeText,
                        () => RemoveMeshRequested?.Invoke(mesh.Path),
                        "ee4v-face-expression-groups__remove-mesh"));
                }

                _meshList.Add(row);
            }
        }

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
                    _blendShapeGroupSectionText);
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

        private void AddSection(
            IEnumerable<BlendShapeGroup> groups,
            string sectionName)
        {
            var sectionGroups = groups.ToArray();
            if (sectionGroups.Length == 0)
            {
                return;
            }

            _options.Add(new GroupOption(null, sectionName, 0, true));
            for (var index = 0; index < sectionGroups.Length; index++)
            {
                var group = sectionGroups[index];
                _options.Add(new GroupOption(
                    group.Key,
                    group.Name,
                    group.Count,
                    false));
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
                bool isSectionHeader)
            {
                GroupKey = groupKey;
                DisplayName = displayName;
                Count = count;
                IsSectionHeader = isSectionHeader;
            }

            internal string GroupKey { get; }
            internal string DisplayName { get; }
            internal int Count { get; }
            internal bool IsSectionHeader { get; }
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
                EnableInClassList(
                    "ee4v-face-expression-group-row--section",
                    option?.IsSectionHeader == true);
                _count.SetText(option?.IsSectionHeader == true
                    ? string.Empty
                    : (option?.Count ?? 0).ToString());
            }
        }
    }
}
