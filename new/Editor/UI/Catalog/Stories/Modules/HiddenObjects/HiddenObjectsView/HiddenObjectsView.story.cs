using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.HiddenObjects;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class HiddenObjectsViewCatalogRegistrarStory
    {
        private sealed class HiddenObjectsViewCatalogRegistrar
            : CatalogWindow.ICatalogRegistrar
        {
            public int Order
            {
                get { return 102; }
            }

            public void Register(
                CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Content/Icon/icon.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/SearchField/search-field.uss");
                registry.RegisterStyleSheet(
                    "Editor/EditorEnhancements/HiddenObjects/UI/hidden-objects-window.uss");
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        "hidden-objects-view",
                        "Domain/HiddenObjects",
                        "HiddenObjectsView",
                        "Hierarchy から隠された object を Scene ごとに検索、選択、復帰する管理 view です。",
                        "Unity 標準の editor window に合わせた toolbar、仮想化 TreeView、status/action bar で構成し、表示 state と入力 event だけを扱います。",
                        new[]
                        {
                            "SearchField",
                            "Icon",
                            "TreeView"
                        },
                        CatalogWindow.ComponentImplementationKind
                            .UiToolkit,
                        (window, parent) =>
                            BuildHiddenObjectsViewStory(
                                window,
                                parent)));
            }
        }

        private static void BuildHiddenObjectsViewStory(
            CatalogWindow window,
            VisualElement parent)
        {
            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface();
            surface.style.paddingLeft = UiSpacingTokens.None;
            surface.style.paddingRight = UiSpacingTokens.None;
            surface.style.paddingTop = UiSpacingTokens.None;
            surface.style.paddingBottom = UiSpacingTokens.None;
            surface.style.height = 360f;

            var selectedIds = new HashSet<int> { 103 };
            var query = string.Empty;
            var selectedSceneHandle = 0;
            var sourceGroups = CreateSourceGroups();
            var view = new HiddenObjectsView(
                new HiddenObjectsViewText(
                    "Search hidden objects",
                    "Filter hidden objects by name",
                    "Clear search",
                    "Filter by Scene",
                    "Refresh",
                    "Scan all loaded Scenes again",
                    "Select all",
                    "Clear selection",
                    "Show in Hierarchy"));
            Action refresh = null;

            view.QueryChanged += value =>
            {
                query = value ?? string.Empty;
                refresh();
            };
            view.SceneChanged += sceneHandle =>
            {
                selectedSceneHandle = sceneHandle;
                refresh();
            };
            view.SelectionChanged += (instanceId, selected) =>
            {
                if (selected)
                {
                    selectedIds.Add(instanceId);
                }
                else
                {
                    selectedIds.Remove(instanceId);
                }

                refresh();
            };
            view.SelectAllRequested += () =>
            {
                foreach (var group in FilterGroups(
                             sourceGroups,
                             selectedSceneHandle,
                             query))
                {
                    AddHiddenIds(group.Roots, selectedIds);
                }

                refresh();
            };
            view.ClearSelectionRequested += () =>
            {
                selectedIds.Clear();
                refresh();
            };
            view.RevealRequested += () =>
            {
                selectedIds.Clear();
                refresh();
            };

            refresh = () =>
            {
                var groups = FilterGroups(
                    sourceGroups,
                    selectedSceneHandle,
                    query);
                var visibleCount = groups.Sum(
                    group => CountHidden(group.Roots));
                var displayedGroups = groups
                    .Select(group => ApplySelection(
                        group,
                        selectedIds))
                    .ToArray();
                view.SetState(new HiddenObjectsViewState(
                    displayedGroups,
                    new[]
                    {
                        new HiddenObjectSceneOptionViewState(
                            0,
                            "All Scenes"),
                        new HiddenObjectSceneOptionViewState(
                            10,
                            "Main"),
                        new HiddenObjectSceneOptionViewState(
                            20,
                            "UI")
                    },
                    selectedSceneHandle,
                    query,
                    string.Format(
                        "{0} hidden · {1} shown · {2} selected",
                        4,
                        visibleCount,
                        selectedIds.Count),
                    string.IsNullOrWhiteSpace(query)
                        ? "No hidden objects"
                        : "No matching objects",
                    string.IsNullOrWhiteSpace(query)
                        ? "No hidden objects were found."
                        : "No hidden objects match the current filters.",
                    visibleCount,
                    selectedIds.Count));
            };

            surface.Add(view);
            preview.Body.Add(surface);
            refresh();
        }

        private static IReadOnlyList<HiddenObjectSceneGroupViewState>
            CreateSourceGroups()
        {
            var objectIcon = IconState.FromBuiltinIcon(
                UiBuiltinIcon.GenericFile,
                UiSizeTokens.Size16);
            return new[]
            {
                new HiddenObjectSceneGroupViewState(
                    10,
                    "Main",
                    "3 hidden",
                    new[]
                    {
                        new HiddenObjectNodeViewState(
                            101,
                            "Environment",
                            false,
                            false,
                            objectIcon,
                            new[]
                            {
                                new HiddenObjectNodeViewState(
                                    102,
                                    "Reflection Probe",
                                    true,
                                    false,
                                    objectIcon,
                                    null),
                                new HiddenObjectNodeViewState(
                                    103,
                                    "Debug Lights",
                                    true,
                                    true,
                                    objectIcon,
                                    null)
                            }),
                        new HiddenObjectNodeViewState(
                            104,
                            "Runtime Helpers",
                            true,
                            false,
                            objectIcon,
                            null)
                    }),
                new HiddenObjectSceneGroupViewState(
                    20,
                    "UI",
                    "1 hidden",
                    new[]
                    {
                        new HiddenObjectNodeViewState(
                            201,
                            "Safe Area Preview",
                            true,
                            false,
                            objectIcon,
                            null)
                    })
            };
        }

        private static IReadOnlyList<HiddenObjectSceneGroupViewState>
            FilterGroups(
                IReadOnlyList<HiddenObjectSceneGroupViewState> groups,
                int selectedSceneHandle,
                string query)
        {
            var results =
                new List<HiddenObjectSceneGroupViewState>();
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (selectedSceneHandle != 0 &&
                    group.SceneHandle != selectedSceneHandle)
                {
                    continue;
                }

                var roots = FilterNodes(group.Roots, query);
                if (roots.Count > 0)
                {
                    results.Add(new HiddenObjectSceneGroupViewState(
                        group.SceneHandle,
                        group.SceneName,
                        string.Format(
                            "{0} hidden",
                            CountHidden(roots)),
                        roots));
                }
            }

            return results;
        }

        private static IReadOnlyList<HiddenObjectNodeViewState>
            FilterNodes(
                IReadOnlyList<HiddenObjectNodeViewState> nodes,
                string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return nodes;
            }

            var results = new List<HiddenObjectNodeViewState>();
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var children = FilterNodes(node.Children, query);
                var matches = node.IsHidden &&
                    node.Name.IndexOf(
                        query,
                        StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matches && children.Count == 0)
                {
                    continue;
                }

                results.Add(new HiddenObjectNodeViewState(
                    node.InstanceId,
                    node.Name,
                    node.IsHidden,
                    node.IsSelected,
                    node.Icon,
                    children));
            }

            return results;
        }

        private static HiddenObjectSceneGroupViewState ApplySelection(
            HiddenObjectSceneGroupViewState group,
            ISet<int> selectedIds)
        {
            return new HiddenObjectSceneGroupViewState(
                group.SceneHandle,
                group.SceneName,
                group.HiddenCountText,
                group.Roots
                    .Select(node => ApplySelection(node, selectedIds))
                    .ToArray());
        }

        private static HiddenObjectNodeViewState ApplySelection(
            HiddenObjectNodeViewState node,
            ISet<int> selectedIds)
        {
            return new HiddenObjectNodeViewState(
                node.InstanceId,
                node.Name,
                node.IsHidden,
                selectedIds.Contains(node.InstanceId),
                node.Icon,
                node.Children
                    .Select(child => ApplySelection(
                        child,
                        selectedIds))
                    .ToArray());
        }

        private static int CountHidden(
            IReadOnlyList<HiddenObjectNodeViewState> nodes)
        {
            var count = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                count += nodes[i].IsHidden ? 1 : 0;
                count += CountHidden(nodes[i].Children);
            }

            return count;
        }

        private static void AddHiddenIds(
            IReadOnlyList<HiddenObjectNodeViewState> nodes,
            ISet<int> selectedIds)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].IsHidden)
                {
                    selectedIds.Add(nodes[i].InstanceId);
                }

                AddHiddenIds(nodes[i].Children, selectedIds);
            }
        }
    }
}
