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
        private VisualElement BuildBodyScaleControls()
        {
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
                    _context.Host.ShowParts();
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
