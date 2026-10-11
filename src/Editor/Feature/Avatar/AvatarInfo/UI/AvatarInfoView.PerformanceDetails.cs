using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarInfo
{
    public sealed partial class AvatarInfoView
    {
        private Action _closePerformanceDetails;

        private enum PerformanceDetailDisplay
        {
            PrefabCards,
            RankedBars,
            ConstraintGraph,
            Treemap
        }

        private static PerformanceDetailDisplay GetPerformanceDetailDisplay(string category)
        {
            switch (category)
            {
                case "PolyCount":
                case "TextureMegabytes": return PerformanceDetailDisplay.Treemap;
                case "ParticleTotalCount": return PerformanceDetailDisplay.RankedBars;
                case "ConstraintDepth": return PerformanceDetailDisplay.ConstraintGraph;
                default: return PerformanceDetailDisplay.PrefabCards;
            }
        }

        private void OpenPerformanceDetails(IReadOnlyList<AvatarInfoAnalysis.PerformanceMetric> metrics)
        {
            if (_closePerformanceDetails != null || panel == null || metrics.Count == 0) { return; }
            var host = (VisualElement)this;
            while (host.parent != null && host.parent != panel.visualTree) { host = host.parent; }
            var previousFocus = panel.focusController.focusedElement as VisualElement;
            var background = host.Children().Select(element =>
                (Element: element, Enabled: element.enabledSelf)).ToArray();
            var overlay = new VisualElement { name = "overviewPerformanceDetailsOverlay", focusable = true };
            UiComposition.Prepare(overlay, "Editor/Feature/Avatar/AvatarInfo/UI/avatar-info.uss");
            overlay.AddToClassList("ee4v-avatar-info__details-overlay");
            var closed = false;
            void Close()
            {
                if (closed) { return; }
                closed = true;
                UnregisterCallback<DetachFromPanelEvent>(OnViewDetached);
                foreach (var item in background) { item.Element.SetEnabled(item.Enabled); }
                overlay.RemoveFromHierarchy();
                _closePerformanceDetails = null;
                if (previousFocus?.panel != null && previousFocus.enabledInHierarchy) { previousFocus.Focus(); }
            }
            void OnViewDetached(DetachFromPanelEvent evt)
            {
                if (evt.target == this) { Close(); }
            }
            _closePerformanceDetails = Close;
            RegisterCallback<DetachFromPanelEvent>(OnViewDetached);
            overlay.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == overlay) { Close(); } });
            overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == overlay) { Close(); }
                evt.StopPropagation();
            });
            overlay.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
            overlay.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) { Close(); evt.PreventDefault(); }
                evt.StopPropagation();
            });

            var window = new VisualElement();
            window.AddToClassList("ee4v-avatar-info__details-window");
            var heading = new VisualElement();
            heading.AddToClassList("ee4v-avatar-info__details-heading");
            heading.Add(UiTextFactory.Create(I18N.Get("workflow.overview.details.open") + " · " +
                I18N.Get(_mobile ? "workflow.overview.mobile" : "workflow.overview.desktop"),
                UiClassNames.SectionTitle));
            heading.Add(new UiButton(I18N.Get("workflow.overview.details.close"), Close,
                variant: UiButtonVariant.Ghost));
            window.Add(heading);
            var body = new VisualElement();
            body.AddToClassList("ee4v-avatar-info__details-body");
            var tabs = CreatePerformanceDetailsScrollView("ee4v-avatar-info__details-tabs");
            var content = CreatePerformanceDetailsScrollView("ee4v-avatar-info__details-content");
            content.contentContainer.AddToClassList("ee4v-avatar-info__details-content-container");
            body.Add(tabs);
            body.Add(content);
            window.Add(body);
            var buttons = new UiButton[metrics.Count];
            void SelectMetric(int index)
            {
                for (var i = 0; i < buttons.Length; i++)
                    buttons[i].EnableInClassList("ee4v-avatar-info__details-tab--active", i == index);
                content.Clear();
                content.scrollOffset = Vector2.zero;
                BuildPerformanceDetails(content, metrics[index]);
            }
            foreach (var group in new[] { "rendering", "dynamics", "effects" })
            {
                var indices = Enumerable.Range(0, metrics.Count).Where(index =>
                    GetPerformanceMetricGroup(metrics[index].Category) == group).ToArray();
                if (indices.Length == 0) { continue; }
                tabs.Add(UiTextFactory.Create(I18N.Get("workflow.overview.group." + group),
                    UiClassNames.SecondaryText, "ee4v-avatar-info__details-tab-group"));
                foreach (var index in indices)
                {
                    var button = new UiButton(I18N.Get("workflow.overview.metric." + metrics[index].Category),
                        () => SelectMetric(index), variant: UiButtonVariant.Ghost,
                        labelTypographyClassName: "ee4v-avatar-info__details-tab-label");
                    button.name = "overviewDetailsTab" + metrics[index].Category;
                    button.AddToClassList("ee4v-avatar-info__details-tab");
                    button.LabelText.SetWhiteSpace(WhiteSpace.Normal);
                    button.SetContentAlignment(Justify.FlexStart);
                    buttons[index] = button;
                    tabs.Add(button);
                }
            }
            SelectMetric(0);
            overlay.Add(window);
            foreach (var item in background) { item.Element.SetEnabled(false); }
            host.Add(overlay);
            overlay.schedule.Execute(() => { if (!closed) { buttons[0].Focus(); } });
        }

        private static ScrollView CreatePerformanceDetailsScrollView(string className)
        {
            var scrollView = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible
            };
            scrollView.AddToClassList(className);
            scrollView.verticalScroller.AddToClassList("ee4v-avatar-info__details-scroller");
            return scrollView;
        }

        private static void BuildPerformanceDetails(VisualElement content,
            AvatarInfoAnalysis.PerformanceMetric metric)
        {
            var display = GetPerformanceDetailDisplay(metric.Category);
            content.EnableInClassList("ee4v-avatar-info__details-content--treemap",
                display == PerformanceDetailDisplay.Treemap);
            content.EnableInClassList("ee4v-avatar-info__details-content--prefab",
                display == PerformanceDetailDisplay.PrefabCards);
            content.EnableInClassList("ee4v-avatar-info__details-content--graph",
                display == PerformanceDetailDisplay.ConstraintGraph);
            var title = UiTextFactory.Create(I18N.Get("workflow.overview.metric." + metric.Category) +
                " · " + metric.Value, UiClassNames.SectionTitle);
            title.SetWhiteSpace(WhiteSpace.Normal);
            title.style.flexShrink = 0;
            content.Add(title);
            if (metric.DetailFailures > 0)
            {
                var error = UiTextFactory.Create(string.Format(
                    I18N.Get("workflow.overview.details.failed"), metric.DetailFailures), UiClassNames.FormError);
                error.SetWhiteSpace(WhiteSpace.Normal);
                content.Add(error);
            }
            var captured = metric.DetailUnit.HasValue && (metric.Category != "ConstraintDepth" ||
                metric.DetailUnit == AvatarInfoAnalysis.PerformanceDetailUnit.ConstraintPath &&
                metric.ConstraintGraph != null) ? metric.Details : null;
            var entries = captured?.Where(entry => entry.Amount > 0 &&
                    !double.IsInfinity(entry.Amount) && !double.IsNaN(entry.Amount))
                .OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
            if (entries == null || entries.Length == 0)
            {
                var empty = UiTextFactory.Create(I18N.Get(captured == null
                    ? "workflow.overview.details.notCaptured" : metric.Amount.GetValueOrDefault() == 0
                        ? "workflow.overview.details.empty" : "workflow.overview.details.unavailable"),
                    UiClassNames.SecondaryText);
                empty.SetWhiteSpace(WhiteSpace.Normal);
                content.Add(empty);
                return;
            }
            if (display == PerformanceDetailDisplay.ConstraintGraph)
            {
                BuildPerformanceConstraintGraph(content, metric.ConstraintGraph);
                return;
            }
            if (display != PerformanceDetailDisplay.Treemap)
            {
                BuildPerformanceDetailGallery(content, entries, display, metric.DetailUnit.Value, metric.Category);
                return;
            }
            BuildPerformanceTreemap(content, entries, metric.DetailUnit.Value);
        }

        private static void BuildPerformanceDetailGallery(VisualElement content,
            IReadOnlyList<AvatarInfoAnalysis.PerformanceDetail> entries, PerformanceDetailDisplay display,
            AvatarInfoAnalysis.PerformanceDetailUnit unit, string category)
        {
            var groupByPrefab = display == PerformanceDetailDisplay.PrefabCards &&
                unit != AvatarInfoAnalysis.PerformanceDetailUnit.Prefab;
            var shown = groupByPrefab ? entries.GroupBy(entry => entry.ItemPath).Select(group =>
                new AvatarInfoAnalysis.PerformanceDetail
                {
                    Name = group.First().ItemName, Path = group.Key,
                    ItemName = group.First().ItemName, ItemPath = group.Key,
                    ItemAssetPath = group.First().ItemAssetPath,
                    Amount = group.Sum(entry => entry.Amount),
                    Value = group.Sum(entry => entry.Amount).ToString("N0"), Children = group.ToArray()
                }).ToArray() : entries.ToArray();
            var rowUnit = groupByPrefab ? AvatarInfoAnalysis.PerformanceDetailUnit.Prefab : unit;
            var prefabCards = display == PerformanceDetailDisplay.PrefabCards;
            var rail = prefabCards ? new ScrollView(ScrollViewMode.Horizontal)
            {
                name = "overviewPerformancePrefabRail",
                horizontalScrollerVisibility = ScrollerVisibility.AlwaysVisible,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            } : null;
            var gallery = rail != null ? rail.contentContainer : new VisualElement();
            gallery.name = "overviewPerformanceDetailGallery";
            gallery.AddToClassList("ee4v-avatar-info__details-gallery");
            if (rail != null)
            {
                rail.AddToClassList("ee4v-avatar-info__details-prefab-rail");
                rail.horizontalScroller.AddToClassList("ee4v-avatar-info__details-scroller");
                gallery.AddToClassList("ee4v-avatar-info__details-gallery--rail");
                content.Add(rail);
            }
            else { content.Add(gallery); }
            var selection = new VisualElement { name = "overviewPerformanceDetailSelection" };
            selection.AddToClassList("ee4v-avatar-info__details-selection");
            selection.EnableInClassList("ee4v-avatar-info__details-selection--fill", prefabCards);
            content.Add(selection);
            var maximum = shown.Max(entry => entry.Amount);
            var total = shown.Sum(entry => entry.Amount);
            var ordered = shown.OrderByDescending(entry => entry.Amount)
                .ThenBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
            var buttons = new UiButton[ordered.Length];
            void Select(int index)
            {
                for (var i = 0; i < buttons.Length; i++)
                    buttons[i].EnableInClassList("ee4v-avatar-info__details-card--selected", i == index);
                var entry = ordered[index];
                selection.Clear();
                GetPerformanceDetailHeading(entry, rowUnit, out var title, out var location);
                var name = UiTextFactory.Create(title,
                    UiClassNames.SectionTitle);
                name.SetWhiteSpace(WhiteSpace.Normal);
                name.tooltip = GetPerformanceDetailLocation(entry, rowUnit);
                selection.Add(name);
                var path = UiTextFactory.Create(location, UiClassNames.SecondaryText);
                path.SetWhiteSpace(WhiteSpace.Normal);
                path.tooltip = name.tooltip;
                if (category != "ConstraintDepth") { selection.Add(path); }
                if (category == "BoneCount" || category == "PhysBoneComponentCount" ||
                    category == "PhysBoneTransformCount" || category == "PhysBoneCollisionCheckCount")
                    BuildPerformanceBoneTree(selection, entry.Children, category);
                else if (category == "ConstraintsCount")
                    BuildPerformanceConstraintRelations(selection, entry.Children);
                else
                    AddPerformanceDetailChildren(selection, entry.Children,
                        groupByPrefab ? (AvatarInfoAnalysis.PerformanceDetailUnit?)unit : null, true,
                        preserveSingleValues: category == "MaterialCount",
                        contentsKey: category == "PhysBoneColliderCount" ? "workflow.overview.details.colliders"
                            : "workflow.overview.details.contents",
                        childContentsKey: category == "PhysBoneColliderCount"
                            ? "workflow.overview.details.colliderUsers" : null);
            }
            for (var i = 0; i < ordered.Length; i++)
            {
                var index = i;
                var entry = ordered[i];
                var button = CreatePerformanceDetailCard(GetPerformanceDetailName(entry, rowUnit),
                    entry.Value, () => Select(index), GetPerformanceDetailLocation(entry, rowUnit));
                button.name = "overviewPerformanceDetailCard" + index;
                if (prefabCards) { button.LabelText.SetWhiteSpace(WhiteSpace.NoWrap); }
                button.EnableInClassList("ee4v-avatar-info__details-card--chart",
                    display != PerformanceDetailDisplay.PrefabCards);
                var track = new VisualElement { pickingMode = PickingMode.Ignore };
                track.AddToClassList("ee4v-avatar-info__details-bar");
                {
                    var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                    fill.AddToClassList("ee4v-avatar-info__details-bar-fill");
                    fill.style.width = Length.Percent((float)(entry.Amount /
                        (display == PerformanceDetailDisplay.PrefabCards ? total : maximum) * 100));
                    track.Add(fill);
                    if (display == PerformanceDetailDisplay.PrefabCards)
                        button.Content.Add(UiTextFactory.Create((entry.Amount / total).ToString("P1"),
                            UiClassNames.SecondaryText));
                }
                button.Content.Add(track);
                buttons[i] = button;
                if (display == PerformanceDetailDisplay.PrefabCards) { AddPerformanceCardCell(gallery, button); }
                else { gallery.Add(button); }
            }
            Select(0);
        }

        private static void BuildPerformanceConstraintRelations(VisualElement parent,
            IReadOnlyList<AvatarInfoAnalysis.PerformanceDetail> constraints, bool groupByType = true)
        {
            if (constraints == null || constraints.Count == 0) { return; }
            if (constraints.All(detail => detail.Constraint == null))
            {
                var missing = UiTextFactory.Create(I18N.Get("workflow.overview.details.notCaptured"),
                    UiClassNames.SecondaryText);
                missing.SetWhiteSpace(WhiteSpace.Normal);
                parent.Add(missing);
                return;
            }
            parent.AddToClassList("ee4v-avatar-info__details-selection--constraints");
            var scroll = CreatePerformanceDetailsScrollView("ee4v-avatar-info__details-children");
            scroll.name = "overviewPerformanceConstraintRelations";
            scroll.AddToClassList("ee4v-avatar-info__details-constraints");
            parent.Add(scroll);
            VisualElement Endpoint(AvatarInfoAnalysis.PerformanceConstraintEndpoint endpoint)
            {
                var node = new VisualElement();
                node.AddToClassList("ee4v-avatar-info__details-constraint-endpoint");
                var heading = new VisualElement();
                heading.AddToClassList("ee4v-avatar-info__details-constraint-endpoint-heading");
                var name = UiTextFactory.Create(endpoint?.Name ??
                    I18N.Get("workflow.overview.details.constraintUnassigned"));
                name.SetWhiteSpace(WhiteSpace.Normal);
                heading.Add(name);
                if (endpoint?.Weight != null)
                    heading.Add(UiTextFactory.Create(string.Format(
                        I18N.Get("workflow.overview.details.constraintWeight"),
                        endpoint.Weight.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)),
                        UiClassNames.SecondaryText, "ee4v-avatar-info__details-constraint-weight"));
                node.Add(heading);
                if (!string.IsNullOrEmpty(endpoint?.Path))
                {
                    var path = UiTextFactory.Create(GetPerformanceObjectDisplayPath(endpoint.Path, endpoint.DisplayPath),
                        UiClassNames.SecondaryText);
                    path.SetWhiteSpace(WhiteSpace.Normal);
                    path.tooltip = endpoint.Path;
                    node.Add(path);
                    node.tooltip = endpoint.Path;
                }
                return node;
            }
            foreach (var group in constraints.GroupBy(detail => groupByType
                         ? detail.Constraint?.Type ?? I18N.Get("workflow.overview.details.unit.Constraint") : string.Empty)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var section = new VisualElement();
                section.AddToClassList("ee4v-avatar-info__details-constraint-group");
                section.EnableInClassList("ee4v-avatar-info__details-constraint-group--collapsible", groupByType);
                scroll.Add(section);
                var cards = new VisualElement();
                cards.AddToClassList("ee4v-avatar-info__details-constraint-group-cards");
                if (groupByType)
                {
                    var expanded = true;
                    UiButton heading = null;
                    heading = new UiButton(group.Key, () =>
                    {
                        expanded = !expanded;
                        cards.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                        heading.SetIcon(FluentUiIcons.CreateState(expanded ? "chevron_down.png" : "chevron_right.png",
                            UiSizeTokens.Size12));
                    }, tooltip: group.Key, icon: FluentUiIcons.CreateState("chevron_down.png", UiSizeTokens.Size12),
                        variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SectionTitle);
                    heading.AddToClassList("ee4v-avatar-info__details-constraint-group-heading");
                    heading.LabelText.SetWhiteSpace(WhiteSpace.NoWrap);
                    heading.SetContentAlignment(Justify.FlexStart);
                    var count = UiTextFactory.Create(group.Count().ToString("N0"), UiClassNames.SecondaryText,
                        "ee4v-avatar-info__details-constraint-group-count");
                    count.SetWhiteSpace(WhiteSpace.NoWrap);
                    count.style.flexShrink = 0;
                    count.pickingMode = PickingMode.Ignore;
                    heading.Content.Add(count);
                    section.Add(heading);
                }
                section.Add(cards);
                var firstCard = true;
                foreach (var detail in group.OrderBy(detail => detail.Path, StringComparer.Ordinal))
                {
                    var card = new VisualElement();
                    card.AddToClassList("ee4v-avatar-info__details-constraint-card");
                    card.EnableInClassList("ee4v-avatar-info__details-constraint-card--separated", !firstCard);
                    firstCard = false;
                    cards.Add(card);
                    var relation = detail.Constraint;
                    var title = UiTextFactory.Create(groupByType ? relation?.Target?.Name ?? detail.Name
                        : relation?.Type ?? detail.Name, UiClassNames.SectionTitle);
                    title.SetWhiteSpace(WhiteSpace.Normal);
                    card.Add(title);
                    if (relation == null)
                    {
                        var missing = UiTextFactory.Create(I18N.Get("workflow.overview.details.notCaptured"),
                            UiClassNames.SecondaryText);
                        missing.SetWhiteSpace(WhiteSpace.Normal);
                        card.Add(missing);
                        continue;
                    }
                    var connection = new VisualElement();
                    connection.AddToClassList("ee4v-avatar-info__details-constraint-connection");
                    var sources = new VisualElement();
                    sources.AddToClassList("ee4v-avatar-info__details-constraint-side");
                    sources.Add(UiTextFactory.Create(I18N.Get("workflow.overview.details.constraintSources"),
                        UiClassNames.SecondaryText));
                    if (relation.Sources == null || relation.Sources.Count == 0) { sources.Add(Endpoint(null)); }
                    else { foreach (var source in relation.Sources) { sources.Add(Endpoint(source)); } }
                    connection.Add(sources);
                    connection.Add(UiTextFactory.Create("→", "ee4v-avatar-info__details-constraint-arrow"));
                    var target = new VisualElement();
                    target.AddToClassList("ee4v-avatar-info__details-constraint-side");
                    target.Add(UiTextFactory.Create(I18N.Get("workflow.overview.details.constraintTarget"),
                        UiClassNames.SecondaryText));
                    target.Add(Endpoint(relation.Target));
                    connection.Add(target);
                    card.Add(connection);
                    var component = UiTextFactory.Create(string.Format(
                        I18N.Get("workflow.overview.details.constraintComponent"), GetPerformanceDetailDisplayPath(detail)),
                        UiClassNames.SecondaryText, "ee4v-avatar-info__details-constraint-component");
                    component.SetWhiteSpace(WhiteSpace.Normal);
                    component.tooltip = detail.Path;
                    card.Add(component);
                }
            }
        }

        private sealed class PerformanceBoneTreeNode
        {
            public AvatarInfoAnalysis.PerformanceDetail Detail;
            public int Count;
            public bool HasChildren;
        }

        private static void BuildPerformanceBoneTree(VisualElement parent,
            IReadOnlyList<AvatarInfoAnalysis.PerformanceDetail> nodes, string category = "BoneCount")
        {
            var physBone = category != "BoneCount";
            if (nodes == null || nodes.Count == 0 || nodes.Any(node => node.ParentPath == null))
            {
                var empty = UiTextFactory.Create(I18N.Get("workflow.overview.details.notCaptured"),
                    UiClassNames.SecondaryText);
                empty.SetWhiteSpace(WhiteSpace.Normal);
                parent.Add(empty);
                return;
            }
            parent.AddToClassList("ee4v-avatar-info__details-selection--tree");
            var children = nodes.GroupBy(node => node.ParentPath)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var nextId = 0;
            SearchableTreeItemData<PerformanceBoneTreeNode> Build(AvatarInfoAnalysis.PerformanceDetail node)
            {
                var id = ++nextId;
                var descendants = children.TryGetValue(node.Path, out var direct)
                    ? direct.Select(Build).ToArray() : Array.Empty<SearchableTreeItemData<PerformanceBoneTreeNode>>();
                var entry = new PerformanceBoneTreeNode
                {
                    Detail = node, HasChildren = descendants.Length > 0,
                    Count = (int)node.Amount + descendants.Sum(child => child.Data.Count)
                };
                var tooltip = node.Path + "\n" + string.Format(
                    I18N.Get("workflow.overview.details.branchMetricCount"),
                    I18N.Get("workflow.overview.metric." + category), entry.Count);
                if (node.Amount == 0) { tooltip += "\n" + I18N.Get(physBone
                    ? "workflow.overview.details.physBoneContext" : "workflow.overview.details.boneContext"); }
                if (physBone && node.Children != null)
                    tooltip += "\n" + string.Join("\n", node.Children.Select(component => component.Path));
                return new SearchableTreeItemData<PerformanceBoneTreeNode>(id, entry, node.Name, tooltip, descendants);
            }
            var roots = children.TryGetValue(string.Empty, out var top)
                ? top.Select(Build).ToArray() : Array.Empty<SearchableTreeItemData<PerformanceBoneTreeNode>>();
            var selectedPath = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText,
                "ee4v-avatar-info__details-bone-path");
            selectedPath.SetWhiteSpace(WhiteSpace.Normal);
            selectedPath.style.display = DisplayStyle.None;
            var tree = new SearchableTreeView<PerformanceBoneTreeNode>(() =>
            {
                var row = new VisualElement();
                row.AddToClassList("ee4v-avatar-info__details-bone-row");
                var name = UiTextFactory.Create(string.Empty, "ee4v-avatar-info__details-bone-name");
                name.name = "boneName";
                row.Add(name);
                var attachment = UiTextFactory.Create(string.Empty, "ee4v-avatar-info__details-bone-attachment");
                attachment.name = "boneAttachment";
                row.Add(attachment);
                var count = UiTextFactory.Create(string.Empty, "ee4v-avatar-info__details-bone-count");
                count.name = "boneCount";
                row.Add(count);
                return row;
            }, (row, node) =>
            {
                row.EnableInClassList("ee4v-avatar-info__details-bone-row--context", node.Detail.Amount == 0);
                var name = row.Q<UiTextElement>("boneName");
                name.SetText(node.Detail.Name);
                name.SetColor(node.Detail.Amount == 0 ? UiColorTokens.TextMuted : UiColorTokens.TextPrimary);
                var count = row.Q<UiTextElement>("boneCount");
                count.SetText(node.Count.ToString("N0"));
                count.style.display = node.HasChildren || physBone &&
                    category != "PhysBoneComponentCount" && node.Detail.Amount > 0
                    ? DisplayStyle.Flex : DisplayStyle.None;
                var attachment = row.Q<UiTextElement>("boneAttachment");
                var components = node.Detail.Children?.Count ?? 0;
                attachment.SetText(I18N.Get("workflow.overview.details.unit.PhysBone") +
                    (components > 1 ? " × " + components.ToString("N0") : string.Empty));
                attachment.style.display = physBone && components > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }, selected =>
            {
                var node = selected.FirstOrDefault();
                selectedPath.SetText(node != null ? GetPerformanceDetailDisplayPath(node.Detail) : string.Empty);
                selectedPath.tooltip = node == null ? string.Empty : node.Detail.Path +
                    (physBone && node.Detail.Children != null ? "\n" + string.Join("\n",
                        node.Detail.Children.Select(component => component.Path +
                            (category == "PhysBoneComponentCount" ? string.Empty : " · " + component.Value))) : string.Empty);
                selectedPath.style.display = node != null ? DisplayStyle.Flex : DisplayStyle.None;
            }, emptyText: I18N.Get("workflow.overview.details.boneNoMatches"),
                searchPlaceholder: I18N.Get(physBone ? "workflow.overview.details.physBoneSearch"
                    : "workflow.overview.details.boneSearch"), fixedItemHeight: 26,
                verticalScrollerVisibility: ScrollerVisibility.AlwaysVisible);
            tree.name = physBone ? "overviewPerformancePhysBoneTree" : "overviewPerformanceBoneTree";
            tree.AddToClassList("ee4v-avatar-info__details-bone-tree");
            parent.Add(tree);
            parent.Add(selectedPath);
            tree.SetItems(roots);
            var native = tree.Q<TreeView>();
            if (physBone) { native.CollapseAll(); }
            else foreach (var root in roots)
            {
                if (root.Children.Count > 0) { native.ExpandItem(root.Id, false); }
                foreach (var child in root.Children.Where(child => child.Children.Count > 0))
                    native.ExpandItem(child.Id, false);
            }
            var scroll = native.Q<ScrollView>();
            scroll.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            scroll.verticalScroller.AddToClassList("ee4v-avatar-info__details-scroller");
        }

        private static UiButton CreatePerformanceDetailCard(string name, string value, Action select, string path,
            bool compact = false)
        {
            var button = new UiButton(name, select, tooltip: name + "\n" + value + "\n" + path,
                variant: UiButtonVariant.Ghost, labelTypographyClassName: "ee4v-avatar-info__details-card-name");
            button.AddToClassList("ee4v-avatar-info__details-card");
            button.EnableInClassList("ee4v-avatar-info__details-card--compact", compact);
            button.LabelText.SetWhiteSpace(WhiteSpace.Normal);
            button.SetContentAlignment(Justify.FlexStart);
            if (!string.IsNullOrEmpty(value))
            {
                var amount = UiTextFactory.Create(compact ? "× " + value : value,
                    "ee4v-avatar-info__details-card-value");
                amount.EnableInClassList("ee4v-avatar-info__details-card-value--badge", compact);
                amount.SetFontSize(compact ? 12 : 24);
                amount.SetWhiteSpace(compact ? WhiteSpace.NoWrap : WhiteSpace.Normal);
                amount.pickingMode = PickingMode.Ignore;
                button.Content.Add(amount);
            }
            return button;
        }

        private static void AddPerformanceCardCell(VisualElement gallery, UiButton button)
        {
            var cell = new VisualElement();
            cell.AddToClassList("ee4v-avatar-info__details-card-cell");
            cell.Add(button);
            gallery.Add(cell);
        }

        private static void ConfigurePerformanceCardGrid(VisualElement gallery)
        {
            void Layout()
            {
                var columns = Mathf.Clamp(Mathf.FloorToInt(gallery.contentRect.width / 190), 1, 4);
                foreach (var cell in gallery.Children()) { cell.style.width = Length.Percent(100f / columns); }
            }
            gallery.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (evt.newRect.width != evt.oldRect.width) { Layout(); }
            });
            gallery.schedule.Execute(Layout);
        }

        private static string GetPerformanceItemName(string name, string path) => path == null
            ? I18N.Get("workflow.overview.parameterItemsUnknown") : path.Length == 0
                ? I18N.Get("workflow.overview.parameterItemsBase") : name;

        private static string GetPerformanceDetailName(AvatarInfoAnalysis.PerformanceDetail entry,
            AvatarInfoAnalysis.PerformanceDetailUnit unit) => unit == AvatarInfoAnalysis.PerformanceDetailUnit.Prefab
            ? GetPerformanceItemName(entry.ItemName, entry.ItemPath)
            : entry.Name;

        private static string GetPerformanceDetailLocation(AvatarInfoAnalysis.PerformanceDetail entry,
            AvatarInfoAnalysis.PerformanceDetailUnit unit)
        {
            var path = entry.Path ?? string.Empty;
            if (unit != AvatarInfoAnalysis.PerformanceDetailUnit.Prefab &&
                unit != AvatarInfoAnalysis.PerformanceDetailUnit.Texture &&
                unit != AvatarInfoAnalysis.PerformanceDetailUnit.ConstraintGroup &&
                unit != AvatarInfoAnalysis.PerformanceDetailUnit.ConstraintPath)
                path = GetPerformanceItemName(entry.ItemName, entry.ItemPath) + "\n" + path;
            if (!string.IsNullOrEmpty(entry.ItemAssetPath)) { path += "\n" + entry.ItemAssetPath; }
            return path;
        }

        private static void GetPerformanceDetailHeading(AvatarInfoAnalysis.PerformanceDetail entry,
            AvatarInfoAnalysis.PerformanceDetailUnit unit, out string title, out string location)
        {
            if (unit == AvatarInfoAnalysis.PerformanceDetailUnit.Prefab ||
                unit == AvatarInfoAnalysis.PerformanceDetailUnit.Texture ||
                unit == AvatarInfoAnalysis.PerformanceDetailUnit.ConstraintGroup ||
                unit == AvatarInfoAnalysis.PerformanceDetailUnit.ConstraintPath)
            {
                title = GetPerformanceDetailName(entry, unit) + " · " + entry.Value;
                location = GetPerformanceDetailLocation(entry, unit);
                if (unit == AvatarInfoAnalysis.PerformanceDetailUnit.Prefab)
                {
                    location = GetPerformanceDetailDisplayPath(entry);
                    if (!string.IsNullOrEmpty(entry.ItemAssetPath))
                        location += (location.Length > 0 ? "\n" : string.Empty) + entry.ItemAssetPath;
                }
                return;
            }
            title = GetPerformanceItemName(entry.ItemName, entry.ItemPath);
            location = GetPerformanceDetailDisplayPath(entry) + " · " + entry.Value;
        }

        private static string GetPerformanceDetailDisplayPath(AvatarInfoAnalysis.PerformanceDetail entry) =>
            GetPerformanceObjectDisplayPath(entry.Path ?? entry.Name, entry.DisplayPath);

        private static string GetPerformanceObjectDisplayPath(string path, string displayPath = null)
        {
            if (!string.IsNullOrEmpty(displayPath)) { return displayPath; }
            path = path ?? string.Empty;
            var component = path.LastIndexOf(" / ", StringComparison.Ordinal);
            if (component >= 0) { path = path.Substring(0, component); }
            return System.Text.RegularExpressions.Regex.Replace(path, @"\[\d+\](?=/|$)", string.Empty);
        }

        private static void AddPerformanceDetailChildren(VisualElement parent,
            IReadOnlyList<AvatarInfoAnalysis.PerformanceDetail> children,
            AvatarInfoAnalysis.PerformanceDetailUnit? unit = null, bool expanded = false,
            bool preserveSingleValues = false, string contentsKey = "workflow.overview.details.contents",
            string childContentsKey = null)
        {
            if (children == null || children.Count == 0) { return; }
            var showValues = unit.HasValue &&
                (preserveSingleValues || children.Any(child => child.Amount != 1));
            var details = CreatePerformanceDetailsScrollView("ee4v-avatar-info__details-children");
            details.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            var toggle = new UiButton(string.Format(I18N.Get(contentsKey), children.Count), () =>
            {
                details.style.display = details.style.display.value == DisplayStyle.None
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }, variant: UiButtonVariant.Ghost);
            toggle.AddToClassList("ee4v-avatar-info__details-children-toggle");
            parent.Add(toggle);
            parent.Add(details);
            var gallery = new VisualElement();
            gallery.AddToClassList("ee4v-avatar-info__details-gallery");
            gallery.AddToClassList("ee4v-avatar-info__details-gallery--cards");
            details.Add(gallery);
            var selection = new VisualElement();
            selection.AddToClassList("ee4v-avatar-info__details-child-selection");
            selection.style.display = DisplayStyle.None;
            parent.Add(selection);
            var buttons = new List<UiButton>();
            foreach (var child in children)
            {
                UiButton button = null;
                button = CreatePerformanceDetailCard(child.Name,
                    showValues ? child.Value : null, () =>
                {
                    foreach (var item in buttons)
                        item.EnableInClassList("ee4v-avatar-info__details-card--selected", item == button);
                    selection.Clear();
                    selection.style.display = DisplayStyle.Flex;
                    var path = UiTextFactory.Create(GetPerformanceDetailDisplayPath(child), UiClassNames.SecondaryText);
                    path.SetWhiteSpace(WhiteSpace.Normal);
                    path.tooltip = child.Path;
                    selection.Add(path);
                    AddPerformanceDetailChildren(selection, child.Children, expanded: true,
                        contentsKey: childContentsKey ?? "workflow.overview.details.contents");
                }, child.Path, compact: true);
                buttons.Add(button);
                AddPerformanceCardCell(gallery, button);
            }
            ConfigurePerformanceCardGrid(gallery);
        }

        private static void BuildPerformanceTreemap(VisualElement content,
            IReadOnlyList<AvatarInfoAnalysis.PerformanceDetail> details,
            AvatarInfoAnalysis.PerformanceDetailUnit unit)
        {
            var entries = details.Select((entry, index) => new { Entry = entry, Color = GetParameterItemColor(index) })
                .OrderByDescending(item => item.Entry.Amount).ToArray();
            var map = new VisualElement { name = "overviewPerformanceTreemap" };
            map.AddToClassList("ee4v-avatar-info__details-treemap");
            content.Add(map);
            var selection = new VisualElement { name = "overviewPerformanceDetailSelection" };
            selection.AddToClassList("ee4v-avatar-info__details-selection");
            selection.AddToClassList("ee4v-avatar-info__details-selection--treemap");
            var heading = new VisualElement();
            heading.AddToClassList("ee4v-avatar-info__details-selection-heading");
            var selectedValue = UiTextFactory.Create(string.Empty);
            selectedValue.SetWhiteSpace(WhiteSpace.Normal);
            var selectedPath = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            selectedPath.SetWhiteSpace(WhiteSpace.Normal);
            heading.Add(selectedValue);
            heading.Add(selectedPath);
            selection.Add(heading);
            var related = new VisualElement { name = "overviewPerformanceDetailRelated" };
            related.AddToClassList("ee4v-avatar-info__details-related");
            selection.Add(related);
            content.Add(selection);
            var cells = new UiButton[entries.Length];
            void SelectEntry(int index)
            {
                var entry = entries[index].Entry;
                GetPerformanceDetailHeading(entry, unit, out var title, out var location);
                selectedValue.SetText(title);
                selectedPath.SetText(location);
                heading.tooltip = GetPerformanceDetailLocation(entry, unit);
                selectedValue.tooltip = heading.tooltip;
                selectedPath.tooltip = heading.tooltip;
                related.Clear();
                related.style.display = entry.Children?.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                AddPerformanceDetailChildren(related, entry.Children, contentsKey:
                    unit == AvatarInfoAnalysis.PerformanceDetailUnit.Texture
                        ? "workflow.overview.details.usages" : "workflow.overview.details.contents");
                for (var i = 0; i < cells.Length; i++)
                    cells[i].EnableInClassList("ee4v-avatar-info__details-tile--selected", i == index);
            }
            for (var i = 0; i < entries.Length; i++)
            {
                var index = i;
                var entry = entries[i].Entry;
                var cell = new UiButton(entry.Name, () => SelectEntry(index),
                    tooltip: entry.Name + "\n" + entry.Value + "\n" + entry.Path,
                    variant: UiButtonVariant.Ghost,
                    labelTypographyClassName: "ee4v-avatar-info__details-tile-name");
                cell.AddToClassList("ee4v-avatar-info__details-tile");
                cell.style.backgroundColor = Color.Lerp(UiColorTokens.SurfaceRaised, entries[i].Color, 0.35f);
                cell.SetContentAlignment(Justify.Center);
                cell.SetLabelColor(UiColorTokens.TextPrimary);
                cell.SetLabelTextAlign(TextAnchor.MiddleCenter);
                var value = UiTextFactory.Create(entry.Value, "ee4v-avatar-info__details-tile-value");
                value.SetWhiteSpace(WhiteSpace.NoWrap);
                value.SetTextAlign(TextAnchor.MiddleCenter);
                value.pickingMode = PickingMode.Ignore;
                cell.Content.Add(value);
                var valueText = value.Q<Label>();
                var nameText = cell.LabelText.Q<Label>();
                void UpdateLabels()
                {
                    var available = cell.contentRect.size;
                    var valueSize = valueText.MeasureTextSize(entry.Value, 0, MeasureMode.Undefined,
                        0, MeasureMode.Undefined);
                    var nameSize = nameText.MeasureTextSize(entry.Name, 0, MeasureMode.Undefined,
                        0, MeasureMode.Undefined);
                    var showValue = available.x >= Mathf.Ceil(valueSize.x) &&
                        available.y >= Mathf.Ceil(valueSize.y);
                    value.style.display = showValue ? DisplayStyle.Flex : DisplayStyle.None;
                    cell.LabelText.style.display = showValue && available.x >= 80 &&
                        available.y >= Mathf.Ceil(valueSize.y + nameSize.y)
                        ? DisplayStyle.Flex : DisplayStyle.None;
                }
                cell.RegisterCallback<GeometryChangedEvent>(_ => UpdateLabels());
                cell.schedule.Execute(UpdateLabels);
                cell.RegisterCallback<FocusInEvent>(_ => SelectEntry(index));
                cells[i] = cell;
                map.Add(cell);
            }
            SelectEntry(0);
            var weights = entries.Select(item => Math.Max(double.Epsilon,
                item.Entry.Amount / entries[0].Entry.Amount)).ToArray();
            void Layout()
            {
                var rect = map.contentRect;
                if (rect.width > 0 && rect.height > 0)
                    LayoutTreemap(cells, weights, 0, cells.Length, new Rect(0, 0, rect.width, rect.height),
                        UnityEditor.EditorGUIUtility.pixelsPerPoint);
            }
            map.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (evt.newRect.size != evt.oldRect.size) { Layout(); }
            });
            map.schedule.Execute(Layout);
        }

        private static void LayoutTreemap(UiButton[] cells, double[] weights, int start, int end, Rect rect,
            float pixelsPerPoint)
        {
            if (end - start == 1)
            {
                var cell = cells[start];
                var left = Mathf.Round(rect.xMin * pixelsPerPoint) / pixelsPerPoint;
                var top = Mathf.Round(rect.yMin * pixelsPerPoint) / pixelsPerPoint;
                var right = Mathf.Round(rect.xMax * pixelsPerPoint) / pixelsPerPoint;
                var bottom = Mathf.Round(rect.yMax * pixelsPerPoint) / pixelsPerPoint;
                var shortestSide = Mathf.Min(right - left, bottom - top);
                var gap = Mathf.Min(2f, Mathf.Floor(shortestSide * 0.1f * pixelsPerPoint) / pixelsPerPoint);
                var width = Mathf.Max(0, right - left - gap);
                var height = Mathf.Max(0, bottom - top - gap);
                cell.EnableInClassList("ee4v-avatar-info__details-tile--small", Mathf.Min(width, height) < 20f);
                cell.style.left = left;
                cell.style.top = top;
                cell.style.width = width;
                cell.style.height = height;
                return;
            }
            var total = 0d;
            for (var i = start; i < end; i++) { total += weights[i]; }
            var split = start + 1;
            var first = weights[start];
            while (split < end - 1 && Math.Abs(first + weights[split] - total / 2) < Math.Abs(first - total / 2))
            {
                first += weights[split];
                split++;
            }
            var ratio = (float)(first / total);
            if (rect.width >= rect.height)
            {
                var width = rect.width * ratio;
                LayoutTreemap(cells, weights, start, split, new Rect(rect.x, rect.y, width, rect.height), pixelsPerPoint);
                LayoutTreemap(cells, weights, split, end,
                    new Rect(rect.x + width, rect.y, rect.width - width, rect.height), pixelsPerPoint);
            }
            else
            {
                var height = rect.height * ratio;
                LayoutTreemap(cells, weights, start, split, new Rect(rect.x, rect.y, rect.width, height), pixelsPerPoint);
                LayoutTreemap(cells, weights, split, end,
                    new Rect(rect.x, rect.y + height, rect.width, rect.height - height), pixelsPerPoint);
            }
        }
    }
}
