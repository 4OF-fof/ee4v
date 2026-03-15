using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.TagListView {
    public class TagListContent : VisualElement {
        private readonly Dictionary<string, bool> _foldoutStates = new();

        public TagListContent() {
            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;
        }

        public event Action<string> OnTagSelected;
        public event Action<string, VisualElement> OnTagRightClicked;

        public void DrawFlatList(List<string> tags, Func<string, int> getCount) {
            Clear();
            foreach (var tag in tags) {
                var count = getCount(tag);
                Add(new TagItem(tag, tag, count, OnTagSelected, OnTagRightClicked));
            }
        }

        public void DrawHierarchyList(TagNode rootNode, Func<string, int> getCount, Func<TagNode, int> getNodeCount) {
            Clear();
            RenderTreeNodes(rootNode, this, getCount, getNodeCount);
        }

        private void RenderTreeNodes(TagNode node, VisualElement container, Func<string, int> getCount,
            Func<TagNode, int> getNodeCount) {
            var leafContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginBottom = 4,
                    width = Length.Percent(100)
                }
            };
            container.Add(leafContainer);

            var sortedChildren = node.Children.Values
                .OrderByDescending(n => string.IsNullOrEmpty(n.FullPath) ? 0 : getCount(n.FullPath))
                .ThenBy(n => n.Name)
                .ToList();

            foreach (var child in sortedChildren) {
                var isFolder = child.Children.Count > 0;

                if (isFolder) {
                    if (!string.IsNullOrEmpty(child.FullPath)) {
                        var count = getCount(child.FullPath);
                        leafContainer.Add(new TagItem(child.Name, child.FullPath, count, OnTagSelected,
                            OnTagRightClicked));
                    }

                    var groupTagCount = getNodeCount(child);
                    var foldoutKey = child.FullPath ?? child.Name;
                    var isOpen = _foldoutStates.GetValueOrDefault(foldoutKey, false);

                    var group = new TagGroup(child.Name, groupTagCount, isOpen,
                        newState => _foldoutStates[foldoutKey] = newState);

                    container.Add(group);
                    RenderTreeNodes(child, group, getCount, getNodeCount);
                }
                else {
                    var count = getCount(child.FullPath);
                    leafContainer.Add(new TagItem(child.Name, child.FullPath, count, OnTagSelected, OnTagRightClicked));
                }
            }
        }
    }

    public class TagNode {
        public TagNode(string name, string fullPath) {
            Name = name;
            FullPath = fullPath;
        }

        public string Name { get; }
        public string FullPath { get; set; }
        public Dictionary<string, TagNode> Children { get; } = new();
    }
}