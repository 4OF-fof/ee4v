using System.IO;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.ProjectExtension.Toolbar;
using _4OF.ee4v.ProjectExtension.Toolbar.Tab;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.Components.Toolbar {
    public class Tab : IProjectToolbarComponent {
        public int Priority => 10;
        public ToolbarPosition Position => ToolbarPosition.Left;
        public string Name => "Tab";
        public string Description => I18N.Get("_System.ProjectExtension.Toolbar.Tab.Description");
        public string Trigger => I18N.Get("_System.ProjectExtension.Toolbar.Tab.Trigger");

        public VisualElement CreateElement() {
            var addButton = new AddButton();
            var scrollView = new ScrollView(ScrollViewMode.Horizontal) {
                style = {
                    flexGrow = 1,
                    height = 20
                },
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };

            var tabContainer = scrollView.contentContainer;
            tabContainer.name = "ee4v-project-toolbar-tabContainer";
            tabContainer.style.alignItems = Align.Center;
            tabContainer.style.height = Length.Percent(100);
            tabContainer.style.flexDirection = FlexDirection.Row;

            tabContainer.Add(addButton);


            addButton.clicked += () =>
            {
                var tab = new TabEntry("Assets");
                TabManager.Insert(tabContainer.childCount - 1, tab);
                TabManager.SelectTab(tab);
            };

            TabControl(tabContainer);
            RegisterDropEvents(tabContainer);

            return scrollView;
        }


        private static void TabControl(VisualElement tabContainer) {
            VisualElement dragging = null;
            VisualElement placeholder = null;
            VisualElement potentialDrag = null;
            var placeholderInserted = false;
            var originalIndex = -1;
            var pointerDownPos = Vector2.zero;
            const float dragThreshold = 15f;

            tabContainer.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                if (evt.target is not VisualElement target) return;
                var t = target;
                while (t != null) {
                    if (t.name == "ee4v-project-toolbar-tabContainer-tab-close") return;
                    t = t.parent;
                }

                var tabElement = target;
                while (tabElement != null && tabElement.name != "ee4v-project-toolbar-tabContainer-tab")
                    tabElement = tabElement.parent;
                if (tabElement == null || !tabContainer.Contains(tabElement)) return;

                potentialDrag = tabElement;
                pointerDownPos = evt.localPosition;
                tabContainer.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);

            tabContainer.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (potentialDrag == null && dragging == null) return;

                if (potentialDrag != null && !tabContainer.Contains(potentialDrag)) potentialDrag = null;
                if (dragging != null && !tabContainer.Contains(dragging)) {
                    if (placeholder != null && tabContainer.Contains(placeholder)) {
                        tabContainer.Remove(placeholder);
                        placeholderInserted = false;
                        placeholder = null;
                    }

                    dragging.style.opacity = 1f;
                    dragging = null;
                    potentialDrag = null;
                    return;
                }

                var localPos = evt.localPosition;

                if (dragging == null) {
                    if (Vector2.Distance(pointerDownPos, localPos) < dragThreshold) return;
                    dragging = potentialDrag;
                    potentialDrag = null;
                    originalIndex = tabContainer.IndexOf(dragging);

                    var w = dragging is { layout: { width: > 0 } } ? dragging.layout.width : 60f;
                    placeholder = new VisualElement {
                        name = "ee4v-project-toolbar-tabContainer-placeholder",
                        style = {
                            width = w,
                            height = dragging is { layout: { height: > 0 } } ? dragging.layout.height : 18f
                        }
                    };
                    if (dragging != null) {
                        placeholder.style.marginTop = dragging.style.marginTop;
                        placeholder.style.backgroundColor = ColorPreset.SMouseOverBackground;

                        placeholderInserted = false;

                        dragging.style.opacity = 0.5f;
                    }
                }

                var pointerX = localPos.x;
                var desiredIndex = 0;
                var children = tabContainer.Children()
                    .Where(e => e.name == "ee4v-project-toolbar-tabContainer-tab" || e == placeholder).ToList();
                if (children.Count == 0) return;

                foreach (var child in children) {
                    if (child == dragging) continue;
                    var cx = child.layout.x + child.layout.width / 2f;
                    if (pointerX < cx) {
                        desiredIndex = tabContainer.IndexOf(child);
                        break;
                    }

                    desiredIndex = tabContainer.IndexOf(child) + 1;
                }

                desiredIndex = Mathf.Clamp(desiredIndex, 0, tabContainer.childCount - 1);

                var wouldChangeOrder = !(desiredIndex == originalIndex || desiredIndex == originalIndex + 1);

                if (!wouldChangeOrder) {
                    if (!placeholderInserted || placeholder == null || !tabContainer.Contains(placeholder)) return;
                    tabContainer.Remove(placeholder);
                    placeholderInserted = false;
                }
                else {
                    var addButton = tabContainer.Children()
                        .FirstOrDefault(c => c.name == "ee4v-project-toolbar-tabContainer-addButton");
                    var addIndex = addButton != null ? tabContainer.IndexOf(addButton) : tabContainer.childCount - 1;
                    var insertPos = Mathf.Min(desiredIndex, addIndex);
                    if (!placeholderInserted) {
                        tabContainer.Insert(insertPos, placeholder);
                        placeholderInserted = true;
                    }
                    else if (tabContainer.IndexOf(placeholder) != insertPos) {
                        tabContainer.Remove(placeholder);
                        tabContainer.Insert(insertPos, placeholder);
                    }

                    if (addButton == null || !placeholderInserted) return;
                    var phIndex = tabContainer.IndexOf(placeholder);
                    var addIdxNow = tabContainer.IndexOf(addButton);
                    if (phIndex <= addIdxNow) return;
                    tabContainer.Remove(placeholder);
                    tabContainer.Insert(addIdxNow, placeholder);
                }
            }, TrickleDown.TrickleDown);

            tabContainer.RegisterCallback<PointerUpEvent>(evt =>
            {
                tabContainer.ReleasePointer(evt.pointerId);

                if (dragging != null && !tabContainer.Contains(dragging)) {
                    if (placeholder != null && tabContainer.Contains(placeholder)) {
                        tabContainer.Remove(placeholder);
                        placeholderInserted = false;
                        placeholder = null;
                    }

                    dragging.style.opacity = 1f;
                    dragging = null;
                    potentialDrag = null;
                    return;
                }

                switch (dragging) {
                    case null when potentialDrag != null:
                        TabManager.SelectTab(potentialDrag);
                        potentialDrag = null;
                        return;
                    case null:
                        return;
                }

                var placeholderIndex = -1;
                if (placeholder != null && tabContainer.Contains(placeholder))
                    placeholderIndex = tabContainer.IndexOf(placeholder);
                else if (dragging != null && tabContainer.Contains(dragging))
                    placeholderIndex = tabContainer.IndexOf(dragging);

                var path = dragging.tooltip;

                if (placeholder != null && tabContainer.Contains(placeholder)) tabContainer.Remove(placeholder);
                if (dragging != null) dragging.style.opacity = 1f;

                if (placeholderIndex >= 0 && originalIndex != placeholderIndex && !string.IsNullOrEmpty(path)) {
                    var addButton = tabContainer.Children()
                        .FirstOrDefault(c => c.name == "ee4v-project-toolbar-tabContainer-addButton");
                    var addIndex = addButton != null ? tabContainer.IndexOf(addButton) : tabContainer.childCount - 1;
                    var finalTarget = Mathf.Clamp(placeholderIndex, 0, addIndex);
                    TabManager.Move(originalIndex, finalTarget);
                }

                potentialDrag = null;
                dragging = null;
                placeholder = null;
            }, TrickleDown.TrickleDown);
        }

        private static void RegisterDropEvents(VisualElement tabContainer) {
            tabContainer.RegisterCallback<DragEnterEvent>(evt =>
            {
                if (evt.currentTarget != tabContainer) return;
                if (DragAndDrop.paths.Any(AssetDatabase.IsValidFolder))
                    tabContainer.style.backgroundColor = ColorPreset.SDropArea;
            }, TrickleDown.TrickleDown);
            tabContainer.RegisterCallback<DragLeaveEvent>(evt =>
            {
                if (evt.currentTarget != tabContainer) return;
                tabContainer.style.backgroundColor = StyleKeyword.Null;
            }, TrickleDown.TrickleDown);
            tabContainer.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (evt.currentTarget != tabContainer) return;
                DragAndDrop.visualMode = DragAndDrop.paths.Any(AssetDatabase.IsValidFolder)
                    ? DragAndDropVisualMode.Link
                    : DragAndDropVisualMode.Rejected;
            }, TrickleDown.TrickleDown);

            tabContainer.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (evt.currentTarget != tabContainer) return;
                var folderPathList = DragAndDrop.paths.Where(AssetDatabase.IsValidFolder).ToList();
                if (folderPathList.Count == 0) return;

                DragAndDrop.AcceptDrag();
                var insertIndex = tabContainer.childCount - 1;
                var createdEntries =
                    folderPathList.Select(path => new { path, name = Path.GetFileName(path) }).ToList();
                foreach (var newTab in createdEntries.Select(entry => new TabEntry(entry.path, entry.name))) {
                    TabManager.Insert(insertIndex, newTab);
                    insertIndex++;
                }

                evt.StopPropagation();
                tabContainer.style.backgroundColor = StyleKeyword.Null;
            }, TrickleDown.TrickleDown);
        }
    }
}