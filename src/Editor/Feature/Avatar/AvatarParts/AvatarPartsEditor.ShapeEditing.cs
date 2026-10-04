using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.AvatarEditing;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private void ApplyBodyScale(
            IReadOnlyList<string> targetPaths,
            Vector3 multipliers,
            bool updateAvatarViewPosition = false,
            bool rebuildOnFailure = true)
        {
            if (_context.Root == null ||
                targetPaths == null ||
                targetPaths.Count == 0)
            {
                return;
            }

            var targets = _resolvedBodyScaleTargets;
            targets.Clear();
            foreach (var path in targetPaths)
            {
                var target = string.IsNullOrEmpty(path)
                    ? _context.Root.transform
                    : _context.Root.transform.Find(path);
                if (target != null)
                {
                    targets.Add(new KeyValuePair<string, Transform>(
                        path,
                        target));
                }
            }
            if (targets.Count == 0)
            {
                return;
            }

            var previewScales = _bodyScalePreviewScales;
            previewScales.Clear();
            foreach (var pair in targets)
            {
                previewScales[pair.Key] = Vector3.Scale(
                    GetCachedBaseLocalScale(pair.Key, pair.Value),
                    multipliers);
            }

            if (_bodyScaleDragging)
            {
                _context.Preview?.SetTransformScales(
                    previewScales,
                    recalculateBounds: false);
                _context.Host.Repaint();
                _pendingBodySizeChange = PendingBodySizeChange.Scale;
                _pendingBodyScaleTargetPaths = targetPaths;
                _pendingBodyScaleMultipliers = multipliers;
                _pendingBodyScaleUpdatesViewPosition =
                    updateAvatarViewPosition;
                return;
            }
            if (!_context.Edits.CanEditPrefab())
            {
                return;
            }

            try
            {
                Component avatarDescriptor = null;
                var baseViewPosition = Vector3.zero;
                if (updateAvatarViewPosition &&
                    TryGetWorkingAvatarViewPosition(
                        out avatarDescriptor,
                        out var currentViewPosition))
                {
                    baseViewPosition = GetBaseAvatarViewPosition(
                        avatarDescriptor,
                        currentViewPosition);
                }
                var undoObjects = targets
                    .Select(pair => (UnityEngine.Object)pair.Value)
                    .ToList();
                if (avatarDescriptor != null)
                {
                    undoObjects.Add(avatarDescriptor);
                }
                Undo.RecordObjects(
                    undoObjects.Distinct().ToArray(),
                    I18N.Get("workflow.appearance.sizeUndo"));
                foreach (var pair in targets)
                {
                    var scale = previewScales[pair.Key];
                    pair.Value.localScale = scale;
                    if (PrefabUtility.IsPartOfPrefabInstance(pair.Value))
                    {
                        PrefabUtility
                            .RecordPrefabInstancePropertyModifications(
                                pair.Value);
                    }
                    EditorUtility.SetDirty(pair.Value);
                }
                if (avatarDescriptor != null)
                {
                    var serialized = new SerializedObject(avatarDescriptor);
                    var viewPosition = serialized.FindProperty("ViewPosition");
                    if (viewPosition != null)
                    {
                        viewPosition.vector3Value = Vector3.Scale(
                            baseViewPosition,
                            multipliers);
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (PrefabUtility.IsPartOfPrefabInstance(
                            avatarDescriptor))
                    {
                        PrefabUtility
                            .RecordPrefabInstancePropertyModifications(
                                avatarDescriptor);
                    }
                    EditorUtility.SetDirty(avatarDescriptor);
                }
                EditorUtility.SetDirty(_context.Root);
                _bodyScaleDirty = true;
                _context.Preview?.SetTransformScales(
                    previewScales,
                    recalculateBounds: true);
                SaveBodyScalePrefab(rebuildOnFailure);
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void ApplyBodyBlendShape(
            string rendererPath,
            string shapeName,
            float weight,
            bool rebuildOnFailure = true)
        {
            if (_context.Root == null ||
                string.IsNullOrEmpty(shapeName))
            {
                return;
            }

            if (_bodyScaleDragging)
            {
                _context.Preview?.SetBlendShapeWeight(
                    rendererPath,
                    shapeName,
                    weight,
                    recalculateBounds: false);
                _context.Host.Repaint();
                _pendingBodySizeChange =
                    PendingBodySizeChange.BlendShape;
                _pendingBodyBlendShapeRendererPath = rendererPath;
                _pendingBodyBlendShapeName = shapeName;
                _pendingBodyBlendShapeWeight = weight;
                return;
            }
            if (!_context.Edits.CanEditPrefab())
            {
                return;
            }

            var target = string.IsNullOrEmpty(rendererPath)
                ? _context.Root.transform
                : _context.Root.transform.Find(rendererPath);
            var renderer = target == null
                ? null
                : target.GetComponent<SkinnedMeshRenderer>();
            var shapeIndex = renderer?.sharedMesh == null
                ? -1
                : renderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (renderer == null || shapeIndex < 0)
            {
                return;
            }

            try
            {
                SetBodyBlendShapeRendererWeight(renderer, shapeName, weight);
                EditorUtility.SetDirty(_context.Root);
                _bodyScaleDirty = true;
                _context.Preview?.SetBlendShapeWeight(
                    rendererPath,
                    shapeName,
                    weight,
                    recalculateBounds: false);
                SaveBodyScalePrefab(rebuildOnFailure);
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void ApplyBodyBlendShapeGroup(
            IReadOnlyList<BodyBlendShapeDefinition> definitions,
            float weight,
            bool rebuildOnFailure = true)
        {
            if (definitions == null || definitions.Count == 0 ||
                _context.Root == null)
            {
                return;
            }
            if (_bodyScaleDragging)
            {
                PreviewBodyBlendShapeGroup(definitions, weight);
                _pendingBodySizeChange = PendingBodySizeChange.BlendShape;
                _pendingBodyBlendShapeGroup = definitions;
                _pendingIndividualBlendShape = null;
                _pendingBodyBlendShapeWeight = weight;
                return;
            }
            if (!_context.Edits.CanEditPrefab())
            {
                return;
            }

            try
            {
                var source = ResolveBodyBlendShapeGroupSource(definitions);
                var shapeName = definitions[0].ShapeName;
                var undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(
                    I18N.Get("workflow.appearance.sizeUndo"));
                var targets = new HashSet<SkinnedMeshRenderer>();
                foreach (var definition in definitions)
                {
                    var target = GetBodyBlendShapeRenderer(definition);
                    if (target?.sharedMesh == null)
                    {
                        continue;
                    }
                    var existingSource = ResolveBodyBlendShapeSource(
                        target, definition.ShapeName);
                    var bindingSource = existingSource ?? source;
                    if (bindingSource != null && target != bindingSource)
                    {
                        SetBodyBlendShapeSyncBinding(
                            target,
                            bindingSource,
                            definition.ShapeName,
                            null);
                        targets.Add(bindingSource);
                    }
                    targets.Add(target);
                }
                foreach (var target in targets)
                {
                    SetBodyBlendShapeRendererWeight(
                        target, shapeName, weight);
                }
                Undo.CollapseUndoOperations(undoGroup);
                _bodyScaleDirty = true;
                PreviewBodyBlendShapeGroup(definitions, weight);
                SaveBodyScalePrefab(rebuildOnFailure);
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        public bool PrepareBodyBlendShapeSyncForSave()
        {
            if (_context.Root == null || !_context.Edits.CanEditPrefab())
            {
                return false;
            }
            var undoGroup = -1;
            try
            {
                // New bindings can bring previously excluded meshes into a
                // group. Complete those groups before committing the Prefab.
                while (EnsureBodyBlendShapeSyncBindings(
                           ReadBodyBlendShapes(true), ref undoGroup)) { }
                if (undoGroup >= 0)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                    InvalidateBlendShapes();
                    SaveBodyScalePrefab(false);
                }
                return true;
            }
            catch (Exception exception)
            {
                if (undoGroup >= 0) { Undo.CollapseUndoOperations(undoGroup); }
                ReportBodyScaleFailure(exception, false);
                return false;
            }
        }

        private bool EnsureBodyBlendShapeSyncBindings(
            IReadOnlyList<BodyBlendShapeDefinition> definitions,
            ref int undoGroup)
        {
            var changed = false;
            foreach (var group in definitions
                         .GroupBy(definition => new
                         {
                             definition.Category,
                             definition.ShapeName
                         })
                         .Where(group => group.Count() > 1))
            {
                var members = group.ToArray();
                var source = ResolveBodyBlendShapeGroupSource(members);
                var sourceIndex = source?.sharedMesh == null
                    ? -1
                    : source.sharedMesh.GetBlendShapeIndex(
                        group.Key.ShapeName);
                if (sourceIndex < 0)
                {
                    continue;
                }
                var sourceWeight = source.GetBlendShapeWeight(sourceIndex);
                foreach (var definition in members)
                {
                    var target = GetBodyBlendShapeRenderer(definition);
                    if (target == null || target == source ||
                        HasBodyBlendShapeSyncBinding(
                            target, definition.ShapeName))
                    {
                        continue;
                    }
                    if (undoGroup < 0)
                    {
                        Undo.IncrementCurrentGroup();
                        undoGroup = Undo.GetCurrentGroup();
                        Undo.SetCurrentGroupName(
                            I18N.Get("workflow.appearance.sizeUndo"));
                    }
                    SetBodyBlendShapeSyncBinding(
                        target, source, definition.ShapeName, null);
                    SetBodyBlendShapeRendererWeight(
                        target, definition.ShapeName, sourceWeight);
                    definition.Value = sourceWeight;
                    _bodyScaleDirty = true;
                    changed = true;
                }
            }
            return changed;
        }

        private void ApplyIndividualBodyBlendShape(
            IReadOnlyList<BodyBlendShapeDefinition> group,
            BodyBlendShapeDefinition definition,
            float weight,
            bool rebuildOnFailure = true)
        {
            if (_context.Root == null || definition == null)
            {
                return;
            }
            if (_bodyScaleDragging)
            {
                _context.Preview?.SetBlendShapeWeight(
                    definition.RendererPath,
                    definition.ShapeName,
                    weight,
                    recalculateBounds: false);
                _context.Host.Repaint();
                _pendingBodySizeChange = PendingBodySizeChange.BlendShape;
                _pendingBodyBlendShapeGroup = group;
                _pendingIndividualBlendShape = definition;
                _pendingBodyBlendShapeWeight = weight;
                return;
            }
            if (!_context.Edits.CanEditPrefab())
            {
                return;
            }

            try
            {
                var target = GetBodyBlendShapeRenderer(definition);
                if (target?.sharedMesh == null)
                {
                    return;
                }
                var source = ResolveBodyBlendShapeSource(
                    target, definition.ShapeName) ??
                    ResolveBodyBlendShapeGroupSource(group);
                var editsSource = source == target;
                if (source != null && source != target)
                {
                    var sourceIndex = source.sharedMesh.GetBlendShapeIndex(
                        definition.ShapeName);
                    if (sourceIndex >= 0)
                    {
                        SetBodyBlendShapeSyncBinding(
                            target,
                            source,
                            definition.ShapeName,
                            BuildIndividualBodyBlendShapeRemap(
                                source.GetBlendShapeWeight(sourceIndex),
                                weight));
                    }
                }
                SetBodyBlendShapeRendererWeight(
                    target, definition.ShapeName, weight);
                _bodyScaleDirty = true;
                _context.Preview?.SetBlendShapeWeight(
                    definition.RendererPath,
                    definition.ShapeName,
                    weight,
                    recalculateBounds: false);
                SaveBodyScalePrefab(rebuildOnFailure);
                if (editsSource && _context.UiRoot.panel != null)
                {
                    _context.UiRoot.schedule.Execute(() =>
                    {
                        _context.Host.InvalidateControls(AvatarEditorPanel.Shape);
                        _context.Host.ShowParts();
                    });
                }
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void PreviewBodyBlendShapeGroup(
            IReadOnlyList<BodyBlendShapeDefinition> definitions,
            float weight)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                paths.Add(definition.RendererPath);
            }
            var source = ResolveBodyBlendShapeGroupSource(definitions);
            if (source != null)
            {
                paths.Add(AnimationUtility.CalculateTransformPath(
                    source.transform, _context.Root.transform));
            }
            foreach (var path in paths)
            {
                _context.Preview?.SetBlendShapeWeight(
                    path,
                    definitions[0].ShapeName,
                    weight,
                    recalculateBounds: false);
            }
            _context.Host.Repaint();
        }

        private void SetBodyBlendShapeRendererWeight(
            SkinnedMeshRenderer renderer,
            string shapeName,
            float weight)
        {
            var index = renderer?.sharedMesh == null
                ? -1
                : renderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (index < 0 || Mathf.Approximately(
                    renderer.GetBlendShapeWeight(index), weight))
            {
                return;
            }
            Undo.RecordObject(renderer,
                I18N.Get("workflow.appearance.sizeUndo"));
            renderer.SetBlendShapeWeight(index, weight);
            if (PrefabUtility.IsPartOfPrefabInstance(renderer))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    renderer);
            }
            EditorUtility.SetDirty(renderer);
        }

        private void SetBodyBlendShapeSyncBinding(
            SkinnedMeshRenderer target,
            SkinnedMeshRenderer source,
            string shapeName,
            AnimationCurve remap)
        {
            var sync = target.GetComponent<ModularAvatarBlendshapeSync>();
            if (sync == null)
            {
                sync = Undo.AddComponent<ModularAvatarBlendshapeSync>(
                    target.gameObject);
            }
            Undo.RecordObject(sync,
                I18N.Get("workflow.appearance.sizeUndo"));
            if (sync.Bindings == null)
            {
                sync.Bindings = new List<BlendshapeBinding>();
            }
            var index = sync.Bindings.FindIndex(binding =>
                (string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape) == shapeName);
            var bindingValue = index >= 0
                ? sync.Bindings[index]
                : new BlendshapeBinding
                {
                    ReferenceMesh = new AvatarObjectReference(),
                    Blendshape = shapeName,
                    LocalBlendshape = string.Empty
                };
            if (index < 0)
            {
                bindingValue.ReferenceMesh.Set(source.gameObject);
            }
            bindingValue.RemapCurveIsValid = true;
            bindingValue.RemapCurve = remap ??
                AnimationCurve.Linear(0f, 0f, 100f, 100f);
            if (index >= 0)
            {
                sync.Bindings[index] = bindingValue;
            }
            else
            {
                sync.Bindings.Add(bindingValue);
            }
            if (PrefabUtility.IsPartOfPrefabInstance(sync))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(sync);
            }
            EditorUtility.SetDirty(sync);
        }

        private static AnimationCurve BuildIndividualBodyBlendShapeRemap(
            float sourceWeight,
            float targetWeight)
        {
            var source = Mathf.Clamp(sourceWeight, 0f, 100f);
            var target = Mathf.Clamp(targetWeight, 0f, 100f);
            if (source <= 0f)
            {
                return AnimationCurve.Linear(0f, target, 100f, 100f);
            }
            if (source >= 100f)
            {
                return AnimationCurve.Linear(0f, 0f, 100f, target);
            }
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(source, target),
                new Keyframe(100f, 100f));
        }

    }
}
