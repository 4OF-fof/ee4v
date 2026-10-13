using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using static Ee4v.AvatarEditing.AvatarBodyAnalysis;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private readonly HashSet<string> _expandedBodyShapeGroups = new HashSet<string>(StringComparer.Ordinal)
        {
            "favorites"
        };

        private VisualElement BuildBodyShapeGroup(string key, string title, Action<VisualElement> populate,
            Action onExpanded = null)
        {
            var group = new VisualElement();
            group.AddToClassList("ee4v-body-shape-group");
            var children = new VisualElement();
            children.AddToClassList("ee4v-body-shape-group__content");
            var populated = false;
            UiButton header = null;
            void Update()
            {
                var expanded = _expandedBodyShapeGroups.Contains(key);
                header.EnableInClassList("ee4v-body-shape-group__header--collapsed", !expanded);
                header.SetIcon(FluentUiIcons.CreateState(expanded
                    ? "chevron_down.png" : "chevron_right.png", UiSizeTokens.Size12));
                if (expanded)
                {
                    if (!populated)
                    {
                        populate(children);
                        populated = true;
                    }
                    if (children.parent == null) { group.Add(children); }
                    onExpanded?.Invoke();
                }
                else { children.RemoveFromHierarchy(); }
            }
            header = new UiButton(title, () =>
            {
                if (!_expandedBodyShapeGroups.Add(key)) { _expandedBodyShapeGroups.Remove(key); }
                Update();
            }, title, variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SectionTitle);
            header.AddToClassList("ee4v-body-shape-group__header");
            header.SetContentAlignment(Justify.FlexStart);
            group.Add(header);
            Update();
            return group;
        }

        private VisualElement BuildBodyScaleControls()
        {
            var blendShapeRenders = new Dictionary<string, List<Action<float>>>(StringComparer.Ordinal);
            var content = new VisualElement();
            content.AddToClassList(
                "ee4v-modification-workflow__size-content");

            if (!_context.Edits.CanEditPrefab())
            {
                content.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.sizeProtected"),
                    HelpBoxMessageType.Warning));
                return content;
            }

            var animator = FindHumanoidAnimator();
            if (!_context.SelectedBodyPart.HasValue)
            {
                var primaryScaleList = new VisualElement();
                primaryScaleList.AddToClassList(
                    "ee4v-modification-workflow__size-list");
                var wholeBody = BodyScaleDefinitions.First(definition =>
                    definition.UsesAvatarRoot);
                var wholeBodyTargets = ResolveBodyScaleTargets(
                    wholeBody,
                    animator);
                var isAvatarScope = !_context.SelectedPrefabSiblingIndex.HasValue ||
                                    _context.SelectedPrefabSiblingIndex == -1;
                if (isAvatarScope && TryGetWorkingAvatarViewPosition(
                        out var avatarDescriptor,
                        out var currentViewPosition))
                {
                    var baseViewPosition = GetBaseAvatarViewPosition(
                        avatarDescriptor,
                        currentViewPosition);
                    primaryScaleList.Add(BuildBodyHeightControl(
                        wholeBody,
                        GetTransformPaths(wholeBodyTargets),
                        currentViewPosition,
                        baseViewPosition));
                }
                else
                {
                    var prefabTargets = ResolvePrefabScaleTargets();
                    if (prefabTargets.Count > 0)
                    {
                        primaryScaleList.Add(BuildBodyScaleControl(
                            new BodyScaleDefinition(
                                "workflow.appearance.sizePart.prefab",
                                false,
                                false),
                            GetTransformPaths(prefabTargets),
                            GetBodyScaleMultipliers(prefabTargets)));
                    }
                }
                content.Add(primaryScaleList);
            }

            var allBodyShapes = GetBodyBlendShapes();
            var bodyShapes = allBodyShapes
                .Where(definition =>
                    MatchesSelectedBodyPart(definition.Category))
                .ToArray();
            if (bodyShapes.Length == 0)
            {
                content.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get(_context.SelectedBodyPart.HasValue
                        ? "workflow.appearance.bodyShapePartEmpty"
                        : "workflow.appearance.bodyShapeEmpty"),
                    HelpBoxMessageType.Info));
            }
            else
            {
                var bodyShapeList = new VisualElement();
                bodyShapeList.AddToClassList(
                    "ee4v-modification-workflow__size-list");
                var favoriteGroups = bodyShapes.GroupBy(shape => shape.ShapeName, StringComparer.Ordinal)
                    .Select(group => group.ToArray()).ToArray();
                var favoriteKeys = favoriteGroups.Select(group => group.Select(BodyFavoriteKey).ToArray()).ToArray();
                VisualElement favoriteGroup = null;
                VisualElement[] favoriteSlots = null;
                void RefreshFavorites()
                {
                    var anyFavorites = false;
                    for (var index = 0; index < favoriteGroups.Length; index++)
                    {
                        var selected = favoriteKeys[index].Any(BlendShapeFavorites.Contains);
                        anyFavorites |= selected;
                        if (favoriteSlots == null || !_expandedBodyShapeGroups.Contains("favorites")) { continue; }
                        var slot = favoriteSlots[index];
                        if (selected && slot.childCount == 0)
                        {
                            var definitions = favoriteGroups[index];
                            slot.Add(definitions.Length == 1
                                ? BuildBodyBlendShapeControl(definitions[0],
                                    weight => ApplyBodyBlendShape(definitions[0].RendererPath, definitions[0].ShapeName, weight),
                                    out _, rendersByChannel: blendShapeRenders)
                                : BuildGroupedBodyBlendShapeControl(definitions, blendShapeRenders));
                        }
                        slot.style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                    if (favoriteGroup != null)
                        favoriteGroup.style.display = anyFavorites ? DisplayStyle.Flex : DisplayStyle.None;
                }
                favoriteGroup = BuildBodyShapeGroup("favorites", I18N.Get("workflow.favorites.title"), favorites =>
                {
                    favoriteSlots = favoriteGroups.Select(_ => new VisualElement()).ToArray();
                    foreach (var slot in favoriteSlots) { favorites.Add(slot); }
                }, RefreshFavorites);
                bodyShapeList.Add(favoriteGroup);
                content.RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    BlendShapeFavorites.Changed += RefreshFavorites;
                    RefreshFavorites();
                });
                content.RegisterCallback<DetachFromPanelEvent>(_ => BlendShapeFavorites.Changed -= RefreshFavorites);
                foreach (BodyPartCategory category in Enum.GetValues(
                             typeof(BodyPartCategory)))
                {
                    if (category == BodyPartCategory.Arms)
                    {
                        continue;
                    }
                    var categoryShapes = bodyShapes
                        .Where(definition =>
                            MatchesBodyPartGroup(
                                category, definition.Category))
                        .ToArray();
                    if (categoryShapes.Length == 0)
                    {
                        continue;
                    }

                    void PopulateCategory(VisualElement categoryContent)
                    {
                        var shapeGroups = categoryShapes
                            .GroupBy(shape => shape.ShapeName,
                                StringComparer.Ordinal)
                            .ToArray();
                        var roleGroups = shapeGroups
                            .Select((shapes, index) => new
                            {
                                Shapes = shapes.ToArray(),
                                Key = string.IsNullOrWhiteSpace(shapes.First().Group)
                                    ? "\0" + index
                                    : shapes.First().Group
                            })
                            .GroupBy(item => item.Key, StringComparer.Ordinal);
                        foreach (var roleGroup in roleGroups)
                        {
                            var groupedShapes = roleGroup
                                .SelectMany(item => item.Shapes)
                                .ToArray();
                            var hasRoleTitle = !string.IsNullOrWhiteSpace(
                                                   groupedShapes[0].Group) &&
                                               (groupedShapes.Length > 1 ||
                                                !string.Equals(
                                                    groupedShapes[0].Group,
                                                    groupedShapes[0].DisplayName,
                                                    StringComparison.OrdinalIgnoreCase));
                            void PopulateRole(VisualElement roleContent)
                            {
                                foreach (var shapeGroup in roleGroup)
                                {
                                    var control = shapeGroup.Shapes.Length == 1
                                        ? BuildBodyBlendShapeControl(
                                            shapeGroup.Shapes[0],
                                            weight => ApplyBodyBlendShape(
                                                shapeGroup.Shapes[0].RendererPath,
                                                shapeGroup.Shapes[0].ShapeName,
                                                weight),
                                            out _, rendersByChannel: blendShapeRenders)
                                        : BuildGroupedBodyBlendShapeControl(
                                            shapeGroup.Shapes, blendShapeRenders);
                                    roleContent.Add(control);
                                }
                            }
                            if (hasRoleTitle)
                            {
                                categoryContent.Add(BuildBodyShapeGroup("role:" + category + ":" + roleGroup.Key,
                                    groupedShapes[0].Group, PopulateRole));
                            }
                            else { PopulateRole(categoryContent); }
                        }
                    }
                    if (!_context.SelectedBodyPart.HasValue)
                    {
                        bodyShapeList.Add(BuildBodyShapeGroup("category:" + category,
                            I18N.Get(GetBodyPartCategoryLocalizationKey(category)), PopulateCategory));
                    }
                    else
                    {
                        var categoryContent = new VisualElement();
                        PopulateCategory(categoryContent);
                        bodyShapeList.Add(categoryContent);
                    }
                }
                RefreshFavorites();
                content.Add(bodyShapeList);
            }

            var bodyPartControls = new List<Action<VisualElement>>();
            for (var index = 1; index < BodyScaleDefinitions.Count; index++)
            {
                var definition = BodyScaleDefinitions[index];
                if (_context.SelectedBodyPart.HasValue &&
                    !MatchesSelectedBodyPart(
                        (BodyPartCategory)(index - 1)))
                {
                    continue;
                }
                var targets = ResolveBodyScaleTargets(definition, animator);
                if (targets.Count == 0)
                {
                    continue;
                }

                bodyPartControls.Add(parent => parent.Add(BuildBodyScaleControl(
                    definition,
                    GetTransformPaths(targets),
                    GetBodyScaleMultipliers(targets))));
            }

            if (bodyPartControls.Count > 0)
            {
                void PopulateAdvanced(VisualElement parent)
                {
                    foreach (var build in bodyPartControls) { build(parent); }
                }
                if (_context.SelectedBodyPart.HasValue)
                {
                    var advancedList = new VisualElement();
                    advancedList.AddToClassList("ee4v-modification-workflow__size-list");
                    PopulateAdvanced(advancedList);
                    content.Insert(0, advancedList);
                }
                else
                {
                    content.Add(BuildAdvancedBodyScaleFoldout(
                        PopulateAdvanced));
                }
            }
            return content;
        }

        private bool MatchesSelectedBodyPart(BodyPartCategory category)
        {
            return !_context.SelectedBodyPart.HasValue ||
                MatchesBodyPartGroup(_context.SelectedBodyPart.Value, category);
        }

        private IReadOnlyList<string> GetTransformPaths(
            IReadOnlyList<Transform> targets)
        {
            return targets
                .Where(target => target != null)
                .Select(target =>
                    AnimationUtility.CalculateTransformPath(
                        target,
                        _context.Root.transform))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private VisualElement BuildAdvancedBodyScaleFoldout(
            Action<VisualElement> populate)
        {
            var foldout = new VisualElement();
            foldout.AddToClassList(
                "ee4v-modification-workflow__advanced-size");
            var advancedList = new VisualElement();
            advancedList.AddToClassList("ee4v-modification-workflow__size-list");
            var populated = false;

            UiButton toggle = null;
            void Update()
            {
                UpdateAdvancedBodyScaleFoldout(toggle, advancedList);
                if (_advancedBodyScaleExpanded)
                {
                    if (!populated)
                    {
                        populate(advancedList);
                        populated = true;
                    }
                    if (advancedList.parent == null) { foldout.Add(advancedList); }
                }
                else { advancedList.RemoveFromHierarchy(); }
            }
            toggle = new UiButton(
                I18N.Get("workflow.appearance.advancedSizeTitle"),
                () =>
                {
                    _advancedBodyScaleExpanded =
                        !_advancedBodyScaleExpanded;
                    Update();
                },
                icon: FluentUiIcons.CreateState(
                    _advancedBodyScaleExpanded
                        ? "chevron_down.png"
                        : "chevron_right.png",
                    UiSizeTokens.Size12),
                variant: UiButtonVariant.Ghost);
            toggle.AddToClassList(
                "ee4v-modification-workflow__advanced-size-toggle");
            foldout.Add(toggle);

            Update();
            return foldout;
        }

        private void UpdateAdvancedBodyScaleFoldout(
            UiButton toggle,
            VisualElement content)
        {
            toggle?.SetIcon(FluentUiIcons.CreateState(
                _advancedBodyScaleExpanded
                    ? "chevron_down.png"
                    : "chevron_right.png",
                UiSizeTokens.Size12));
            content?.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                !_advancedBodyScaleExpanded);
        }

        private VisualElement BuildBodyHeightControl(
            BodyScaleDefinition definition,
            IReadOnlyList<string> targetPaths,
            Vector3 currentViewPosition,
            Vector3 baseViewPosition)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__size-scale-group");
            var baseHeightMeters = baseViewPosition.y;
            var minimumHeight = Mathf.Max(0.01f, baseHeightMeters - 1f);
            var maximumHeight = baseHeightMeters + 1f;
            var row = CreateSizeControlRow(
                I18N.Get(definition.LocalizationKey),
                out var controls);
            var slider = new Slider(minimumHeight, maximumHeight);
            slider.AddToClassList(
                "ee4v-modification-workflow__size-slider");
            slider.tooltip = I18N.Get(
                "workflow.appearance.heightSliderTooltip",
                FormatBodyHeight(minimumHeight),
                FormatBodyHeight(maximumHeight));
            controls.Add(slider);
            var value = UiTextFactory.CreateFloatField();
            value.AddToClassList(
                "ee4v-modification-workflow__size-value");
            controls.Add(value);
            controls.Add(CreateSizeUnit("m"));
            var reset = CreateSizeResetButton(() =>
                slider.value = baseHeightMeters);
            controls.Add(reset);

            var rendering = false;
            var appliedHeight = Mathf.Clamp(
                Mathf.Round(currentViewPosition.y * 100f) / 100f,
                minimumHeight,
                maximumHeight);
            void SetHeight(float height, bool apply)
            {
                var normalized = Mathf.Clamp(
                    Mathf.Round(height * 100f) / 100f,
                    minimumHeight,
                    maximumHeight);
                rendering = true;
                slider.SetValueWithoutNotify(normalized);
                value.SetValueWithoutNotify(normalized);
                rendering = false;
                reset.SetEnabled(!Mathf.Approximately(
                    normalized,
                    baseHeightMeters));
                if (apply &&
                    !Mathf.Approximately(normalized, appliedHeight))
                {
                    appliedHeight = normalized;
                    var multiplier = normalized / baseHeightMeters;
                    ApplyBodyScale(
                        targetPaths,
                        Vector3.one * multiplier,
                        updateAvatarViewPosition: true);
                }
            }

            RegisterBodySizeSliderDrag(slider);
            slider.RegisterValueChangedCallback(evt =>
            {
                if (!rendering)
                {
                    SetHeight(evt.newValue, true);
                }
            });
            value.RegisterValueChangedCallback(evt =>
            {
                if (!rendering &&
                    !float.IsNaN(evt.newValue) &&
                    !float.IsInfinity(evt.newValue))
                {
                    SetHeight(evt.newValue, true);
                }
            });
            SetHeight(currentViewPosition.y, false);
            group.Add(row);
            return group;
        }

        private VisualElement BuildBodyScaleControl(
            BodyScaleDefinition definition,
            IReadOnlyList<string> targetPaths,
            Vector3 initialMultipliers)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__size-scale-group");
            var partName = I18N.Get(definition.LocalizationKey);
            var multipliers = RoundBodyScaleMultipliers(initialMultipliers);
            var axisRenderers = new Action<float>[3];
            var axisRows = new VisualElement();
            axisRows.AddToClassList(
                "ee4v-modification-workflow__size-axis-rows");
            UiButton axisToggle = null;
            void ToggleAxes()
            {
                if (!_expandedBodyScaleAxes.Add(
                        definition.LocalizationKey))
                {
                    _expandedBodyScaleAxes.Remove(
                        definition.LocalizationKey);
                }
                UpdateBodyScaleAxisFoldout(
                    axisToggle,
                    axisRows,
                    definition.LocalizationKey,
                    partName);
            }
            axisToggle = AvatarEditingUi.CreateIconButton(
                string.Empty,
                "chevron_right.png",
                UiSizeTokens.Size12,
                UiButtonVariant.Ghost,
                ToggleAxes,
                "ee4v-modification-workflow__size-axis-toggle");
            var row = BuildScalePercentControl(
                partName,
                I18N.Get(
                    "workflow.appearance.sizeSliderTooltip",
                    partName),
                AverageVectorComponent(multipliers) * 100f,
                isChild: false,
                percent =>
                {
                    var multiplier = percent / 100f;
                    multipliers = Vector3.one * multiplier;
                    foreach (var renderAxis in axisRenderers)
                    {
                        renderAxis?.Invoke(percent);
                    }
                    ApplyBodyScale(targetPaths, multipliers);
                },
                out var renderUniform,
                axisToggle,
                ToggleAxes);
            group.Add(row);

            for (var axis = 0; axis < 3; axis++)
            {
                var capturedAxis = axis;
                var axisName = GetScaleAxisName(axis);
                var axisRow = BuildScalePercentControl(
                    axisName,
                    I18N.Get(
                        "workflow.appearance.axisScaleSliderTooltip",
                        partName,
                        axisName),
                    GetVectorComponent(multipliers, axis) * 100f,
                    isChild: true,
                    percent =>
                    {
                        multipliers = SetVectorComponent(
                            multipliers,
                            capturedAxis,
                            percent / 100f);
                        renderUniform(
                            AverageVectorComponent(multipliers) * 100f);
                        ApplyBodyScale(targetPaths, multipliers);
                    },
                    out axisRenderers[axis]);
                axisRows.Add(axisRow);
            }
            group.Add(axisRows);
            UpdateBodyScaleAxisFoldout(
                axisToggle,
                axisRows,
                definition.LocalizationKey,
                partName);
            return group;
        }

        private void UpdateBodyScaleAxisFoldout(
            UiButton toggle,
            VisualElement axisRows,
            string key,
            string partName)
        {
            var expanded = _expandedBodyScaleAxes.Contains(key);
            toggle?.SetIcon(FluentUiIcons.CreateState(
                expanded
                    ? "chevron_down.png"
                    : "chevron_right.png",
                UiSizeTokens.Size12));
            if (toggle != null)
            {
                toggle.tooltip = I18N.Get(
                    expanded
                        ? "workflow.appearance.sizeAxisCollapse"
                        : "workflow.appearance.sizeAxisExpand",
                    partName);
            }
            axisRows?.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                !expanded);
        }

        private VisualElement BuildScalePercentControl(
            string label,
            string tooltip,
            float initialPercent,
            bool isChild,
            Action<float> changed,
            out Action<float> render,
            VisualElement leading = null,
            Action labelClicked = null)
        {
            var row = CreateSizeControlRow(
                label,
                out var controls,
                isChild,
                leading,
                labelClicked);
            var slider = new Slider(
                MinimumBodyScale * 100f,
                MaximumBodyScale * 100f);
            slider.AddToClassList(
                "ee4v-modification-workflow__size-slider");
            slider.tooltip = tooltip;
            controls.Add(slider);
            var value = UiTextFactory.CreateFloatField();
            value.AddToClassList(
                "ee4v-modification-workflow__size-value");
            controls.Add(value);
            controls.Add(CreateSizeUnit("%"));
            var reset = CreateSizeResetButton(() => slider.value = 100f);
            controls.Add(reset);

            var rendering = false;
            var renderedPercent = Mathf.Round(initialPercent);
            void RenderScalePercent(float percent)
            {
                var normalized = Mathf.Round(percent);
                var sliderValue = Mathf.Clamp(
                    normalized,
                    MinimumBodyScale * 100f,
                    MaximumBodyScale * 100f);
                rendering = true;
                slider.SetValueWithoutNotify(sliderValue);
                value.SetValueWithoutNotify(normalized);
                rendering = false;
                renderedPercent = normalized;
                reset.SetEnabled(!Mathf.Approximately(normalized, 100f));
            }

            void SetScalePercent(float percent)
            {
                var normalized = Mathf.Round(percent);
                var changedValue = !Mathf.Approximately(
                    normalized,
                    renderedPercent);
                RenderScalePercent(normalized);
                if (changedValue)
                {
                    changed?.Invoke(normalized);
                }
            }

            RegisterBodySizeSliderDrag(slider);
            slider.RegisterValueChangedCallback(evt =>
            {
                if (!rendering)
                {
                    SetScalePercent(evt.newValue);
                }
            });
            value.RegisterValueChangedCallback(evt =>
            {
                if (!rendering &&
                    !float.IsNaN(evt.newValue) &&
                    !float.IsInfinity(evt.newValue))
                {
                    SetScalePercent(evt.newValue);
                }
            });
            render = RenderScalePercent;
            RenderScalePercent(initialPercent);
            return row;
        }

        private VisualElement BuildGroupedBodyBlendShapeControl(
            IReadOnlyList<BodyBlendShapeDefinition> definitions,
            Dictionary<string, List<Action<float>>> rendersByChannel)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__size-scale-group");
            var first = definitions[0];
            var key = first.Category + "|" + first.ShapeName;
            var children = new VisualElement();
            children.AddToClassList(
                "ee4v-modification-workflow__size-axis-rows");
            Action populateChildren = null;
            var childrenPopulated = false;
            UiButton toggle = null;
            void UpdateFoldout()
            {
                var expanded = _expandedBodyBlendShapeGroups.Contains(key);
                toggle.SetIcon(FluentUiIcons.CreateState(
                    expanded ? "chevron_down.png" : "chevron_right.png",
                    UiSizeTokens.Size12));
                toggle.tooltip = I18N.Get(expanded
                    ? "workflow.appearance.bodyShapeCollapse"
                    : "workflow.appearance.bodyShapeExpand", first.DisplayName);
                children.EnableInClassList(
                    "ee4v-modification-workflow__hidden", !expanded);
                if (expanded)
                {
                    if (!childrenPopulated)
                    {
                        populateChildren();
                        childrenPopulated = true;
                    }
                    if (children.parent == null) { group.Add(children); }
                }
                else { children.RemoveFromHierarchy(); }
            }
            void ToggleChildren()
            {
                if (!_expandedBodyBlendShapeGroups.Add(key))
                {
                    _expandedBodyBlendShapeGroups.Remove(key);
                }
                UpdateFoldout();
            }
            toggle = AvatarEditingUi.CreateIconButton(
                string.Empty,
                "chevron_right.png",
                UiSizeTokens.Size12,
                UiButtonVariant.Ghost,
                ToggleChildren,
                "ee4v-modification-workflow__size-axis-toggle");

            var source = ResolveBodyBlendShapeGroupSource(definitions);
            var sourceValue = source == null
                ? first.Value
                : GetEffectiveBodyBlendShapeWeight(
                    source, first.ShapeName);
            var sourceDefinition = definitions.FirstOrDefault(definition =>
                IsBodyBlendShapeGroupSource(definition, source));
            var parentDefinition = new BodyBlendShapeDefinition
            {
                RendererPath = sourceDefinition?.RendererPath ?? first.RendererPath,
                ShapeName = first.ShapeName,
                DisplayName = first.DisplayName,
                Value = sourceValue,
                BaseValue = sourceDefinition?.BaseValue ?? first.BaseValue
            };
            var childRenderers = new List<Action<float>>();
            var parent = BuildBodyBlendShapeControl(
                parentDefinition,
                weight =>
                {
                    ApplyBodyBlendShapeGroup(definitions, weight);
                    foreach (var render in childRenderers)
                    {
                        render(weight);
                    }
                },
                out var renderParent,
                leading: toggle,
                labelClicked: ToggleChildren,
                rendersByChannel: rendersByChannel);
            group.Add(parent);
            populateChildren = () =>
            {
                foreach (var definition in definitions
                             .OrderBy(definition =>
                                 IsBodyBlendShapeGroupSource(
                                     definition, source) ? 0 : 1))
                {
                    var child = BuildBodyBlendShapeControl(
                        definition,
                        weight =>
                        {
                            ApplyIndividualBodyBlendShape(
                                definitions, definition, weight);
                            if (IsBodyBlendShapeGroupSource(
                                    definition, source))
                            {
                                renderParent(weight);
                            }
                        },
                        out var renderChild,
                        isChild: true,
                        showRenderer: true,
                        rendersByChannel: rendersByChannel);
                    child.AddToClassList(
                        "ee4v-modification-workflow__size-control--blendshape-child");
                    children.Add(child);
                    childRenderers.Add(renderChild);
                }
            };
            UpdateFoldout();
            return group;
        }

        private VisualElement BuildBodyBlendShapeControl(
            BodyBlendShapeDefinition definition,
            Action<float> changed,
            out Action<float> render,
            bool isChild = false,
            VisualElement leading = null,
            Action labelClicked = null,
            bool showRenderer = false,
            Dictionary<string, List<Action<float>>> rendersByChannel = null)
        {
            var renderer = GetBodyBlendShapeRenderer(definition);
            if (renderer != null)
                definition.Value = GetEffectiveBodyBlendShapeWeight(renderer, definition.ShapeName);
            var label = definition.DisplayName;
            var tooltip = showRenderer
                ? $"{definition.RendererDisplayPath}\n{definition.ShapeName}"
                : label;
            var row = CreateSizeControlRow(
                label,
                out var controls,
                isChild: isChild,
                leading: leading,
                labelClicked: labelClicked,
                tooltip: tooltip,
                detail: showRenderer
                    ? definition.RendererName
                    : null);
            var favoriteKey = BodyFavoriteKey(definition);
            var selectedFavorite = BlendShapeFavorites.Contains(favoriteKey);
            UiButton favorite = null;
            void RefreshFavorite(string changedKey)
            {
                if (!string.Equals(changedKey, favoriteKey, StringComparison.Ordinal)) { return; }
                var selected = BlendShapeFavorites.Contains(favoriteKey);
                if (selectedFavorite == selected) { return; }
                selectedFavorite = selected;
                favorite.SetIcon(AvatarEditingUi.CreateBlendShapeFavoriteIcon(selected));
                favorite.tooltip = I18N.Get(selected ? "workflow.favorites.remove" : "workflow.favorites.add");
            }
            favorite = new UiButton(string.Empty, () =>
            {
                BlendShapeFavorites.Toggle(favoriteKey);
                RefreshFavorite(favoriteKey);
            }, I18N.Get(selectedFavorite
                ? "workflow.favorites.remove" : "workflow.favorites.add"),
                AvatarEditingUi.CreateBlendShapeFavoriteIcon(selectedFavorite), UiButtonVariant.Ghost);
            if (!isChild)
            {
                row.RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    BlendShapeFavorites.KeyChanged += RefreshFavorite;
                    RefreshFavorite(favoriteKey);
                });
                row.RegisterCallback<DetachFromPanelEvent>(_ => BlendShapeFavorites.KeyChanged -= RefreshFavorite);
            }
            favorite.AddToClassList("ee4v-body-shape-favorite");
            favorite.style.display = isChild ? DisplayStyle.None : DisplayStyle.Flex;
            row.Insert(0, favorite);
            var slider = new Slider(
                MinimumBodyBlendShapeWeight,
                MaximumBodyBlendShapeWeight);
            slider.AddToClassList(
                "ee4v-modification-workflow__size-slider");
            slider.tooltip = I18N.Get(
                "workflow.appearance.bodyShapeSliderTooltip",
                label) + (showRenderer
                    ? $"\n{definition.RendererDisplayPath}\n{definition.ShapeName}"
                    : string.Empty);
            controls.Add(slider);
            var value = UiTextFactory.CreateFloatField();
            value.AddToClassList(
                "ee4v-modification-workflow__size-value");
            controls.Add(value);
            var reset = CreateSizeResetButton(() =>
                slider.value = definition.BaseValue);
            controls.Add(reset);

            var rendering = false;
            var appliedWeight = Mathf.Clamp(
                Mathf.Round(definition.Value),
                MinimumBodyBlendShapeWeight,
                MaximumBodyBlendShapeWeight);
            void RenderLocalWeight(float weight)
            {
                var normalized = Mathf.Clamp(
                    Mathf.Round(weight),
                    MinimumBodyBlendShapeWeight,
                    MaximumBodyBlendShapeWeight);
                rendering = true;
                slider.SetValueWithoutNotify(normalized);
                value.SetValueWithoutNotify(normalized);
                rendering = false;
                reset.SetEnabled(!Mathf.Approximately(
                    normalized,
                    definition.BaseValue));
                appliedWeight = normalized;
                definition.Value = normalized;
            }
            var renderKey = definition.RendererPath + "\0" + definition.ShapeName;
            rendersByChannel = rendersByChannel ?? new Dictionary<string, List<Action<float>>>(StringComparer.Ordinal);
            if (!rendersByChannel.TryGetValue(renderKey, out var renders))
            {
                renders = new List<Action<float>>();
                rendersByChannel.Add(renderKey, renders);
            }
            renders.Add(RenderLocalWeight);
            void RenderWeight(float weight)
            {
                foreach (var update in renders) { update(weight); }
            }
            void SetWeight(float weight)
            {
                var normalized = Mathf.Clamp(
                    Mathf.Round(weight),
                    MinimumBodyBlendShapeWeight,
                    MaximumBodyBlendShapeWeight);
                if (!Mathf.Approximately(normalized, appliedWeight))
                {
                    RenderWeight(normalized);
                    changed?.Invoke(normalized);
                }
            }

            RegisterBodySizeSliderDrag(slider);
            slider.RegisterValueChangedCallback(evt =>
            {
                if (!rendering)
                {
                    SetWeight(evt.newValue);
                }
            });
            value.RegisterValueChangedCallback(evt =>
            {
                if (!rendering &&
                    !float.IsNaN(evt.newValue) &&
                    !float.IsInfinity(evt.newValue))
                {
                    SetWeight(evt.newValue);
                }
            });
            render = RenderWeight;
            RenderWeight(definition.Value);
            return row;
        }

        private string BodyFavoriteKey(BodyBlendShapeDefinition definition) =>
            BlendShapeFavorites.Key(GetBodyBlendShapeRenderer(definition)?.sharedMesh, definition.ShapeName);

        private VisualElement CreateSizeControlRow(
            string label,
            out VisualElement controls,
            bool isChild = false,
            VisualElement leading = null,
            Action labelClicked = null,
            string tooltip = null,
            string detail = null)
        {
            var row = new VisualElement();
            row.AddToClassList(
                "ee4v-modification-workflow__size-control");
            row.EnableInClassList(
                "ee4v-modification-workflow__size-control--child",
                isChild);
            if (leading != null)
            {
                row.Add(leading);
            }
            else if (isChild)
            {
                var indent = new VisualElement();
                indent.AddToClassList(
                    "ee4v-modification-workflow__size-axis-indent");
                row.Add(indent);
            }
            var name = UiTextFactory.Create(
                label,
                "ee4v-modification-workflow__size-control-name");
            name.tooltip = tooltip ?? label ?? string.Empty;
            if (labelClicked != null)
            {
                name.RegisterCallback<ClickEvent>(evt =>
                {
                    labelClicked();
                    evt.StopPropagation();
                });
            }
            if (detail == null)
            {
                row.Add(name);
            }
            else
            {
                row.AddToClassList(
                    "ee4v-modification-workflow__size-control--detailed");
                var labels = new VisualElement();
                labels.AddToClassList(
                    "ee4v-modification-workflow__size-control-labels");
                labels.Add(name);
                var detailLabel = UiTextFactory.Create(
                    detail,
                    UiClassNames.SecondaryText,
                    "ee4v-modification-workflow__size-control-detail");
                detailLabel.tooltip = tooltip ?? detail;
                labels.Add(detailLabel);
                row.Add(labels);
            }
            controls = new VisualElement();
            controls.AddToClassList(
                "ee4v-modification-workflow__size-control-row");
            row.Add(controls);
            return row;
        }

        private UiTextElement CreateSizeUnit(string unit)
        {
            var label = UiTextFactory.Create(
                unit,
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__size-unit");
            label.SetTextAlign(TextAnchor.MiddleLeft);
            return label;
        }

        private UiButton CreateSizeResetButton(Action reset)
        {
            var button = new UiButton(
                I18N.Get("workflow.appearance.sizeReset"),
                reset,
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(
                "ee4v-modification-workflow__size-reset");
            return button;
        }

        private void RegisterBodySizeSliderDrag(Slider slider)
        {
            var dragContainer = slider.Q<VisualElement>(
                className: "unity-base-slider__drag-container") ?? slider;
            dragContainer.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    BeginBodyScaleDrag();
                }
            }, TrickleDown.TrickleDown);
            dragContainer.RegisterCallback<PointerUpEvent>(_ =>
                EndBodyScaleDrag());
            dragContainer.RegisterCallback<PointerCaptureOutEvent>(_ =>
                EndBodyScaleDrag());
        }

        private static string FormatBodyHeight(float value)
        {
            return value.ToString("0.00") + " m";
        }

        public VisualElement BuildShapePartsTabs(
            Action<AvatarEditorPanel> selectPanel = null,
            AvatarEditorPanel? selectedPanel = null)
        {
            var tabs = new VisualElement();
            UiComposition.Prepare(tabs,
                "Editor/Feature/Shared/AvatarEditing/avatar-editing.uss",
                "Editor/Feature/Avatar/AvatarParts/avatar-parts.uss");
            tabs.AddToClassList(
                "ee4v-modification-workflow__shape-parts-tabs");
            AddShapePartsButton(
                tabs,
                AvatarEditorPanel.Parts,
                "workflow.shapeParts.parts", selectPanel);
            AddShapePartsButton(
                tabs,
                AvatarEditorPanel.Shape,
                "workflow.shapeParts.shape", selectPanel);
            if (selectPanel != null)
            {
                AddShapePartsButton(tabs, AvatarEditorPanel.Material,
                    "workflow.shapeParts.material", selectPanel);
            }
            RefreshShapePartsTabs(tabs, selectedPanel);
            return tabs;
        }

        public void RefreshShapePartsTabs(VisualElement tabs, AvatarEditorPanel? selectedPanel = null)
        {
            var current = selectedPanel ?? (_shapePartsSection == ShapePartsSection.Shape
                ? AvatarEditorPanel.Shape : AvatarEditorPanel.Parts);
            tabs.userData = current;
            foreach (var child in tabs.Children())
            {
                if (child.userData is AvatarEditorPanel panel)
                {
                    child.EnableInClassList("ee4v-modification-workflow__shape-parts-tab--active",
                        panel == current);
                    if (child is UiButton button)
                    {
                        button.SetLabelColor(panel == current ? UiColorTokens.TextPrimary : UiColorTokens.TextMuted);
                    }
                }
            }
        }

        private void AddShapePartsButton(
            VisualElement tabs,
            AvatarEditorPanel panel,
            string labelKey,
            Action<AvatarEditorPanel> selectPanel)
        {
            var button = new UiButton(
                I18N.Get(labelKey),
                () =>
                {
                    if (tabs.userData is AvatarEditorPanel current && current == panel)
                    {
                        return;
                    }
                    if (selectPanel != null)
                    {
                        selectPanel(panel);
                        return;
                    }
                    if (_shapePartsSection == ShapePartsSection.Shape)
                    {
                        EndBodyScaleDrag();
                        SaveBodyScalePrefab();
                        if (_bodyScaleDirty) { return; }
                    }
                    _shapePartsSection = panel == AvatarEditorPanel.Shape
                        ? ShapePartsSection.Shape : ShapePartsSection.Parts;
                    _context.Host.ShowParts();
                    _context.ControlsHost.scrollOffset = Vector2.zero;
                },
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(
                "ee4v-modification-workflow__shape-parts-tab");
            button.userData = panel;
            tabs.Add(button);
        }
    }
}
