using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    public sealed class AvatarScaleData
    {
        public string Part;
        public string[] TargetPaths;
        public float[] Multipliers;
    }
    public sealed class AvatarBodyShapeData
    {
        public string RendererPath, ShapeName, Category, Group, SourceRendererPath;
        public float Value, BaseValue;
    }
    public sealed class AvatarAttachmentData
    {
        public string PartPath, OwnerPath, Kind, TargetPath, Mode;
        public bool MatchScale, Exists;
    }
    public sealed class AvatarAuthoringData
    {
        public string ScopePath;
        public float? HeightMeters, BaseHeightMeters;
        public IReadOnlyList<AvatarScaleData> Scales;
        public IReadOnlyList<AvatarBodyShapeData> BlendShapes;
        public IReadOnlyList<AvatarAttachmentData> Attachments;
    }
    public sealed class AvatarShapeEditRequest
    {
        public string Action, ScopePath, Part, RendererPath, ShapeName, Category;
        public float[] Multipliers;
        public float? Value;
    }
    public sealed class AvatarAttachmentEditRequest
    {
        public string PartPath, Kind, OwnerPath, TargetPath, Mode;
        public bool? MatchScale;
    }

    public sealed partial class AvatarPartsEditor
    {
        private bool _throwOnEditingFailure;

        private static AvatarPartsEditor AuthoringEditor(GameObject root, string scopePath, bool writable)
        {
            if (root == null || EditorUtility.IsPersistent(root) || !root.scene.IsValid() || !root.scene.isLoaded ||
                EditorSceneManager.IsPreviewScene(root.scene) || writable && EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use a loaded user Scene instance in Edit Mode for authoring writes.");
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (writable && !PrefabUtility.IsAnyPrefabInstanceRoot(root))
                throw new InvalidOperationException("Authoring writes require an Avatar or work Prefab instance so source-relative values remain stable.");
            if (writable && (PrefabUtility.IsPartOfImmutablePrefab(root) || prefab != null && !AssetDatabase.IsOpenForEdit(prefab)))
                throw new InvalidOperationException("The avatar Prefab is read-only.");
            Action changed = () => EditorSceneManager.MarkSceneDirty(root.scene);
            var context = new AvatarEditingContext(new AvatarEditingServices(() => true, _ => false, () => true,
                () => AssetDatabase.GetAssetPath(prefab), _ => changed(), changed, message => { throw new InvalidOperationException(message); }, _ => { }),
                new AvatarEditingHost(() => { }, () => { }, () => { }, () => { }, () => { }))
                { Root = root, PrefabAsset = prefab, UiRoot = new VisualElement(),
                    PrefabSiblingIndices = Enumerable.Range(0, root.transform.childCount)
                        .Where(index => PrefabUtility.IsAnyPrefabInstanceRoot(root.transform.GetChild(index).gameObject)).ToArray() };
            context.Host.CreateShapeNaming = AvatarShapeNaming.CreateReadOnly;
            if (!string.IsNullOrEmpty(scopePath))
            {
                var scope = Relative(root, scopePath);
                if (scope.parent != root.transform || !context.PrefabSiblingIndices.Contains(scope.GetSiblingIndex()))
                    throw new ArgumentException("scopePath must identify a direct child Prefab instance.");
                context.SelectedPrefabSiblingIndex = scope.GetSiblingIndex();
            }
            return new AvatarPartsEditor(context) { _throwOnEditingFailure = true };
        }

        public static AvatarAuthoringData InspectAuthoring(GameObject root, string scopePath = null) =>
            AuthoringEditor(root, scopePath, false).AuthoringSnapshot();

        private AvatarAuthoringData AuthoringSnapshot()
        {
            var root = _context.Root; var animator = _context.FindHumanoidAnimator();
            var scopes = BodyScaleDefinitions.Select(definition => new { definition, targets = definition.UsesAvatarRoot && _context.SelectedPrefabSiblingIndex.HasValue
                    ? ResolvePrefabScaleTargets() : ResolveBodyScaleTargets(definition, animator) })
                .Where(entry => entry.targets.Count > 0)
                .Select(entry => new AvatarScaleData { Part = entry.definition.LocalizationKey.Split('.').Last(),
                    TargetPaths = entry.targets.Select(target => AnimationUtility.CalculateTransformPath(target, root.transform)).ToArray(),
                    Multipliers = Vector(GetBodyScaleMultipliers(entry.targets)) }).ToArray();
            var shapes = GetBodyBlendShapes().Select(shape => new AvatarBodyShapeData { RendererPath = shape.RendererPath, ShapeName = shape.ShapeName,
                Category = shape.Category.ToString(), Group = shape.Group, Value = shape.Value, BaseValue = shape.BaseValue,
                SourceRendererPath = Path(ResolveBodyBlendShapeSource(GetBodyBlendShapeRenderer(shape), shape.ShapeName)?.transform) }).ToArray();
            var attachments = new List<AvatarAttachmentData>();
            foreach (var index in _context.PrefabSiblingIndices.Where(index => !_context.SelectedPrefabSiblingIndex.HasValue || _context.SelectedPrefabSiblingIndex == index))
            {
                var part = root.transform.GetChild(index); var merges = part.GetComponentsInChildren<ModularAvatarMergeArmature>(true); var proxies = part.GetComponentsInChildren<ModularAvatarBoneProxy>(true);
                attachments.AddRange(merges.Select(merge => new AvatarAttachmentData { PartPath = Path(part), OwnerPath = Path(merge.transform), Kind = "MergeArmature", TargetPath = Path(merge.mergeTargetObject?.transform), Exists = true }));
                attachments.AddRange(proxies.Select(proxy => new AvatarAttachmentData { PartPath = Path(part), OwnerPath = Path(proxy.transform), Kind = "BoneProxy", TargetPath = Path(proxy.target), Mode = proxy.attachmentMode.ToString(), MatchScale = proxy.matchScale, Exists = true }));
                if (merges.Length + proxies.Length == 0)
                {
                    var armature = FindPartArmature(part); var target = RecommendedAttachmentTarget(part, armature);
                    attachments.Add(new AvatarAttachmentData { PartPath = Path(part), OwnerPath = Path(armature ?? part),
                        Kind = armature != null ? "MergeArmature" : "BoneProxy", TargetPath = Path(target), Mode = "AsChildKeepWorldPose" });
                }
            }
            Component descriptor = null; var view = Vector3.zero;
            var hasHeight = !_context.SelectedPrefabSiblingIndex.HasValue && TryGetWorkingAvatarViewPosition(out descriptor, out view);
            return new AvatarAuthoringData { ScopePath = _context.SelectedPrefabSiblingIndex.HasValue ? Path(root.transform.GetChild(_context.SelectedPrefabSiblingIndex.Value)) : string.Empty,
                HeightMeters = hasHeight ? view.y : (float?)null, BaseHeightMeters = hasHeight ? GetBaseAvatarViewPosition(descriptor, view).y : (float?)null,
                Scales = scopes, BlendShapes = shapes, Attachments = attachments };
        }

        public static AvatarAuthoringData EditAuthoring(GameObject root, AvatarShapeEditRequest request, bool dryRun = false)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var editor = AuthoringEditor(root, request.ScopePath, true);
            if (dryRun) return editor.EditAuthoring(request, true);
            Undo.IncrementCurrentGroup(); var undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(I18N.Get("workflow.appearance.sizeUndo"));
            try { var result = editor.EditAuthoring(request, false); Undo.CollapseUndoOperations(undo); return result; }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        private AvatarAuthoringData EditAuthoring(AvatarShapeEditRequest request, bool dryRun)
        {
            var editor = this;
            var targets = Array.Empty<string>(); var multipliers = Vector3.one; var updateView = false;
            switch (request.Action)
            {
                case "setScale":
                    if (request.Multipliers == null || request.Multipliers.Length != 3 || request.Multipliers.Any(value => !Finite(value) || value <= 0))
                        throw new ArgumentException("multipliers requires three finite positive ratios; 1 is the source Prefab's scale.");
                    var definition = BodyScaleDefinitions.SingleOrDefault(part => part.LocalizationKey.Split('.').Last() == request.Part)
                        ?? throw new ArgumentException("Unknown scale part; inspect authoring first.");
                    var transforms = definition.UsesAvatarRoot && editor._context.SelectedPrefabSiblingIndex.HasValue
                        ? editor.ResolvePrefabScaleTargets() : editor.ResolveBodyScaleTargets(definition, editor._context.FindHumanoidAnimator());
                    if (transforms.Count == 0) throw new InvalidOperationException("No editable scale targets exist for this part.");
                    targets = transforms.Select(target => editor.Path(target)).ToArray();
                    multipliers = new Vector3(request.Multipliers[0], request.Multipliers[1], request.Multipliers[2]);
                    updateView = definition.UsesAvatarRoot && !editor._context.SelectedPrefabSiblingIndex.HasValue;
                    break;
                case "setHeight":
                    if (!request.Value.HasValue || !Finite(request.Value.Value) || editor._context.SelectedPrefabSiblingIndex.HasValue ||
                        !editor.TryGetWorkingAvatarViewPosition(out var descriptor, out var view)) throw new ArgumentException("A finite height and an Avatar with ViewPosition are required.");
                    var baseHeight = editor.GetBaseAvatarViewPosition(descriptor, view).y;
                    if (!Finite(baseHeight) || baseHeight <= 0.01f) throw new InvalidOperationException("The base Avatar height is invalid.");
                    var height = Mathf.Clamp(Mathf.Round(request.Value.Value * 100f) / 100f, Mathf.Max(0.01f, baseHeight - 1f), baseHeight + 1f);
                    multipliers = Vector3.one * (height / baseHeight); targets = new[] { string.Empty }; updateView = true;
                    break;
                case "setBlendshape":
                case "setBlendshapeGroup":
                    if (!request.Value.HasValue || !Finite(request.Value.Value)) throw new ArgumentException("A finite BlendShape value is required.");
                    var group = editor.GetBodyBlendShapes().Where(shape => shape.ShapeName == request.ShapeName && shape.Category.ToString() == request.Category).ToArray();
                    if (group.Length == 0) throw new ArgumentException("The body BlendShape group was not found. Inspect authoring first.");
                    var shape = request.Action == "setBlendshape" ? group.SingleOrDefault(item => item.RendererPath == request.RendererPath) : null;
                    if (request.Action == "setBlendshape" && shape == null) throw new ArgumentException("The body BlendShape was not found in the selected group.");
                    if (!dryRun)
                    {
                        if (shape != null) editor.ApplyIndividualBodyBlendShape(group, shape, Mathf.Clamp(request.Value.Value, 0, 100), false);
                        else editor.ApplyBodyBlendShapeGroup(group, Mathf.Clamp(request.Value.Value, 0, 100), false);
                    }
                    return editor.AuthoringSnapshot();
                case "prepareBlendshapeSync":
                    if (!dryRun && !editor.PrepareBodyBlendShapeSyncForSave()) throw new InvalidOperationException("Body BlendShape synchronization could not be prepared.");
                    return editor.AuthoringSnapshot();
                default: throw new ArgumentException("Unknown body editing action.");
            }
            foreach (var path in targets)
            {
                var scale = Vector3.Scale(GetBaseLocalScale(Relative(editor._context.Root, path)), multipliers);
                if (!Finite(scale.x) || !Finite(scale.y) || !Finite(scale.z)) throw new ArgumentException("The resulting Transform scale must be finite.");
            }
            if (updateView && editor.TryGetWorkingAvatarViewPosition(out var viewDescriptor, out var currentView))
            {
                var position = Vector3.Scale(editor.GetBaseAvatarViewPosition(viewDescriptor, currentView), multipliers);
                if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)) throw new ArgumentException("The resulting ViewPosition must be finite.");
            }
            if (!dryRun) editor.ApplyBodyScale(targets, multipliers, updateView, false);
            return editor.AuthoringSnapshot();
        }

        public static void PrepareAuthoringForSave(GameObject root)
        {
            if (!AuthoringEditor(root, null, true).PrepareBodyBlendShapeSyncForSave())
                throw new InvalidOperationException("Body BlendShape synchronization could not be prepared for saving.");
        }

        public static AvatarAuthoringData EditAttachment(GameObject root, AvatarAttachmentEditRequest request, bool dryRun = false)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var editor = AuthoringEditor(root, request.PartPath, true);
            var part = Relative(root, request.PartPath); var armature = FindPartArmature(part);
            var kind = request.Kind == "auto" ? armature != null ? "MergeArmature" : "BoneProxy" : request.Kind;
            if (kind != "MergeArmature" && kind != "BoneProxy") throw new ArgumentException("kind must be auto, MergeArmature or BoneProxy.");
            var owner = request.OwnerPath == null ? kind == "MergeArmature" ? armature : part : Relative(root, request.OwnerPath);
            if (owner == null || !owner.IsChildOf(part)) throw new ArgumentException("The attachment owner must belong to this part.");
            var target = request.TargetPath == null ? editor.RecommendedAttachmentTarget(part, armature) : Relative(root, request.TargetPath);
            if (!editor.ValidAttachmentTarget(part, target)) throw new ArgumentException("The attachment target must belong to the Avatar and be outside this part.");
            var mode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
            if (request.Mode != null && (!Enum.TryParse(request.Mode, out mode) || !Enum.IsDefined(typeof(BoneProxyAttachmentMode), mode))) throw new ArgumentException("Unknown Bone Proxy mode.");
            if (kind == "MergeArmature" && (request.Mode != null || request.MatchScale.HasValue)) throw new ArgumentException("Mode and matchScale apply only to Bone Proxy.");
            var existingCount = kind == "MergeArmature" ? owner.GetComponents<ModularAvatarMergeArmature>().Length : owner.GetComponents<ModularAvatarBoneProxy>().Length;
            if (existingCount > 1) throw new ArgumentException("The attachment component is ambiguous.");
            if (existingCount == 0 && part.GetComponentsInChildren<ModularAvatarMergeArmature>(true).Length + part.GetComponentsInChildren<ModularAvatarBoneProxy>(true).Length > 0)
                throw new ArgumentException("This part already has attachment settings. Specify the existing ownerPath and kind.");
            if (!dryRun) editor.ApplyAttachment(part, owner, kind, target, request.Mode == null ? (BoneProxyAttachmentMode?)null : mode, request.MatchScale);
            return editor.AuthoringSnapshot();
        }

        private Transform RecommendedAttachmentTarget(Transform part, Transform armature)
        {
            var animator = _context.Root.GetComponentsInChildren<Animator>(true).FirstOrDefault(candidate => !candidate.transform.IsChildOf(part) && candidate.isHuman);
            var target = armature != null ? animator?.GetBoneTransform(HumanBodyBones.Hips)?.parent : animator?.GetBoneTransform(HumanBodyBones.Head);
            return target ?? (armature == null ? null : _context.Root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => !candidate.IsChildOf(part) && candidate != _context.Root.transform && string.Equals(candidate.name, armature.name, StringComparison.OrdinalIgnoreCase)));
        }
        private void ApplyAttachment(Transform part, Transform owner, string kind, Transform target, BoneProxyAttachmentMode? mode, bool? matchScale)
        {
            if (!ValidAttachmentTarget(part, target) || !_context.Edits.CanEditPrefab()) throw new ArgumentException("Attachment is not editable.");
            Undo.IncrementCurrentGroup(); var undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(I18N.Get("workflow.attachment.undo"));
            try
            {
                if (kind == "MergeArmature")
                {
                    var merge = owner.GetComponent<ModularAvatarMergeArmature>() ?? Undo.AddComponent<ModularAvatarMergeArmature>(owner.gameObject);
                    ChangeAttachment(merge, () => merge.mergeTarget.Set(target.gameObject));
                }
                else
                {
                    var proxy = owner.GetComponent<ModularAvatarBoneProxy>(); var created = proxy == null;
                    proxy = proxy ?? Undo.AddComponent<ModularAvatarBoneProxy>(owner.gameObject);
                    ChangeAttachment(proxy, () => { proxy.target = target; if (created || mode.HasValue) proxy.attachmentMode = mode ?? BoneProxyAttachmentMode.AsChildKeepWorldPose;
                        if (matchScale.HasValue) proxy.matchScale = matchScale.Value; });
                }
                Undo.CollapseUndoOperations(undo);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
        private string Path(Transform target) => target == null ? null : AnimationUtility.CalculateTransformPath(target, _context.Root.transform);
        private static Transform Relative(GameObject root, string path)
        {
            if (path == null) throw new ArgumentException("An Avatar-relative path is required.");
            var matches = root.GetComponentsInChildren<Transform>(true).Where(target => AnimationUtility.CalculateTransformPath(target, root.transform) == path).Take(2).ToArray();
            if (matches.Length != 1) throw new ArgumentException("The Avatar-relative path is missing or ambiguous: " + path);
            return matches[0];
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float[] Vector(Vector3 value) => new[] { value.x, value.y, value.z };
    }
}
