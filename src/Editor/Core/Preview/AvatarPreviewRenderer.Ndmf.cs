using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ee4v.Core.Preview
{
    public sealed partial class AvatarPreviewRenderer
    {
        private sealed class PreviewFilter : IRenderFilter
        {
            private readonly AvatarPreviewRenderer _owner;
            internal PreviewFilter(AvatarPreviewRenderer owner) => _owner = owner;
            public bool CanEnableRenderers => true;
            public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
            {
                if (_owner._disposed || _owner.Root == null) return ImmutableList<RenderGroup>.Empty;
                var renderers = context.GetComponentsInChildren<Renderer>(_owner.Root, true)
                    .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer)
                    .ToArray();
                return renderers.Length == 0 ? ImmutableList<RenderGroup>.Empty : ImmutableList.Create(RenderGroup.For(renderers));
            }
            public Task<IRenderFilterNode> Instantiate(RenderGroup group,
                IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
                => Task.FromResult<IRenderFilterNode>(new PreviewNode(_owner));
        }

        private sealed class PreviewNode : IRenderFilterNode
        {
            private readonly AvatarPreviewRenderer _owner;
            private readonly Dictionary<Transform, Transform> _bones = new Dictionary<Transform, Transform>();
            private readonly Dictionary<Transform, Transform> _adjustedBones = new Dictionary<Transform, Transform>();
            private readonly Dictionary<SkinnedMeshRenderer, Transform[]> _boneArrays = new Dictionary<SkinnedMeshRenderer, Transform[]>();
            private readonly Dictionary<Transform, (Transform Parent, Vector3 Position, Quaternion Rotation, Vector3 Scale,
                Vector3 OutputPosition, Quaternion OutputRotation, Vector3 OutputScale)> _transforms =
                new Dictionary<Transform, (Transform, Vector3, Quaternion, Vector3, Vector3, Quaternion, Vector3)>();
            private readonly Dictionary<Transform, (Transform Parent, Transform OutputParent, Vector3 Position, Quaternion Rotation, Vector3 Scale)> _meshParents =
                new Dictionary<Transform, (Transform, Transform, Vector3, Quaternion, Vector3)>();
            private Scene _boneScene;
            private GameObject _boneRoot;
            internal PreviewNode(AvatarPreviewRenderer owner) => _owner = owner;
            public RenderAspects WhatChanged => RenderAspects.Shapes;
            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (_owner._disposed || original == null || proxy == null) return;
                if (_owner._rendering) _owner._frameProxies[original] = proxy;
                if (EditorApplication.isPlaying) return;
                RestoreTransform(proxy.transform);
                if (_owner._activeStates.Count > 0)
                {
                    var current = original.transform;
                    var active = true;
                    var changed = false;
                    while (current != null)
                    {
                        var overridden = _owner._activeStates.TryGetValue(current.gameObject, out var value);
                        active &= overridden ? value : current.gameObject.activeSelf;
                        changed |= overridden;
                        if (current == _owner.Root.transform) break;
                        current = current.parent;
                    }
                    if (changed) proxy.enabled = original.enabled && active;
                }
                if (proxy is SkinnedMeshRenderer skinned && _owner._shapes.TryGetValue((SkinnedMeshRenderer)original, out var shapes))
                {
                    foreach (var pair in shapes)
                    {
                        var index = skinned.sharedMesh == null ? -1 : skinned.sharedMesh.GetBlendShapeIndex(pair.Key);
                        if (index >= 0) skinned.SetBlendShapeWeight(index, pair.Value);
                    }
                    skinned.forceMatrixRecalculationPerRender = true;
                }
                if (_owner._scales.Count == 0) return;
                _boneScene = proxy.gameObject.scene;
                var desired = GetBone(original.transform);
                UpdateBones();
                if (proxy is MeshRenderer)
                {
                    var relative = original.transform.worldToLocalMatrix * proxy.transform.localToWorldMatrix;
                    _meshParents[proxy.transform] = (proxy.transform.parent, desired,
                        proxy.transform.localPosition, proxy.transform.localRotation, proxy.transform.localScale);
                    proxy.transform.SetParent(desired, false);
                    SetLocalMatrix(proxy.transform, relative);
                    return;
                }
                var correction = desired.localToWorldMatrix * original.transform.worldToLocalMatrix;
                var parent = proxy.transform.parent;
                var position = proxy.transform.localPosition;
                var rotation = proxy.transform.localRotation;
                var scale = proxy.transform.localScale;
                SetMatrix(proxy.transform, correction * proxy.transform.localToWorldMatrix);
                _transforms[proxy.transform] = (parent, position, rotation, scale,
                    proxy.transform.localPosition, proxy.transform.localRotation, proxy.transform.localScale);
                if (!(original is SkinnedMeshRenderer source) || !(proxy is SkinnedMeshRenderer target)) return;
                var sourceBones = source.bones;
                var upstreamBones = target.bones;
                foreach (var bone in sourceBones) if (bone != null) GetBone(bone);
                if (source.rootBone != null) GetBone(source.rootBone);
                UpdateBones();
                if (!_boneArrays.TryGetValue(target, out var output) || output.Length != upstreamBones.Length)
                    _boneArrays[target] = output = new Transform[upstreamBones.Length];
                for (var index = 0; index < upstreamBones.Length; index++)
                {
                    var bone = upstreamBones[index];
                    var sourceBone = bone != null && bone.IsChildOf(_owner.Root.transform)
                        ? bone : index < sourceBones.Length ? sourceBones[index] : null;
                    output[index] = sourceBone == null || bone == null ? bone : AdjustBone(sourceBone, bone);
                }
                target.bones = output;
                if (source.rootBone != null && target.rootBone != null)
                    target.rootBone = AdjustBone(source.rootBone, target.rootBone);
                target.forceMatrixRecalculationPerRender = true;
            }
            private Transform AdjustBone(Transform source, Transform upstream)
            {
                var desired = GetBone(source);
                if (!_adjustedBones.TryGetValue(upstream, out var result))
                {
                    result = new GameObject("ee4v adjusted bone") { hideFlags = HideFlags.HideAndDontSave }.transform;
                    result.SetParent(desired, false);
                    _adjustedBones[upstream] = result;
                }
                var relative = source.worldToLocalMatrix * upstream.localToWorldMatrix;
                SetLocalMatrix(result, relative);
                return result;
            }
            private void RestoreTransform(Transform transform)
            {
                if (!_transforms.TryGetValue(transform, out var state) || transform.parent != state.Parent) return;
                if (transform.localPosition == state.OutputPosition) transform.localPosition = state.Position;
                if (transform.localRotation == state.OutputRotation) transform.localRotation = state.Rotation;
                if (transform.localScale == state.OutputScale) transform.localScale = state.Scale;
            }
            private Transform GetBone(Transform source)
            {
                if (_bones.TryGetValue(source, out var result)) return result;
                if (_boneRoot == null)
                {
                    _boneRoot = new GameObject("ee4v preview bones") { hideFlags = HideFlags.HideAndDontSave };
                    SceneManager.MoveGameObjectToScene(_boneRoot, _boneScene);
                }
                result = new GameObject(source.name) { hideFlags = HideFlags.HideAndDontSave }.transform;
                var parent = source.parent == null ? _boneRoot.transform : GetBone(source.parent);
                result.SetParent(parent, false);
                _bones[source] = result;
                CopyBone(source, result);
                return result;
            }
            private void UpdateBones()
            {
                foreach (var pair in _bones) if (pair.Key != null) CopyBone(pair.Key, pair.Value);
            }
            private void CopyBone(Transform source, Transform target)
            {
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = _owner._scales.TryGetValue(source, out var scale) ? scale : source.localScale;
            }
            private static void SetMatrix(Transform target, Matrix4x4 matrix)
            {
                var local = target.parent == null ? matrix : target.parent.worldToLocalMatrix * matrix;
                SetLocalMatrix(target, local);
            }
            private static void SetLocalMatrix(Transform target, Matrix4x4 matrix)
            {
                target.localPosition = matrix.GetColumn(3);
                target.localRotation = matrix.rotation;
                target.localScale = matrix.lossyScale;
            }
            public void Dispose()
            {
                foreach (var transform in _transforms.Keys) if (transform != null) RestoreTransform(transform);
                foreach (var pair in _meshParents)
                {
                    if (pair.Key == null || pair.Key.parent != pair.Value.OutputParent) continue;
                    pair.Key.SetParent(pair.Value.Parent, false);
                    pair.Key.localPosition = pair.Value.Position;
                    pair.Key.localRotation = pair.Value.Rotation;
                    pair.Key.localScale = pair.Value.Scale;
                }
                if (_boneRoot != null) Object.DestroyImmediate(_boneRoot);
            }
        }
    }
}
