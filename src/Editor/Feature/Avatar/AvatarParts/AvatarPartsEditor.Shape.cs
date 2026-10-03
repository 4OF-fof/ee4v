using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using static Ee4v.AvatarEditing.AvatarBodyAnalysis;
using static Ee4v.AvatarEditing.AvatarEditingUi;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private VisualElement BuildBodyScaleControls()
        {
            var content = new VisualElement();
            content.AddToClassList(
                "ee4v-modification-workflow__size-content");

            if (!_context.CanEditPrefab())
            {
                content.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.sizeProtected"),
                    HelpBoxMessageType.Warning));
                return content;
            }

            var animator = FindHumanoidAnimator();
            if (!_context.SelectedBodyPart.HasValue &&
                (!_context.SelectedPrefabSiblingIndex.HasValue ||
                 _context.SelectedPrefabSiblingIndex == -1))
            {
                var primaryScaleList = new VisualElement();
                primaryScaleList.AddToClassList(
                    "ee4v-modification-workflow__size-list");
                var wholeBody = BodyScaleDefinitions.First(definition =>
                    definition.UsesAvatarRoot);
                var wholeBodyTargets = ResolveBodyScaleTargets(
                    wholeBody,
                    animator);
                if (TryGetWorkingAvatarViewPosition(
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
                    primaryScaleList.Add(UiTextFactory.CreateHelpBox(
                        I18N.Get("workflow.appearance.heightDescriptorRequired"),
                        HelpBoxMessageType.Warning));
                }
                content.Add(primaryScaleList);
            }

            var allBodyShapes = GetBodyBlendShapes();
            EnsureBodyBlendShapeSyncBindings(allBodyShapes);
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

                    if (!_context.SelectedBodyPart.HasValue &&
                        category != BodyPartCategory.Other)
                    {
                        var categoryLabel = UiTextFactory.Create(
                            I18N.Get(GetBodyPartCategoryLocalizationKey(
                                category)),
                            UiClassNames.SecondaryText,
                            "ee4v-modification-workflow__size-group-title");
                        bodyShapeList.Add(categoryLabel);
                    }
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
                        if (hasRoleTitle)
                        {
                            bodyShapeList.Add(UiTextFactory.Create(
                                groupedShapes[0].Group,
                                UiClassNames.SecondaryText,
                                "ee4v-modification-workflow__size-role-title"));
                        }
                        foreach (var shapeGroup in roleGroup)
                        {
                            var control = shapeGroup.Shapes.Length == 1
                                ? BuildBodyBlendShapeControl(
                                    shapeGroup.Shapes[0],
                                    weight => ApplyBodyBlendShape(
                                        shapeGroup.Shapes[0].RendererPath,
                                        shapeGroup.Shapes[0].ShapeName,
                                        weight),
                                    out _)
                                : BuildGroupedBodyBlendShapeControl(
                                    shapeGroup.Shapes);
                            if (hasRoleTitle)
                            {
                                control.AddToClassList(
                                    "ee4v-modification-workflow__size-control--child");
                            }
                            bodyShapeList.Add(control);
                        }
                    }
                }
                content.Add(bodyShapeList);
            }

            var advancedList = new VisualElement();
            advancedList.AddToClassList(
                "ee4v-modification-workflow__size-list");
            var hasBodyPartControls = false;
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

                hasBodyPartControls = true;
                advancedList.Add(BuildBodyScaleControl(
                    definition,
                    GetTransformPaths(targets),
                    GetBodyScaleMultipliers(targets)));
            }

            if (hasBodyPartControls)
            {
                if (_context.SelectedBodyPart.HasValue)
                {
                    content.Insert(0, advancedList);
                }
                else
                {
                    content.Add(BuildAdvancedBodyScaleFoldout(
                        advancedList));
                }
            }
            else if (_context.SelectedBodyPart != BodyPartCategory.Other)
            {
                var warning = UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.sizeHumanoidRequired"),
                    HelpBoxMessageType.Warning);
                if (_context.SelectedBodyPart.HasValue)
                {
                    content.Insert(0, warning);
                }
                else
                {
                    content.Add(warning);
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
            VisualElement advancedList)
        {
            var foldout = new VisualElement();
            foldout.AddToClassList(
                "ee4v-modification-workflow__advanced-size");

            UiButton toggle = null;
            toggle = new UiButton(
                I18N.Get("workflow.appearance.advancedSizeTitle"),
                () =>
                {
                    _advancedBodyScaleExpanded =
                        !_advancedBodyScaleExpanded;
                    UpdateAdvancedBodyScaleFoldout(toggle, advancedList);
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

            foldout.Add(advancedList);
            UpdateAdvancedBodyScaleFoldout(toggle, advancedList);
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
            IReadOnlyList<BodyBlendShapeDefinition> definitions)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__size-scale-group");
            var first = definitions[0];
            var key = first.Category + "|" + first.ShapeName;
            var children = new VisualElement();
            children.AddToClassList(
                "ee4v-modification-workflow__size-axis-rows");
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
                labelClicked: ToggleChildren);
            group.Add(parent);
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
                    showRenderer: true);
                child.AddToClassList(
                    "ee4v-modification-workflow__size-control--blendshape-child");
                children.Add(child);
                childRenderers.Add(renderChild);
            }
            group.Add(children);
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
            bool showRenderer = false)
        {
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
            void RenderWeight(float weight)
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

        private IReadOnlyList<BodyBlendShapeDefinition>
            GetBodyBlendShapes()
        {
            if (_context.Root == null)
            {
                return Array.Empty<BodyBlendShapeDefinition>();
            }

            if (_bodyBlendShapesCache != null)
            {
                foreach (var definition in _bodyBlendShapesCache)
                {
                    var renderer = GetBodyBlendShapeRenderer(definition);
                    definition.Value = GetEffectiveBodyBlendShapeWeight(
                        renderer, definition.ShapeName);
                }
                return _bodyBlendShapesCache;
            }

            var renderers = _context.Root
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer =>
                    renderer != null &&
                    _context.IsInSelectedPrefabScope(renderer.transform) &&
                    renderer.sharedMesh != null &&
                    renderer.sharedMesh.blendShapeCount > 0)
                .ToArray();
            if (renderers.Length == 0)
            {
                return Array.Empty<BodyBlendShapeDefinition>();
            }

            var naming = _context.CreateShapeNaming(
                _context.Root,
                renderers.Select(renderer => renderer.sharedMesh));
            var result = new List<BodyBlendShapeDefinition>();
            foreach (var renderer in renderers)
            {
                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    _context.Root.transform);
                var meshAssetPath = AssetDatabase.GetAssetPath(
                    renderer.sharedMesh);
                var sourceAssetGuid = string.Empty;
                var sourceMeshLocalId = 0L;
                if (string.Equals(
                        System.IO.Path.GetExtension(meshAssetPath),
                        ".fbx",
                        StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        renderer.sharedMesh,
                        out sourceAssetGuid,
                        out sourceMeshLocalId);
                }
                for (var shapeIndex = 0;
                     shapeIndex < renderer.sharedMesh.blendShapeCount;
                     shapeIndex++)
                {
                    var shapeName = renderer.sharedMesh
                        .GetBlendShapeName(shapeIndex);
                    if (naming.IsHeader(shapeName))
                    {
                        continue;
                    }
                    naming.TryGetMapping(
                        sourceAssetGuid,
                        sourceMeshLocalId,
                        shapeName,
                        out var mapping);
                    if (string.Equals(
                            mapping.AppearancePart,
                            "expression",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (!TryGetPresetBodyPart(
                            mapping.AppearancePart,
                            out var category))
                    {
                        if (IsBodyMesh(renderer))
                        {
                            continue;
                        }
                        category = ClassifyBodyPart(
                            shapeName,
                            renderer.name);
                    }
                    result.Add(new BodyBlendShapeDefinition
                    {
                        RendererPath = rendererPath,
                        RendererDisplayPath = string.IsNullOrEmpty(rendererPath)
                            ? renderer.name
                            : rendererPath,
                        RendererName = renderer.name,
                        ShapeName = shapeName,
                        DisplayName = GetBodyBlendShapeDisplayName(shapeName),
                        Category = category,
                        Group = string.IsNullOrWhiteSpace(
                            mapping.AppearanceGroup)
                            ? mapping.Role?.Trim() ?? string.Empty
                            : mapping.AppearanceGroup.Trim(),
                        Value = GetEffectiveBodyBlendShapeWeight(
                            renderer, shapeName),
                        BaseValue = GetBaseBlendShapeWeight(
                            renderer,
                            shapeName)
                    });
                }
            }
            AddSyncedBodyBlendShapes(result);
            _bodyBlendShapesCache = result;
            return _bodyBlendShapesCache;
        }

        private void AddSyncedBodyBlendShapes(
            List<BodyBlendShapeDefinition> definitions)
        {
            var synced = definitions
                .Where(definition => ResolveBodyBlendShapeSource(
                    GetBodyBlendShapeRenderer(definition),
                    definition.ShapeName) != null)
                .GroupBy(definition => definition.ShapeName,
                    StringComparer.Ordinal)
                .ToArray();
            foreach (var group in synced)
            {
                var representative = group.First();
                foreach (var renderer in _context.Root
                             .GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer?.sharedMesh == null ||
                        renderer.sharedMesh.GetBlendShapeIndex(group.Key) < 0)
                    {
                        continue;
                    }
                    var path = AnimationUtility.CalculateTransformPath(
                        renderer.transform, _context.Root.transform);
                    if (definitions.Any(definition =>
                            definition.RendererPath == path &&
                            definition.ShapeName == group.Key))
                    {
                        continue;
                    }
                    definitions.Add(new BodyBlendShapeDefinition
                    {
                        RendererPath = path,
                        RendererDisplayPath = path,
                        RendererName = renderer.name,
                        ShapeName = group.Key,
                        DisplayName = representative.DisplayName,
                        Category = representative.Category,
                        Group = representative.Group,
                        Value = GetEffectiveBodyBlendShapeWeight(
                            renderer, group.Key),
                        BaseValue = GetBaseBlendShapeWeight(renderer, group.Key)
                    });
                }
                var source = ResolveBodyBlendShapeGroupSource(group.ToArray());
                var sourceDefinition = definitions.FirstOrDefault(definition =>
                    definition.ShapeName == group.Key &&
                    GetBodyBlendShapeRenderer(definition) == source);
                var category = sourceDefinition == null
                    ? representative.Category
                    : sourceDefinition.Category;
                foreach (var definition in definitions.Where(definition =>
                             definition.ShapeName == group.Key))
                {
                    definition.Category = category;
                    definition.Group = representative.Group;
                }
            }
        }

        private SkinnedMeshRenderer GetBodyBlendShapeRenderer(
            BodyBlendShapeDefinition definition)
        {
            if (_context.Root == null || definition == null)
            {
                return null;
            }
            var target = string.IsNullOrEmpty(definition.RendererPath)
                ? _context.Root.transform
                : _context.Root.transform.Find(definition.RendererPath);
            return target == null
                ? null
                : target.GetComponent<SkinnedMeshRenderer>();
        }

        private SkinnedMeshRenderer ResolveBodyBlendShapeGroupSource(
            IReadOnlyList<BodyBlendShapeDefinition> definitions)
        {
            foreach (var definition in definitions)
            {
                var source = ResolveBodyBlendShapeSource(
                    GetBodyBlendShapeRenderer(definition),
                    definition.ShapeName);
                if (source != null)
                {
                    return source;
                }
            }
            return definitions.Count == 0
                ? null
                : GetBodyBlendShapeRenderer(definitions[0]);
        }

        private bool IsBodyBlendShapeGroupSource(
            BodyBlendShapeDefinition definition,
            SkinnedMeshRenderer source)
        {
            return source != null &&
                   GetBodyBlendShapeRenderer(definition) == source;
        }

        private SkinnedMeshRenderer ResolveBodyBlendShapeSource(
            SkinnedMeshRenderer target,
            string localShapeName)
        {
            if (target == null || _context.Root == null)
            {
                return null;
            }
            var sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            if (sync?.Bindings == null)
            {
                return null;
            }
            foreach (var binding in sync.Bindings)
            {
                var localName = string.IsNullOrWhiteSpace(
                    binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape;
                if (localName != localShapeName ||
                    binding.ReferenceMesh == null)
                {
                    continue;
                }
                var path = binding.ReferenceMesh.referencePath;
                GameObject sourceObject = null;
                if (!string.IsNullOrEmpty(path))
                {
                    var sourceTransform = path ==
                        AvatarObjectReference.AVATAR_ROOT
                        ? _context.Root.transform
                        : _context.Root.transform.Find(path);
                    sourceObject = sourceTransform == null
                        ? null
                        : sourceTransform.gameObject;
                }
                if (sourceObject == null)
                {
                    sourceObject = binding.ReferenceMesh.Get(sync);
                }
                var source = sourceObject == null
                    ? null
                    : sourceObject.GetComponent<SkinnedMeshRenderer>();
                if (source?.sharedMesh != null &&
                    source.sharedMesh.GetBlendShapeIndex(
                        binding.Blendshape) >= 0)
                {
                    return source;
                }
            }
            return null;
        }

        private static bool HasBodyBlendShapeSyncBinding(
            SkinnedMeshRenderer target,
            string shapeName)
        {
            var sync = target == null
                ? null
                : target.GetComponent<ModularAvatarBlendshapeSync>();
            return sync?.Bindings != null && sync.Bindings.Any(binding =>
                (string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape) == shapeName);
        }

        private float GetEffectiveBodyBlendShapeWeight(
            SkinnedMeshRenderer renderer,
            string shapeName)
        {
            var index = renderer?.sharedMesh == null
                ? -1
                : renderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (index < 0)
            {
                return 0f;
            }
            var sync = renderer.GetComponent<ModularAvatarBlendshapeSync>();
            if (sync?.Bindings != null)
            {
                foreach (var binding in sync.Bindings)
                {
                    var localName = string.IsNullOrWhiteSpace(
                        binding.LocalBlendshape)
                        ? binding.Blendshape
                        : binding.LocalBlendshape;
                    if (localName != shapeName)
                    {
                        continue;
                    }
                    var source = ResolveBodyBlendShapeSource(
                        renderer, shapeName);
                    var sourceIndex = source?.sharedMesh == null
                        ? -1
                        : source.sharedMesh.GetBlendShapeIndex(
                            binding.Blendshape);
                    if (sourceIndex >= 0)
                    {
                        var weight = source.GetBlendShapeWeight(sourceIndex);
                        return Mathf.Clamp(
                            binding.RemapCurveIsValid
                                ? EvaluateBodyBlendShapeRemap(
                                    binding.RemapCurve, weight)
                                : weight,
                            MinimumBodyBlendShapeWeight,
                            MaximumBodyBlendShapeWeight);
                    }
                }
            }
            return Mathf.Clamp(renderer.GetBlendShapeWeight(index),
                MinimumBodyBlendShapeWeight,
                MaximumBodyBlendShapeWeight);
        }

        private static float EvaluateBodyBlendShapeRemap(
            AnimationCurve curve,
            float weight)
        {
            if (curve == null || curve.length < 2)
            {
                return weight;
            }
            var keys = curve.keys;
            for (var index = 1; index < keys.Length; index++)
            {
                if (weight > keys[index].time && index < keys.Length - 1)
                {
                    continue;
                }
                var previous = keys[index - 1];
                var next = keys[index];
                var duration = next.time - previous.time;
                return Mathf.Approximately(duration, 0f)
                    ? next.value
                    : Mathf.LerpUnclamped(previous.value, next.value,
                        (weight - previous.time) / duration);
            }
            return keys[keys.Length - 1].value;
        }

        private static bool TryGetPresetBodyPart(
            string part,
            out BodyPartCategory category)
        {
            switch (part)
            {
                case "head":
                    category = BodyPartCategory.Head;
                    break;
                case "chest":
                    category = BodyPartCategory.Chest;
                    break;
                case "waist":
                    category = BodyPartCategory.Waist;
                    break;
                case "shoulders":
                    category = BodyPartCategory.Shoulders;
                    break;
                case "arms":
                    category = BodyPartCategory.Arms;
                    break;
                case "hands":
                    category = BodyPartCategory.Hands;
                    break;
                case "legs":
                    category = BodyPartCategory.Legs;
                    break;
                case "feet":
                    category = BodyPartCategory.Feet;
                    break;
                case "other":
                    category = BodyPartCategory.Other;
                    break;
                default:
                    category = BodyPartCategory.Other;
                    return false;
            }

            return true;
        }

        private static string GetBodyBlendShapeDisplayName(string shapeName)
        {
            var name = (shapeName ?? string.Empty).Trim();
            var separator = name.IndexOf('/');
            if (separator < 0 || separator >= name.Length - 1)
            {
                return name;
            }

            var role = name.Substring(separator + 1).Trim();
            return role.Length > 0 ? role : name;
        }

        private static bool IsBodyMesh(SkinnedMeshRenderer renderer)
        {
            return renderer != null &&
                   (string.Equals(
                        NormalizeBlendShapeName(renderer.name),
                        "body",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        NormalizeBlendShapeName(renderer.sharedMesh?.name),
                        "body",
                        StringComparison.Ordinal));
        }

        private static float GetBaseBlendShapeWeight(
            SkinnedMeshRenderer renderer,
            string shapeName)
        {
            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(renderer) as
                SkinnedMeshRenderer;
            var index = source?.sharedMesh == null
                ? -1
                : source.sharedMesh.GetBlendShapeIndex(shapeName);
            return index < 0
                ? MinimumBodyBlendShapeWeight
                : Mathf.Clamp(
                    source.GetBlendShapeWeight(index),
                    MinimumBodyBlendShapeWeight,
                    MaximumBodyBlendShapeWeight);
        }

        private static bool TryGetAvatarViewPosition(
            GameObject avatar,
            out Component descriptor,
            out Vector3 viewPosition)
        {
            descriptor = avatar == null
                ? null
                : avatar.GetComponentsInChildren<Component>(true)
                    .FirstOrDefault(component =>
                        component != null &&
                        string.Equals(
                            component.GetType().FullName,
                            AvatarDescriptorTypeName,
                            StringComparison.Ordinal));
            return TryReadAvatarViewPosition(descriptor, out viewPosition) &&
                   viewPosition.y > 0.01f;
        }

        private bool TryGetWorkingAvatarViewPosition(
            out Component descriptor,
            out Vector3 viewPosition)
        {
            if (_avatarDescriptor != null &&
                TryReadAvatarViewPosition(
                    _avatarDescriptor,
                    out viewPosition) &&
                viewPosition.y > 0.01f)
            {
                descriptor = _avatarDescriptor;
                return true;
            }

            var found = TryGetAvatarViewPosition(
                _context.Root,
                out descriptor,
                out viewPosition);
            _avatarDescriptor = found ? descriptor : null;
            return found;
        }

        private static bool TryReadAvatarViewPosition(
            Component descriptor,
            out Vector3 viewPosition)
        {
            viewPosition = Vector3.zero;
            if (descriptor == null)
            {
                return false;
            }

            var serialized = new SerializedObject(descriptor);
            var property = serialized.FindProperty("ViewPosition");
            if (property == null ||
                property.propertyType != SerializedPropertyType.Vector3)
            {
                return false;
            }

            viewPosition = property.vector3Value;
            return true;
        }

        private Vector3 GetBaseAvatarViewPosition(
            Component descriptor,
            Vector3 currentViewPosition)
        {
            if (_baseAvatarViewPosition.HasValue)
            {
                return _baseAvatarViewPosition.Value;
            }

            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(descriptor) as Component;
            if (!TryReadAvatarViewPosition(source, out var baseViewPosition) ||
                baseViewPosition.y <= 0.01f)
            {
                baseViewPosition = currentViewPosition;
            }
            _baseAvatarViewPosition = baseViewPosition;
            return baseViewPosition;
        }

        private Animator FindHumanoidAnimator()
        {
            if (_context.Root == null)
            {
                return null;
            }

            return _context.Root
                .GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(animator =>
                    animator != null &&
                    _context.IsInSelectedPrefabScope(animator.transform) &&
                    animator.avatar != null &&
                    animator.avatar.isHuman &&
                    animator.isHuman);
        }

        private IReadOnlyList<Transform> ResolveBodyScaleTargets(
            BodyScaleDefinition definition,
            Animator animator)
        {
            if (definition.UsesAvatarRoot)
            {
                return _context.Root != null &&
                       (!_context.SelectedPrefabSiblingIndex.HasValue ||
                        _context.SelectedPrefabSiblingIndex == -1)
                    ? new[] { _context.Root.transform }
                    : Array.Empty<Transform>();
            }
            if (animator == null)
            {
                return Array.Empty<Transform>();
            }

            var targets = new List<Transform>();
            foreach (var bone in definition.Bones)
            {
                var target = animator.GetBoneTransform(bone);
                if (target == null ||
                    !_context.IsInSelectedPrefabScope(target))
                {
                    continue;
                }

                targets.Add(target);
                if (definition.UsesFirstAvailableBone)
                {
                    break;
                }
            }
            return targets;
        }

        private static Vector3 GetBodyScaleMultipliers(
            IReadOnlyList<Transform> targets)
        {
            var sum = Vector3.zero;
            var count = 0;
            foreach (var target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                var baseScale = GetBaseLocalScale(target);
                sum += new Vector3(
                    GetScaleRatio(target.localScale.x, baseScale.x),
                    GetScaleRatio(target.localScale.y, baseScale.y),
                    GetScaleRatio(target.localScale.z, baseScale.z));
                count++;
            }

            return count == 0
                ? Vector3.one
                : RoundBodyScaleMultipliers(sum / count);
        }

        private static float GetScaleRatio(float value, float baseValue)
        {
            return Mathf.Abs(baseValue) < 0.0001f
                ? 1f
                : value / baseValue;
        }

        private static Vector3 RoundBodyScaleMultipliers(Vector3 value)
        {
            return new Vector3(
                Mathf.Round(value.x * 100f) / 100f,
                Mathf.Round(value.y * 100f) / 100f,
                Mathf.Round(value.z * 100f) / 100f);
        }

        private static float AverageVectorComponent(Vector3 value)
        {
            return (value.x + value.y + value.z) / 3f;
        }

        private static float GetVectorComponent(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        }

        private static Vector3 SetVectorComponent(
            Vector3 value,
            int axis,
            float component)
        {
            if (axis == 0)
            {
                value.x = component;
            }
            else if (axis == 1)
            {
                value.y = component;
            }
            else
            {
                value.z = component;
            }
            return value;
        }

        private static string GetScaleAxisName(int axis)
        {
            return I18N.Get(
                axis == 0
                    ? "workflow.appearance.sizeAxis.x"
                    : axis == 1
                        ? "workflow.appearance.sizeAxis.y"
                        : "workflow.appearance.sizeAxis.z");
        }

        private static Vector3 GetBaseLocalScale(Transform target)
        {
            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(target) as Transform;
            return source != null ? source.localScale : Vector3.one;
        }

        private Vector3 GetCachedBaseLocalScale(
            string path,
            Transform target)
        {
            var key = path ?? string.Empty;
            if (_bodyScaleBaseScales.TryGetValue(key, out var scale))
            {
                return scale;
            }

            scale = GetBaseLocalScale(target);
            _bodyScaleBaseScales[key] = scale;
            return scale;
        }

        private void ApplyBodyScale(
            IReadOnlyList<string> targetPaths,
            Vector3 multipliers,
            bool updateAvatarViewPosition = false,
            bool rebuildOnFailure = true)
        {
            if (_context.Root == null ||
                targetPaths == null ||
                targetPaths.Count == 0)
            {
                return;
            }

            var targets = _resolvedBodyScaleTargets;
            targets.Clear();
            foreach (var path in targetPaths)
            {
                var target = string.IsNullOrEmpty(path)
                    ? _context.Root.transform
                    : _context.Root.transform.Find(path);
                if (target != null)
                {
                    targets.Add(new KeyValuePair<string, Transform>(
                        path,
                        target));
                }
            }
            if (targets.Count == 0)
            {
                return;
            }

            var previewScales = _bodyScalePreviewScales;
            previewScales.Clear();
            foreach (var pair in targets)
            {
                previewScales[pair.Key] = Vector3.Scale(
                    GetCachedBaseLocalScale(pair.Key, pair.Value),
                    multipliers);
            }

            if (_bodyScaleDragging)
            {
                _context.Preview?.SetTransformScales(
                    previewScales,
                    recalculateBounds: false);
                _context.Repaint();
                _pendingBodySizeChange = PendingBodySizeChange.Scale;
                _pendingBodyScaleTargetPaths = targetPaths;
                _pendingBodyScaleMultipliers = multipliers;
                _pendingBodyScaleUpdatesViewPosition =
                    updateAvatarViewPosition;
                return;
            }
            if (!_context.CanEditPrefab())
            {
                return;
            }

            try
            {
                Component avatarDescriptor = null;
                var baseViewPosition = Vector3.zero;
                if (updateAvatarViewPosition &&
                    TryGetWorkingAvatarViewPosition(
                        out avatarDescriptor,
                        out var currentViewPosition))
                {
                    baseViewPosition = GetBaseAvatarViewPosition(
                        avatarDescriptor,
                        currentViewPosition);
                }
                var undoObjects = targets
                    .Select(pair => (UnityEngine.Object)pair.Value)
                    .ToList();
                if (avatarDescriptor != null)
                {
                    undoObjects.Add(avatarDescriptor);
                }
                Undo.RecordObjects(
                    undoObjects.Distinct().ToArray(),
                    I18N.Get("workflow.appearance.sizeUndo"));
                foreach (var pair in targets)
                {
                    var scale = previewScales[pair.Key];
                    pair.Value.localScale = scale;
                    if (PrefabUtility.IsPartOfPrefabInstance(pair.Value))
                    {
                        PrefabUtility
                            .RecordPrefabInstancePropertyModifications(
                                pair.Value);
                    }
                    EditorUtility.SetDirty(pair.Value);
                }
                if (avatarDescriptor != null)
                {
                    var serialized = new SerializedObject(avatarDescriptor);
                    var viewPosition = serialized.FindProperty("ViewPosition");
                    if (viewPosition != null)
                    {
                        viewPosition.vector3Value = Vector3.Scale(
                            baseViewPosition,
                            multipliers);
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (PrefabUtility.IsPartOfPrefabInstance(
                            avatarDescriptor))
                    {
                        PrefabUtility
                            .RecordPrefabInstancePropertyModifications(
                                avatarDescriptor);
                    }
                    EditorUtility.SetDirty(avatarDescriptor);
                }
                EditorUtility.SetDirty(_context.Root);
                _bodyScaleDirty = true;
                _context.Preview?.SetTransformScales(
                    previewScales,
                    recalculateBounds: true);
                SaveBodyScalePrefab(rebuildOnFailure);
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void ApplyBodyBlendShape(
            string rendererPath,
            string shapeName,
            float weight,
            bool rebuildOnFailure = true)
        {
            if (_context.Root == null ||
                string.IsNullOrEmpty(shapeName))
            {
                return;
            }

            if (_bodyScaleDragging)
            {
                _context.Preview?.SetBlendShapeWeight(
                    rendererPath,
                    shapeName,
                    weight,
                    recalculateBounds: false);
                _context.Repaint();
                _pendingBodySizeChange =
                    PendingBodySizeChange.BlendShape;
                _pendingBodyBlendShapeRendererPath = rendererPath;
                _pendingBodyBlendShapeName = shapeName;
                _pendingBodyBlendShapeWeight = weight;
                return;
            }
            if (!_context.CanEditPrefab())
            {
                return;
            }

            var target = string.IsNullOrEmpty(rendererPath)
                ? _context.Root.transform
                : _context.Root.transform.Find(rendererPath);
            var renderer = target == null
                ? null
                : target.GetComponent<SkinnedMeshRenderer>();
            var shapeIndex = renderer?.sharedMesh == null
                ? -1
                : renderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (renderer == null || shapeIndex < 0)
            {
                return;
            }

            try
            {
                Undo.RecordObject(
                    renderer,
                    I18N.Get("workflow.appearance.sizeUndo"));
                renderer.SetBlendShapeWeight(shapeIndex, weight);
                if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(
                        renderer);
                }
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(_context.Root);
                _bodyScaleDirty = true;
                _context.Preview?.SetBlendShapeWeight(
                    rendererPath,
                    shapeName,
                    weight,
                    recalculateBounds: false);
                SaveBodyScalePrefab(rebuildOnFailure);
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void ApplyBodyBlendShapeGroup(
            IReadOnlyList<BodyBlendShapeDefinition> definitions,
            float weight,
            bool rebuildOnFailure = true)
        {
            if (definitions == null || definitions.Count == 0 ||
                _context.Root == null)
            {
                return;
            }
            if (_bodyScaleDragging)
            {
                PreviewBodyBlendShapeGroup(definitions, weight);
                _pendingBodySizeChange = PendingBodySizeChange.BlendShape;
                _pendingBodyBlendShapeGroup = definitions;
                _pendingIndividualBlendShape = null;
                _pendingBodyBlendShapeWeight = weight;
                return;
            }
            if (!_context.CanEditPrefab())
            {
                return;
            }

            try
            {
                var source = ResolveBodyBlendShapeGroupSource(definitions);
                var shapeName = definitions[0].ShapeName;
                var undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(
                    I18N.Get("workflow.appearance.sizeUndo"));
                var targets = new HashSet<SkinnedMeshRenderer>();
                foreach (var definition in definitions)
                {
                    var target = GetBodyBlendShapeRenderer(definition);
                    if (target?.sharedMesh == null)
                    {
                        continue;
                    }
                    var existingSource = ResolveBodyBlendShapeSource(
                        target, definition.ShapeName);
                    var bindingSource = existingSource ?? source;
                    if (bindingSource != null && target != bindingSource)
                    {
                        SetBodyBlendShapeSyncBinding(
                            target,
                            bindingSource,
                            definition.ShapeName,
                            null);
                        targets.Add(bindingSource);
                    }
                    targets.Add(target);
                }
                foreach (var target in targets)
                {
                    SetBodyBlendShapeRendererWeight(
                        target, shapeName, weight);
                }
                Undo.CollapseUndoOperations(undoGroup);
                _bodyScaleDirty = true;
                PreviewBodyBlendShapeGroup(definitions, weight);
                SaveBodyScalePrefab(rebuildOnFailure);
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void EnsureBodyBlendShapeSyncBindings(
            IReadOnlyList<BodyBlendShapeDefinition> definitions)
        {
            if (!_context.CanEditPrefab())
            {
                return;
            }
            var changed = false;
            try
            {
                var undoGroup = -1;
                foreach (var group in definitions
                             .GroupBy(definition => new
                             {
                                 definition.Category,
                                 definition.ShapeName
                             })
                             .Where(group => group.Count() > 1))
                {
                    var members = group.ToArray();
                    var source = ResolveBodyBlendShapeGroupSource(members);
                    var sourceIndex = source?.sharedMesh == null
                        ? -1
                        : source.sharedMesh.GetBlendShapeIndex(
                            group.Key.ShapeName);
                    if (sourceIndex < 0)
                    {
                        continue;
                    }
                    var sourceWeight = source.GetBlendShapeWeight(sourceIndex);
                    foreach (var definition in members)
                    {
                        var target = GetBodyBlendShapeRenderer(definition);
                        if (target == null || target == source ||
                            HasBodyBlendShapeSyncBinding(
                                target, definition.ShapeName))
                        {
                            continue;
                        }
                        if (!changed)
                        {
                            Undo.IncrementCurrentGroup();
                            undoGroup = Undo.GetCurrentGroup();
                            Undo.SetCurrentGroupName(
                                I18N.Get("workflow.appearance.sizeUndo"));
                        }
                        SetBodyBlendShapeSyncBinding(
                            target, source, definition.ShapeName, null);
                        SetBodyBlendShapeRendererWeight(
                            target, definition.ShapeName, sourceWeight);
                        definition.Value = sourceWeight;
                        changed = true;
                    }
                }
                if (changed)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                    _bodyScaleDirty = true;
                    SaveBodyScalePrefab(false);
                }
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, false);
            }
        }

        private void ApplyIndividualBodyBlendShape(
            IReadOnlyList<BodyBlendShapeDefinition> group,
            BodyBlendShapeDefinition definition,
            float weight,
            bool rebuildOnFailure = true)
        {
            if (_context.Root == null || definition == null)
            {
                return;
            }
            if (_bodyScaleDragging)
            {
                _context.Preview?.SetBlendShapeWeight(
                    definition.RendererPath,
                    definition.ShapeName,
                    weight,
                    recalculateBounds: false);
                _context.Repaint();
                _pendingBodySizeChange = PendingBodySizeChange.BlendShape;
                _pendingBodyBlendShapeGroup = group;
                _pendingIndividualBlendShape = definition;
                _pendingBodyBlendShapeWeight = weight;
                return;
            }
            if (!_context.CanEditPrefab())
            {
                return;
            }

            try
            {
                var target = GetBodyBlendShapeRenderer(definition);
                if (target?.sharedMesh == null)
                {
                    return;
                }
                var source = ResolveBodyBlendShapeSource(
                    target, definition.ShapeName) ??
                    ResolveBodyBlendShapeGroupSource(group);
                var editsSource = source == target;
                if (source != null && source != target)
                {
                    var sourceIndex = source.sharedMesh.GetBlendShapeIndex(
                        definition.ShapeName);
                    if (sourceIndex >= 0)
                    {
                        SetBodyBlendShapeSyncBinding(
                            target,
                            source,
                            definition.ShapeName,
                            BuildIndividualBodyBlendShapeRemap(
                                source.GetBlendShapeWeight(sourceIndex),
                                weight));
                    }
                }
                SetBodyBlendShapeRendererWeight(
                    target, definition.ShapeName, weight);
                _bodyScaleDirty = true;
                _context.Preview?.SetBlendShapeWeight(
                    definition.RendererPath,
                    definition.ShapeName,
                    weight,
                    recalculateBounds: false);
                SaveBodyScalePrefab(rebuildOnFailure);
                if (editsSource && _context.UiRoot.panel != null)
                {
                    _context.UiRoot.schedule.Execute(() =>
                    {
                        _context.InvalidateControls(AvatarEditorPanel.Shape);
                        _context.ShowParts();
                    });
                }
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void PreviewBodyBlendShapeGroup(
            IReadOnlyList<BodyBlendShapeDefinition> definitions,
            float weight)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                paths.Add(definition.RendererPath);
            }
            var source = ResolveBodyBlendShapeGroupSource(definitions);
            if (source != null)
            {
                paths.Add(AnimationUtility.CalculateTransformPath(
                    source.transform, _context.Root.transform));
            }
            foreach (var path in paths)
            {
                _context.Preview?.SetBlendShapeWeight(
                    path,
                    definitions[0].ShapeName,
                    weight,
                    recalculateBounds: false);
            }
            _context.Repaint();
        }

        private void SetBodyBlendShapeRendererWeight(
            SkinnedMeshRenderer renderer,
            string shapeName,
            float weight)
        {
            var index = renderer?.sharedMesh == null
                ? -1
                : renderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (index < 0 || Mathf.Approximately(
                    renderer.GetBlendShapeWeight(index), weight))
            {
                return;
            }
            Undo.RecordObject(renderer,
                I18N.Get("workflow.appearance.sizeUndo"));
            renderer.SetBlendShapeWeight(index, weight);
            if (PrefabUtility.IsPartOfPrefabInstance(renderer))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    renderer);
            }
            EditorUtility.SetDirty(renderer);
        }

        private void SetBodyBlendShapeSyncBinding(
            SkinnedMeshRenderer target,
            SkinnedMeshRenderer source,
            string shapeName,
            AnimationCurve remap)
        {
            var sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            if (sync == null)
            {
                sync = Undo.AddComponent<ModularAvatarBlendshapeSync>(
                    target.gameObject);
            }
            Undo.RecordObject(sync,
                I18N.Get("workflow.appearance.sizeUndo"));
            if (sync.Bindings == null)
            {
                sync.Bindings = new List<BlendshapeBinding>();
            }
            var index = sync.Bindings.FindIndex(binding =>
                (string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape) == shapeName);
            var bindingValue = index >= 0
                ? sync.Bindings[index]
                : new BlendshapeBinding
                {
                    ReferenceMesh = new AvatarObjectReference
                    {
                        referencePath = AnimationUtility.CalculateTransformPath(
                            source.transform, _context.Root.transform)
                    },
                    Blendshape = shapeName,
                    LocalBlendshape = string.Empty
                };
            bindingValue.RemapCurveIsValid = true;
            bindingValue.RemapCurve = remap ??
                AnimationCurve.Linear(0f, 0f, 100f, 100f);
            if (index >= 0)
            {
                sync.Bindings[index] = bindingValue;
            }
            else
            {
                sync.Bindings.Add(bindingValue);
            }
            if (PrefabUtility.IsPartOfPrefabInstance(sync))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(sync);
            }
            EditorUtility.SetDirty(sync);
        }

        private static AnimationCurve BuildIndividualBodyBlendShapeRemap(
            float sourceWeight,
            float targetWeight)
        {
            var source = Mathf.Clamp(sourceWeight, 0f, 100f);
            var target = Mathf.Clamp(targetWeight, 0f, 100f);
            if (source <= 0f)
            {
                return AnimationCurve.Linear(0f, target, 100f, 100f);
            }
            if (source >= 100f)
            {
                return AnimationCurve.Linear(0f, 0f, 100f, target);
            }
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(source, target),
                new Keyframe(100f, 100f));
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

        private void BeginBodyScaleDrag()
        {
            if (_bodyScaleDragging)
            {
                return;
            }

            _bodyScaleDragging = true;
            ClearPendingBodySizeChange();
        }

        public void EndBodyScaleDrag(bool rebuildOnFailure = true)
        {
            if (!_bodyScaleDragging &&
                _pendingBodySizeChange == PendingBodySizeChange.None)
            {
                return;
            }

            var pendingChange = _pendingBodySizeChange;
            var targetPaths = _pendingBodyScaleTargetPaths;
            var multipliers = _pendingBodyScaleMultipliers;
            var updateAvatarViewPosition =
                _pendingBodyScaleUpdatesViewPosition;
            var rendererPath = _pendingBodyBlendShapeRendererPath;
            var shapeName = _pendingBodyBlendShapeName;
            var weight = _pendingBodyBlendShapeWeight;
            var blendShapeGroup = _pendingBodyBlendShapeGroup;
            var individualBlendShape = _pendingIndividualBlendShape;
            ClearPendingBodySizeChange();
            _bodyScaleDragging = false;
            if (pendingChange == PendingBodySizeChange.Scale)
            {
                ApplyBodyScale(
                    targetPaths,
                    multipliers,
                    updateAvatarViewPosition,
                    rebuildOnFailure);
            }
            else if (pendingChange == PendingBodySizeChange.BlendShape)
            {
                if (individualBlendShape != null)
                {
                    ApplyIndividualBodyBlendShape(
                        blendShapeGroup,
                        individualBlendShape,
                        weight,
                        rebuildOnFailure);
                }
                else if (blendShapeGroup != null)
                {
                    ApplyBodyBlendShapeGroup(
                        blendShapeGroup,
                        weight,
                        rebuildOnFailure);
                }
                else
                {
                    ApplyBodyBlendShape(
                        rendererPath,
                        shapeName,
                        weight,
                        rebuildOnFailure);
                }
            }
            _context.Preview?.FlushUpdates(
                recalculateBounds:
                pendingChange == PendingBodySizeChange.Scale);
            if (pendingChange == PendingBodySizeChange.None)
            {
                SaveBodyScalePrefab(rebuildOnFailure);
            }
        }

        public void ClearPendingBodySizeChange()
        {
            _pendingBodySizeChange = PendingBodySizeChange.None;
            _pendingBodyScaleTargetPaths = null;
            _pendingBodyScaleUpdatesViewPosition = false;
            _pendingBodyBlendShapeRendererPath = null;
            _pendingBodyBlendShapeName = null;
            _pendingBodyBlendShapeGroup = null;
            _pendingIndividualBlendShape = null;
        }

        public void SaveBodyScalePrefab(bool rebuildOnFailure = true)
        {
            if (!_bodyScaleDirty || !_context.CanEditPrefab())
            {
                return;
            }
            _bodyScaleDirty = false;
            _context.WorkingSceneDirty = true;
            _context.Changed();
            _context.Feedback = string.Empty;
        }

        public void ReportBodyScaleFailure(
            Exception exception,
            bool rebuildControls)
        {
            Debug.LogException(exception);
            _context.Feedback = I18N.Get(
                "workflow.appearance.sizeSaveFailed");
            _context.FeedbackType = HelpBoxMessageType.Error;
            if (rebuildControls && _context.UiRoot.panel != null)
            {
                _context.UiRoot.schedule.Execute(() =>
                    _context.ShowParts());
            }
        }

        private static string FormatBodyHeight(float value)
        {
            return value.ToString("0.00") + " m";
        }

        public VisualElement BuildShapePartsTabs()
        {
            var tabs = new VisualElement();
            UiComposition.Prepare(tabs,
                "Editor/Feature/Shared/AvatarEditing/avatar-editing.uss",
                "Editor/Feature/Avatar/AvatarParts/avatar-parts.uss");
            tabs.AddToClassList(
                "ee4v-modification-workflow__shape-parts-tabs");
            AddShapePartsButton(
                tabs,
                ShapePartsSection.Parts,
                "workflow.shapeParts.parts");
            AddShapePartsButton(
                tabs,
                ShapePartsSection.Shape,
                "workflow.shapeParts.shape");
            return tabs;
        }

        private void AddShapePartsButton(
            VisualElement tabs,
            ShapePartsSection section,
            string labelKey)
        {
            var button = new UiButton(
                I18N.Get(labelKey),
                () =>
                {
                    if (_shapePartsSection == section)
                    {
                        return;
                    }
                    if (_shapePartsSection == ShapePartsSection.Shape)
                    {
                        EndBodyScaleDrag();
                        SaveBodyScalePrefab();
                        if (_bodyScaleDirty) { return; }
                    }
                    _shapePartsSection = section;
                    _context.ShowParts();
                    _context.ControlsHost.scrollOffset = Vector2.zero;
                },
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(
                "ee4v-modification-workflow__shape-parts-tab");
            button.EnableInClassList(
                "ee4v-modification-workflow__shape-parts-tab--active",
                section == _shapePartsSection);
            tabs.Add(button);
        }
    }
}
