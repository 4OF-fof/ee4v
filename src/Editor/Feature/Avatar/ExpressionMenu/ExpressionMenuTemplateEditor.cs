using System;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.I18n;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
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

        private static MenuTemplateKind Kind(ReactiveComponent effect) => effect is ModularAvatarObjectToggle ? MenuTemplateKind.ObjectToggle :
            effect is ModularAvatarMaterialSwap ? MenuTemplateKind.MaterialSwap :
            effect is ModularAvatarMaterialSetter ? MenuTemplateKind.MaterialSetter : MenuTemplateKind.ShapeChanger;

        private void BuildEffect(VisualElement parent, ReactiveComponent effect)
        {
            var card = new VisualElement();
            card.AddToClassList("ee4v-menu-template__change");
            parent.Add(card);
            var heading = Row(card);
            heading.Add(UiTextFactory.Create(T("kind" + Kind(effect)), UiClassNames.SectionTitle));
            heading.Add(new UiButton(T("removeEffect"), () => Run(() =>
            {
                ExpressionMenuTemplateModel.RemoveEffect(_context, effect);
                Refresh();
            }), variant: UiButtonVariant.Ghost));
            AddToggle(card, T("inverted"), effect.Inverted, value => Edit(effect, () => effect.Inverted = value));
            if (effect is ModularAvatarObjectToggle toggle) BuildToggle(card, toggle);
            else if (effect is ModularAvatarMaterialSwap swap) BuildSwap(card, swap);
            else if (effect is ModularAvatarMaterialSetter setter) BuildSetter(card, setter);
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
                    Edit(effect, () => { var obj = effect.Objects[index]; obj.Object = reference; effect.Objects[index] = obj; });
                });
                Missing(row, entry.Object, effect);
                AddToggle(row, T("active"), entry.Active, value => Edit(effect, () =>
                { var obj = effect.Objects[index]; obj.Active = value; effect.Objects[index] = obj; }));
            }
            card.Add(new UiButton(T("addTarget"), () => Edit(effect,
                () => effect.Objects.Add(new ToggledObject { Object = new AvatarObjectReference(), Active = true }), rebuild: true), variant: UiButtonVariant.Ghost));
        }

        private void BuildSwap(VisualElement card, ModularAvatarMaterialSwap effect)
        {
            AddObject<GameObject>(card, T("scope"), effect.Root?.Get(effect), true,
                target => { var reference = ExpressionMenuTemplateModel.Reference(_context, target); Edit(effect, () => effect.Root = reference); });
            Missing(card, effect.Root, effect);
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

        private void BuildSetter(VisualElement card, ModularAvatarMaterialSetter effect)
        {
            for (var i = 0; i < effect.Objects.Count; i++)
            {
                var index = i;
                var entry = effect.Objects[i];
                var row = Entry(card, () => Edit(effect, () => effect.Objects.RemoveAt(index), rebuild: true));
                var renderer = entry.Object?.Get(effect)?.GetComponent<Renderer>();
                AddObject<Renderer>(row, T("target"), renderer, true, target =>
                {
                    if (target != null && target.GetComponent<Renderer>() != target)
                        throw new InvalidOperationException(T("firstRenderer"));
                    var reference = ExpressionMenuTemplateModel.Reference(_context, target == null ? null : target.gameObject);
                    Edit(effect, () =>
                    {
                        effect.Objects[index].Object = reference;
                        effect.Objects[index].MaterialIndex = target == null ? 0 :
                            Mathf.Clamp(effect.Objects[index].MaterialIndex, 0, Math.Max(0, target.sharedMaterials.Length - 1));
                    }, rebuild: true);
                });
                Missing(row, entry.Object, effect);
                if (renderer != null && (entry.MaterialIndex < 0 || entry.MaterialIndex >= renderer.sharedMaterials.Length))
                    row.Add(UiTextFactory.CreateHelpBox(T("invalidSlot"), HelpBoxMessageType.Error));
                AddInteger(row, T("materialSlot"), entry.MaterialIndex, value =>
                {
                    if (value < 0 || renderer != null && value >= renderer.sharedMaterials.Length)
                        throw new InvalidOperationException(T("invalidSlot"));
                    Edit(effect, () => effect.Objects[index].MaterialIndex = value);
                });
                AddObject<Material>(row, T("toMaterial"), entry.Material, false,
                    value => Edit(effect, () => effect.Objects[index].Material = value));
            }
            card.Add(new UiButton(T("addTarget"), () => Edit(effect,
                () => effect.Objects.Add(new MaterialSwitchObject { Object = new AvatarObjectReference() }), rebuild: true), variant: UiButtonVariant.Ghost));
        }

        private void BuildShapes(VisualElement card, ModularAvatarShapeChanger effect)
        {
            for (var i = 0; i < effect.Shapes.Count; i++)
            {
                var index = i;
                var entry = effect.Shapes[i];
                var row = Entry(card, () => Edit(effect, () => effect.Shapes.RemoveAt(index), rebuild: true));
                var renderer = entry.Object?.Get(effect)?.GetComponent<SkinnedMeshRenderer>();
                AddObject<SkinnedMeshRenderer>(row, T("target"), renderer, true, target =>
                {
                    var reference = ExpressionMenuTemplateModel.Reference(_context, target == null ? null : target.gameObject);
                    Edit(effect, () =>
                    {
                        effect.Shapes[index].Object = reference;
                        effect.Shapes[index].ShapeName = target?.sharedMesh != null && target.sharedMesh.blendShapeCount > 0
                            ? target.sharedMesh.GetBlendShapeName(0) : "";
                    }, rebuild: true);
                });
                Missing(row, entry.Object, effect);
                var mesh = renderer?.sharedMesh;
                if (mesh != null && mesh.blendShapeCount > 0)
                {
                    var names = Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToList();
                    var shape = UiTextFactory.CreatePopupField(T("blendShape"), names, Math.Max(0, names.IndexOf(entry.ShapeName)));
                    shape.RegisterValueChangedCallback(evt => Edit(effect, () => effect.Shapes[index].ShapeName = evt.newValue));
                    row.Add(shape);
                    if (!names.Contains(entry.ShapeName)) row.Add(UiTextFactory.CreateHelpBox(T("missingShape"), HelpBoxMessageType.Error));
                }
                AddToggle(row, T("deleteShape"), entry.ChangeType == ShapeChangeType.Delete,
                    value => Edit(effect, () => effect.Shapes[index].ChangeType = value ? ShapeChangeType.Delete : ShapeChangeType.Set));
                AddFloat(row, T("value"), entry.Value, value =>
                {
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException(T("invalidValue"));
                    Edit(effect, () => effect.Shapes[index].Value = value);
                });
            }
            card.Add(new UiButton(T("addShape"), () => Edit(effect,
                () => effect.Shapes.Add(new ChangedShape { Object = new AvatarObjectReference(), ChangeType = ShapeChangeType.Set, Value = 100 }), rebuild: true), variant: UiButtonVariant.Ghost));
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

        private static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-menu-template__row");
            parent.Add(row);
            return row;
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

        private void AddToggle(VisualElement host, string label, bool value, Action<bool> changed)
        {
            var field = UiTextFactory.CreateToggle(label);
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(evt => Run(() => changed(evt.newValue)));
            host.Add(field);
        }

        private void AddInteger(VisualElement host, string label, int value, Action<int> changed)
        {
            var field = UiTextFactory.CreateIntegerField(label);
            field.isDelayed = true;
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(evt => Run(() =>
            {
                try { changed(evt.newValue); }
                catch { field.SetValueWithoutNotify(evt.previousValue); throw; }
            }));
            host.Add(field);
        }

        private void AddFloat(VisualElement host, string label, float value, Action<float> changed)
        {
            var field = UiTextFactory.CreateFloatField(label);
            field.isDelayed = true;
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(evt => Run(() => changed(evt.newValue)));
            host.Add(field);
        }
    }
}


