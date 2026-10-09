using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.AvatarEvaluation;
using Ee4v.Core.I18n;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;

namespace Ee4v.ExpressionMenu
{
    internal sealed class ExpressionMenuPreview : VisualElement
    {
        private readonly float[] _values;
        private readonly PrefabScenePreview _preview;
        private readonly ExpressionMenuAnimationRecipe.PreviewAnimation _animation;
        private readonly UiTextElement _parameters;
        private bool _disposed;
        private double _changedAt;
        private static string T(string key) => I18N.Get("expressionMenu." + key);

        internal ExpressionMenuPreview(AvatarEditingContext context, MenuEntry entry, Action back)
        {
            AddToClassList("ee4v-expression-menu__preview-column");
            _preview = new PrefabScenePreview() { name = "expressionMenuPreview" };
            _preview.AddToClassList("ee4v-expression-menu__preview");
            _preview.SetFlexibleLayout(true);
            _preview.SetFullBodyFraming(false);
            _preview.SetViewToggleVisible(false);
            _preview.FocusBodyPart(null);
            if (context.Preview != null)
            {
                _preview.MatchFullBodyFraming(context.Preview);
            }
            else
            {
                var reference = new PrefabScenePreview();
                try
                {
                    reference.SetPrefab(context.Root);
                    _preview.MatchFullBodyFraming(reference);
                }
                finally { reference.Dispose(); }
            }
            var returnButton = new UiButton(T("backToMenu"), back,
                icon: FluentUiIcons.CreateState("arrow_left.png")) { name = "expressionMenuBack" };
            returnButton.AddToClassList("ee4v-expression-menu__preview-back");
            _preview.FeatureOverlay.Add(returnButton);
            Add(_preview);

            var controls = new VisualElement { name = "expressionMenuPreviewControls" };
            controls.AddToClassList("ee4v-expression-menu__preview-controls");
            var overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            overlay.AddToClassList("ee4v-expression-menu__preview-control-overlay");
            overlay.Add(controls);
            _preview.FeatureOverlay.Add(overlay);
            var item = entry.Owner as ModularAvatarMenuItem;
            var type = entry.Control.type;
            var axes = type == ControlType.TwoAxisPuppet ? 2 : type == ControlType.FourAxisPuppet ? 4 : 1;
            _values = new float[axes];
            var recipe = ExpressionMenuAnimationRecipe.Find(item);
            if (type == ControlType.Toggle)
            {
                _values[0] = item != null && item.isDefault ? 1f : 0f;
                var toggle = new VisualElement { name = "expressionMenuPreviewToggle", tooltip = T("previewToggle") };
                toggle.AddToClassList("ee4v-expression-menu__preview-toggle");
                UiButton off = null;
                UiButton on = null;
                void Select(bool enabled)
                {
                    off.EnableInClassList("ee4v-ui-prefab-scene-preview__side-button--active", !enabled);
                    on.EnableInClassList("ee4v-ui-prefab-scene-preview__side-button--active", enabled);
                }
                off = new UiButton(TemplateText.Get("offValue"), () => { Change(0, 0f); Select(false); }) { name = "expressionMenuPreviewOff" };
                on = new UiButton(TemplateText.Get("onValue"), () => { Change(0, 1f); Select(true); }) { name = "expressionMenuPreviewOn" };
                off.AddToClassList("ee4v-ui-prefab-scene-preview__side-button");
                on.AddToClassList("ee4v-ui-prefab-scene-preview__side-button");
                toggle.Add(on);
                toggle.Add(off);
                Select(_values[0] != 0f);
                controls.Add(toggle);
            }
            else if (type == ControlType.Button)
            {
                var execute = new UiButton(T("previewExecute"), () =>
                {
                    Change(0, 1f);
                    controls.schedule.Execute(() => Change(0, 0f)).StartingIn(150);
                }, variant: UiButtonVariant.Ghost) { name = "expressionMenuPreviewButton" };
                execute.AddToClassList("ee4v-expression-menu__preview-execute");
                controls.Add(execute);
            }
            else if (axes > 1)
            {
                BuildPuppetInput(controls, recipe);
            }
            else
            {
                controls.AddToClassList("ee4v-expression-menu__preview-controls--sliders");
                _values[0] = Mathf.Clamp01((recipe?.InitialValue ?? 0f) / 100f);
                var slider = UiTextFactory.CreateSlider(T("previewValue"), 0f, 100f);
                slider.name = "expressionMenuPreviewSlider0";
                slider.showInputField = true;
                slider.SetValueWithoutNotify(_values[0] * 100f);
                slider.RegisterValueChangedCallback(evt => Change(0, evt.newValue / 100f));
                controls.Add(slider);
            }
            _parameters = UiTextFactory.Create("", "ee4v-expression-menu__preview-parameters");
            _parameters.style.display = DisplayStyle.None;
            controls.Add(_parameters);
            try
            {
                _animation = item == null ? null : ExpressionMenuAnimationRecipe.CreatePreviewAnimation(context, item);
                if (_animation == null)
                {
                    controls.SetEnabled(false);
                    controls.tooltip = T("previewUnavailable");
                }
                else
                {
                    _preview.SetAnimationSampler(frame => _animation.Sample(frame, _values,
                        (float)(EditorApplication.timeSinceStartup - _changedAt)));
                    UpdateParameters();
                }
            }
            catch (Exception exception)
            {
                controls.SetEnabled(false);
                Add(UiTextFactory.CreateHelpBox(exception.GetBaseException().Message, HelpBoxMessageType.Error));
            }
            _changedAt = EditorApplication.timeSinceStartup;
            _preview.SetPrefab(context.Root);
            RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != this) return;
                _disposed = true;
                _preview.SetAnimationSampler(null);
                _preview.Dispose();
                _animation?.Dispose();
            });
        }

        private void BuildPuppetInput(VisualElement controls, ExpressionMenuAnimationRecipe recipe)
        {
            var axisMask = _values.Length == 4 ? 3 : GetTwoAxisInputMask(recipe);
            var pad = new VisualElement { pickingMode = PickingMode.Ignore };
            pad.AddToClassList("ee4v-expression-menu__preview-puppet-pad");
            var input = new VisualElement
            {
                name = "expressionMenuPreviewPuppetInput",
                focusable = true,
                tooltip = T("previewPuppetInput")
            };
            input.AddToClassList("ee4v-expression-menu__preview-puppet-input");
            foreach (var axis in new[] { "horizontal", "vertical" })
            {
                if ((axisMask & (axis == "horizontal" ? 1 : 2)) == 0) continue;
                var guide = new VisualElement { pickingMode = PickingMode.Ignore };
                guide.AddToClassList("ee4v-expression-menu__preview-puppet-" + axis);
                input.Add(guide);
            }
            var indicator = new VisualElement { name = "expressionMenuPreviewPuppetIndicator", pickingMode = PickingMode.Ignore };
            indicator.AddToClassList("ee4v-expression-menu__preview-puppet-indicator");
            input.Add(indicator);
            input.AddManipulator(new PuppetInputManipulator(axisMask, value =>
            {
                if (_values.Length == 2)
                {
                    _values[0] = value.x;
                    _values[1] = value.y;
                }
                else
                {
                    _values[0] = Mathf.Max(0f, value.y);
                    _values[1] = Mathf.Max(0f, value.x);
                    _values[2] = Mathf.Max(0f, -value.y);
                    _values[3] = Mathf.Max(0f, -value.x);
                }
                indicator.style.left = Length.Percent((value.x + 1f) * 50f);
                indicator.style.top = Length.Percent((1f - value.y) * 50f);
                RefreshOperation();
            }));
            pad.Add(input);
            controls.Add(pad);
        }

        private static int GetTwoAxisInputMask(ExpressionMenuAnimationRecipe recipe)
        {
            if (recipe == null) return 1;
            var axes = recipe.Transforms.Select(action => action.SourceAxis)
                .Concat(recipe.RadialShapes.Select(action => action.Axis))
                .Concat(recipe.ParameterActions.Select(action => action.Axis));
            var mask = axes.Where(axis => axis >= 0 && axis < 2)
                .Aggregate(0, (current, axis) => current | (1 << axis));
            if (recipe.Actions.Count > 0 || recipe.ReactiveActions.Count > 0 ||
                recipe.MaterialValues.Count > 0 || recipe.Components.Count > 0) mask |= 1;
            return mask == 0 ? 1 : mask;
        }

        private sealed class PuppetInputManipulator : PointerManipulator
        {
            private readonly Action<Vector2> _change;
            private readonly int _axisMask;
            private readonly HashSet<KeyCode> _keys = new HashSet<KeyCode>();
            private int _pointer = -1;

            internal PuppetInputManipulator(int axisMask, Action<Vector2> change)
            {
                _axisMask = axisMask;
                _change = change;
            }

            private void Change(Vector2 value) => _change(new Vector2(
                (_axisMask & 1) != 0 ? value.x : 0f,
                (_axisMask & 2) != 0 ? value.y : 0f));

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnPointerDown);
                target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                target.RegisterCallback<PointerUpEvent>(OnPointerUp);
                target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
                target.RegisterCallback<KeyDownEvent>(OnKeyDown);
                target.RegisterCallback<KeyUpEvent>(OnKeyUp);
                target.RegisterCallback<FocusOutEvent>(OnFocusOut);
                target.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                Reset();
                target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
                target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
                target.UnregisterCallback<KeyUpEvent>(OnKeyUp);
                target.UnregisterCallback<FocusOutEvent>(OnFocusOut);
                target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            }

            private void UpdatePosition(Vector2 position)
            {
                var rect = target.contentRect;
                if (rect.width <= 0f || rect.height <= 0f) return;
                Change(new Vector2(Mathf.Clamp((position.x - rect.xMin) / rect.width * 2f - 1f, -1f, 1f),
                    Mathf.Clamp(1f - (position.y - rect.yMin) / rect.height * 2f, -1f, 1f)));
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                if (evt.button != 0 || _pointer >= 0 || !target.enabledInHierarchy) return;
                target.Focus();
                _keys.Clear();
                _pointer = evt.pointerId;
                target.CapturePointer(_pointer);
                target.AddToClassList("ee4v-expression-menu__preview-puppet-input--active");
                UpdatePosition(evt.localPosition);
                evt.StopPropagation();
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (evt.pointerId != _pointer) return;
                UpdatePosition(evt.localPosition);
                evt.StopPropagation();
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                if (evt.pointerId != _pointer || evt.button != 0) return;
                Reset();
                evt.StopPropagation();
            }

            private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
            {
                if (evt.pointerId == _pointer) Reset();
            }

            private static bool IsDirectionKey(KeyCode key) => key == KeyCode.LeftArrow ||
                key == KeyCode.RightArrow || key == KeyCode.UpArrow || key == KeyCode.DownArrow;

            private void UpdateKeys()
            {
                target.EnableInClassList("ee4v-expression-menu__preview-puppet-input--active", _keys.Count > 0);
                Change(new Vector2((_keys.Contains(KeyCode.RightArrow) ? 1f : 0f) - (_keys.Contains(KeyCode.LeftArrow) ? 1f : 0f),
                    (_keys.Contains(KeyCode.UpArrow) ? 1f : 0f) - (_keys.Contains(KeyCode.DownArrow) ? 1f : 0f)));
            }

            private void OnKeyDown(KeyDownEvent evt)
            {
                if (!IsDirectionKey(evt.keyCode) || !target.enabledInHierarchy) return;
                if (_pointer < 0 && _keys.Add(evt.keyCode)) UpdateKeys();
                evt.StopPropagation();
                evt.PreventDefault();
            }

            private void OnKeyUp(KeyUpEvent evt)
            {
                if (!IsDirectionKey(evt.keyCode)) return;
                if (_pointer < 0 && _keys.Remove(evt.keyCode)) UpdateKeys();
                evt.StopPropagation();
                evt.PreventDefault();
            }

            private void Reset()
            {
                var pointer = _pointer;
                var active = pointer >= 0 || _keys.Count > 0;
                _pointer = -1;
                _keys.Clear();
                target.RemoveFromClassList("ee4v-expression-menu__preview-puppet-input--active");
                if (active) Change(Vector2.zero);
                if (pointer >= 0) target.ReleasePointer(pointer);
            }

            private void OnFocusOut(FocusOutEvent evt) => Reset();
            private void OnDetach(DetachFromPanelEvent evt) => Reset();
        }


        private void Change(int axis, float value)
        {
            _values[axis] = value;
            RefreshOperation();
        }

        private void RefreshOperation()
        {
            if (_disposed) return;
            _changedAt = EditorApplication.timeSinceStartup;
            UpdateParameters();
            _preview.RefreshPreview();
        }

        private void UpdateParameters()
        {
            var text = _animation?.DescribeParameters(_values) ?? "";
            _parameters.SetText(text);
            _parameters.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }

    internal sealed partial class ExpressionMenuAnimationRecipe
    {
        internal static PreviewAnimation CreatePreviewAnimation(AvatarEditingContext context, ModularAvatarMenuItem item)
        {
            var source = Find(item);
            var copy = source != null ? Instantiate(source) : CreateInstance<ExpressionMenuAnimationRecipe>();
            copy.hideFlags = HideFlags.HideAndDontSave;
            copy.Mode = EffectiveMode(item);
            try
            {
                copy.ImportEffects(context, item, removeOriginal: false);
                return new PreviewAnimation(context, item, copy);
            }
            catch { DestroyImmediate(copy); throw; }
        }

        internal sealed class PreviewAnimation : IDisposable
        {
            private readonly AvatarEditingContext _context;
            private readonly ModularAvatarMenuItem _item;
            private readonly ExpressionMenuAnimationRecipe _recipe;
            private readonly List<PreparedAction> _clips;
            private bool _buttonExecuted;

            internal PreviewAnimation(AvatarEditingContext context, ModularAvatarMenuItem item, ExpressionMenuAnimationRecipe recipe)
            {
                _context = context;
                _item = item;
                _recipe = recipe;
                _clips = recipe.PrepareClips(context, item);
            }

            internal void Sample(AvatarAnimationFrame frame, float[] values, float elapsed)
            {
                var continuous = IsContinuous(_item);
                foreach (var pair in _clips)
                {
                    var clip = continuous || values[0] != 0 ? pair.on : pair.off;
                    if (clip == null) continue;
                    var normalized = AxisCount(_item) == 2 && EffectiveMode(_item) == MenuBehaviorMode.Puppet
                        ? (values[pair.axis] + 1f) / 2f : values[pair.axis];
                    var time = continuous ? Mathf.Clamp01(normalized) * clip.length :
                        clip.isLooping && clip.length > 0 ? elapsed % clip.length : Mathf.Min(elapsed, clip.length);
                    frame.Sample(clip, time);
                    if (pair.transform != null) SampleTransform(frame, pair.transform, values);
                }
            }

            private void SampleTransform(AvatarAnimationFrame frame, MenuTransformAction action, float[] values)
            {
                var four = AxisCount(_item) == 4;
                var horizontal = four ? Mathf.Lerp(values[1], -1f, values[3]) : values[action.SourceAxis];
                var vertical = four ? Mathf.Lerp(values[0], -1f, values[2]) : 0f;
                foreach (var target in action.Targets.Where(target => target.Path != null))
                {
                    var destination = frame.ResolveTransform(target.Path);
                    var source = ResolveTransform(_context, target.Path);
                    if (destination == null || source == null) continue;
                    foreach (var property in TransformProperties(target, source))
                    {
                        var value = property.current;
                        value[action.HorizontalAxis] = Mathf.Lerp(property.min[action.HorizontalAxis], property.max[action.HorizontalAxis], (horizontal + 1f) / 2f);
                        if (four) value[action.VerticalAxis] = Mathf.Lerp(property.min[action.VerticalAxis], property.max[action.VerticalAxis], (vertical + 1f) / 2f);
                        if (property.property == "m_LocalPosition") destination.localPosition = value;
                        else if (property.property == "m_LocalScale") destination.localScale = value;
                        else destination.localEulerAngles = value;
                    }
                }
            }

            internal string DescribeParameters(float[] values)
            {
                if (EffectiveMode(_item) == MenuBehaviorMode.Button)
                {
                    _buttonExecuted |= values[0] != 0;
                    if (!_buttonExecuted) return "";
                }
                return string.Join("\n", _recipe.ParameterActions.Where(action => !string.IsNullOrEmpty(action.Parameter)).Select(action =>
                {
                    var type = ParameterType(_item, action);
                    if (type == AnimatorControllerParameterType.Trigger) return action.Parameter + ": Trigger";
                    var input = SourceMinimum(_item) < 0 ? (values[action.Axis] + 1f) / 2f : values[action.Axis];
                    var value = IsContinuous(_item) ? Mathf.Lerp(action.Off, action.On, input) :
                        EffectiveMode(_item) == MenuBehaviorMode.Button || values[0] != 0 ? action.On : action.Off;
                    return action.Parameter + ": " + (type == AnimatorControllerParameterType.Bool ? (value != 0).ToString() :
                        type == AnimatorControllerParameterType.Int ? ((int)value).ToString() : value.ToString("0.###"));
                }));
            }

            public void Dispose()
            {
                DestroyTemporaryClips(_clips);
                if (_recipe != null) Object.DestroyImmediate(_recipe);
            }
        }
    }
}
