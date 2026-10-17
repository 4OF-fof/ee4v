using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace Ee4v.AvatarEditing
{
    public enum AttachmentState { Connected, Missing, Partial, NeedsReview, NotRequired }
    public enum AttachmentKind { MergeArmature, BoneProxy }

    public sealed class AttachmentConnection
    {
        public Transform Owner { get; set; }
        public Transform Target { get; set; }
        public AttachmentKind Kind { get; set; }
        public Component Component { get; internal set; }
        public bool Exists => Component != null;
        public string Reason { get; internal set; }
    }

    public sealed class AttachmentPlan
    {
        public AttachmentState State { get; internal set; }
        public IReadOnlyList<AttachmentConnection> Connections { get; internal set; }
        public IReadOnlyList<string> ReviewReasons { get; internal set; }
        public bool NeedsAttention => State != AttachmentState.Connected && State != AttachmentState.NotRequired;
        public bool CanApply => ReviewReasons.Count == 0 && Connections.Any(connection => !connection.Exists);
    }

    public static class AvatarAttachmentAnalysis
    {
        public static AttachmentPlan Analyze(GameObject avatar, Transform part)
        {
            if (avatar == null || part == null || part == avatar.transform || !part.IsChildOf(avatar.transform))
                throw new ArgumentException("The part must belong to the avatar.");
            return new Analysis(avatar, part).Run();
        }

        public static bool IsValidTarget(GameObject avatar, Transform part, Transform target) =>
            avatar != null && part != null && target != null && target.IsChildOf(avatar.transform) && !target.IsChildOf(part);

        private sealed class Analysis
        {
            private readonly GameObject _avatar;
            private readonly Transform _part;
            private readonly List<AttachmentConnection> _connections = new List<AttachmentConnection>();
            private readonly HashSet<string> _review = new HashSet<string>();
            private readonly HashSet<Transform> _required = new HashSet<Transform>();
            private readonly Dictionary<string, Transform> _boneNames = new Dictionary<string, Transform>();
            private readonly Dictionary<Transform, Transform> _anchors = new Dictionary<Transform, Transform>();
            private readonly List<Renderer> _renderers = new List<Renderer>();
            private Component[] _components;
            private Animator _animator;
            private int _covered;

            internal Analysis(GameObject avatar, Transform part) { _avatar = avatar; _part = part; }

            internal AttachmentPlan Run()
            {
                _animator = _avatar.GetComponentsInChildren<Animator>(true)
                    .FirstOrDefault(animator => !animator.transform.IsChildOf(_part) && animator.isHuman);
                if (_animator != null)
                {
                    foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
                    {
                        if (bone == HumanBodyBones.LastBone) continue;
                        var target = _animator.GetBoneTransform(bone);
                        if (target == null || target.IsChildOf(_part)) continue;
                        AddName(target.name, target);
                        AddName(bone.ToString(), target);
                    }
                }
                _components = _part.GetComponentsInChildren<Component>(true);
                if (_part.GetComponentsInChildren<Transform>(true).Any(transform => !HasEditorOnlyParent(transform) && GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0))
                    _review.Add("missingScripts");
                ReadExisting();
                ReadRequirements();
                var pending = new HashSet<Transform>();
                foreach (var required in _required)
                {
                    if (!required.IsChildOf(_part))
                    {
                        if (required.IsChildOf(_avatar.transform)) _covered++;
                        else _review.Add("externalReference");
                        continue;
                    }
                    if (IsConnected(required, new HashSet<Transform>())) { _covered++; continue; }
                    var anchor = FindAnchor(required);
                    if (anchor != null) pending.Add(anchor);
                    else pending.Add(required);
                }
                if (pending.Count > 0) Propose(pending);
                var suggestions = _connections.Count(connection => !connection.Exists);
                var state = _review.Count > 0 ? AttachmentState.NeedsReview
                    : suggestions > 0 ? _covered > 0 ? AttachmentState.Partial : AttachmentState.Missing
                    : _required.Count == 0 && _connections.Count == 0 ? AttachmentState.NotRequired : AttachmentState.Connected;
                return new AttachmentPlan { State = state,
                    Connections = _connections.OrderBy(connection => AnimationUtility.CalculateTransformPath(connection.Owner, _part), StringComparer.Ordinal).ThenBy(connection => connection.Kind).ToArray(),
                    ReviewReasons = _review.OrderBy(reason => reason, StringComparer.Ordinal).ToArray() };
            }

            private static string Normalize(string name) => new string((name ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

            private void AddName(string name, Transform target)
            {
                var key = Normalize(name);
                if (_boneNames.TryGetValue(key, out var previous) && previous != target) _boneNames[key] = null;
                else if (!_boneNames.ContainsKey(key)) _boneNames.Add(key, target);
            }

            private Transform Expected(Transform source)
            {
                if (source == null || source == _part) return null;
                if (_boneNames.TryGetValue(Normalize(source.name), out var target)) return target;
                AttachmentConnection closest = null;
                foreach (var connection in _connections.Where(connection => connection.Component is ModularAvatarMergeArmature && source.IsChildOf(connection.Owner)))
                    if (closest == null || connection.Owner.IsChildOf(closest.Owner)) closest = connection;
                if (closest == null) return null;
                var merge = (ModularAvatarMergeArmature)closest.Component;
                var prefix = merge.prefix ?? string.Empty;
                var suffix = merge.suffix ?? string.Empty;
                var name = source.name;
                if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal) || name.Length <= prefix.Length + suffix.Length) return null;
                var key = Normalize(name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length));
                if (!_boneNames.TryGetValue(key, out target)) return null;
                if (target == null) _review.Add("ambiguousBone");
                return target;
            }

            private Transform FindAnchor(Transform transform)
            {
                for (var current = transform; current != null && current != _part; current = current.parent)
                {
                    if (_boneNames.TryGetValue(Normalize(current.name), out var named) && named == null)
                    { _review.Add("ambiguousBone"); return null; }
                    var target = Expected(current);
                    if (target == null) continue;
                    _anchors[current] = target;
                    return current;
                }
                return null;
            }

            private void ReadExisting()
            {
                foreach (var component in _components)
                {
                    if (component == null || HasEditorOnlyParent(component.transform)) continue;
                    if (component is ModularAvatarMergeArmature merge)
                        AddExisting(merge, AttachmentKind.MergeArmature, merge.mergeTargetObject?.transform);
                    else if (component is ModularAvatarBoneProxy proxy)
                        AddExisting(proxy, AttachmentKind.BoneProxy, proxy.target);
                }
            }

            private void AddExisting(Behaviour component, AttachmentKind kind, Transform target)
            {
                var reason = !component.enabled ? "disabled" : target == null ? "unassigned"
                    : !target.IsChildOf(_avatar.transform) || target.IsChildOf(component.transform) ? "invalidTarget"
                    : component is ModularAvatarBoneProxy proxy && proxy.attachmentMode == BoneProxyAttachmentMode.Unset ? "unsetMode" : "existing";
                _connections.Add(new AttachmentConnection { Owner = component.transform, Target = target, Kind = kind, Component = component, Reason = reason });
                if (reason != "existing") _review.Add(reason);
            }

            private void ReadRequirements()
            {
                foreach (var renderer in _part.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.CompareTag("EditorOnly") || HasEditorOnlyParent(renderer.transform)) continue;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        if (skin.sharedMesh == null) continue;
                        _renderers.Add(renderer);
                        if (skin.rootBone != null) _required.Add(skin.rootBone);
                        try
                        {
                            var indices = new HashSet<int>();
                            var weights = skin.sharedMesh.GetAllBoneWeights();
                            try
                            {
                                foreach (var weight in weights)
                                    if (weight.weight > 0) indices.Add(weight.boneIndex);
                            }
                            finally { weights.Dispose(); }
                            var bones = skin.bones;
                            foreach (var index in indices)
                            {
                                if (index < 0 || index >= bones.Length || bones[index] == null) _review.Add("missingBone");
                                else _required.Add(bones[index]);
                            }
                            if (indices.Count == 0) _required.Add(renderer.transform);
                        }
                        catch (UnityException) { _review.Add("unreadableMesh"); }
                    }
                    else if (renderer is MeshRenderer && renderer.GetComponent<MeshFilter>()?.sharedMesh != null)
                    { _renderers.Add(renderer); _required.Add(renderer.transform); }
                }
                foreach (var component in _components.Where(component => component != null))
                {
                    if (HasEditorOnlyParent(component.transform)) continue;
                    var name = component.GetType().Name;
                    if (name == "VRCPhysBone" || name == "VRCPhysBoneCollider" || name == "VRCContactSender" || name == "VRCContactReceiver")
                    {
                        var serialized = new SerializedObject(component);
                        var root = serialized.FindProperty("rootTransform")?.objectReferenceValue as Transform ?? component.transform;
                        _required.Add(root);
                        var colliders = serialized.FindProperty("colliders");
                        if (colliders != null && colliders.isArray)
                            for (var index = 0; index < colliders.arraySize; index++)
                            {
                                var collider = colliders.GetArrayElementAtIndex(index).objectReferenceValue as Component;
                                if (collider != null) _required.Add(new SerializedObject(collider).FindProperty("rootTransform")?.objectReferenceValue as Transform ?? collider.transform);
                            }
                    }
                    if (component is Animator animator && animator.runtimeAnimatorController != null)
                        ReadAnimation(animator.runtimeAnimatorController, animator.transform);
                    if (name == "ModularAvatarMergeAnimator")
                    {
                        var serialized = new SerializedObject(component);
                        var controller = serialized.FindProperty("animator")?.objectReferenceValue as RuntimeAnimatorController;
                        var relative = serialized.FindProperty("pathMode")?.intValue == 0;
                        var path = serialized.FindProperty("relativePathRoot")?.FindPropertyRelative("referencePath")?.stringValue;
                        var animationRoot = relative ? string.IsNullOrEmpty(path) ? component.transform : _avatar.transform.Find(path) : _avatar.transform;
                        if (controller != null) ReadAnimation(controller, animationRoot);
                    }
                }
            }

            private bool HasEditorOnlyParent(Transform transform)
            {
                for (var current = transform; current != null && current.IsChildOf(_part); current = current.parent)
                    if (current.CompareTag("EditorOnly")) return true;
                return false;
            }

            private void ReadAnimation(RuntimeAnimatorController controller, Transform animationRoot)
            {
                foreach (var clip in controller.animationClips.Where(clip => clip != null).Distinct())
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(binding => binding.type == typeof(Transform)))
                    {
                        var target = animationRoot == null ? null : string.IsNullOrEmpty(binding.path) ? animationRoot : animationRoot.Find(binding.path);
                        if (target == null) _review.Add("animationTarget");
                        else if (target.IsChildOf(_part)) _required.Add(target);
                    }
            }

            private bool IsConnected(Transform required, HashSet<Transform> visited)
            {
                if (required == null || !visited.Add(required)) return false;
                if (!required.IsChildOf(_part)) return required.IsChildOf(_avatar.transform);
                for (var current = required; current != null && current.IsChildOf(_part); current = current.parent)
                {
                    var connection = _connections.FirstOrDefault(candidate => candidate.Exists && candidate.Owner == current);
                    if (connection != null)
                    {
                        if (connection.Reason != "existing") return false;
                        if (!IsConnected(connection.Target, visited)) return false;
                        if (connection.Kind == AttachmentKind.BoneProxy)
                        {
                            var proxyAnchor = FindAnchor(required);
                            if (Expected(connection.Owner) == connection.Target && proxyAnchor != null &&
                                proxyAnchor.IsChildOf(connection.Owner) && _anchors[proxyAnchor] != connection.Target)
                                return false;
                            return true;
                        }
                        var merge = (ModularAvatarMergeArmature)connection.Component;
                        var anchor = FindAnchor(required);
                        var mapped = Map(merge.transform, connection.Target, anchor ?? required, merge.prefix, merge.suffix);
                        if (anchor != null && anchor.IsChildOf(merge.transform))
                        {
                            if (mapped == _anchors[anchor]) return true;
                            _review.Add("hierarchyMismatch");
                            return false;
                        }
                        var targetAnchor = connection.Target;
                        if (mapped != null || _boneNames.Values.Contains(targetAnchor)) return true;
                        _review.Add("hierarchyMismatch");
                        return false;
                    }
                    var constraint = current.GetComponent<ParentConstraint>();
                    if (constraint != null && constraint.enabled && constraint.constraintActive && constraint.weight > 0 && constraint.sourceCount > 0)
                    {
                        var sources = new List<ConstraintSource>(); constraint.GetSources(sources);
                        var active = sources.Where(source => source.weight > 0).ToArray();
                        if (active.Length > 0 && active.All(source => IsConnected(source.sourceTransform, new HashSet<Transform>(visited)))) return true;
                    }
                    if (current.GetComponents<Component>().Any(component => component != null && component.GetType().Name.Contains("Constraint")))
                    { _review.Add("constraint"); return false; }
                }
                return _part.parent != null && _part.parent != _avatar.transform && IsConnected(_part.parent, visited);
            }

            private static Transform Map(Transform source, Transform target, Transform bone, string prefix = "", string suffix = "")
            {
                prefix = prefix ?? string.Empty;
                suffix = suffix ?? string.Empty;
                if (bone == null || !bone.IsChildOf(source)) return null;
                if (bone == source) return target;
                foreach (var segment in AnimationUtility.CalculateTransformPath(bone, source).Split('/'))
                {
                    if (!segment.StartsWith(prefix, StringComparison.Ordinal) || !segment.EndsWith(suffix, StringComparison.Ordinal) || segment.Length <= prefix.Length + suffix.Length) return null;
                    var name = segment.Substring(prefix.Length, segment.Length - prefix.Length - suffix.Length);
                    var matches = target.Cast<Transform>().Where(child => child.name == name).Take(2).ToArray();
                    if (matches.Length != 1) return null;
                    target = matches[0];
                }
                return target;
            }

            private void Propose(HashSet<Transform> pending)
            {
                foreach (var source in pending) FindAnchor(source);
                var anchors = pending.Where(source => _anchors.ContainsKey(source))
                    .OrderBy(source => AnimationUtility.CalculateTransformPath(source, _part), StringComparer.Ordinal).ToList();
                while (anchors.Count > 0)
                {
                    var candidates = new List<AttachmentConnection>();
                    foreach (var anchor in anchors)
                    {
                        AddCandidate(anchor, _anchors[anchor]);
                        if (anchor.parent != null && anchor.parent != _part && _anchors[anchor].parent != null)
                            AddCandidate(anchor.parent, _anchors[anchor].parent);
                    }
                    var best = candidates.OrderByDescending(candidate => anchors.Count(anchor => anchor.IsChildOf(candidate.Owner)))
                        .ThenBy(candidate => AnimationUtility.CalculateTransformPath(candidate.Owner, _part).Split('/').Length)
                        .ThenBy(candidate => AnimationUtility.CalculateTransformPath(candidate.Owner, _part), StringComparer.Ordinal).FirstOrDefault();
                    if (best != null)
                    {
                        Suggest(best.Owner, best.Target, best.Kind, best.Reason);
                        anchors.RemoveAll(anchor => anchor.IsChildOf(best.Owner));
                        pending.RemoveWhere(source => source.IsChildOf(best.Owner));
                    }
                    else
                    {
                        var anchor = anchors[0]; anchors.RemoveAt(0);
                        if (HasBlockingAncestor(anchor))
                        { _review.Add("existingCoverage"); pending.Remove(anchor); continue; }
                        Suggest(anchor, _anchors[anchor], AttachmentKind.BoneProxy, "namedBone");
                        pending.Remove(anchor);
                    }

                    void AddCandidate(Transform owner, Transform target)
                    {
                        if (OverlapsExisting(owner) || !IsValidTarget(_avatar, _part, target)) return;
                        var contained = anchors.Where(anchor => anchor.IsChildOf(owner)).ToArray();
                        if (contained.Length < 2 || contained.Any(anchor => Map(owner, target, anchor) != _anchors[anchor])) return;
                        candidates.Add(new AttachmentConnection { Owner = owner, Target = target, Kind = AttachmentKind.MergeArmature, Reason = "matchingHierarchy" });
                    }
                }
                if (pending.Count == 0) return;
                if (_connections.Count == 0 && _review.Count == 0 && _anchors.Count == 0)
                {
                    var target = EstimateTarget();
                    if (target != null)
                    {
                        Suggest(_part, target, AttachmentKind.BoneProxy, "estimatedBone");
                        _review.Add("estimatedBone");
                        return;
                    }
                }
                _review.Add("unknownStructure");
            }

            private bool HasBlockingAncestor(Transform owner) => _connections.Any(connection => connection.Exists && owner.IsChildOf(connection.Owner) &&
                (owner == connection.Owner || connection.Kind != AttachmentKind.BoneProxy || connection.Reason != "existing" || Expected(connection.Owner) != connection.Target));

            private bool OverlapsExisting(Transform owner) => HasBlockingAncestor(owner) ||
                _connections.Any(connection => connection.Exists && connection.Owner.IsChildOf(owner));

            private void Suggest(Transform owner, Transform target, AttachmentKind kind, string reason)
            {
                if (_connections.Any(connection => connection.Owner == owner)) return;
                _connections.Add(new AttachmentConnection { Owner = owner, Target = target, Kind = kind, Reason = reason });
            }

            private Transform EstimateTarget()
            {
                if (_animator == null) return null;
                var center = _part.position;
                if (_renderers.Count > 0)
                {
                    var bounds = _renderers[0].bounds;
                    foreach (var renderer in _renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    center = bounds.center;
                }
                var bones = new[] { HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.Chest, HumanBodyBones.Hips,
                    HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
                return bones.Select(bone => bone == HumanBodyBones.Chest
                        ? _animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? _animator.GetBoneTransform(HumanBodyBones.Chest)
                        : _animator.GetBoneTransform(bone))
                    .Where(target => target != null && IsValidTarget(_avatar, _part, target)).Distinct()
                    .OrderBy(target => (target.position - center).sqrMagnitude)
                    .ThenBy(target => AnimationUtility.CalculateTransformPath(target, _avatar.transform), StringComparer.Ordinal)
                    .FirstOrDefault();
            }
        }
    }
}
