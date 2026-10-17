using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private Transform _positionTarget;
        private Transform _positionAvatarBone;
        private HumanBodyBones _positionAvatarReference;
        private Transform _positionCustomAvatarBone;
        private string _positionAutoMessage;
        private static readonly List<HumanBodyBones> PositionAvatarReferences = new List<HumanBodyBones> {
            HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.Chest, HumanBodyBones.Hips,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.LastBone
        };

        public bool CanAdjustItemPosition => SelectedPositionTarget() != null;

        private Transform SelectedPositionTarget()
        {
            var root = _context.Root;
            var index = _context.SelectedPrefabSiblingIndex;
            if (root == null || !index.HasValue || index.Value < 0 || index.Value >= root.transform.childCount ||
                !_context.PrefabSiblingIndices.Contains(index.Value)) return null;
            var target = root.transform.GetChild(index.Value);
            return PrefabUtility.IsAnyPrefabInstanceRoot(target.gameObject) ? target : null;
        }

        private VisualElement BuildPositionControls()
        {
            var content = new VisualElement();
            content.AddToClassList("ee4v-item-position");
            var target = SelectedPositionTarget();
            if (target == null) return content;
            if (!_context.Edits.CanEditPrefab())
            {
                content.Add(UiTextFactory.CreateMessagePanel(I18N.Get("workflow.assets.protected"), MessageSeverity.Warning));
                return content;
            }

            if (_positionTarget != target)
            {
                _positionTarget = target;
                _positionAvatarReference = HumanBodyBones.Head;
                _positionAvatarBone = ResolvePositionAvatarReference(FindPositionAvatarAnimator(target), _positionAvatarReference);
                _positionCustomAvatarBone = null;
                _positionAutoMessage = null;
            }

            var source = PrefabUtility.GetCorrespondingObjectFromSource(target);
            var basePosition = source != null ? source.localPosition : Vector3.zero;
            var baseScale = source != null ? source.localScale : Vector3.one;
            var renderers = new List<Action>();
            var renderedPosition = target.localPosition;
            var renderedScale = target.localScale;
            void Render()
            {
                if (target == null) return;
                foreach (var render in renderers) render();
                renderedPosition = target.localPosition;
                renderedScale = target.localScale;
            }

            content.Add(BuildPositionAutoControls(target, Render, out var refreshAuto));
            renderers.Add(refreshAuto);
            content.Add(UiTextFactory.Create(I18N.Get("workflow.position.offset"), UiClassNames.SectionTitle));
            for (var axis = 0; axis < 3; axis++)
            {
                var index = axis;
                content.Add(BuildPositionScalar(I18N.Get("workflow.position.axis." + "xyz"[axis]),
                    -50f, 50f, "cm", () => (target.localPosition[index] - basePosition[index]) * 100f,
                    value =>
                    {
                        var position = target.localPosition;
                        position[index] = basePosition[index] + value / 100f;
                        ApplyPositionTransform(target, position, target.localScale);
                        Render();
                    }, renderers, 0f));
            }

            content.Add(UiTextFactory.Create(I18N.Get("workflow.position.scale"), UiClassNames.SectionTitle));
            Vector3 Multipliers() => new Vector3(GetScaleRatio(target.localScale.x, baseScale.x),
                GetScaleRatio(target.localScale.y, baseScale.y), GetScaleRatio(target.localScale.z, baseScale.z));
            content.Add(BuildPositionScalar(I18N.Get("workflow.position.uniform"), 50f, 200f, "%",
                () => AverageVectorComponent(Multipliers()) * 100f,
                value =>
                {
                    if (value > 0f) ApplyPositionScale(target, baseScale * (value / 100f));
                    Render();
                }, renderers, 100f, true));
            for (var axis = 0; axis < 3; axis++)
            {
                var index = axis;
                content.Add(BuildPositionScalar(GetScaleAxisName(axis), 50f, 200f, "%",
                    () => Multipliers()[index] * 100f,
                    value =>
                    {
                        var scale = target.localScale;
                        scale[index] = baseScale[index] * (value / 100f);
                        if (value > 0f) ApplyPositionScale(target, scale);
                        Render();
                    }, renderers, 100f, true));
            }
            content.Add(UiTextFactory.Create(I18N.Get("workflow.position.pivotHint"),
                UiClassNames.SecondaryText, "ee4v-item-position__description"));
            content.Add(new UiButton(I18N.Get("workflow.position.reset"), () =>
            {
                ApplyPositionTransform(target, basePosition, baseScale);
                Render();
            }, variant: UiButtonVariant.Ghost));
            content.schedule.Execute(() =>
            {
                if (target != null && (target.localPosition != renderedPosition || target.localScale != renderedScale)) Render();
            }).Every(200);
            Render();
            return content;
        }

        private VisualElement BuildPositionScalar(string label, float minimum, float maximum, string unit,
            Func<float> read, Action<float> apply, ICollection<Action> renderers, float resetValue, bool requiresReference = false)
        {
            var row = CreateSizeControlRow(label, out var controls);
            var target = SelectedPositionTarget();
            var input = new ScalarInput(minimum, maximum, I18N.Get("workflow.appearance.sizeReset"), null, unit);
            input.ValueField.isDelayed = true;
            controls.Add(input);
            void Render()
            {
                if (target == null) return;
                row.SetEnabled(!requiresReference || IsPositionAvatarBone(target, _positionAvatarBone));
                var value = read();
                input.Slider.SetValueWithoutNotify(value);
                input.ValueField.SetValueWithoutNotify(value);
                input.ResetButton.SetEnabled(!Mathf.Approximately(value, resetValue));
            }
            void Apply(float value)
            {
                if (target == null || target != SelectedPositionTarget()) return;
                if (Finite(value)) apply(value);
                Render();
            }
            input.Slider.RegisterValueChangedCallback(evt => Apply(evt.newValue));
            input.ValueField.RegisterValueChangedCallback(evt => Apply(evt.newValue));
            input.ResetButton.clicked += () => Apply(resetValue);
            var undoGroup = -1;
            void EndDrag()
            {
                if (undoGroup >= 0 && Undo.GetCurrentGroup() == undoGroup) Undo.CollapseUndoOperations(undoGroup);
                undoGroup = -1;
            }
            input.Slider.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
            }, TrickleDown.TrickleDown);
            input.Slider.RegisterCallback<PointerUpEvent>(_ => EndDrag());
            input.Slider.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            row.RegisterCallback<DetachFromPanelEvent>(_ => EndDrag());
            renderers.Add(Render);
            return row;
        }

        private VisualElement BuildPositionAutoControls(Transform target,
            Action render, out Action refresh)
        {
            var panel = new VisualElement();
            panel.AddToClassList("ee4v-item-position__auto");
            panel.Add(UiTextFactory.Create(I18N.Get("workflow.position.autoTitle"), UiClassNames.SectionTitle));
            panel.Add(UiTextFactory.Create(I18N.Get("workflow.position.autoHint"),
                UiClassNames.SecondaryText, "ee4v-item-position__description"));
            UiButton apply = null;
            Action refreshReference = null;
            var explanation = UiTextFactory.Create(string.Empty,
                UiClassNames.SecondaryText, "ee4v-item-position__description");
            void RefreshAvailability()
            {
                var available = TryCalculatePositionAdjustment(target, out _, out _, out var message);
                if (!string.IsNullOrEmpty(_positionAutoMessage)) message = _positionAutoMessage;
                apply?.SetPrimaryActionEnabled(available);
                explanation.SetText(message);
                explanation.EnableInClassList("ee4v-modification-workflow__hidden", string.IsNullOrEmpty(message));
                refreshReference?.Invoke();
            }
            UnityEditor.UIElements.ObjectField AddBoneField(VisualElement host, string label, Func<Transform> read,
                Action<Transform> set)
            {
                var field = UiTextFactory.CreateObjectField();
                field.objectType = typeof(Transform);
                field.allowSceneObjects = true;
                field.SetValueWithoutNotify(read());
                field.RegisterValueChangedCallback(evt =>
                {
                    var bone = evt.newValue as Transform;
                    if (bone != null && !IsPositionAvatarBone(target, bone))
                    {
                        field.SetValueWithoutNotify(read());
                        return;
                    }
                    set(bone);
                    _positionAutoMessage = null;
                    render();
                });
                host.Add(new FormInput(I18N.Get(label), field));
                return field;
            }
            string ReferenceLabel(HumanBodyBones bone) => I18N.Get("workflow.position.references." + bone);
            var referencePicker = UiTextFactory.CreatePopupField(string.Empty, PositionAvatarReferences,
                PositionAvatarReferences.IndexOf(_positionAvatarReference), ReferenceLabel, ReferenceLabel);
            referencePicker.name = "ee4v-position-avatar-reference";
            panel.Add(new FormInput(I18N.Get("workflow.position.target"), referencePicker));
            var customReference = new VisualElement { name = "ee4v-position-custom-reference" };
            var avatar = AddBoneField(customReference, "workflow.position.customBone", () => _positionCustomAvatarBone,
                bone => { _positionCustomAvatarBone = bone; _positionAvatarBone = bone; });
            avatar.name = "ee4v-position-custom-bone";
            panel.Add(customReference);
            referencePicker.RegisterValueChangedCallback(evt =>
            {
                _positionAvatarReference = evt.newValue;
                _positionAvatarBone = evt.newValue == HumanBodyBones.LastBone ? _positionCustomAvatarBone
                    : ResolvePositionAvatarReference(FindPositionAvatarAnimator(target), evt.newValue);
                _positionAutoMessage = null;
                render();
            });
            refreshReference = () =>
            {
                referencePicker.SetValueWithoutNotify(_positionAvatarReference);
                customReference.style.display = _positionAvatarReference == HumanBodyBones.LastBone
                    ? DisplayStyle.Flex : DisplayStyle.None;
                avatar.SetValueWithoutNotify(_positionCustomAvatarBone);
            };
            panel.Add(explanation);
            apply = new UiButton(I18N.Get("workflow.position.auto"), () =>
            {
                if (!TryCalculatePositionAdjustment(target, out var position, out var scale, out var message, true))
                {
                    _positionAutoMessage = message;
                    render();
                    return;
                }
                Undo.IncrementCurrentGroup();
                ApplyPositionTransform(target, position, scale);
                _positionAutoMessage = message;
                render();
            });
            panel.Add(apply);
            refresh = RefreshAvailability;
            RefreshAvailability();
            return panel;
        }

        private Animator FindPositionAvatarAnimator(Transform target) => _context.Root.GetComponentsInChildren<Animator>(true)
            .FirstOrDefault(animator => animator.avatar != null && animator.avatar.isValid && animator.isHuman &&
                !animator.transform.IsChildOf(target) && PrefabHierarchyUtility.IsInScope(animator.transform,
                    _context.Root.transform, -1, _context.PrefabSiblingIndices));

        private bool IsPositionAvatarBone(Transform target, Transform bone) => bone != null &&
            bone.IsChildOf(_context.Root.transform) && !bone.IsChildOf(target);

        private static Transform ResolvePositionAvatarReference(Animator animator, HumanBodyBones bone) =>
            animator == null || bone == HumanBodyBones.LastBone ? null : bone == HumanBodyBones.Chest
                ? animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest)
                : animator.GetBoneTransform(bone);

        private bool TryCalculatePositionAdjustment(Transform target,
            out Vector3 position, out Vector3 scale, out string message, bool fitGeometry = false)
        {
            position = Vector3.zero;
            scale = Vector3.one;
            message = I18N.Get("workflow.position.anchorsRequired");
            if (target == null || target != SelectedPositionTarget() || !_context.Edits.CanEditPrefab())
            {
                message = I18N.Get("workflow.position.unavailable");
                return false;
            }
            if (_positionAvatarBone == null && _positionAvatarReference != HumanBodyBones.LastBone)
            {
                message = I18N.Get("workflow.position.referenceUnavailable");
                return false;
            }
            if (!IsPositionAvatarBone(target, _positionAvatarBone)) return false;
            var head = FindPositionAvatarAnimator(target)?.GetBoneTransform(HumanBodyBones.Head);
            if (head == null || _positionAvatarBone != head)
            {
                message = I18N.Get("workflow.position.geometryUnsupported");
                return false;
            }
            message = I18N.Get("workflow.position.geometryReady");
            if (!fitGeometry) return true;
            if (!ItemAttachmentFitter.TryFit(target, _context.Root.transform, head,
                    transform => PrefabHierarchyUtility.IsInScope(transform, _context.Root.transform, -1, _context.PrefabSiblingIndices),
                    IsPositionSurfaceVisible,
                    out position, out scale))
            {
                message = I18N.Get("workflow.position.geometryUnavailable");
                return false;
            }
            if (!ValidPositionVector(position) || !ValidPositionScale(scale))
            {
                message = I18N.Get("workflow.position.invalidValues");
                return false;
            }
            var original = PrefabUtility.GetCorrespondingObjectFromSource(target);
            var originalScale = original != null ? original.localScale : Vector3.one;
            var percent = AverageVectorComponent(new Vector3(GetScaleRatio(scale.x, originalScale.x),
                GetScaleRatio(scale.y, originalScale.y), GetScaleRatio(scale.z, originalScale.z))) * 100f;
            message = I18N.Get("workflow.position.geometryApplied", percent.ToString("0.#"));
            return true;
        }

        private bool IsPositionSurfaceVisible(Transform transform)
        {
            var path = new List<int>();
            for (var current = transform; current != null && current != _context.Root.transform; current = current.parent)
                path.Add(current.GetSiblingIndex());
            path.Reverse();
            for (var length = 1; length <= path.Count; length++)
                if (_context.HiddenPreviewParts.Contains(GameObjectPreview.GetPartKey(-1, path.GetRange(0, length)))) return false;
            if (path.Count == 0) return !_context.BasePrefabHidden;
            var index = path[0];
            return _context.PrefabSiblingIndices.Contains(index)
                ? !_context.HiddenPrefabSiblingIndices.Contains(index) : !_context.BasePrefabHidden;
        }

        private void ApplyPositionScale(Transform target, Vector3 scale)
        {
            if (!IsPositionAvatarBone(target, _positionAvatarBone)) return;
            var pivot = _positionAvatarBone;
            var localPoint = target.InverseTransformPoint(pivot.position);
            var pivotPosition = target.parent.InverseTransformPoint(pivot.position);
            var position = pivotPosition - target.localRotation * Vector3.Scale(scale, localPoint);
            ApplyPositionTransform(target, position, scale);
        }

        private void ApplyPositionTransform(Transform target, Vector3 position, Vector3 scale)
        {
            if (target == null || target != SelectedPositionTarget() || !_context.Edits.CanEditPrefab() ||
                EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(target) ||
                !ValidPositionVector(position) || !ValidPositionScale(scale) ||
                target.localPosition == position && target.localScale == scale) return;
            Undo.RecordObject(target, I18N.Get("workflow.position.undo"));
            _positionAutoMessage = null;
            target.localPosition = position;
            target.localScale = scale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
            _context.Edits.WorkingSceneDirty = true;
            InvalidateShapeTargets();
            ClearData();
            _context.Host.InvalidateControls(AvatarEditorPanel.Shape);
            _context.Host.InvalidateControls(AvatarEditorPanel.Parts);
            _context.Host.InvalidateControls(AvatarEditorPanel.Material);
            _context.Host.InvalidateMaterialData();
            _context.Edits.Changed();
            _context.Preview?.ReloadTargetPreservingView(_context.Root);
            _context.Host.Repaint();
        }

        private static bool ValidPositionVector(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool ValidPositionScale(Vector3 value) => ValidPositionVector(value) &&
            Mathf.Abs(value.x) >= 0.000001f && Mathf.Abs(value.y) >= 0.000001f && Mathf.Abs(value.z) >= 0.000001f;
    }
}
