using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ee4v.ExpressionMenu
{
    internal sealed partial class ExpressionMenuTemplateEditor
    {
        private VisualElement PropertyCard(VisualElement parent, string name, string title, bool synced,
            Action remove, Action<bool> sync)
        {
            var card = new VisualElement { name = name };
            card.AddToClassList("ee4v-menu-template__change");
            parent.Add(card);
            var header = new SectionHeader(T(title));
            header.Actions.Add(RemoveActionButton(header, remove));
            card.Add(header);
            AddSync(card, synced, sync, true);
            return card;
        }

        private void ChangeProperty(Action change, bool refresh = false)
        {
            try { ExpressionMenuAnimationRecipe.Change(_context, _item, change); }
            catch { Refresh(); throw; }
            if (refresh) Refresh();
        }

        private void VectorInputs(VisualElement host, Vector4 current, int count, bool readOnly, Action<Vector4> change)
        {
            var group = new VisualElement();
            group.AddToClassList("ee4v-menu-template__vector");
            host.Add(group);
            for (var index = 0; index < count; index++)
            {
                var channel = index;
                var field = UiTextFactory.CreateFloatField("XYZW"[index].ToString(), "ee4v-menu-template__vector-value");
                field.isDelayed = true;
                field.isReadOnly = readOnly;
                field.SetValueWithoutNotify(current[index]);
                field.RegisterValueChangedCallback(evt => Run(() =>
                {
                    try
                    {
                        ExpressionMenuAnimationRecipe.ValidateMaterialValue(evt.newValue);
                        var next = current;
                        next[channel] = evt.newValue;
                        change(next);
                        current = next;
                    }
                    catch { field.SetValueWithoutNotify(evt.previousValue); throw; }
                }));
                group.Add(field);
            }
        }

        private void MaterialInput(VisualElement host, MenuMaterialProperty property, Vector4 vector,
            float scalar, Object reference, bool readOnly, Action<Vector4> setVector, Action<float> setScalar, Action<Object> setObject)
        {
            if (property.Kind == MenuMaterialPropertyKind.Color)
            {
                var field = UiTextFactory.CreateColorField("");
                field.hdr = property.Hdr;
                field.showAlpha = true;
                field.SetValueWithoutNotify((Color)vector);
                field.SetEnabled(!readOnly);
                field.RegisterValueChangedCallback(evt => Run(() => setVector(evt.newValue)));
                host.Add(field);
            }
            else if (property.Count > 1)
                VectorInputs(host, vector, property.Count, readOnly, setVector);
            else if (property.Kind == MenuMaterialPropertyKind.Texture || property.Kind == MenuMaterialPropertyKind.Shader)
            {
                var field = UiTextFactory.CreateObjectField("");
                field.objectType = property.Kind == MenuMaterialPropertyKind.Texture ? typeof(Texture) : typeof(Shader);
                field.allowSceneObjects = false;
                field.SetValueWithoutNotify(reference);
                field.SetEnabled(!readOnly);
                field.RegisterValueChangedCallback(evt => Run(() => setObject(evt.newValue)));
                host.Add(field);
            }
            else if (property.Boolean)
            {
                var field = UiTextFactory.CreateToggle("");
                field.SetValueWithoutNotify(scalar != 0);
                field.SetEnabled(!readOnly);
                field.RegisterValueChangedCallback(evt => Run(() => setScalar(evt.newValue ? 1 : 0)));
                host.Add(field);
            }
            else if (property.Integral)
            {
                var field = UiTextFactory.CreateIntegerField("");
                field.isReadOnly = readOnly;
                field.isDelayed = true;
                field.SetValueWithoutNotify((int)scalar);
                field.RegisterValueChangedCallback(evt => Run(() => setScalar(evt.newValue)));
                host.Add(field);
            }
            else
            {
                var field = UiTextFactory.CreateFloatField("");
                field.isReadOnly = readOnly;
                field.isDelayed = true;
                field.SetValueWithoutNotify(scalar);
                field.RegisterValueChangedCallback(evt => Run(() => setScalar(evt.newValue)));
                host.Add(field);
            }
        }

        private void BuildMaterialValues(VisualElement parent, ExpressionMenuAnimationRecipe recipe, int index)
        {
            MenuMaterialValueAction Action() => recipe.MaterialValues[index];
            var card = PropertyCard(parent, "materialValueAction", "kindMaterialValue", Action().Synced,
                () => ChangeProperty(() => recipe.MaterialValues.RemoveAt(index), true),
                value => ChangeProperty(() => Action().Synced = value));
            var radial = ExpressionMenuAnimationRecipe.EffectiveMode(_item) == MenuBehaviorMode.Radial;
            for (var rowIndex = 0; rowIndex < Action().Targets.Count; rowIndex++)
            {
                var targetIndex = rowIndex;
                MenuMaterialValueTarget Target() => Action().Targets[targetIndex];
                var row = Entry(card, "materialValueEntry", targetIndex, () => ChangeProperty(() => Action().Targets.RemoveAt(targetIndex), true));
                var renderer = ExpressionMenuAnimationRecipe.ResolveMaterialRenderer(_context, Target());
                void Initialize()
                {
                    var vector = ExpressionMenuAnimationRecipe.MaterialCurrentVector(_context, Target());
                    Target().Minimum = Target().Maximum = vector.x;
                    Target().MinimumVector = Target().MaximumVector = vector;
                    var material = ExpressionMenuAnimationRecipe.ResolveMaterialRenderer(_context, Target())?.sharedMaterials.FirstOrDefault(value => value != null);
                    var property = ExpressionMenuAnimationRecipe.MaterialProperty(renderer, Target());
                    Target().Texture = material != null && property?.Kind == MenuMaterialPropertyKind.Texture && material.HasProperty(property.ShaderName) ?
                        material.GetTexture(property.ShaderName) : null;
                    Target().Shader = material != null ? material.shader : null;
                }
                AddObject<Renderer>(row, T("targetRenderer"), renderer, true, value =>
                {
                    if (value != null)
                    {
                        ExpressionMenuTemplateModel.Reference(_context, value.gameObject);
                        if (value.gameObject.GetComponent<Renderer>() != value)
                            throw new InvalidOperationException(T("ambiguousMaterialRenderer"));
                    }
                    ChangeProperty(() =>
                    {
                        renderer = value;
                        Target().Path = value == null ? null : AnimationUtility.CalculateTransformPath(value.transform, _context.Root.transform);
                        Target().Property = ExpressionMenuAnimationRecipe.MaterialProperties(value).FirstOrDefault(property => !radial || property.Animated)?.Name;
                        Initialize();
                    }, true);
                });
                var properties = ExpressionMenuAnimationRecipe.MaterialProperties(renderer).Where(property => !radial || property.Animated).ToArray();
                var choices = properties.Select(property => property.Label).ToList();
                var selected = Array.FindIndex(properties, property => property.Name == Target().Property);
                if (selected < 0) choices.Insert(0, T("selectMaterialProperty"));
                var selector = UiTextFactory.CreatePopupField(T("materialProperty"), choices, Math.Max(0, selected));
                selector.name = "materialProperty";
                selector.SetEnabled(properties.Length > 0);
                selector.RegisterValueChangedCallback(evt => Run(() => ChangeProperty(() =>
                {
                    Target().Property = properties.FirstOrDefault(property => property.Label == evt.newValue)?.Name;
                    Initialize();
                }, true)));
                row.Add(selector);
                var propertyInfo = selected >= 0 ? properties[selected] : null;
                if (propertyInfo == null)
                {
                    if (Target().Path != null && (renderer == null || !string.IsNullOrEmpty(Target().Property)))
                        row.Add(UiTextFactory.CreateHelpBox(T("missingMaterialProperty"), HelpBoxMessageType.Error));
                    continue;
                }
                var transition = AddTransition(row, T(radial ? "rangeStart" : "offValue"), T(radial ? "rangeEnd" : "onValue"));
                transition.Before.name = "materialMinimum";
                transition.After.name = "materialMaximum";
                void Endpoint(VisualElement host, bool maximum)
                {
                    var baseline = ExpressionMenuAnimationRecipe.MaterialCurrentVector(_context, Target());
                    var readOnly = !radial && !maximum;
                    var vector = readOnly ? baseline : maximum ? Target().MaximumVector : Target().MinimumVector;
                    var scalar = readOnly ? baseline.x : maximum ? Target().Maximum : Target().Minimum;
                    var material = renderer.sharedMaterials.FirstOrDefault(value => value != null &&
                        (propertyInfo.ShaderName == null || propertyInfo.Kind == MenuMaterialPropertyKind.Keyword || value.HasProperty(propertyInfo.ShaderName)));
                    Object reference = propertyInfo.Kind == MenuMaterialPropertyKind.Shader ? maximum ? Target().Shader : material?.shader :
                        maximum ? Target().Texture : material != null && propertyInfo.Kind == MenuMaterialPropertyKind.Texture ? material.GetTexture(propertyInfo.ShaderName) : null;
                    MaterialInput(host, propertyInfo, vector, scalar, reference, readOnly,
                        value => ChangeProperty(() => { if (maximum) Target().MaximumVector = value; else Target().MinimumVector = value; }),
                        value => ChangeProperty(() => { if (maximum) Target().Maximum = value; else Target().Minimum = value; }),
                        value => ChangeProperty(() =>
                        {
                            if (propertyInfo.Kind == MenuMaterialPropertyKind.Texture) Target().Texture = value as Texture;
                            else Target().Shader = value as Shader;
                        }));
                }
                Endpoint(transition.Before, false);
                Endpoint(transition.After, true);
            }
            AddEntryButton(card, "addMaterialValue", () => ChangeProperty(() =>
                Action().Targets.Add(new MenuMaterialValueTarget()), true));
            card.SetEnabled(ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
        }

        private void BuildButtonParameter(VisualElement card, ExpressionMenuAnimationRecipe recipe, int index, ExpressionMenuParameterCatalog.Entry known)
        {
            MenuParameterAction Action() => recipe.ParameterActions[index];
            var types = new[] { AnimatorControllerParameterType.Trigger, AnimatorControllerParameterType.Bool,
                AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Float };
            var selector = UiTextFactory.CreatePopupField(T("parameterType"), types.Select(type => type.ToString()).ToList(), Array.IndexOf(types, Action().ButtonType));
            selector.name = "buttonParameterType";
            selector.SetEnabled(known == null);
            selector.RegisterValueChangedCallback(evt => Run(() => ChangeProperty(() =>
            {
                Action().ButtonType = types.First(type => type.ToString() == evt.newValue);
                Action().On = Action().ButtonType == AnimatorControllerParameterType.Bool ? 0 : 1;
            }, true)));
            card.Add(selector);
            if (Action().ButtonType == AnimatorControllerParameterType.Trigger) return;
            var host = new VisualElement { name = "buttonParameterValue" };
            card.Add(new FormInput(T("setValue"), host));
            var kind = Action().ButtonType == AnimatorControllerParameterType.Bool ? MenuMaterialPropertyKind.Keyword :
                Action().ButtonType == AnimatorControllerParameterType.Int ? MenuMaterialPropertyKind.Integer : MenuMaterialPropertyKind.Float;
            MaterialInput(host, new MenuMaterialProperty { Kind = kind }, Vector4.zero, Action().On, null, false,
                _ => { }, value => ChangeProperty(() => Action().On = value), _ => { });
        }

        private void BuildTransforms(VisualElement parent, ExpressionMenuAnimationRecipe recipe, int index)
        {
            MenuTransformAction Action() => recipe.Transforms[index];
            var card = PropertyCard(parent, "transformAction", "kindTransform", Action().Synced,
                () => ChangeProperty(() => recipe.Transforms.RemoveAt(index), true),
                value => ChangeProperty(() => Action().Synced = value));
            var mode = ExpressionMenuAnimationRecipe.EffectiveMode(_item);
            var puppet = mode == MenuBehaviorMode.Puppet;
            var continuous = ExpressionMenuAnimationRecipe.IsContinuous(_item);
            if (puppet)
            {
                if (ExpressionMenuAnimationRecipe.AxisCount(_item) == 2)
                {
                    var source = UiTextFactory.CreatePopupField(T("puppetInput"), new List<string> { T("axisHorizontal"), T("axisVertical") }, Action().SourceAxis);
                    source.name = "transformSourceAxis";
                    source.RegisterValueChangedCallback(evt => Run(() => ChangeProperty(() =>
                        Action().SourceAxis = evt.newValue == T("axisHorizontal") ? 0 : 1, true)));
                    card.Add(source);
                }
                void Axis(bool vertical)
                {
                    var selector = UiTextFactory.CreatePopupField(T(ExpressionMenuAnimationRecipe.AxisCount(_item) == 2 ? "transformAxis" :
                        vertical ? "verticalTransformAxis" : "horizontalTransformAxis"),
                        new List<string> { "X", "Y", "Z" }, vertical ? Action().VerticalAxis : Action().HorizontalAxis);
                    selector.name = vertical ? "transformVerticalAxis" : "transformHorizontalAxis";
                    selector.RegisterValueChangedCallback(evt => Run(() => ChangeProperty(() =>
                    {
                        var selected = "XYZ".IndexOf(evt.newValue, StringComparison.Ordinal);
                        if (vertical)
                        {
                            if (Action().HorizontalAxis == selected) Action().HorizontalAxis = Action().VerticalAxis;
                            Action().VerticalAxis = selected;
                        }
                        else
                        {
                            if (Action().VerticalAxis == selected) Action().VerticalAxis = Action().HorizontalAxis;
                            Action().HorizontalAxis = selected;
                        }
                    }, true)));
                    card.Add(selector);
                }
                Axis(false);
                if (ExpressionMenuAnimationRecipe.AxisCount(_item) == 4) Axis(true);
            }
            for (var rowIndex = 0; rowIndex < Action().Targets.Count; rowIndex++)
            {
                var targetIndex = rowIndex;
                MenuTransformTarget Target() => Action().Targets[targetIndex];
                var row = Entry(card, "transformEntry", targetIndex, () => ChangeProperty(() => Action().Targets.RemoveAt(targetIndex), true));
                var transform = ExpressionMenuAnimationRecipe.ResolveTransform(_context, Target().Path);
                AddObject<GameObject>(row, T("target"), transform != null ? transform.gameObject : null, true, value =>
                {
                    if (value != null) ExpressionMenuTemplateModel.Reference(_context, value);
                    ChangeProperty(() =>
                    {
                        Target().Path = value == null ? null : AnimationUtility.CalculateTransformPath(value.transform, _context.Root.transform);
                        ExpressionMenuAnimationRecipe.InitializeTransform(Target(), value != null ? value.transform : null);
                    }, true);
                });
                if (Target().Path != null && transform == null)
                    row.Add(UiTextFactory.CreateHelpBox(T("missingTransform"), HelpBoxMessageType.Error));
                void Property(string key, bool enabled, Action<bool> setEnabled, Func<bool, Vector3> get, Action<bool, Vector3> set, Vector3 baseline)
                {
                    var toggle = UiTextFactory.CreateToggle(T(key));
                    toggle.SetValueWithoutNotify(enabled);
                    toggle.RegisterValueChangedCallback(evt => Run(() => ChangeProperty(() => setEnabled(evt.newValue), true)));
                    row.Add(toggle);
                    if (!enabled) return;
                    var host = new VisualElement();
                    host.SetEnabled(transform != null);
                    row.Add(host);
                    if (puppet)
                    {
                        var axes = ExpressionMenuAnimationRecipe.AxisCount(_item) == 4 ?
                            new[] { Action().HorizontalAxis, Action().VerticalAxis } : new[] { Action().HorizontalAxis };
                        foreach (var axis in axes)
                        {
                            var input = new VisualElement();
                            input.AddToClassList("ee4v-menu-template__range");
                            host.Add(new FormInput("XYZ"[axis].ToString(), input));
                            void Endpoint(bool maximum)
                            {
                                var field = UiTextFactory.CreateFloatField("", "ee4v-menu-template__range-value");
                                field.tooltip = T(maximum ? "positiveValue" : "negativeValue");
                                field.isDelayed = true;
                                field.SetValueWithoutNotify(get(maximum)[axis]);
                                field.RegisterValueChangedCallback(evt => Run(() => ChangeProperty(() =>
                                {
                                    var vector = get(maximum);
                                    vector[axis] = evt.newValue;
                                    set(maximum, vector);
                                })));
                                input.Add(field);
                            }
                            Endpoint(false);
                            input.Add(UiTextFactory.Create("〜", "ee4v-menu-template__range-separator"));
                            Endpoint(true);
                        }
                    }
                    else
                    {
                        var transition = AddTransition(host, T(continuous ? "rangeStart" : "offValue"), T(continuous ? "rangeEnd" : "onValue"));
                        VectorInputs(transition.Before, continuous ? get(false) : baseline, 3, !continuous,
                            value => ChangeProperty(() => set(false, value)));
                        VectorInputs(transition.After, get(true), 3, false, value => ChangeProperty(() => set(true, value)));
                    }
                }
                Property("transformPosition", Target().Position, value => Target().Position = value,
                    max => max ? Target().MaximumPosition : Target().MinimumPosition,
                    (max, value) => { if (max) Target().MaximumPosition = value; else Target().MinimumPosition = value; },
                    transform != null ? transform.localPosition : Vector3.zero);
                Property("transformRotation", Target().Rotation, value => Target().Rotation = value,
                    max => max ? Target().MaximumRotation : Target().MinimumRotation,
                    (max, value) => { if (max) Target().MaximumRotation = value; else Target().MinimumRotation = value; },
                    transform != null ? transform.localEulerAngles : Vector3.zero);
                Property("transformScale", Target().Scale, value => Target().Scale = value,
                    max => max ? Target().MaximumScale : Target().MinimumScale,
                    (max, value) => { if (max) Target().MaximumScale = value; else Target().MinimumScale = value; },
                    transform != null ? transform.localScale : Vector3.one);
            }
            AddEntryButton(card, "addTransform", () => ChangeProperty(() =>
                Action().Targets.Add(new MenuTransformTarget()), true));
            card.SetEnabled(ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
        }

        private void BuildComponents(VisualElement parent, ExpressionMenuAnimationRecipe recipe, int index)
        {
            MenuComponentAction Action() => recipe.Components[index];
            var card = PropertyCard(parent, "componentAction", "kindComponent", Action().Synced,
                () => ChangeProperty(() => recipe.Components.RemoveAt(index), true),
                value => ChangeProperty(() => Action().Synced = value));
            for (var rowIndex = 0; rowIndex < Action().Targets.Count; rowIndex++)
            {
                var targetIndex = rowIndex;
                MenuComponentTarget Target() => Action().Targets[targetIndex];
                var row = Entry(card, "componentEntry", targetIndex, () => ChangeProperty(() => Action().Targets.RemoveAt(targetIndex), true));
                var transform = ExpressionMenuAnimationRecipe.ResolveTransform(_context, Target().Path);
                AddObject<GameObject>(row, T("target"), transform != null ? transform.gameObject : null, true, value =>
                {
                    if (value != null) ExpressionMenuTemplateModel.Reference(_context, value);
                    ChangeProperty(() =>
                    {
                        Target().Path = value == null ? null : AnimationUtility.CalculateTransformPath(value.transform, _context.Root.transform);
                        Target().Type = ExpressionMenuAnimationRecipe.EnabledComponents(value).FirstOrDefault()?.GetType().AssemblyQualifiedName;
                        Target().Enabled = ExpressionMenuAnimationRecipe.ComponentCurrentValue(_context, Target());
                    }, true);
                });
                var components = ExpressionMenuAnimationRecipe.EnabledComponents(transform != null ? transform.gameObject : null);
                string Label(Component component) => components.Count(value => value.GetType().Name == component.GetType().Name) > 1 ?
                    component.GetType().FullName : component.GetType().Name;
                var labels = components.Select(Label).ToList();
                var selected = Array.FindIndex(components, component => component.GetType().AssemblyQualifiedName == Target().Type);
                if (selected < 0) labels.Insert(0, T("selectComponent"));
                var selector = UiTextFactory.CreatePopupField(T("component"), labels, Math.Max(0, selected));
                selector.name = "componentType";
                selector.SetEnabled(components.Length > 0);
                selector.RegisterValueChangedCallback(evt => Run(() => ChangeProperty(() =>
                {
                    Target().Type = components.FirstOrDefault(component => Label(component) == evt.newValue)?.GetType().AssemblyQualifiedName;
                    Target().Enabled = ExpressionMenuAnimationRecipe.ComponentCurrentValue(_context, Target());
                }, true)));
                row.Add(selector);
                if (selected < 0)
                {
                    if (Target().Path != null)
                        row.Add(UiTextFactory.CreateHelpBox(T(components.Length == 0 ? "noEnabledComponent" : "missingComponent"), HelpBoxMessageType.Error));
                    continue;
                }
                var transition = AddTransition(row, T("offValue"), T("onValue"));
                var off = UiTextFactory.CreateToggle("");
                off.SetValueWithoutNotify(ExpressionMenuAnimationRecipe.ComponentCurrentValue(_context, Target()));
                off.SetEnabled(false);
                transition.Before.Add(off);
                var on = UiTextFactory.CreateToggle("");
                on.SetValueWithoutNotify(Target().Enabled);
                on.RegisterValueChangedCallback(evt => Run(() => ChangeProperty(() => Target().Enabled = evt.newValue)));
                transition.After.Add(on);
            }
            AddEntryButton(card, "addComponent", () => ChangeProperty(() =>
                Action().Targets.Add(new MenuComponentTarget()), true));
            card.SetEnabled(ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
        }
    }
}
