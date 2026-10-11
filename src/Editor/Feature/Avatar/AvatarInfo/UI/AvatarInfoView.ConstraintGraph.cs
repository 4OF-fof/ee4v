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
        private sealed class PerformanceConstraintGraphNode
        {
            public string Key;
            public AvatarInfoAnalysis.PerformanceDetail Detail;
            public AvatarInfoAnalysis.PerformanceConstraintEndpoint Endpoint;
            public int Depth;
            public Rect Bounds;
            public UiButton Button;
        }

        private static void BuildPerformanceConstraintGraph(VisualElement content,
            IReadOnlyList<AvatarInfoAnalysis.PerformanceDetail> details)
        {
            if (details == null || details.Count == 0 || details.Any(detail => detail.Constraint?.Depth == null ||
                    detail.Constraint.Dependencies == null)) { return; }
            var nodes = details.ToDictionary(detail => detail.Path, detail => new PerformanceConstraintGraphNode
            {
                Key = detail.Path, Detail = detail, Endpoint = detail.Constraint.Target,
                Depth = detail.Constraint.Depth.Value
            }, StringComparer.Ordinal);
            var edges = new List<(PerformanceConstraintGraphNode From, PerformanceConstraintGraphNode To)>();
            var targets = new HashSet<string>(details.Select(detail => detail.Constraint.Target?.Path)
                .Where(path => path != null), StringComparer.Ordinal);
            foreach (var detail in details)
            {
                var node = nodes[detail.Path];
                foreach (var dependency in detail.Constraint.Dependencies)
                    if (nodes.TryGetValue(dependency, out var parent)) { edges.Add((parent, node)); }
                var sources = detail.Constraint.Sources ?? Array.Empty<AvatarInfoAnalysis.PerformanceConstraintEndpoint>();
                for (var index = 0; index < sources.Count; index++)
                {
                    var source = sources[index];
                    if (source.Path != null && targets.Contains(source.Path)) { continue; }
                    var key = "source:" + (source.Path ?? detail.Path + ":" + index);
                    if (!nodes.TryGetValue(key, out var input))
                    {
                        input = new PerformanceConstraintGraphNode { Key = key, Endpoint = source, Depth = 0 };
                        nodes.Add(key, input);
                    }
                    if (!edges.Any(edge => edge.From == input && edge.To == node)) { edges.Add((input, node)); }
                }
            }
            var incoming = edges.GroupBy(edge => edge.To).ToDictionary(group => group.Key,
                group => group.Select(edge => edge.From).ToArray());
            const float nodeWidth = 184;
            const float nodeHeight = 76;
            const float columnWidth = 248;
            const float rowHeight = 100;
            var layers = nodes.Values.GroupBy(node => node.Depth).OrderBy(group => group.Key).ToArray();
            foreach (var layer in layers)
            {
                var ordered = layer.OrderBy(node => incoming.TryGetValue(node, out var parents)
                        ? parents.Average(parent => parent.Bounds.center.y) : 0)
                    .ThenBy(node => node.Key, StringComparer.Ordinal).ToArray();
                for (var index = 0; index < ordered.Length; index++)
                    ordered[index].Bounds = new Rect(16 + layer.Key * columnWidth, 42 + index * rowHeight,
                        nodeWidth, nodeHeight);
            }
            var width = nodes.Values.Max(node => node.Bounds.xMax) + 16;
            var height = nodes.Values.Max(node => node.Bounds.yMax) + 16;
            var root = new VisualElement { name = "overviewPerformanceConstraintGraph" };
            root.AddToClassList("ee4v-avatar-info__constraint-graph");
            content.Add(root);
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-avatar-info__constraint-graph-toolbar");
            root.Add(toolbar);
            var viewport = new VisualElement
            {
                name = "overviewPerformanceConstraintGraphViewport"
            };
            viewport.AddToClassList("ee4v-avatar-info__constraint-graph-viewport");
            root.Add(viewport);
            var canvas = new VisualElement { pickingMode = PickingMode.Ignore };
            canvas.style.position = Position.Absolute;
            canvas.style.left = 0;
            canvas.style.top = 0;
            canvas.style.width = width;
            canvas.style.height = height;
            canvas.style.transformOrigin = new TransformOrigin(0, 0, 0);
            viewport.Add(canvas);
            var selection = new VisualElement { name = "overviewPerformanceConstraintGraphSelection" };
            selection.AddToClassList("ee4v-avatar-info__details-selection");
            selection.AddToClassList("ee4v-avatar-info__constraint-graph-selection");
            root.Add(selection);
            var highlighted = new HashSet<PerformanceConstraintGraphNode>();
            var suppressClick = false;
            void Select(PerformanceConstraintGraphNode node)
            {
                highlighted.Clear();
                var pending = new Stack<PerformanceConstraintGraphNode>();
                pending.Push(node);
                while (pending.Count > 0)
                {
                    var current = pending.Pop();
                    if (!highlighted.Add(current)) { continue; }
                    if (incoming.TryGetValue(current, out var parents))
                        foreach (var parent in parents) { pending.Push(parent); }
                }
                foreach (var item in nodes.Values)
                {
                    item.Button.EnableInClassList("ee4v-avatar-info__constraint-graph-node--selected", item == node);
                    item.Button.EnableInClassList("ee4v-avatar-info__constraint-graph-node--ancestor",
                        highlighted.Contains(item) && item != node);
                }
                selection.Clear();
                var name = node.Endpoint?.Name ?? node.Detail?.Name ??
                    I18N.Get("workflow.overview.details.constraintUnassigned");
                var title = UiTextFactory.Create(name + " · " + (node.Detail != null
                    ? string.Format(I18N.Get("workflow.overview.details.constraintDepth"), node.Depth)
                    : I18N.Get("workflow.overview.details.constraintSources")), UiClassNames.SectionTitle);
                title.SetWhiteSpace(WhiteSpace.Normal);
                selection.Add(title);
                if (node.Detail != null)
                    BuildPerformanceConstraintRelations(selection, new[] { node.Detail }, groupByType: false);
                else
                {
                    var path = UiTextFactory.Create(GetPerformanceObjectDisplayPath(node.Endpoint?.Path,
                        node.Endpoint?.DisplayPath), UiClassNames.SecondaryText);
                    path.SetWhiteSpace(WhiteSpace.Normal);
                    path.tooltip = node.Endpoint?.Path;
                    selection.Add(path);
                }
                canvas.MarkDirtyRepaint();
            }
            foreach (var layer in layers)
            {
                var heading = UiTextFactory.Create(layer.Key == 0
                    ? I18N.Get("workflow.overview.details.constraintSources")
                    : string.Format(I18N.Get("workflow.overview.details.constraintDepth"), layer.Key),
                    UiClassNames.SecondaryText);
                heading.style.position = Position.Absolute;
                heading.style.left = 16 + layer.Key * columnWidth;
                heading.style.top = 12;
                canvas.Add(heading);
            }
            foreach (var node in nodes.Values)
            {
                var name = node.Endpoint?.Name ?? node.Detail?.Name ??
                    I18N.Get("workflow.overview.details.constraintUnassigned");
                var button = new UiButton(name, () => { if (!suppressClick) { Select(node); } }, variant: UiButtonVariant.Ghost,
                    tooltip: name + "\n" + node.Endpoint?.Path + "\n" + node.Detail?.Path,
                    labelTypographyClassName: "ee4v-avatar-info__constraint-graph-node-name");
                button.name = "overviewConstraintGraphNode" + node.Key;
                button.AddToClassList("ee4v-avatar-info__constraint-graph-node");
                button.EnableInClassList("ee4v-avatar-info__constraint-graph-node--source", node.Detail == null);
                button.LabelText.SetWhiteSpace(WhiteSpace.NoWrap);
                button.SetContentAlignment(Justify.FlexStart);
                button.Content.Add(UiTextFactory.Create(node.Detail?.Constraint.Type ??
                    I18N.Get("workflow.overview.details.constraintSources"), UiClassNames.SecondaryText,
                    "ee4v-avatar-info__constraint-graph-node-type"));
                button.style.left = node.Bounds.x;
                button.style.top = node.Bounds.y;
                button.style.width = node.Bounds.width;
                button.style.height = node.Bounds.height;
                node.Button = button;
                canvas.Add(button);
            }
            canvas.generateVisualContent += context =>
            {
                var painter = context.painter2D;
                foreach (var edge in edges.OrderBy(edge => highlighted.Contains(edge.From) && highlighted.Contains(edge.To)))
                {
                    var active = highlighted.Contains(edge.From) && highlighted.Contains(edge.To);
                    var from = new Vector2(edge.From.Bounds.xMax, edge.From.Bounds.center.y);
                    var to = new Vector2(edge.To.Bounds.xMin, edge.To.Bounds.center.y);
                    var bend = Math.Max(28, (to.x - from.x) * 0.5f);
                    var color = active ? (Color)UiColorTokens.Focus : (Color)UiColorTokens.TextMuted;
                    if (!active) { color.a *= 0.45f; }
                    painter.strokeColor = color;
                    painter.lineWidth = active ? 2.5f : 1.5f;
                    painter.BeginPath();
                    painter.MoveTo(from);
                    painter.BezierCurveTo(from + Vector2.right * bend, to - Vector2.right * bend, to);
                    painter.Stroke();
                    painter.fillColor = color;
                    painter.BeginPath();
                    painter.MoveTo(to);
                    painter.LineTo(to + new Vector2(-8, -4));
                    painter.LineTo(to + new Vector2(-8, 4));
                    painter.ClosePath();
                    painter.Fill();
                }
            };
            var zoom = 1f;
            var pan = Vector2.zero;
            var fitView = true;
            UiButton zoomButton = null;
            void RefreshOuterScroll()
            {
                if (!(content is ScrollView outer) || outer.panel == null) { return; }
                var viewportHeight = outer.contentViewport.layout.height;
                var contentHeight = Math.Max(outer.contentContainer.layout.height,
                    outer.contentContainer.Children().Select(child => child.layout.yMax).DefaultIfEmpty(0).Max());
                if (viewportHeight <= 0 || contentHeight <= 0) { return; }
                var maximum = Math.Max(0, contentHeight - viewportHeight);
                outer.verticalScroller.Adjust(Math.Min(1, viewportHeight / contentHeight));
                outer.verticalScroller.highValue = maximum;
                outer.verticalScroller.SetEnabled(maximum > 0);
                outer.scrollOffset = new Vector2(outer.scrollOffset.x,
                    Mathf.Clamp(outer.scrollOffset.y, 0, maximum));
            }
            root.RegisterCallback<GeometryChangedEvent>(evt => root.schedule.Execute(RefreshOuterScroll));
            void ApplyView()
            {
                canvas.transform.scale = new Vector3(zoom, zoom, 1);
                canvas.transform.position = new Vector3(pan.x, pan.y, 0);
                zoomButton?.LabelText.SetText(zoom.ToString("P0"));
                canvas.MarkDirtyRepaint();
                root.schedule.Execute(RefreshOuterScroll);
            }
            void SetZoom(float value, Vector2 anchor)
            {
                fitView = false;
                var next = Mathf.Clamp(value, 0.0001f, 3f);
                pan = anchor - (anchor - pan) * (next / zoom);
                zoom = next;
                ApplyView();
            }
            void Fit()
            {
                var area = viewport.contentRect;
                if (area.width <= 0 || area.height <= 0) { return; }
                fitView = true;
                zoom = Math.Min(1, Math.Min(Math.Max(1, area.width - 24) / width,
                    Math.Max(1, area.height - 24) / height));
                pan = area.center - new Vector2(width, height) * (zoom * 0.5f);
                ApplyView();
            }
            viewport.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (fitView) { Fit(); }
            });
            viewport.RegisterCallback<WheelEvent>(evt =>
            {
                SetZoom(zoom * Mathf.Exp(-evt.delta.y * 0.08f), viewport.WorldToLocal(evt.mousePosition));
                evt.StopImmediatePropagation();
                evt.PreventDefault();
            }, TrickleDown.TrickleDown);
            var pointer = -1;
            var panButton = -1;
            var dragStart = Vector2.zero;
            var panStart = Vector2.zero;
            var dragging = false;
            PerformanceConstraintGraphNode pressedNode = null;
            viewport.RegisterCallback<PointerDownEvent>(evt =>
            {
                if ((evt.button != 0 && evt.button != 2) || pointer >= 0) { return; }
                pointer = evt.pointerId;
                panButton = evt.button;
                dragStart = evt.position;
                panStart = pan;
                dragging = false;
                suppressClick = false;
                pressedNode = null;
                for (var target = evt.button == 0 ? evt.target as VisualElement : null;
                     target != null && target != viewport; target = target.parent)
                {
                    if (!(target is UiButton button)) { continue; }
                    pressedNode = nodes.Values.FirstOrDefault(node => node.Button == button);
                    break;
                }
                viewport.CapturePointer(pointer);
                evt.StopImmediatePropagation();
                evt.PreventDefault();
            }, TrickleDown.TrickleDown);
            viewport.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (evt.pointerId != pointer) { return; }
                var delta = (Vector2)evt.position - dragStart;
                if (!dragging && delta.sqrMagnitude < 16) { return; }
                dragging = true;
                suppressClick = true;
                fitView = false;
                if (!viewport.HasPointerCapture(pointer)) { viewport.CapturePointer(pointer); }
                pan = panStart + delta;
                ApplyView();
                evt.StopImmediatePropagation();
                evt.PreventDefault();
            }, TrickleDown.TrickleDown);
            viewport.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.pointerId != pointer || evt.button != panButton) { return; }
                var moved = dragging;
                var clicked = pressedNode;
                pointer = -1;
                panButton = -1;
                dragging = false;
                pressedNode = null;
                viewport.ReleasePointer(evt.pointerId);
                if (!moved && clicked != null) { Select(clicked); }
                evt.StopImmediatePropagation();
                evt.PreventDefault();
                viewport.schedule.Execute(() => suppressClick = false);
            }, TrickleDown.TrickleDown);
            viewport.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (evt.target != viewport) { return; }
                pointer = -1;
                panButton = -1;
                dragging = false;
                pressedNode = null;
            });
            toolbar.Add(new UiButton("−", () => SetZoom(zoom / 1.25f, viewport.contentRect.center),
                tooltip: I18N.Get("workflow.overview.details.graphZoomOut"), variant: UiButtonVariant.Ghost));
            zoomButton = new UiButton("100%", () => SetZoom(1, viewport.contentRect.center),
                tooltip: I18N.Get("workflow.overview.details.graphActualSize"), variant: UiButtonVariant.Ghost);
            toolbar.Add(zoomButton);
            toolbar.Add(new UiButton("+", () => SetZoom(zoom * 1.25f, viewport.contentRect.center),
                tooltip: I18N.Get("workflow.overview.details.graphZoomIn"), variant: UiButtonVariant.Ghost));
            toolbar.Add(new UiButton(I18N.Get("workflow.overview.details.graphFit"), Fit,
                variant: UiButtonVariant.Ghost));
            ApplyView();
            Select(nodes.Values.Where(node => node.Detail != null).OrderByDescending(node => node.Depth)
                .ThenBy(node => node.Key, StringComparer.Ordinal).First());
            root.schedule.Execute(Fit);
        }
    }
}
