using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private Transform _positionTarget;
        private Transform _positionSourceBone;
        private Transform _positionAvatarBone;
        private bool _positionMatchScale;
        private bool _positionUseGeometry = true;
        private string _positionAutoMessage;

        private sealed class PositionBonePair
        {
            internal Transform Source;
            internal Transform Target;
        }

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

            var pairs = FindPositionBonePairs(target);
            if (_positionTarget != target)
            {
                _positionTarget = target;
                var first = pairs.FirstOrDefault();
                var head = FindPositionAvatarAnimator(target)?.GetBoneTransform(HumanBodyBones.Head);
                _positionSourceBone = first?.Source ?? target;
                _positionAvatarBone = first?.Target ?? head;
                _positionMatchScale = true;
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

            content.Add(BuildPositionAutoControls(target, pairs, Render, out var refreshAuto));
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
                }, renderers, 100f));
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
                    }, renderers, 100f));
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
            Func<float> read, Action<float> apply, ICollection<Action> renderers, float resetValue)
        {
            var row = CreateSizeControlRow(label, out var controls);
            var target = SelectedPositionTarget();
            var input = new ScalarInput(minimum, maximum, I18N.Get("workflow.appearance.sizeReset"), null, unit);
            input.ValueField.isDelayed = true;
            controls.Add(input);
            void Render()
            {
                if (target == null) return;
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

        private VisualElement BuildPositionAutoControls(Transform target, IReadOnlyList<PositionBonePair> pairs,
            Action render, out Action refresh)
        {
            var panel = new VisualElement();
            panel.AddToClassList("ee4v-item-position__auto");
            panel.Add(UiTextFactory.Create(I18N.Get("workflow.position.autoTitle"), UiClassNames.SectionTitle));
            panel.Add(UiTextFactory.Create(I18N.Get("workflow.position.autoHint"),
                UiClassNames.SecondaryText, "ee4v-item-position__description"));
            UiButton apply = null;
            Action refreshPicker = null;
            var explanation = UiTextFactory.Create(string.Empty,
                UiClassNames.SecondaryText, "ee4v-item-position__description");
            void RefreshAvailability()
            {
                var available = TryCalculatePositionAdjustment(target, pairs, out _, out _, out var message);
                if (available && _positionUseGeometry && _positionAvatarBone != null &&
                    _positionAvatarBone == FindPositionAvatarAnimator(target)?.GetBoneTransform(HumanBodyBones.Head))
                    message = I18N.Get("workflow.position.geometryReady");
                if (!string.IsNullOrEmpty(_positionAutoMessage)) message = _positionAutoMessage;
                apply?.SetPrimaryActionEnabled(available);
                explanation.SetText(message);
                explanation.EnableInClassList("ee4v-modification-workflow__hidden", string.IsNullOrEmpty(message));
                refreshPicker?.Invoke();
            }
            UnityEditor.UIElements.ObjectField AddBoneField(VisualElement host, string label, Func<Transform> read,
                Action<Transform> set, bool itemSide)
            {
                var field = UiTextFactory.CreateObjectField();
                field.objectType = typeof(Transform);
                field.allowSceneObjects = true;
                field.SetValueWithoutNotify(read());
                field.RegisterValueChangedCallback(evt =>
                {
                    var bone = evt.newValue as Transform;
                    if (bone != null && !(itemSide ? bone.IsChildOf(target) : IsPositionAvatarBone(target, bone)))
                    {
                        field.SetValueWithoutNotify(read());
                        return;
                    }
                    set(bone);
                    _positionAutoMessage = null;
                    RefreshAvailability();
                });
                host.Add(new FormInput(I18N.Get(label), field));
                return field;
            }
            var source = AddBoneField(panel, "workflow.position.source", () => _positionSourceBone,
                bone => _positionSourceBone = bone, true);
            var avatar = AddBoneField(panel, "workflow.position.target", () => _positionAvatarBone,
                bone => _positionAvatarBone = bone, false);
            var matchScale = UiTextFactory.CreateToggle(I18N.Get("workflow.position.matchScale"));
            matchScale.SetValueWithoutNotify(_positionMatchScale);
            matchScale.RegisterValueChangedCallback(evt => { _positionMatchScale = evt.newValue; _positionAutoMessage = null; RefreshAvailability(); });
            panel.Add(matchScale);
            var methods = new List<string> { I18N.Get("workflow.position.geometryMethod"), I18N.Get("workflow.position.boneMethod") };
            var methodPicker = UiTextFactory.CreatePopupField(string.Empty, methods, _positionUseGeometry ? 0 : 1);
            methodPicker.RegisterValueChangedCallback(evt =>
            {
                _positionUseGeometry = evt.newValue == methods[0];
                _positionAutoMessage = null;
                RefreshAvailability();
            });
            panel.Insert(2, new FormInput(I18N.Get("workflow.position.method"), methodPicker));
            if (pairs.Count > 0)
            {
                var manual = new PositionBonePair();
                var options = new[] { manual }.Concat(pairs).ToList();
                PositionBonePair Selected() => pairs.FirstOrDefault(pair =>
                    pair.Source == _positionSourceBone && pair.Target == _positionAvatarBone) ?? manual;
                var index = options.IndexOf(Selected());
                string Label(PositionBonePair pair) => pair == manual ? I18N.Get("workflow.position.custom")
                    : pair.Target.name + " ← " + pair.Source.name;
                var picker = UiTextFactory.CreatePopupField(string.Empty, options, index, Label, Label);
                refreshPicker = () => picker.SetValueWithoutNotify(Selected());
                picker.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue == manual) { RefreshAvailability(); return; }
                    _positionSourceBone = evt.newValue.Source;
                    _positionAvatarBone = evt.newValue.Target;
                    _positionAutoMessage = null;
                    source.SetValueWithoutNotify(_positionSourceBone);
                    avatar.SetValueWithoutNotify(_positionAvatarBone);
                    RefreshAvailability();
                });
                panel.Insert(3, new FormInput(I18N.Get("workflow.position.bonePair"), picker));
            }
            panel.Add(explanation);
            apply = new UiButton(I18N.Get("workflow.position.auto"), () =>
            {
                if (!TryCalculatePositionAdjustment(target, pairs, out var position, out var scale, out var message, true)) return;
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

        private IReadOnlyList<PositionBonePair> FindPositionBonePairs(Transform target)
        {
            var mappings = new Dictionary<Transform, Transform>();
            foreach (var merge in target.GetComponentsInChildren<ModularAvatarMergeArmature>(true))
            {
                var bones = merge.GetBonesMapping();
                if (bones == null) continue;
                foreach (var pair in bones)
                    if (pair.Item2 != null && pair.Item2.IsChildOf(target) && IsPositionAvatarBone(target, pair.Item1))
                        mappings[pair.Item1] = pair.Item2;
            }
            foreach (var proxy in target.GetComponentsInChildren<ModularAvatarBoneProxy>(true))
                if (IsPositionAvatarBone(target, proxy.target) && !mappings.ContainsKey(proxy.target))
                    mappings[proxy.target] = proxy.transform;
            var animator = FindPositionAvatarAnimator(target);
            if (animator != null)
            {
                var itemAnimator = target.GetComponentsInChildren<Animator>(true)
                    .FirstOrDefault(candidate => candidate.avatar != null && candidate.avatar.isValid && candidate.isHuman);
                var armature = FindPartArmature(target);
                var named = (armature != null ? armature.GetComponentsInChildren<Transform>(true)
                    : target.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(renderer => renderer.bones))
                    .Where(bone => bone != null && bone != target && bone.IsChildOf(target)).Distinct()
                    .GroupBy(bone => bone.name, StringComparer.Ordinal).Where(group => group.Count() == 1)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
                for (var index = 0; index < (int)HumanBodyBones.LastBone; index++)
                {
                    var bone = (HumanBodyBones)index;
                    var avatarBone = animator.GetBoneTransform(bone);
                    if (!IsPositionAvatarBone(target, avatarBone) || mappings.ContainsKey(avatarBone)) continue;
                    var itemBone = itemAnimator != null ? itemAnimator.GetBoneTransform(bone) : null;
                    if (itemBone == null) named.TryGetValue(avatarBone.name, out itemBone);
                    if (itemBone != null && itemBone.IsChildOf(target)) mappings[avatarBone] = itemBone;
                }
            }
            var hips = animator?.GetBoneTransform(HumanBodyBones.Hips);
            var head = animator?.GetBoneTransform(HumanBodyBones.Head);
            return mappings.OrderBy(pair => pair.Key == hips ? 0 : pair.Key == head ? 1 : 2)
                .ThenBy(pair => AnimationUtility.CalculateTransformPath(pair.Key, _context.Root.transform), StringComparer.Ordinal)
                .Select(pair => new PositionBonePair { Source = pair.Value, Target = pair.Key }).ToArray();
        }

        private bool TryCalculatePositionAdjustment(Transform target, IReadOnlyList<PositionBonePair> pairs,
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
            if (_positionSourceBone == null || !_positionSourceBone.IsChildOf(target) ||
                !IsPositionAvatarBone(target, _positionAvatarBone)) return false;
            var head = FindPositionAvatarAnimator(target)?.GetBoneTransform(HumanBodyBones.Head);
            if (fitGeometry && _positionUseGeometry && head != null && _positionAvatarBone == head &&
                ItemAttachmentFitter.TryFit(target, _context.Root.transform, head,
                    transform => PrefabHierarchyUtility.IsInScope(transform, _context.Root.transform, -1, _context.PrefabSiblingIndices),
                    IsPositionSurfaceVisible,
                    _positionMatchScale, out position, out scale) && ValidPositionVector(position) && ValidPositionScale(scale))
            {
                var original = PrefabUtility.GetCorrespondingObjectFromSource(target);
                var originalScale = original != null ? original.localScale : Vector3.one;
                var percent = AverageVectorComponent(new Vector3(GetScaleRatio(scale.x, originalScale.x),
                    GetScaleRatio(scale.y, originalScale.y), GetScaleRatio(scale.z, originalScale.z))) * 100f;
                message = I18N.Get("workflow.position.geometryApplied", percent.ToString("0.#"));
                return true;
            }
            message = string.Empty;
            var factor = 1f;
            if (_positionMatchScale)
            {
                if (TryEstimatePositionScale(target, pairs, out var reference, out var sourceDistance, out var avatarDistance))
                {
                    factor = avatarDistance / sourceDistance;
                    message = I18N.Get("workflow.position.scaleEstimated", _positionSourceBone.name, reference.Source.name,
                        (sourceDistance * 100f).ToString("0.##"), (avatarDistance * 100f).ToString("0.##"));
                }
                else message = I18N.Get("workflow.position.scaleUnavailable");
            }
            if (fitGeometry && _positionUseGeometry)
                message = I18N.Get("workflow.position.geometryFallback") + (string.IsNullOrEmpty(message) ? string.Empty : "\n" + message);
            scale = target.localScale * factor;
            position = target.parent.InverseTransformPoint(_positionAvatarBone.position -
                (_positionSourceBone.position - target.position) * factor);
            if (ValidPositionVector(position) && ValidPositionScale(scale)) return true;
            message = I18N.Get("workflow.position.invalidValues");
            return false;
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

        private bool TryEstimatePositionScale(Transform target, IReadOnlyList<PositionBonePair> pairs,
            out PositionBonePair reference, out float sourceDistance, out float avatarDistance)
        {
            reference = null;
            sourceDistance = 0f;
            avatarDistance = 0f;
            var bestScore = int.MaxValue;
            foreach (var pair in pairs)
            {
                if (pair.Source == null || !pair.Source.IsChildOf(target) ||
                    !IsPositionAvatarBone(target, pair.Target)) continue;
                var sourceDepth = PositionBoneDepth(pair.Source, _positionSourceBone);
                var avatarDepth = PositionBoneDepth(pair.Target, _positionAvatarBone);
                var child = sourceDepth > 0 && avatarDepth > 0;
                if (!child)
                {
                    sourceDepth = PositionBoneDepth(_positionSourceBone, pair.Source);
                    avatarDepth = PositionBoneDepth(_positionAvatarBone, pair.Target);
                    if (sourceDepth <= 0 || avatarDepth <= 0) continue;
                }
                var score = (sourceDepth + avatarDepth) * 2 + (child ? 0 : 1);
                if (score >= bestScore) continue;
                var sourceLength = Vector3.Distance(_positionSourceBone.position, pair.Source.position);
                var avatarLength = Vector3.Distance(_positionAvatarBone.position, pair.Target.position);
                if (!Finite(sourceLength) || !Finite(avatarLength) || sourceLength < 0.000001f || avatarLength < 0.000001f)
                    continue;
                reference = pair;
                sourceDistance = sourceLength;
                avatarDistance = avatarLength;
                bestScore = score;
            }
            return reference != null;
        }

        private static int PositionBoneDepth(Transform bone, Transform ancestor)
        {
            var depth = 0;
            for (var current = bone; current != null; current = current.parent, depth++)
                if (current == ancestor) return depth;
            return -1;
        }

        private void ApplyPositionScale(Transform target, Vector3 scale)
        {
            var pivot = _positionSourceBone != null && _positionSourceBone.IsChildOf(target) ? _positionSourceBone : target;
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
