using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetModificationWorkflowView
    {
        private VisualElement _variantChangesHoverAnchor;
        private VisualElement _variantChangesHover;
        private IVisualElementScheduledItem _variantChangesPendingShow;
        private IVisualElementScheduledItem _variantChangesPendingHide;

        private void AttachVariantChangesHover(UiButton anchor, bool discard)
        {
            anchor.RegisterCallback<PointerEnterEvent>(_ =>
            {
                HideVariantChangesHover();
                if (!anchor.enabledInHierarchy) { return; }
                _variantChangesHoverAnchor = anchor;
                _variantChangesPendingShow = anchor.schedule.Execute(() =>
                {
                    _variantChangesPendingShow = null;
                    if (_variantChangesHoverAnchor == anchor && anchor.enabledInHierarchy)
                    {
                        ShowVariantChangesHover(anchor, discard);
                    }
                }).StartingIn(300);
            });
            anchor.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (_variantChangesHoverAnchor != anchor) { return; }
                _variantChangesPendingShow?.Pause();
                _variantChangesPendingShow = null;
                ScheduleHideVariantChangesHover();
            });
            anchor.RegisterCallback<PointerDownEvent>(_ => HideVariantChangesHover(), TrickleDown.TrickleDown);
            anchor.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (_variantChangesHoverAnchor == anchor) { HideVariantChangesHover(); }
            });
        }

        private void ShowVariantChangesHover(VisualElement anchor, bool discard)
        {
            if (_disposed || _savingVariant || _avatarContext.Root == null || !anchor.enabledInHierarchy) { return; }
            var popup = new VisualElement();
            popup.AddToClassList("ee4v-modification-workflow__changes-hover");
            popup.style.width = Mathf.Min(600f, Mathf.Max(0f, contentRect.width - 16f));
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList("ee4v-modification-workflow__changes-list");
            popup.Add(scroll);
            try
            {
                _manager = _manager ?? AssetManagerWindowSession.GetManager();
                var variants = AssetManagerWindowSession.TryGetVariantManager(_manager);
                var details = variants.GetChangeDetails(GetWorkingAssetPath());
                var pending = DescribeSceneChanges();
                AddChangeGroups(scroll, discard ? details.DiscardChanges : details.SaveChanges,
                    !discard || details.CurrentRevisionNumber.HasValue
                        ? pending : Array.Empty<(AssetVariantChangeKind Kind, string Content)>());
            }
            catch (Exception exception)
            {
                AddChangeText(scroll, I18N.Get("variant.changesFailed"));
                AddChangeText(scroll, exception.Message, true);
            }
            _variantChangesHover = popup;
            popup.RegisterCallback<PointerEnterEvent>(_ =>
            {
                _variantChangesPendingHide?.Pause();
                _variantChangesPendingHide = null;
            });
            popup.RegisterCallback<PointerLeaveEvent>(_ => ScheduleHideVariantChangesHover());
            popup.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
            popup.RegisterCallback<GeometryChangedEvent>(_ => PositionVariantChangesHover());
            Add(popup);
            PositionVariantChangesHover();
        }

        private void PositionVariantChangesHover()
        {
            if (_variantChangesHover == null || _variantChangesHoverAnchor == null) { return; }
            var bounds = _variantChangesHover.resolvedStyle;
            if (float.IsNaN(bounds.width) || float.IsNaN(bounds.height)) { return; }
            var anchor = this.WorldToLocal(_variantChangesHoverAnchor.worldBound.position);
            _variantChangesHover.style.left = Mathf.Clamp(
                anchor.x + _variantChangesHoverAnchor.worldBound.width - bounds.width,
                8f, Mathf.Max(8f, contentRect.width - bounds.width - 8f));
            _variantChangesHover.style.top = Mathf.Clamp(
                anchor.y + _variantChangesHoverAnchor.worldBound.height + 4f,
                8f, Mathf.Max(8f, contentRect.height - bounds.height - 8f));
        }

        private void ScheduleHideVariantChangesHover()
        {
            _variantChangesPendingHide?.Pause();
            _variantChangesPendingHide = schedule.Execute(HideVariantChangesHover).StartingIn(180);
        }

        private void HideVariantChangesHover()
        {
            _variantChangesPendingShow?.Pause();
            _variantChangesPendingHide?.Pause();
            _variantChangesPendingShow = null;
            _variantChangesPendingHide = null;
            _variantChangesHover?.RemoveFromHierarchy();
            _variantChangesHover = null;
            _variantChangesHoverAnchor = null;
        }

        private static void AddChangeGroups(VisualElement host,
            IReadOnlyList<AssetVariantChange> changes,
            IReadOnlyList<(AssetVariantChangeKind Kind, string Content)> pending)
        {
            var entries = new List<(AssetVariantChangeKind Kind, string Content)>();
            foreach (var change in changes)
            {
                var content = change.Subject == AssetVariantChangeSubject.Dependency
                    ? I18N.Get("variant.dependencyContent", change.Path)
                    : change.Subject == AssetVariantChangeSubject.Metadata
                        ? I18N.Get("variant.metadataContent", change.Path) : change.Path;
                entries.Add((change.Kind, content));
            }
            entries.AddRange(pending);
            if (entries.Count == 0)
            {
                AddChangeText(host, I18N.Get("variant.noTargetChanges"));
                return;
            }
            foreach (var kind in new[] { AssetVariantChangeKind.Added,
                AssetVariantChangeKind.Removed, AssetVariantChangeKind.Modified })
            {
                var contents = entries.Where(entry => entry.Kind == kind).Select(entry => entry.Content).ToArray();
                if (contents.Length == 0) { continue; }
                var added = kind == AssetVariantChangeKind.Added;
                var removed = kind == AssetVariantChangeKind.Removed;
                Color color = added ? UiColorTokens.StatusPassedText
                    : removed ? UiColorTokens.Error : UiColorTokens.Focus;
                var group = new VisualElement();
                group.AddToClassList("ee4v-modification-workflow__changes-group");
                if (host.contentContainer.childCount > 0)
                {
                    group.AddToClassList("ee4v-modification-workflow__changes-group--spaced");
                }
                group.style.borderLeftColor = color;
                var heading = new VisualElement();
                heading.AddToClassList("ee4v-modification-workflow__changes-heading");
                var icon = new Icon(FluentUiIcons.CreateState(
                    added ? "add.png" : removed ? "subtract.png" : "arrow_clockwise.png", tintColor: color));
                icon.AddToClassList("ee4v-modification-workflow__changes-icon");
                heading.Add(icon);
                var title = UiTextFactory.Create(I18N.Get("variant." +
                    (added ? "added" : removed ? "removed" : "modified")), UiClassNames.SectionTitle);
                title.SetColor(color);
                heading.Add(title);
                group.Add(heading);
                for (var index = 0; index < contents.Length; index++)
                {
                    AddChangeText(group, contents[index], index > 0);
                }
                host.Add(group);
            }
        }

        private static void AddChangeText(VisualElement host, string value, bool spaced = false)
        {
            var text = UiTextFactory.Create(value, "ee4v-modification-workflow__changes-row");
            if (spaced) { text.AddToClassList("ee4v-modification-workflow__changes-row--spaced"); }
            text.SetWhiteSpace(WhiteSpace.Normal);
            host.Add(text);
        }

        private IReadOnlyList<(AssetVariantChangeKind Kind, string Content)> DescribeSceneChanges()
        {
            var root = _avatarContext.Root;
            var changes = new List<(AssetVariantChangeKind Kind, string Content)>();
            foreach (var added in PrefabUtility.GetAddedGameObjects(root))
            {
                changes.Add((AssetVariantChangeKind.Added, DescribeSceneObject(added.instanceGameObject)));
            }
            foreach (var removed in PrefabUtility.GetRemovedGameObjects(root))
            {
                changes.Add((AssetVariantChangeKind.Removed, DescribeSceneObject(removed.assetGameObject)));
            }
            foreach (var added in PrefabUtility.GetAddedComponents(root))
            {
                changes.Add((AssetVariantChangeKind.Added, DescribeSceneObject(added.instanceComponent)));
            }
            foreach (var removed in PrefabUtility.GetRemovedComponents(root))
            {
                changes.Add((AssetVariantChangeKind.Removed, DescribeSceneObject(removed.assetComponent)));
            }
            foreach (var change in PrefabUtility.GetObjectOverrides(root, false))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(change.instanceObject);
                if (source == null) { continue; }
                if (PrefabEditingChanges.SerializedValuesEqual(change.instanceObject, source,
                    property => property.prefabOverride && !property.isDefaultOverride)) { continue; }
                using (var current = new SerializedObject(change.instanceObject))
                using (var saved = new SerializedObject(source))
                {
                    var property = current.GetIterator();
                    while (property.Next(true))
                    {
                        if (!property.prefabOverride || property.isDefaultOverride ||
                            property.propertyPath == "m_ObjectHideFlags" ||
                            property.propertyType == SerializedPropertyType.Generic) { continue; }
                        var original = saved.FindProperty(property.propertyPath);
                        if (original != null && property.propertyType == original.propertyType)
                        {
                            if (property.propertyType == SerializedPropertyType.ObjectReference)
                            {
                                var value = property.objectReferenceValue;
                                var expected = original.objectReferenceValue;
                                if (value == expected || value != null && !EditorUtility.IsPersistent(value) &&
                                    PrefabUtility.GetCorrespondingObjectFromSource(value) == expected) { continue; }
                            }
                            else if (SerializedProperty.DataEquals(property, original)) { continue; }
                        }
                        changes.Add((AssetVariantChangeKind.Modified, I18N.Get("variant.sceneProperty",
                            DescribeSceneObject(change.instanceObject), property.propertyPath,
                            DescribePropertyValue(original), DescribePropertyValue(property))));
                    }
                }
            }
            if (_parts.HasPendingPartVisibility)
            {
                changes.Add((AssetVariantChangeKind.Modified, I18N.Get("variant.pendingParts")));
            }
            if (_parts.HasPendingBodySizeChange)
            {
                changes.Add((AssetVariantChangeKind.Modified, I18N.Get("variant.pendingShape")));
            }
            return changes.Distinct().ToArray();
        }

        private static string DescribeSceneObject(UnityEngine.Object value)
        {
            var transform = value is GameObject gameObject ? gameObject.transform : (value as Component)?.transform;
            if (transform == null) { return value == null ? "—" : value.name; }
            var names = new Stack<string>();
            for (var node = transform; node != null; node = node.parent) { names.Push(node.name); }
            var path = string.Join("/", names);
            return value is Component ? path + " · " + value.GetType().Name : path;
        }

        private static string DescribePropertyValue(SerializedProperty property)
        {
            if (property == null) { return "—"; }
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: return property.boolValue ? "ON" : "OFF";
                case SerializedPropertyType.Integer: return property.longValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Float: return property.doubleValue.ToString("G6", CultureInfo.InvariantCulture);
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.Enum:
                    var index = property.enumValueIndex;
                    return index >= 0 && index < property.enumDisplayNames.Length
                        ? property.enumDisplayNames[index] : property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.ObjectReference:
                    var value = property.objectReferenceValue;
                    if (value == null) { return "None"; }
                    var path = AssetDatabase.GetAssetPath(value);
                    return string.IsNullOrEmpty(path) ? DescribeSceneObject(value) : path;
                case SerializedPropertyType.ArraySize: return property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Vector2: return property.vector2Value.ToString("G4");
                case SerializedPropertyType.Vector3: return property.vector3Value.ToString("G4");
                case SerializedPropertyType.Vector4: return property.vector4Value.ToString("G4");
                case SerializedPropertyType.Quaternion: return property.quaternionValue.ToString("G4");
                case SerializedPropertyType.Color: return property.colorValue.ToString();
                default: return property.propertyType.ToString();
            }
        }
    }
}
