using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.I18n;
using Ee4v.Core.EditorIntegration;
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

    internal sealed class ExpressionMenuTemplateEditor : VisualElement
    {
        private readonly AvatarEditingContext _context;
        private readonly ModularAvatarMenuItem _item;
        private readonly VisualElement _fields;
        private readonly HelpBox _feedback;
        private readonly Action _typeChanged;
        private readonly HashSet<string> _collapsedShapeGroups = new HashSet<string>(StringComparer.Ordinal);
        private static string T(string key) => TemplateText.Get(key);

        internal ExpressionMenuTemplateEditor(AvatarEditingContext context, ModularAvatarMenuItem item, Action typeChanged)
        {
            _context = context;
            _item = item;
            _typeChanged = typeChanged;
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
            settings.Add(new SectionHeader(T("behaviorSection")));
            var choices = new List<string> { T("modeUnselected"), T("modeToggle"), T("modeRadial") };
            var type = UiTextFactory.CreatePopupField(T("mode"), choices, (int)mode);
            type.SetEnabled(owned && canEdit);
            type.RegisterValueChangedCallback(evt => Run(() =>
            {
                try
                {
                    ExpressionMenuAnimationRecipe.SetMode(_context, _item, (MenuBehaviorMode)choices.IndexOf(evt.newValue));
                    _typeChanged();
                }
                catch { type.SetValueWithoutNotify(evt.previousValue); throw; }
            }));
            settings.Add(type);
            settings.Add(UiTextFactory.Create(T(mode == MenuBehaviorMode.Radial ? "radialHint" :
                mode == MenuBehaviorMode.Toggle ? "toggleHint" : "selectModeFirst"), UiClassNames.SecondaryText));
            if (mode == MenuBehaviorMode.Unselected) return;
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
                else
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
            }
            if (!owned) return;
            if (ExpressionMenuTemplateModel.Effects(_item).Length == 0 && (recipe == null || recipe.Actions.Count + recipe.RadialShapes.Count + recipe.ReactiveActions.Count == 0))
                _fields.Add(UiTextFactory.Create(T("emptyActions"), UiClassNames.SecondaryText));
            var add = new UiButton(T("addAction"), () =>
            {
                var choices = new GenericMenu();
                foreach (var kind in ExpressionMenuTemplateModel.AvailableActions(_item))
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
            add.SetEnabled(canEdit);
            actionsHeader.Actions.Add(add);
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
            var radial = ExpressionMenuAnimationRecipe.EffectiveMode(_item) == MenuBehaviorMode.Radial;
            AddObject<AnimationClip>(card, T(radial ? "radialClip" : "onClip"), recipe.Actions[index].On, false, value =>
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.Actions[index].On = value));
            if (!radial) AddObject<AnimationClip>(card, T("offClip"), recipe.Actions[index].Off, false, value =>
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.Actions[index].Off = value));
            card.Add(UiTextFactory.Create(T(radial ? "radialClipHint" : "clipHint"), UiClassNames.SecondaryText));
            card.SetEnabled(ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
        }

        private void BuildRadialShapes(VisualElement parent, ExpressionMenuAnimationRecipe recipe, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("ee4v-menu-template__change");
            parent.Add(card);
            var header = new SectionHeader(T("newShapeChanger"));
            header.Actions.Add(new UiButton(T("removeAction"), () => Run(() =>
            {
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.RadialShapes.RemoveAt(index));
                Refresh();
            }), variant: UiButtonVariant.Ghost));
            card.Add(header);
            AddSync(card, recipe.RadialShapes[index].Synced, value =>
                ExpressionMenuAnimationRecipe.Change(_context, _item, () => recipe.RadialShapes[index].Synced = value),
                ExpressionMenuAnimationRecipe.CanEdit(_context, _item));
            card.Add(UiTextFactory.Create(T("radialShapeHint"), UiClassNames.SecondaryText));
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
                AddObject<SkinnedMeshRenderer>(row, T("targetMesh"), renderer, true, value =>
                {
                    if (value != null) ExpressionMenuTemplateModel.Reference(_context, value.gameObject);
                    ExpressionMenuAnimationRecipe.Change(_context, _item, () =>
                    {
                        Target().Path = value == null ? null : AnimationUtility.CalculateTransformPath(value.transform, _context.Root.transform);
                        Target().Shape = value?.sharedMesh != null && value.sharedMesh.blendShapeCount > 0 ?
                            value.sharedMesh.GetBlendShapeName(0) : null;
                    });
                    Refresh();
                });
                var shapes = new List<string> { T("selectShape") };
                if (renderer?.sharedMesh != null)
                    shapes.AddRange(Enumerable.Range(0, renderer.sharedMesh.blendShapeCount).Select(renderer.sharedMesh.GetBlendShapeName));
                var selection = UiTextFactory.CreatePopupField(T("shape"), shapes, Math.Max(0, shapes.IndexOf(Target().Shape)));
                selection.SetEnabled(shapes.Count > 1);
                selection.RegisterValueChangedCallback(evt => Run(() =>
                {
                    try
                    {
                        ExpressionMenuAnimationRecipe.Change(_context, _item, () => Target().Shape =
                            evt.newValue == shapes[0] ? null : evt.newValue);
                    }
                    catch { selection.SetValueWithoutNotify(evt.previousValue); throw; }
                }));
                row.Add(selection);
                if (Target().Path != null && (renderer == null || !string.IsNullOrEmpty(Target().Shape) && !shapes.Contains(Target().Shape)))
                    row.Add(UiTextFactory.CreateHelpBox(T("missingShape"), HelpBoxMessageType.Error));
                void AddEndpoint(string label, float current, Action<float> change)
                {
                    var field = UiTextFactory.CreateFloatField(T(label));
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
                    row.Add(field);
                }
                AddEndpoint("atZero", Target().Minimum, value => Target().Minimum = value);
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
                AddObject<Material>(row, T("fromMaterial"), entry.From, false, value => EditEffect(effect, action, () =>
                { var obj = Swaps()[index]; obj.From = value; Swaps()[index] = obj; }));
                AddObject<Material>(row, T("toMaterial"), entry.To, false, value => EditEffect(effect, action, () =>
                { var obj = Swaps()[index]; obj.To = value; Swaps()[index] = obj; }));
            }
            card.Add(new UiButton(T("addSwap"), () => EditEffect(effect, action,
                () => Swaps().Add(new MatSwap()), rebuild: true), variant: UiButtonVariant.Ghost));
        }


        private sealed class ShapeTarget
        {
            internal SkinnedMeshRenderer Renderer;
            internal string Name;
            internal string Path;
            internal string Category;
            internal string Group;
            internal string FavoriteKey;
            internal string Key => Path + "\0" + Name;
            internal string DisplayName => Name.Contains("/") ? Name.Substring(Name.IndexOf('/') + 1).Trim() : Name;
        }

        private void BuildShapes(VisualElement card, ModularAvatarShapeChanger effect, MenuReactiveAction action = null)
        {
            List<ChangedShape> Shapes() => action?.Shapes ?? effect.Shapes;
            var owner = (Component)effect ?? _item;
            var renderers = _context.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0).ToArray();
            var naming = _context.Host.CreateShapeNaming(_context.Root, renderers.Select(renderer => renderer.sharedMesh));
            var targets = new List<ShapeTarget>();
            foreach (var renderer in renderers)
            {
                var mesh = renderer.sharedMesh;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long meshId);
                for (var index = 0; index < mesh.blendShapeCount; index++)
                {
                    var name = mesh.GetBlendShapeName(index);
                    if (naming.IsHeader(name)) continue;
                    naming.TryGetMapping(guid, meshId, name, out var mapping);
                    var category = Enum.TryParse(mapping.AppearancePart, true, out BodyPartCategory part) ? part :
                        AvatarBodyAnalysis.ClassifyBodyPart(name, renderer.name);
                    targets.Add(new ShapeTarget
                    {
                        Renderer = renderer, Name = name,
                        Path = AnimationUtility.CalculateTransformPath(renderer.transform, _context.Root.transform),
                        Category = mapping.AppearancePart == "expression" ? T("expressionShapes") :
                            T("shapePart" + category),
                        Group = string.IsNullOrWhiteSpace(mapping.AppearanceGroup) ? mapping.Role?.Trim() ?? "" : mapping.AppearanceGroup.Trim(),
                        FavoriteKey = BlendShapeFavorites.Key(mesh, name)
                    });
                }
            }
            var search = new InputField(new InputFieldState());
            card.Add(new FormInput(T("searchShapes"), search));
            var list = new VisualElement();
            list.AddToClassList("ee4v-menu-template__shape-list");
            card.Add(list);
            void BuildList()
            {
                list.Clear();
                var filtered = targets.Where(target => string.IsNullOrWhiteSpace(search.Value) ||
                    (target.Name + " " + target.Path + " " + target.Group).IndexOf(search.Value.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                var renders = new Dictionary<string, List<Action>>(StringComparer.Ordinal);
                void AddRow(VisualElement parent, ShapeTarget target)
                {
                    var row = new VisualElement { tooltip = target.Path + "\n" + target.Name };
                    row.AddToClassList("ee4v-menu-template__shape-row");
                    parent.Add(row);
                    UiButton favorite = null;
                    favorite = new UiButton("", () => BlendShapeFavorites.Toggle(target.FavoriteKey),
                        icon: AvatarEditingUi.CreateBlendShapeFavoriteIcon(BlendShapeFavorites.Contains(target.FavoriteKey)),
                        variant: UiButtonVariant.Ghost);
                    favorite.AddToClassList("ee4v-menu-template__shape-favorite");
                    row.Add(favorite);
                    var label = new VisualElement();
                    label.AddToClassList("ee4v-menu-template__shape-label");
                    row.Add(label);
                    var selected = UiTextFactory.CreateToggle(target.DisplayName);
                    label.Add(selected);
                    label.Add(UiTextFactory.Create(target.Path, UiClassNames.SecondaryText));
                    var value = UiTextFactory.CreateFloatField(T("value"));
                    value.isDelayed = true;
                    value.tooltip = T("value");
                    value.AddToClassList("ee4v-menu-template__shape-value");
                    row.Add(value);
                    ChangedShape Find() => Shapes().FirstOrDefault(shape => shape.ShapeName == target.Name &&
                        shape.Object?.Get(owner) == target.Renderer.gameObject);
                    void Render()
                    {
                        var current = Find();
                        selected.SetValueWithoutNotify(current != null);
                        value.SetValueWithoutNotify(current?.Value ?? 100);
                        value.EnableInClassList("ee4v-menu-template__hidden", current == null);
                    }
                    if (!renders.TryGetValue(target.Key, out var updates)) renders.Add(target.Key, updates = new List<Action>());
                    updates.Add(Render);
                    void Set(bool include, float weight)
                    {
                        Run(() =>
                        {
                            if (float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0 || weight > 100)
                                throw new InvalidOperationException(T("invalidValue"));
                            var reference = ExpressionMenuTemplateModel.Reference(_context, target.Renderer.gameObject);
                            var current = Find();
                            if (include && current != null && Mathf.Approximately(current.Value, weight) &&
                                current.ChangeType == ShapeChangeType.Set)
                                return;
                            EditEffect(effect, action, () =>
                            {
                                Shapes().RemoveAll(shape => shape.ShapeName == target.Name &&
                                    shape.Object?.Get(owner) == target.Renderer.gameObject);
                                if (include) Shapes().Add(new ChangedShape
                                {
                                    Object = reference, ShapeName = target.Name, ChangeType = ShapeChangeType.Set, Value = weight
                                });
                            });
                        });
                        foreach (var render in renders[target.Key]) render();
                    }
                    selected.RegisterValueChangedCallback(evt => Set(evt.newValue, value.value));
                    value.RegisterValueChangedCallback(evt => Set(true, evt.newValue));
                    Render();
                }
                var favorites = filtered.Where(target => BlendShapeFavorites.Contains(target.FavoriteKey)).ToArray();
                if (favorites.Length > 0)
                {
                    var favoriteContent = ShapeGroup(list, "favorites", T("favoriteShapes"));
                    foreach (var target in favorites) AddRow(favoriteContent, target);
                }
                foreach (var category in filtered.GroupBy(target => target.Category))
                {
                    var categoryContent = ShapeGroup(list, "category:" + category.Key, category.Key);
                    foreach (var group in category.GroupBy(target => target.Group))
                    {
                        var groupContent = string.IsNullOrEmpty(group.Key) ? categoryContent :
                            ShapeGroup(categoryContent, "role:" + category.Key + ":" + group.Key, group.Key);
                        foreach (var target in group) AddRow(groupContent, target);
                    }
                }
                if (filtered.Length == 0) list.Add(UiTextFactory.Create(T("noShapes"), UiClassNames.SecondaryText));
                foreach (var entry in Shapes().Where(shape => !targets.Any(target =>
                    target.Name == shape.ShapeName && shape.Object?.Get(owner) == target.Renderer.gameObject)).ToArray())
                {
                    var row = Entry(list, () => EditEffect(effect, action, () => Shapes().Remove(entry), rebuild: true));
                    row.Add(UiTextFactory.CreateHelpBox(T("missingShape") + " " + entry.ShapeName, HelpBoxMessageType.Error));
                    Missing(row, entry.Object, owner);
                }
            }
            search.ValueChanged += _ => BuildList();
            card.RegisterCallback<AttachToPanelEvent>(_ => BlendShapeFavorites.Changed += BuildList);
            card.RegisterCallback<DetachFromPanelEvent>(_ => BlendShapeFavorites.Changed -= BuildList);
            BuildList();
        }

        private void EditEffect(ReactiveComponent effect, MenuReactiveAction action, Action change, bool rebuild = false)
        {
            if (action == null) { Edit(effect, change, rebuild); return; }
            Run(() =>
            {
                try { ExpressionMenuAnimationRecipe.Change(_context, _item, change); }
                catch { Refresh(); throw; }
                if (rebuild) Refresh();
            });
        }

        private VisualElement ShapeGroup(VisualElement parent, string key, string title)
        {
            var group = new VisualElement();
            group.AddToClassList("ee4v-menu-template__shape-group");
            parent.Add(group);
            var content = new VisualElement();
            UiButton header = null;
            void Render()
            {
                var collapsed = _collapsedShapeGroups.Contains(key);
                content.EnableInClassList("ee4v-menu-template__hidden", collapsed);
                header.SetIcon(FluentUiIcons.CreateState(collapsed ? "chevron_right.png" : "chevron_down.png", UiSizeTokens.Size12));
            }
            header = new UiButton(title, () =>
            {
                if (!_collapsedShapeGroups.Add(key)) _collapsedShapeGroups.Remove(key);
                Render();
            }, variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SectionTitle);
            header.AddToClassList("ee4v-menu-template__shape-header");
            header.SetContentAlignment(Justify.FlexStart);
            group.Add(header);
            group.Add(content);
            Render();
            return content;
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
