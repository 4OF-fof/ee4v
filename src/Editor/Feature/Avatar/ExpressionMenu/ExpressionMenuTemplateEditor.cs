using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.I18n;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ee4v.ExpressionMenu
{
    internal static class TemplateText
    {
        internal static string Get(string key) => I18N.Get("expressionMenu.templates." + key);
    }

    internal sealed partial class ExpressionMenuTemplateEditor : VisualElement
    {
        private readonly AvatarEditingContext _context;
        private readonly ModularAvatarMenuItem _item;
        private readonly VisualElement _fields;
        private readonly HelpBox _feedback;
        private readonly Action _typeChanged;
        private ConfirmationOverlay _modeOverlay;
        private static string T(string key) => TemplateText.Get(key);

        internal ExpressionMenuTemplateEditor(AvatarEditingContext context, ModularAvatarMenuItem item, Action typeChanged)
        {
            _context = context;
            _item = item;
            _typeChanged = typeChanged;
            RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == this) _modeOverlay?.Close(); });
            AddToClassList("ee4v-menu-template");
            _feedback = UiTextFactory.CreateHelpBox("", HelpBoxMessageType.Error);
            _feedback.AddToClassList("ee4v-menu-template__hidden");
            Add(_feedback);
            _fields = new VisualElement();
            Add(_fields);
            Refresh();
        }

        private void Refresh()
        {
            _fields.Clear();
            var owned = ExpressionMenuTemplateModel.IsOwned(_item.gameObject);
            var canEdit = ExpressionMenuTemplateModel.CanEdit(_context, _item);
            var mode = ExpressionMenuAnimationRecipe.EffectiveMode(_item);
            var recipe = ExpressionMenuAnimationRecipe.Find(_item);
            if (recipe != null) canEdit &= ExpressionMenuAnimationRecipe.CanEdit(_context, _item);
            var details = BuildIdentity(canEdit);
            if (_item.PortableControl.Type == PortableControlType.SubMenu) return;
            var settings = new VisualElement();
            settings.AddToClassList("ee4v-menu-template__settings");
            details.Add(settings);
            var modes = new[] { MenuBehaviorMode.Toggle, MenuBehaviorMode.Radial, MenuBehaviorMode.Button, MenuBehaviorMode.Puppet };
            var choices = new List<string> { T("modeToggle"), T("modeRadial"), T("modeButton"), T("modePuppet") };
            var type = UiTextFactory.CreatePopupField("", choices, Array.IndexOf(modes, mode));
            type.name = "menuBehaviorMode";
            type.tooltip = T("mode");
            type.SetEnabled(owned && canEdit);
            type.RegisterValueChangedCallback(evt =>
            {
                type.SetValueWithoutNotify(evt.previousValue);
                var nextMode = modes[choices.IndexOf(evt.newValue)];
                if (nextMode != ExpressionMenuAnimationRecipe.EffectiveMode(_item)) ConfirmModeChange(nextMode);
            });
            settings.Add(type);
            if (mode == MenuBehaviorMode.Puppet)
            {
                var layouts = new List<string> { "2 Axis", "4 Axis" };
                var layout = UiTextFactory.CreatePopupField("", layouts, ExpressionMenuAnimationRecipe.AxisCount(_item) == 4 ? 1 : 0);
                layout.name = "puppetLayout";
                layout.tooltip = T("puppetLayout");
                layout.SetEnabled(owned && canEdit);
                layout.RegisterValueChangedCallback(evt =>
                {
                    layout.SetValueWithoutNotify(evt.previousValue);
                    var axes = layouts.IndexOf(evt.newValue) == 1 ? 4 : 2;
                    if (axes != ExpressionMenuAnimationRecipe.AxisCount(_item)) ConfirmModeChange(MenuBehaviorMode.Puppet, axes);
                });
                settings.Add(layout);
            }
            if (owned)
            {
                if (mode == MenuBehaviorMode.Radial)
                {
                    var initial = UiTextFactory.CreateFloatField(T("initialPercent"));
                    initial.isDelayed = true;
                    initial.SetValueWithoutNotify(recipe.InitialValue);
                    initial.SetEnabled(canEdit);
                    initial.RegisterValueChangedCallback(evt => Run(() =>
                    {
                        try
                        {
                            ExpressionMenuAnimationRecipe.ValidatePercent(evt.newValue);
                            ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.InitialValue = evt.newValue);
                        }
                        catch { initial.SetValueWithoutNotify(evt.previousValue); throw; }
                    }));
                    settings.Add(initial);
                }
                else if (mode == MenuBehaviorMode.Toggle)
                {
                    var initial = UiTextFactory.CreateToggle(T("initialEnabled"));
                    initial.SetValueWithoutNotify(_item.isDefault);
                    initial.SetEnabled(canEdit);
                    initial.RegisterValueChangedCallback(evt => Edit(_item, () => _item.isDefault = evt.newValue));
                    settings.Add(initial);
                }
            }
            var actionsHeader = new SectionHeader(T("actionsSection"));
            _fields.Add(actionsHeader);
            foreach (var effect in ExpressionMenuTemplateModel.Effects(_item))
                BuildEffect(_fields, effect);
            if (recipe != null)
            {
                for (var index = 0; index < recipe.ReactiveActions.Count; index++) BuildReactive(_fields, recipe, index);
                for (var index = 0; index < recipe.Actions.Count; index++) BuildAnimation(_fields, recipe, index);
                for (var index = 0; index < recipe.RadialShapes.Count; index++) BuildRadialShapes(_fields, recipe, index);
                for (var index = 0; index < recipe.ParameterActions.Count; index++) BuildParameter(_fields, recipe, index);
                for (var index = 0; index < recipe.MaterialValues.Count; index++) BuildMaterialValues(_fields, recipe, index);
                for (var index = 0; index < recipe.Transforms.Count; index++) BuildTransforms(_fields, recipe, index);
                for (var index = 0; index < recipe.Components.Count; index++) BuildComponents(_fields, recipe, index);
            }
            if (!owned) return;
            var availableActions = ExpressionMenuTemplateModel.AvailableActions(_item);
            var add = new UiButton(T("addAction"), () =>
            {
                var choices = new GenericMenu();
                foreach (var kind in availableActions)
                {
                    var template = kind;
                    choices.AddItem(UiTextFactory.CreateGuiContent(T("kind" + template)), false, () => Run(() =>
                    {
                        ExpressionMenuTemplateModel.AddAction(_context, _item, template);
                        Refresh();
                    }));
                }
                choices.ShowAsContext();
            }, icon: FluentUiIcons.CreateState("add.png"), variant: UiButtonVariant.Ghost);
            add.SetEnabled(canEdit && availableActions.Length > 0);
            actionsHeader.Actions.Add(add);
        }

        private void ConfirmModeChange(MenuBehaviorMode mode, int puppetAxes = 2)
        {
            if (_modeOverlay != null) return;
            if (!ExpressionMenuAnimationRecipe.HasSettingsToDiscard(_item))
            {
                Run(() =>
                {
                    ExpressionMenuAnimationRecipe.SetModeDiscardingSettings(_context, _item, mode, puppetAxes);
                    _typeChanged();
                });
                return;
            }
            var host = (VisualElement)this;
            while (host.parent != null && host.parent != panel?.visualTree) host = host.parent;
            var overlay = new ConfirmationOverlay(host,
                new MessagePanelState(T("discardSettingsTitle"), T("discardSettingsMessage"), MessageSeverity.Warning));
            _modeOverlay = overlay;
            overlay.Closed += () => _modeOverlay = null;
            overlay.AddDiscardAction(T("discardSettings"), () =>
            {
                try { ExpressionMenuAnimationRecipe.SetModeDiscardingSettings(_context, _item, mode, puppetAxes); }
                catch (Exception exception)
                {
                    overlay.SetState(new MessagePanelState(exception.GetBaseException().Message,
                        severity: MessageSeverity.Error));
                    return;
                }
                overlay.Close();
                Run(_typeChanged);
            });
            overlay.Notification.Actions.Add(new UiButton(T("continueEditing"), overlay.Close));
        }

        private VisualElement BuildIdentity(bool canEdit)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-menu-template__identity");
            _fields.Add(row);
            var iconHost = new VisualElement();
            iconHost.AddToClassList("ee4v-menu-template__icon-host");
            row.Add(iconHost);
            var preview = new Image { image = ExpressionMenuModel.DisplayIcon(_item.Control.icon), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            preview.AddToClassList("ee4v-menu-template__icon-preview");
            iconHost.Add(preview);
            var icon = UiTextFactory.CreateObjectField("");
            icon.objectType = typeof(Texture2D);
            icon.allowSceneObjects = false;
            icon.tooltip = I18N.Get("expressionMenu.icon");
            icon.AddToClassList("ee4v-menu-template__icon");
            icon.SetValueWithoutNotify(_item.Control.icon);
            icon.RegisterValueChangedCallback(evt => Run(() =>
            {
                try
                {
                    ExpressionMenuTemplateModel.Edit(_context, _item, () => _item.Control.icon = evt.newValue as Texture2D);
                    _typeChanged();
                }
                catch { icon.SetValueWithoutNotify(evt.previousValue); throw; }
            }));
            iconHost.Add(icon);
            iconHost.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (!icon.enabledInHierarchy || !DragAndDrop.objectReferences.OfType<Texture2D>().Any()) return;
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                evt.StopPropagation();
            });
            iconHost.RegisterCallback<DragPerformEvent>(evt =>
            {
                var texture = DragAndDrop.objectReferences.OfType<Texture2D>().FirstOrDefault();
                if (!icon.enabledInHierarchy || texture == null) return;
                DragAndDrop.AcceptDrag();
                icon.value = texture;
                evt.StopPropagation();
            });
            var details = new VisualElement();
            details.AddToClassList("ee4v-menu-template__details");
            row.Add(details);
            var name = new InputField(new InputFieldState(string.IsNullOrEmpty(_item.label) ? _item.gameObject.name : _item.label)) { IsDelayed = true };
            name.AddToClassList("ee4v-menu-template__name");
            name.tooltip = I18N.Get("expressionMenu.name");
            name.ValueChanged += value => Run(() =>
            {
                ExpressionMenuTemplateModel.Edit(_context, _item, () => _item.label = value);
                _typeChanged();
            });
            details.Add(name);
            iconHost.SetEnabled(canEdit);
            name.SetEnabled(canEdit);
            return details;
        }

        private void AddSync(VisualElement parent, bool synced, Action<bool> change, bool canEdit)
        {
            var field = UiTextFactory.CreateToggle(T("syncPlayers"));
            field.tooltip = T("syncHint");
            field.SetValueWithoutNotify(synced);
            field.SetEnabled(canEdit);
            field.RegisterValueChangedCallback(evt => Run(() =>
            {
                try { change(evt.newValue); }
                catch { field.SetValueWithoutNotify(evt.previousValue); Refresh(); throw; }
            }));
            parent.Add(field);
        }

        private void BuildReactive(VisualElement parent, ExpressionMenuAnimationRecipe recipe, int index)
        {
            var action = recipe.ReactiveActions[index];
            var card = new VisualElement();
            card.AddToClassList("ee4v-menu-template__change");
            parent.Add(card);
            var header = new SectionHeader(T("new" + action.Kind));
            header.Actions.Add(new UiButton(T("removeAction"), () => Run(() =>
            {
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.ReactiveActions.RemoveAt(index));
                Refresh();
            }), variant: UiButtonVariant.Ghost));
            card.Add(header);
            AddSync(card, action.Synced, value =>
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.ReactiveActions[index].Synced = value),
                ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
            if (action.Kind == MenuTemplateKind.ObjectToggle) BuildToggle(card, null, action);
            else if (action.Kind == MenuTemplateKind.MaterialSwap) BuildSwap(card, null, action);
            else BuildShapes(card, null, action);
            card.SetEnabled(ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
        }

        private void BuildEffect(VisualElement parent, ReactiveComponent effect)
        {
            var card = new VisualElement();
            card.AddToClassList("ee4v-menu-template__change");
            parent.Add(card);
            var header = new SectionHeader(T(effect is ModularAvatarObjectToggle ? "newObjectToggle" :
                effect is ModularAvatarMaterialSwap ? "newMaterialSwap" : "newShapeChanger"));
            if (ExpressionMenuTemplateModel.IsOwned(_item.gameObject))
                header.Actions.Add(new UiButton(T("removeAction"), () => Run(() =>
                {
                    ExpressionMenuTemplateModel.RemoveAction(_context, _item, effect);
                    Refresh();
                }), variant: UiButtonVariant.Ghost));
            card.Add(header);
            var owned = ExpressionMenuTemplateModel.IsOwned(_item.gameObject);
            AddSync(card, _item.isSynced, value =>
            {
                ExpressionMenuAnimationRecipe.SetEffectSynced(_context, _item, effect, value);
                Refresh();
            }, owned && ExpressionMenuTemplateModel.CanEdit(_context, effect));
            if (effect is ModularAvatarObjectToggle toggle) BuildToggle(card, toggle);
            else if (effect is ModularAvatarMaterialSwap swap) BuildSwap(card, swap);
            else if (effect is ModularAvatarShapeChanger changer) BuildShapes(card, changer);
            card.SetEnabled(ExpressionMenuTemplateModel.CanEdit(_context, _item) && ExpressionMenuTemplateModel.CanEdit(_context, effect));
        }

        private void BuildAxisSelection(VisualElement parent, int axis, Action<int> change)
        {
            if (ExpressionMenuAnimationRecipe.EffectiveMode(_item) != MenuBehaviorMode.Puppet) return;
            var choices = ExpressionMenuAnimationRecipe.AxisNames(_item).Select(name => T("axis" + name)).ToList();
            var field = UiTextFactory.CreatePopupField(T("puppetAxis"), choices, axis);
            field.name = "puppetAxis";
            field.RegisterValueChangedCallback(evt => Run(() =>
            {
                try { change(choices.IndexOf(evt.newValue)); }
                catch { field.SetValueWithoutNotify(evt.previousValue); Refresh(); throw; }
            }));
            parent.Add(field);
        }

        private void BuildParameter(VisualElement parent, ExpressionMenuAnimationRecipe recipe, int index)
        {
            var action = recipe.ParameterActions[index];
            var card = new VisualElement { name = "parameterAction" };
            card.AddToClassList("ee4v-menu-template__change");
            parent.Add(card);
            var header = new SectionHeader(T("kindParameterValue"));
            header.Actions.Add(new UiButton(T("removeAction"), () => Run(() =>
            {
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.ParameterActions.RemoveAt(index));
                Refresh();
            }), variant: UiButtonVariant.Ghost));
            card.Add(header);
            AddSync(card, action.Synced, value =>
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.ParameterActions[index].Synced = value),
                ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
            BuildAxisSelection(card, action.Axis, value =>
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.ParameterActions[index].Axis = value));
            var options = recipe.ParameterChoices(_context, _item);
            var parameter = new InputField(new InputFieldState(action.Parameter)) { name = "parameterName", IsDelayed = true };
            void SelectParameter(string name)
            {
                try
                {
                    ExpressionMenuAnimationRecipe.Change(_context, _item, () =>
                    {
                        action.Parameter = name;
                        var known = options.FirstOrDefault(entry => entry.Name == name);
                        if (ExpressionMenuAnimationRecipe.EffectiveMode(_item) == MenuBehaviorMode.Button)
                        {
                            var buttonType = known?.Type ?? action.ButtonType;
                            if (action.ButtonType != buttonType)
                            {
                                action.ButtonType = buttonType;
                                action.On = buttonType == AnimatorControllerParameterType.Bool ? 0 : 1;
                            }
                            return;
                        }
                        var type = known != null && known.Type == AnimatorControllerParameterType.Int ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Float;
                        if (action.NumericType != type)
                        {
                            action.NumericType = type;
                            action.Off = 0;
                            action.On = 1;
                        }
                    });
                }
                catch { Refresh(); throw; }
                Refresh();
            }
            parameter.ValueChanged += value => Run(() =>
            {
                try { SelectParameter(value); }
                catch { parameter.SetValueWithoutNotify(action.Parameter); throw; }
            });
            UiButton select = null;
            if (options.Length > 0)
                select = new UiButton(T("chooseParameter"), () =>
                {
                    var menu = new GenericMenu();
                    foreach (var entry in options)
                    {
                        var option = entry;
                        menu.AddItem(UiTextFactory.CreateGuiContent(option.Name + " (" + option.Type + ")"), action.Parameter == option.Name,
                            () => Run(() => SelectParameter(option.Name)));
                    }
                    menu.ShowAsContext();
                }, variant: UiButtonVariant.Ghost);
            var trigger = ExpressionMenuAnimationRecipe.EffectiveMode(_item) == MenuBehaviorMode.Button;
            card.Add(new FormInput(T("parameterName"), parameter, select));
            if (trigger)
            {
                BuildButtonParameter(card, recipe, index, options.FirstOrDefault(entry => entry.Name == action.Parameter));
                card.SetEnabled(ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
                return;
            }
            var radial = ExpressionMenuAnimationRecipe.IsContinuous(_item);
            var values = new VisualElement { name = "parameterValues" };
            if (radial) values.AddToClassList("ee4v-menu-template__range");
            card.Add(new FormInput(T(radial ? "valueRange" : "parameterValues"), values));
            var transition = !radial ? AddTransition(values, T("parameterWhenOff"), T("parameterWhenOn")) : default;
            void SetEndpoint(bool on, float value) => ExpressionMenuAnimationRecipe.Change(_context, _item, () =>
            {
                if (on) action.On = value;
                else action.Off = value;
            });
            void Endpoint(bool on)
            {
                var hint = T(radial ? (on ? "parameterAtHundred" : ExpressionMenuAnimationRecipe.SourceMinimum(_item) < 0 ?
                    "parameterAtNegative" : "parameterAtZero") : (on ? "onValue" : "offValue"));
                var value = on ? action.On : action.Off;
                if (!radial)
                {
                    var choices = new List<string> { "False", "True" };
                    var field = UiTextFactory.CreatePopupField("", choices, value != 0 ? 1 : 0);
                    field.name = on ? "parameterOnValue" : "parameterOffValue";
                    field.tooltip = hint;
                    field.SetValueWithoutNotify(value != 0 ? "True" : "False");
                    field.RegisterValueChangedCallback(evt => Run(() =>
                    {
                        try { SetEndpoint(on, evt.newValue == "True" ? 1 : 0); }
                        catch { field.SetValueWithoutNotify(evt.previousValue); Refresh(); throw; }
                    }));
                    (on ? transition.After : transition.Before).Add(field);
                }
                else if (action.NumericType == AnimatorControllerParameterType.Int)
                {
                    var field = UiTextFactory.CreateIntegerField("", "ee4v-menu-template__range-value");
                    field.tooltip = hint;
                    field.isDelayed = true;
                    field.SetValueWithoutNotify((int)value);
                    field.RegisterValueChangedCallback(evt => Run(() =>
                    {
                        try { SetEndpoint(on, evt.newValue); }
                        catch { field.SetValueWithoutNotify(evt.previousValue); Refresh(); throw; }
                    }));
                    values.Add(field);
                }
                else
                {
                    var field = UiTextFactory.CreateFloatField("", "ee4v-menu-template__range-value");
                    field.tooltip = hint;
                    field.isDelayed = true;
                    field.SetValueWithoutNotify(value);
                    field.RegisterValueChangedCallback(evt => Run(() =>
                    {
                        try { SetEndpoint(on, evt.newValue); }
                        catch { field.SetValueWithoutNotify(evt.previousValue); Refresh(); throw; }
                    }));
                    values.Add(field);
                }
            }
            Endpoint(false);
            if (radial) values.Add(UiTextFactory.Create("〜", "ee4v-menu-template__range-separator"));
            Endpoint(true);
            card.SetEnabled(ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
        }

        private void BuildAnimation(VisualElement parent, ExpressionMenuAnimationRecipe recipe, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("ee4v-menu-template__change");
            parent.Add(card);
            var header = new SectionHeader(T("newAnimationClip"));
            header.Actions.Add(new UiButton(T("removeAction"), () => Run(() =>
            {
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.Actions.RemoveAt(index));
                Refresh();
            }), variant: UiButtonVariant.Ghost));
            card.Add(header);
            AddSync(card, recipe.Actions[index].Synced, value =>
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.Actions[index].Synced = value),
                ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
            var radial = ExpressionMenuAnimationRecipe.IsContinuous(_item);
            AddObject<AnimationClip>(card, T(radial ? "radialClip" : "onClip"), recipe.Actions[index].On, false, value =>
                ExpressionMenuAnimationRecipe.SetClip(_context, _item, index, value, true));
            if (!radial) AddObject<AnimationClip>(card, T("offClip"), recipe.Actions[index].Off, false, value =>
                ExpressionMenuAnimationRecipe.SetClip(_context, _item, index, value, false));
            card.SetEnabled(ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
        }

        private void BuildRadialShapes(VisualElement parent, ExpressionMenuAnimationRecipe recipe, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("ee4v-menu-template__change");
            parent.Add(card);
            var header = new SectionHeader(T("kindShapeChanger"));
            header.Actions.Add(new UiButton(T("removeAction"), () => Run(() =>
            {
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.RadialShapes.RemoveAt(index));
                Refresh();
            }), variant: UiButtonVariant.Ghost));
            card.Add(header);
            AddSync(card, recipe.RadialShapes[index].Synced, value =>
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.RadialShapes[index].Synced = value),
                ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
            BuildAxisSelection(card, recipe.RadialShapes[index].Axis, value =>
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.RadialShapes[index].Axis = value));
            for (var rowIndex = 0; rowIndex < recipe.RadialShapes[index].Targets.Count; rowIndex++)
            {
                var targetIndex = rowIndex;
                MenuRadialShapeTarget Target() => recipe.RadialShapes[index].Targets[targetIndex];
                var row = Entry(card, () =>
                {
                    ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.RadialShapes[index].Targets.RemoveAt(targetIndex));
                    Refresh();
                });
                var renderer = ExpressionMenuAnimationRecipe.ResolveRenderer(_context, Target());
                BuildShapeSelection(row, renderer, Target().Shape, Target().Path != null, value =>
                {
                    if (value != null) ExpressionMenuTemplateModel.Reference(_context, value.gameObject);
                    ExpressionMenuAnimationRecipe.Change(_context, _item, () =>
                    {
                        Target().Path = value == null ? null : AnimationUtility.CalculateTransformPath(value.transform, _context.Root.transform);
                        Target().Shape = value != null && value.sharedMesh != null && value.sharedMesh.blendShapeCount > 0 ?
                            value.sharedMesh.GetBlendShapeName(0) : null;
                    });
                    Refresh();
                }, value => ExpressionMenuAnimationRecipe.Change(_context, _item, () => Target().Shape = value));
                var range = new VisualElement { name = "blendShapeRange" };
                range.AddToClassList("ee4v-menu-template__range");
                var rangeRow = new FormInput(T("valueRange"), range);
                rangeRow.AddToClassList("ee4v-menu-template__range-row");
                row.Add(rangeRow);
                void AddEndpoint(string hint, float current, Action<float> change)
                {
                    var field = UiTextFactory.CreateFloatField("", "ee4v-menu-template__range-value");
                    field.name = hint;
                    field.tooltip = T(hint);
                    field.isDelayed = true;
                    field.SetValueWithoutNotify(current);
                    field.RegisterValueChangedCallback(evt => Run(() =>
                    {
                        try
                        {
                            ExpressionMenuAnimationRecipe.ValidatePercent(evt.newValue);
                            ExpressionMenuAnimationRecipe.Change(_context, _item, () => change(evt.newValue));
                        }
                        catch { field.SetValueWithoutNotify(evt.previousValue); throw; }
                    }));
                    range.Add(field);
                }
                AddEndpoint(ExpressionMenuAnimationRecipe.SourceMinimum(_item) < 0 ? "atNegative" : "atZero", Target().Minimum, value => Target().Minimum = value);
                range.Add(UiTextFactory.Create("〜", "ee4v-menu-template__range-separator"));
                AddEndpoint("atHundred", Target().Maximum, value => Target().Maximum = value);
            }
            card.Add(new UiButton(T("addTarget"), () => Run(() =>
            {
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.RadialShapes[index].Targets.Add(new MenuRadialShapeTarget()));
                Refresh();
            }), variant: UiButtonVariant.Ghost));
            card.SetEnabled(ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
        }


        private void BuildToggle(VisualElement card, ModularAvatarObjectToggle effect, MenuReactiveAction action = null)
        {
            List<ToggledObject> Objects() => action?.Objects ?? effect.Objects;
            var owner = (Component)effect ?? _item;
            var inverted = action?.Inverted ?? effect.Inverted;
            for (var i = 0; i < Objects().Count; i++)
            {
                var index = i;
                var entry = Objects()[i];
                var row = Entry(card, () => EditEffect(effect, action, () => Objects().RemoveAt(index), rebuild: true));
                AddObject<GameObject>(row, T("target"), entry.Object?.Get(owner), true, target =>
                {
                    var reference = ExpressionMenuTemplateModel.Reference(_context, target, false);
                    EditEffect(effect, action, () => { var obj = Objects()[index]; obj.Object = reference; Objects()[index] = obj; });
                });
                Missing(row, entry.Object, owner);
                var initial = UiTextFactory.CreateToggle(T("initialOn"));
                initial.SetValueWithoutNotify(entry.Active != inverted);
                initial.RegisterValueChangedCallback(evt => EditEffect(effect, action, () =>
                {
                    var obj = Objects()[index];
                    obj.Active = evt.newValue != inverted;
                    Objects()[index] = obj;
                }));
                row.Add(initial);
            }
            card.Add(new UiButton(T("addTarget"), () => EditEffect(effect, action,
                () => Objects().Add(new ToggledObject { Object = new AvatarObjectReference(), Active = !inverted }), rebuild: true), variant: UiButtonVariant.Ghost));
        }

        private void BuildSwap(VisualElement card, ModularAvatarMaterialSwap effect, MenuReactiveAction action = null)
        {
            List<MatSwap> Swaps() => action?.Swaps ?? effect.Swaps;
            for (var i = 0; i < Swaps().Count; i++)
            {
                var index = i;
                var entry = Swaps()[i];
                var row = Entry(card, () => EditEffect(effect, action, () => Swaps().RemoveAt(index), rebuild: true));
                var transition = AddTransition(row, T("fromMaterial"), T("toMaterial"));
                AddObject<Material>(transition.Before, "", entry.From, false, value => ChangeEffect(effect, action, () =>
                { var obj = Swaps()[index]; obj.From = value; Swaps()[index] = obj; }));
                AddObject<Material>(transition.After, "", entry.To, false, value => ChangeEffect(effect, action, () =>
                { var obj = Swaps()[index]; obj.To = value; Swaps()[index] = obj; }));
            }
            card.Add(new UiButton(T("addSwap"), () => EditEffect(effect, action,
                () => Swaps().Add(new MatSwap()), rebuild: true), variant: UiButtonVariant.Ghost));
        }


        private void BuildShapes(VisualElement card, ModularAvatarShapeChanger effect, MenuReactiveAction action = null)
        {
            List<ChangedShape> Shapes() => action?.Shapes ?? effect.Shapes;
            var owner = (Component)effect ?? _item;
            for (var index = 0; index < Shapes().Count; index++)
            {
                var targetIndex = index;
                ChangedShape Target() => Shapes()[targetIndex];
                var row = Entry(card, () => EditEffect(effect, action, () => Shapes().RemoveAt(targetIndex), rebuild: true));
                var renderer = Target().Object?.Get(owner)?.GetComponent<SkinnedMeshRenderer>();
                if (renderer == null) renderer = null;
                FloatField off = null;
                void RenderOff()
                {
                    var mesh = renderer != null ? renderer.sharedMesh : null;
                    var shapeIndex = mesh != null && !string.IsNullOrEmpty(Target().ShapeName) ?
                        mesh.GetBlendShapeIndex(Target().ShapeName) : -1;
                    off.SetValueWithoutNotify(shapeIndex >= 0 ? renderer.GetBlendShapeWeight(shapeIndex) : 0);
                }
                BuildShapeSelection(row, renderer, Target().ShapeName,
                    !string.IsNullOrEmpty(Target().Object?.referencePath), value =>
                {
                    var reference = ExpressionMenuTemplateModel.Reference(_context, value == null ? null : value.gameObject);
                    ChangeEffect(effect, action, () =>
                    {
                        Target().Object = reference;
                        Target().ShapeName = value != null && value.sharedMesh != null && value.sharedMesh.blendShapeCount > 0 ?
                            value.sharedMesh.GetBlendShapeName(0) : null;
                    }, rebuild: true);
                }, value =>
                {
                    ChangeEffect(effect, action, () => Target().ShapeName = value);
                    RenderOff();
                });
                Missing(row, Target().Object, owner);
                var transition = AddTransition(row, T("offValue"), T("onValue"));
                off = UiTextFactory.CreateFloatField("");
                off.name = "offValue";
                off.tooltip = T("currentValue");
                off.isReadOnly = true;
                off.SetEnabled(false);
                transition.Before.Add(off);
                RenderOff();
                var weight = UiTextFactory.CreateFloatField("");
                weight.name = "onValue";
                weight.tooltip = T("value");
                weight.isDelayed = true;
                weight.SetValueWithoutNotify(Target().Value);
                weight.RegisterValueChangedCallback(evt => Run(() =>
                {
                    try
                    {
                        ExpressionMenuAnimationRecipe.ValidatePercent(evt.newValue);
                        ChangeEffect(effect, action, () => Target().Value = evt.newValue);
                    }
                    catch { weight.SetValueWithoutNotify(evt.previousValue); throw; }
                }));
                transition.After.Add(weight);
            }
            card.Add(new UiButton(T("addTarget"), () => EditEffect(effect, action, () => Shapes().Add(new ChangedShape
            {
                Object = new AvatarObjectReference(), ChangeType = ShapeChangeType.Set, Value = 100
            }), rebuild: true), variant: UiButtonVariant.Ghost));
        }

        private void BuildShapeSelection(VisualElement row, SkinnedMeshRenderer renderer, string shape, bool hasTarget,
            Action<SkinnedMeshRenderer> changeRenderer, Action<string> changeShape)
        {
            AddObject(row, T("targetMesh"), renderer, true, changeRenderer);
            var shapes = new List<string> { T("selectShape") };
            if (renderer != null && renderer.sharedMesh != null)
                shapes.AddRange(Enumerable.Range(0, renderer.sharedMesh.blendShapeCount).Select(renderer.sharedMesh.GetBlendShapeName));
            var selection = UiTextFactory.CreatePopupField(T("shape"), shapes, Math.Max(0, shapes.IndexOf(shape)));
            selection.SetEnabled(shapes.Count > 1);
            selection.RegisterValueChangedCallback(evt => Run(() =>
            {
                try { changeShape(evt.newValue == shapes[0] ? null : evt.newValue); }
                catch { selection.SetValueWithoutNotify(evt.previousValue); throw; }
            }));
            row.Add(selection);
            if (hasTarget && (renderer == null || !string.IsNullOrEmpty(shape) && !shapes.Contains(shape)))
                row.Add(UiTextFactory.CreateHelpBox(T("missingShape"), HelpBoxMessageType.Error));
        }

        private static (VisualElement Before, VisualElement After) AddTransition(VisualElement parent, string before, string after)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-menu-template__transition");
            parent.Add(row);
            VisualElement Column(string label)
            {
                var column = new VisualElement { tooltip = label };
                column.AddToClassList("ee4v-menu-template__transition-column");
                column.Add(UiTextFactory.Create(label, UiClassNames.SecondaryText));
                row.Add(column);
                return column;
            }
            var left = Column(before);
            row.Add(UiTextFactory.Create("→", "ee4v-menu-template__transition-arrow"));
            return (left, Column(after));
        }

        private void EditEffect(ReactiveComponent effect, MenuReactiveAction action, Action change, bool rebuild = false) =>
            Run(() => ChangeEffect(effect, action, change, rebuild));

        private void ChangeEffect(ReactiveComponent effect, MenuReactiveAction action, Action change, bool rebuild = false)
        {
            if (action == null) ExpressionMenuTemplateModel.Edit(_context, effect, change);
            else
            {
                try { ExpressionMenuAnimationRecipe.Change(_context, _item, change); }
                catch { Refresh(); throw; }
            }
            if (rebuild) Refresh();
        }

        private void Missing(VisualElement parent, AvatarObjectReference reference, Component effect)
        {
            if (reference != null && !string.IsNullOrEmpty(reference.referencePath) && reference.Get(effect) == null)
                parent.Add(UiTextFactory.CreateHelpBox(T("missingTarget") + " " + reference.referencePath, HelpBoxMessageType.Error));
        }

        private VisualElement Entry(VisualElement parent, Action remove)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-menu-template__entry");
            parent.Add(row);
            row.Add(new UiButton(T("removeRow"), () => Run(remove), variant: UiButtonVariant.Ghost));
            return row;
        }


        private void Edit(Object target, Action change, bool rebuild = false) => Run(() =>
        {
            ExpressionMenuTemplateModel.Edit(_context, target, change);
            if (rebuild) Refresh();
        });

        private void Run(Action action)
        {
            try { _feedback.AddToClassList("ee4v-menu-template__hidden"); action(); }
            catch (Exception exception)
            {
                UiTextFactory.SetText(_feedback, exception.GetBaseException().Message);
                _feedback.RemoveFromClassList("ee4v-menu-template__hidden");
            }
        }

        private void AddObject<T>(VisualElement host, string label, T value, bool scene, Action<T> changed) where T : Object
        {
            var field = UiTextFactory.CreateObjectField(label);
            field.objectType = typeof(T);
            field.allowSceneObjects = scene;
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(evt => Run(() =>
            {
                try { changed(evt.newValue as T); }
                catch { field.SetValueWithoutNotify(evt.previousValue); throw; }
            }));
            host.Add(field);
        }


    }
}
