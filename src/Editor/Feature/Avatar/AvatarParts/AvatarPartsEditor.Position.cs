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
        private Transform _positionDragTarget;
        private Transform _positionPendingTarget;
        private Transform _positionExclusionsRoot;
        private readonly Dictionary<Transform, HashSet<Transform>> _positionExcludedItems =
            new Dictionary<Transform, HashSet<Transform>>();
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

            if (_positionExclusionsRoot != _context.Root.transform)
            {
                _positionExclusionsRoot = _context.Root.transform;
                _positionExcludedItems.Clear();
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
            var sourcePosition = source != null ? source.localPosition : Vector3.zero;
            var sourceScale = source != null ? source.localScale : Vector3.one;
            var adjustments = ItemPositionAdjustmentState.instance;
            ItemPositionAdjustmentState.Baseline Baseline() => adjustments.Get(target, sourcePosition, sourceScale);
            void SetBaseline(Vector3 position, Vector3 scale) => adjustments.Set(Baseline(), position, scale);
            var renderers = new List<Action>();
            var renderedPosition = target.localPosition;
            var renderedScale = target.localScale;
            var renderedBasePosition = Baseline().Position;
            var renderedBaseScale = Baseline().Scale;
            UiButton restore = null;
            void Render()
            {
                if (target == null) return;
                foreach (var render in renderers) render();
                renderedPosition = target.localPosition;
                renderedScale = target.localScale;
                renderedBasePosition = Baseline().Position;
                renderedBaseScale = Baseline().Scale;
                restore?.SetEnabled(target.localPosition != sourcePosition || target.localScale != sourceScale ||
                    renderedBasePosition != sourcePosition || renderedBaseScale != sourceScale);
            }

            content.Add(BuildPositionAutoControls(target, Render,
                () => SetBaseline(target.localPosition, target.localScale), out var refreshAuto));
            renderers.Add(refreshAuto);
            content.Add(UiTextFactory.Create(I18N.Get("workflow.position.offset"), UiClassNames.SectionTitle));
            for (var axis = 0; axis < 3; axis++)
            {
                var index = axis;
                var direction = axis == 0 ? -1f : 1f;
                content.Add(BuildPositionScalar(I18N.Get("workflow.position.axis." + "xyz"[axis]),
                    -50f, 50f, "cm", () => (target.localPosition[index] - Baseline().Position[index]) * 100f * direction,
                    value =>
                    {
                        var position = target.localPosition;
                        position[index] = Baseline().Position[index] + value / 100f * direction;
                        ApplyPositionTransform(target, position, target.localScale);
                        Render();
                    }, renderers, 0f));
            }

            content.Add(UiTextFactory.Create(I18N.Get("workflow.position.scale"), UiClassNames.SectionTitle));
            void Scale(Vector3 scale)
            {
                var previousPosition = target.localPosition;
                ApplyPositionScale(target, scale);
                var baseline = Baseline();
                SetBaseline(baseline.Position + (target.localPosition - previousPosition), baseline.Scale);
            }
            Vector3 Multipliers() => new Vector3(GetScaleRatio(target.localScale.x, Baseline().Scale.x),
                GetScaleRatio(target.localScale.y, Baseline().Scale.y), GetScaleRatio(target.localScale.z, Baseline().Scale.z));
            content.Add(BuildPositionScalar(I18N.Get("workflow.position.uniform"), 50f, 200f, "%",
                () => AverageVectorComponent(Multipliers()) * 100f,
                value =>
                {
                    if (value > 0f) Scale(Baseline().Scale * (value / 100f));
                    Render();
                }, renderers, 100f, true, () => target.localScale == Baseline().Scale));
            for (var axis = 0; axis < 3; axis++)
            {
                var index = axis;
                content.Add(BuildPositionScalar(GetScaleAxisName(axis), 50f, 200f, "%",
                    () => Multipliers()[index] * 100f,
                    value =>
                    {
                        var scale = target.localScale;
                        scale[index] = Baseline().Scale[index] * (value / 100f);
                        if (value > 0f) Scale(scale);
                        Render();
                    }, renderers, 100f, true));
            }
            restore = new UiButton(I18N.Get("workflow.position.reset"), () =>
            {
                if (target != SelectedPositionTarget() || EditorApplication.isPlayingOrWillChangePlaymode) return;
                Undo.IncrementCurrentGroup();
                ApplyPositionTransform(target, sourcePosition, sourceScale);
                SetBaseline(sourcePosition, sourceScale);
                _positionAutoMessage = null;
                Render();
            }, icon: FluentUiIcons.CreateState("arrow_clockwise.png", UiSizeTokens.Size14))
            { name = "ee4v-position-restore" };
            restore.AddToClassList("ee4v-item-position__restore");
            content.Add(restore);
            var attachments = new VisualElement { name = "ee4v-position-attachments" };
            attachments.AddToClassList("ee4v-part-attachments-section");
            attachments.Add(UiTextFactory.Create(I18N.Get("workflow.attachment.title"), UiClassNames.SectionTitle,
                "ee4v-part-attachments-section__title"));
            attachments.Add(BuildAttachmentControls(target.GetSiblingIndex()));
            content.Add(attachments);
            content.schedule.Execute(() =>
            {
                if (target != null && (target.localPosition != renderedPosition || target.localScale != renderedScale ||
                    Baseline().Position != renderedBasePosition || Baseline().Scale != renderedBaseScale)) Render();
            }).Every(200);
            Render();
            return content;
        }

        private VisualElement BuildPositionScalar(string label, float minimum, float maximum, string unit,
            Func<float> read, Action<float> apply, ICollection<Action> renderers, float resetValue,
            bool requiresReference = false, Func<bool> isAtReset = null)
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
                input.ResetButton.SetEnabled(isAtReset != null ? !isAtReset() : !Mathf.Approximately(value, resetValue));
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
                if (undoGroup < 0) return;
                _positionDragTarget = null;
                CommitPositionTransform();
                if (undoGroup >= 0 && Undo.GetCurrentGroup() == undoGroup) Undo.CollapseUndoOperations(undoGroup);
                undoGroup = -1;
            }
            input.Slider.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                _positionDragTarget = target;
            }, TrickleDown.TrickleDown);
            input.Slider.RegisterCallback<PointerUpEvent>(_ => EndDrag());
            input.Slider.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            row.RegisterCallback<DetachFromPanelEvent>(_ => EndDrag());
            renderers.Add(Render);
            return row;
        }

        private VisualElement BuildPositionAutoControls(Transform target,
            Action render, Action adjusted, out Action refresh)
        {
            var panel = new VisualElement();
            panel.AddToClassList("ee4v-item-position__auto");
            var header = new SectionHeader(I18N.Get("workflow.position.autoTitle"))
            { name = "ee4v-position-auto-header" };
            panel.Add(header);
            UiButton apply = null;
            Action refreshReference = null;
            var explanation = UiTextFactory.Create(string.Empty,
                UiClassNames.SecondaryText, "ee4v-item-position__description");
            explanation.name = "ee4v-position-auto-message";
            void RefreshAvailability()
            {
                var available = TryCalculatePositionAdjustment(target, out _, out _, out var message);
                if (!string.IsNullOrEmpty(_positionAutoMessage)) message = _positionAutoMessage;
                apply?.SetPrimaryActionEnabled(available);
                explanation.SetText(message);
                explanation.EnableInClassList("ee4v-modification-workflow__hidden", string.IsNullOrEmpty(message));
                refreshReference?.Invoke();
            }
            UnityEditor.UIElements.ObjectField AddBoneField(VisualElement host, Func<Transform> read,
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
                var form = new FormInput(field);
                form.AddToClassList("ee4v-item-position__reference");
                host.Add(form);
                return field;
            }
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
                adjusted();
                _positionAutoMessage = message;
                render();
            }) { name = "ee4v-position-auto-apply" };
            header.Actions.Add(apply);
            string ReferenceLabel(HumanBodyBones bone) => I18N.Get("workflow.position.references." + bone);
            var referencePicker = UiTextFactory.CreatePopupField(string.Empty, PositionAvatarReferences,
                PositionAvatarReferences.IndexOf(_positionAvatarReference), ReferenceLabel, ReferenceLabel);
            referencePicker.name = "ee4v-position-avatar-reference";
            var referenceForm = new FormInput(I18N.Get("workflow.position.target"), referencePicker);
            referenceForm.AddToClassList("ee4v-item-position__reference");
            panel.Add(referenceForm);
            var customReference = new VisualElement { name = "ee4v-position-custom-reference" };
            var avatar = AddBoneField(customReference, () => _positionCustomAvatarBone,
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
            var exclusions = BuildPositionExclusions(target, render);
            if (exclusions != null) panel.Add(exclusions);
            panel.Add(explanation);
            refresh = RefreshAvailability;
            RefreshAvailability();
            return panel;
        }

        private VisualElement BuildPositionExclusions(Transform target, Action render)
        {
            var root = _context.Root.transform;
            var objects = root.GetComponentsInChildren<Transform>(true)
                .Where(item => item != root && !item.IsChildOf(target)).ToArray();
            if (objects.Length == 0) return null;
            if (!_positionExcludedItems.TryGetValue(target, out var excluded))
            {
                excluded = ItemFittingExclusionStore.instance.Read(_context.PrefabAsset, root, target);
                _positionExcludedItems.Add(target, excluded);
            }
            excluded.RemoveWhere(item => item == null || item == root || !item.IsChildOf(root) || item.IsChildOf(target));
            var section = new VisualElement { name = "ee4v-position-exclusions" };
            var header = new SectionHeader(I18N.Get("workflow.position.exclusionsTitle"))
            { name = "ee4v-position-exclusions-header" };
            header.AddToClassList("ee4v-item-position__exclusions-header");
            section.Add(header);
            var list = new VisualElement { name = "ee4v-position-excluded-items" };
            section.Add(list);
            UiButton add = null;
            bool IsExcluded(Transform item)
            {
                for (var current = item; current != null && current != root; current = current.parent)
                    if (excluded.Contains(current)) return true;
                return false;
            }
            Transform[] Available() => root.GetComponentsInChildren<Transform>(true)
                .Where(item => item != root && !item.IsChildOf(target) && !IsExcluded(item)).ToArray();
            string ObjectPath(Transform item) => AnimationUtility.CalculateTransformPath(item, root);
            IReadOnlyList<SearchableTreeItemData<Transform>> BuildTree()
            {
                var available = new HashSet<Transform>(Available());
                SearchableTreeItemData<Transform> Build(Transform item)
                {
                    var children = new List<SearchableTreeItemData<Transform>>();
                    foreach (Transform child in item)
                        if (available.Contains(child)) children.Add(Build(child));
                    var path = ObjectPath(item);
                    return new SearchableTreeItemData<Transform>(item.GetInstanceID(), item, path, path, children);
                }
                var roots = new List<SearchableTreeItemData<Transform>>();
                foreach (Transform child in root)
                    if (available.Contains(child)) roots.Add(Build(child));
                return roots;
            }
            void RefreshList()
            {
                list.Clear();
                foreach (var item in objects.Where(item => item != null && excluded.Contains(item)))
                {
                    var parentPath = item.parent != root ? ObjectPath(item.parent) : string.Empty;
                    var row = new ItemRow(new ItemRowState(item.name, parentPath, layout: ItemRowLayout.Stacked))
                    { userData = item, tooltip = ObjectPath(item) };
                    row.AddToClassList("ee4v-item-position__exclusion-row");
                    row.TitleText.SetWhiteSpace(WhiteSpace.Normal);
                    row.DescriptionText.SetWhiteSpace(WhiteSpace.Normal);
                    row.Trailing.Add(new UiButton(string.Empty, () =>
                    {
                        if (target != SelectedPositionTarget()) return;
                        excluded.Remove(item);
                        ItemFittingExclusionStore.instance.Write(_context.PrefabAsset, root, target, excluded);
                        _positionAutoMessage = null;
                        RefreshList();
                        render();
                    }, tooltip: I18N.Get("workflow.position.exclusionsRemove"),
                        icon: FluentUiIcons.CreateState("dismiss.png", UiSizeTokens.Size14),
                        variant: UiButtonVariant.Ghost) { name = "ee4v-position-exclusion-remove" });
                    list.Add(row);
                }
                var available = Available().Length > 0;
                add?.SetEnabled(available);
                if (add != null) add.tooltip = I18N.Get(available
                    ? "workflow.position.exclusionsAdd" : "workflow.position.exclusionsAllAdded");
            }
            add = new UiButton(string.Empty, () =>
            {
                if (target != SelectedPositionTarget()) return;
                ObjectSelector<Transform>.ShowHierarchyPicker(add, BuildTree(), selected =>
                {
                    if (target != SelectedPositionTarget()) return;
                    var available = new HashSet<Transform>(Available());
                    excluded.UnionWith(selected.Where(item => item != null && available.Contains(item)));
                    var redundant = excluded.Where(item => excluded.Any(parent => parent != item && item.IsChildOf(parent))).ToArray();
                    excluded.ExceptWith(redundant);
                    ItemFittingExclusionStore.instance.Write(_context.PrefabAsset, root, target, excluded);
                    _positionAutoMessage = null;
                    RefreshList();
                    render();
                });
            }, tooltip: I18N.Get("workflow.position.exclusionsAdd"),
                icon: FluentUiIcons.CreateState("add.png", UiSizeTokens.Size14),
                variant: UiButtonVariant.Ghost) { name = "ee4v-position-exclusion-add" };
            header.Actions.Add(add);
            RefreshList();
            return section;
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
            if (target == null || target != SelectedPositionTarget() || !_context.Edits.CanEditPrefab() ||
                EditorApplication.isPlayingOrWillChangePlaymode)
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
            message = null;
            if (!fitGeometry) return true;
            if (!ItemAttachmentFitter.TryFit(target, _context.Root.transform, head,
                    transform => PrefabHierarchyUtility.IsInScope(transform, _context.Root.transform, -1, _context.PrefabSiblingIndices),
                    transform => IsPositionSurfaceVisible(target, transform),
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
            return true;
        }

        private bool IsPositionSurfaceVisible(Transform target, Transform transform)
        {
            if (_positionExcludedItems.TryGetValue(target, out var excluded))
                for (var current = transform; current != null && current != _context.Root.transform; current = current.parent)
                    if (excluded.Contains(current)) return false;
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
            _positionPendingTarget = target;
            if (_positionDragTarget == target)
            {
                _context.Preview?.RefreshTransforms(recalculateBounds: false);
                _context.Host.Repaint();
            }
            else CommitPositionTransform();
        }

        private void CommitPositionTransform()
        {
            var target = _positionPendingTarget;
            _positionPendingTarget = null;
            if (target == null) return;
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
            _context.Preview?.RefreshTransforms();
            _context.Host.Repaint();
        }

        private static bool ValidPositionVector(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool ValidPositionScale(Vector3 value) => ValidPositionVector(value) &&
            Mathf.Abs(value.x) >= 0.000001f && Mathf.Abs(value.y) >= 0.000001f && Mathf.Abs(value.z) >= 0.000001f;
    }
}
