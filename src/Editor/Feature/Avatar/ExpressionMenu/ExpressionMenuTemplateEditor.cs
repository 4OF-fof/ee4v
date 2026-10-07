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
        private readonly HashSet<string> _collapsedShapeGroups = new HashSet<string>(StringComparer.Ordinal);
        private static string T(string key) => TemplateText.Get(key);

        internal ExpressionMenuTemplateEditor(AvatarEditingContext context, ModularAvatarMenuItem item)
        {
            _context = context;
            _item = item;
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
            foreach (var effect in ExpressionMenuTemplateModel.Effects(_item))
                BuildEffect(_fields, effect);
        }

        private void BuildEffect(VisualElement parent, ReactiveComponent effect)
        {
            var card = new VisualElement();
            card.AddToClassList("ee4v-menu-template__change");
            parent.Add(card);
            if (effect is ModularAvatarObjectToggle toggle) BuildToggle(card, toggle);
            else if (effect is ModularAvatarMaterialSwap swap) BuildSwap(card, swap);
            else if (effect is ModularAvatarShapeChanger changer) BuildShapes(card, changer);
            card.SetEnabled(ExpressionMenuTemplateModel.CanEdit(_context, effect));
        }

        private void BuildToggle(VisualElement card, ModularAvatarObjectToggle effect)
        {
            for (var i = 0; i < effect.Objects.Count; i++)
            {
                var index = i;
                var entry = effect.Objects[i];
                var row = Entry(card, () => Edit(effect, () => effect.Objects.RemoveAt(index), rebuild: true));
                AddObject<GameObject>(row, T("target"), entry.Object?.Get(effect), true, target =>
                {
                    var reference = ExpressionMenuTemplateModel.Reference(_context, target, false);
                    Edit(effect, () => { var obj = effect.Objects[index]; obj.Object = reference; effect.Objects[index] = obj; }, initialValue: true);
                });
                Missing(row, entry.Object, effect);
                var initial = UiTextFactory.CreateToggle(T("initialOn"));
                initial.SetValueWithoutNotify(entry.Active != effect.Inverted);
                initial.RegisterValueChangedCallback(evt => Edit(effect, () =>
                {
                    var obj = effect.Objects[index];
                    obj.Active = evt.newValue != effect.Inverted;
                    effect.Objects[index] = obj;
                }, initialValue: true));
                row.Add(initial);
            }
            card.Add(new UiButton(T("addTarget"), () => Edit(effect,
                () => effect.Objects.Add(new ToggledObject { Object = new AvatarObjectReference(), Active = !effect.Inverted }), rebuild: true, initialValue: true), variant: UiButtonVariant.Ghost));
        }

        private void BuildSwap(VisualElement card, ModularAvatarMaterialSwap effect)
        {
            for (var i = 0; i < effect.Swaps.Count; i++)
            {
                var index = i;
                var entry = effect.Swaps[i];
                var row = Entry(card, () => Edit(effect, () => effect.Swaps.RemoveAt(index), rebuild: true));
                AddObject<Material>(row, T("fromMaterial"), entry.From, false, value => Edit(effect, () =>
                { var obj = effect.Swaps[index]; obj.From = value; effect.Swaps[index] = obj; }));
                AddObject<Material>(row, T("toMaterial"), entry.To, false, value => Edit(effect, () =>
                { var obj = effect.Swaps[index]; obj.To = value; effect.Swaps[index] = obj; }));
            }
            card.Add(new UiButton(T("addSwap"), () => Edit(effect,
                () => effect.Swaps.Add(new MatSwap()), rebuild: true), variant: UiButtonVariant.Ghost));
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

        private void BuildShapes(VisualElement card, ModularAvatarShapeChanger effect)
        {
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
                    ChangedShape Find() => effect.Shapes.FirstOrDefault(shape => shape.ShapeName == target.Name &&
                        shape.Object?.Get(effect) == target.Renderer.gameObject);
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
                                current.ChangeType == ShapeChangeType.Set && _item.isDefault)
                                return;
                            Edit(effect, () =>
                            {
                                effect.Shapes.RemoveAll(shape => shape.ShapeName == target.Name &&
                                    shape.Object?.Get(effect) == target.Renderer.gameObject);
                                if (include) effect.Shapes.Add(new ChangedShape
                                {
                                    Object = reference, ShapeName = target.Name, ChangeType = ShapeChangeType.Set, Value = weight
                                });
                            }, initialValue: include);
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
                foreach (var entry in effect.Shapes.Where(shape => !targets.Any(target =>
                    target.Name == shape.ShapeName && shape.Object?.Get(effect) == target.Renderer.gameObject)).ToArray())
                {
                    var row = Entry(list, () => Edit(effect, () => effect.Shapes.Remove(entry), rebuild: true));
                    row.Add(UiTextFactory.CreateHelpBox(T("missingShape") + " " + entry.ShapeName, HelpBoxMessageType.Error));
                    Missing(row, entry.Object, effect);
                }
            }
            search.ValueChanged += _ => BuildList();
            card.RegisterCallback<AttachToPanelEvent>(_ => BlendShapeFavorites.Changed += BuildList);
            card.RegisterCallback<DetachFromPanelEvent>(_ => BlendShapeFavorites.Changed -= BuildList);
            BuildList();
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


        private void Edit(Object target, Action change, bool rebuild = false, bool initialValue = false) => Run(() =>
        {
            ExpressionMenuTemplateModel.Edit(_context, target, change, initialValue ? _item : null);
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
