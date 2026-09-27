using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.PhysBoneCollider
{
    internal sealed class PhysBoneTarget
    {
        internal PhysBoneTarget(
            Transform transform,
            string path,
            int componentCount,
            IReadOnlyList<string> transformPaths)
        {
            Transform = transform;
            Path = path;
            ComponentCount = componentCount;
            TransformPaths = transformPaths;
        }

        internal Transform Transform { get; }

        internal string Path { get; }

        internal int ComponentCount { get; }

        internal IReadOnlyList<string> TransformPaths { get; }
    }

    internal sealed class VrchatPhysBoneColliderGateway
    {
        private const string ColliderTypeName =
            "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBoneCollider";
        private const string PhysBoneTypeName =
            "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone";
        private const string BoneProxyTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarBoneProxy";
        private const string OwnedObjectName = "[ee4v] PhysBone Collider";

        private Type _colliderType;
        private Type _physBoneType;
        private Type _boneProxyType;

        internal VrchatPhysBoneColliderGateway()
        {
        }

        internal VrchatPhysBoneColliderGateway(
            Type colliderType,
            Type physBoneType,
            Type boneProxyType)
        {
            _colliderType = colliderType;
            _physBoneType = physBoneType;
            _boneProxyType = boneProxyType;
        }

        internal bool IsSdkAvailable => ResolveColliderType() != null;
        internal bool IsModularAvatarAvailable => ResolveBoneProxyType() != null;
        internal bool IsAvailable => IsSdkAvailable && IsModularAvatarAvailable;

        internal IReadOnlyList<PhysBoneTarget> FindPhysBoneTargets(
            GameObject avatar,
            IReadOnlyList<GameObject> searchRoots)
        {
            var physBoneType = ResolvePhysBoneType();
            if (avatar == null ||
                searchRoots == null ||
                physBoneType == null ||
                searchRoots.Count == 0)
            {
                return Array.Empty<PhysBoneTarget>();
            }

            return searchRoots
                .Where(root =>
                    root != null &&
                    (root.transform == avatar.transform ||
                     root.transform.IsChildOf(avatar.transform)))
                .SelectMany(root => FindDirectArmatures(root.transform))
                .SelectMany(armature =>
                    armature.GetComponentsInChildren(physBoneType, true))
                .Distinct()
                .Cast<Component>()
                .Select(component => new
                {
                    Root = ResolvePhysBoneTarget(component),
                    Paths = CollectPhysBoneTransformPaths(component, avatar.transform)
                })
                .Where(item =>
                    item.Root != null &&
                    (item.Root == avatar.transform ||
                     item.Root.IsChildOf(avatar.transform)))
                .GroupBy(item => item.Root)
                .Select(group => new PhysBoneTarget(
                    group.Key,
                    AnimationUtility.CalculateTransformPath(
                        group.Key,
                        avatar.transform),
                    group.Count(),
                    group
                        .SelectMany(item => item.Paths)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray()))
                .OrderBy(target => target.Path, StringComparer.Ordinal)
                .ToArray();
        }

        private static IEnumerable<Transform> FindDirectArmatures(Transform root)
        {
            if (root == null)
            {
                yield break;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (string.Equals(
                        child.name,
                        "Armature",
                        StringComparison.OrdinalIgnoreCase))
                {
                    yield return child;
                }
            }
        }

        internal void LoadOwned(
            GameObject avatar,
            IReadOnlyList<PhysBoneColliderDraft> drafts)
        {
            var colliderType = ResolveColliderType();
            if (avatar == null || colliderType == null || drafts == null)
            {
                return;
            }

            var paths = PhysBoneColliderGenerationPaths.Create(avatar);
            var root = avatar.transform.Find(paths.RootName);
            if (root == null || root.parent != avatar.transform)
            {
                return;
            }

            var boneProxyType = ResolveBoneProxyType();
            if (boneProxyType == null)
            {
                return;
            }

            var byName = drafts.ToDictionary(GetOwnedObjectName);
            var loaded = new HashSet<PhysBoneColliderDraft>();
            var owned = new Dictionary<UnityEngine.Object, PhysBoneColliderDraft>();
            foreach (var component in root.GetComponentsInChildren(colliderType, true))
            {
                var proxy = component.GetComponent(boneProxyType);
                var target = ResolveProxyTarget(avatar.transform, proxy);
                if (!byName.TryGetValue(component.gameObject.name, out var draft))
                {
                    continue;
                }

                if (target != null)
                {
                    draft.Rebind(target, avatar.transform);
                }

                owned[component] = draft;
                if (loaded.Add(draft))
                {
                    draft.AssignedPhysBonePaths.Clear();
                    draft.AssignmentsLoaded = true;
                }

                var serialized = new SerializedObject(component);
                var shape = Find(serialized, "shapeType", "_shapeType");
                if (shape != null &&
                    shape.propertyType == SerializedPropertyType.Enum &&
                    !string.Equals(
                        shape.enumNames[shape.enumValueIndex],
                        "Capsule",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var radius = Find(serialized, "radius", "_radius");
                var height = Find(serialized, "height", "_height");
                var position = Find(serialized, "position", "_position");
                var rotation = Find(serialized, "rotation", "_rotation");
                if (radius != null && radius.propertyType == SerializedPropertyType.Float)
                {
                    draft.Radius = Mathf.Max(0.001f, radius.floatValue);
                }

                if (position != null && position.propertyType == SerializedPropertyType.Vector3)
                {
                    draft.Position = position.vector3Value;
                }

                if (height != null && height.propertyType == SerializedPropertyType.Float)
                {
                    draft.Height = Mathf.Max(draft.Radius * 2f, height.floatValue);
                }

                if (rotation != null)
                {
                    if (rotation.propertyType == SerializedPropertyType.Quaternion)
                    {
                        draft.Rotation = rotation.quaternionValue;
                    }
                    else if (rotation.propertyType == SerializedPropertyType.Vector3)
                    {
                        draft.Rotation = Quaternion.Euler(rotation.vector3Value);
                    }
                }
            }

            LoadAssignments(avatar, owned);
        }

        internal bool TryApply(
            GameObject avatar,
            IReadOnlyList<PhysBoneColliderDraft> drafts,
            out int createdCount,
            out string error)
        {
            createdCount = 0;
            error = null;
            var colliderType = ResolveColliderType();
            if (colliderType == null)
            {
                error = "sdkMissing";
                return false;
            }

            var boneProxyType = ResolveBoneProxyType();
            if (boneProxyType == null)
            {
                error = "maMissing";
                return false;
            }

            if (avatar == null || drafts == null || EditorUtility.IsPersistent(avatar))
            {
                error = "sceneAvatarRequired";
                return false;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PhysBone Colliders");
            try
            {
                var paths = PhysBoneColliderGenerationPaths.Create(avatar, true);
                var root = avatar.transform.Find(paths.RootName)?.gameObject;
                if (root != null && root.transform.parent != avatar.transform)
                {
                    root = null;
                }

                var previous = root == null
                    ? Array.Empty<Component>()
                    : root.GetComponentsInChildren(colliderType, true);
                RemoveReferences(avatar, previous);

                if (root == null)
                {
                    root = new GameObject(paths.RootName);
                    Undo.RegisterCreatedObjectUndo(
                        root,
                        "Create PhysBone Collider Prefab");
                    Undo.SetTransformParent(
                        root.transform,
                        avatar.transform,
                        "Parent PhysBone Collider Prefab");
                }
                else if (PrefabUtility.IsAnyPrefabInstanceRoot(root))
                {
                    PrefabUtility.UnpackPrefabInstance(
                        root,
                        PrefabUnpackMode.OutermostRoot,
                        InteractionMode.AutomatedAction);
                }

                ConfigureRoot(root, paths.RootName);

                var created = new Dictionary<Component, PhysBoneColliderDraft>();
                foreach (var draft in drafts.Where(item => item.Enabled))
                {
                    var holder = new GameObject(GetOwnedObjectName(draft));
                    Undo.RegisterCreatedObjectUndo(
                        holder,
                        "Create PhysBone Collider");
                    Undo.SetTransformParent(
                        holder.transform,
                        root.transform,
                        "Parent PhysBone Collider");
                    holder.transform.localPosition = Vector3.zero;
                    holder.transform.localRotation = Quaternion.identity;
                    holder.transform.localScale = Vector3.one;

                    var component = Undo.AddComponent(holder, colliderType);
                    Configure(component, draft);
                    var proxy = Undo.AddComponent(holder, boneProxyType);
                    ConfigureProxy(proxy, avatar.transform, draft.Bone);
                    created.Add(component, draft);
                }

                var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    root,
                    paths.PrefabPath,
                    InteractionMode.AutomatedAction);
                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "The generated PhysBone Collider Prefab could not be saved and connected.");
                }

                AddReferences(avatar, created);

                createdCount = created.Count;
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Undo.RevertAllDownToGroup(undoGroup);
                error = "applyFailed";
                return false;
            }
        }

        private void Configure(Component component, PhysBoneColliderDraft draft)
        {
            var serialized = new SerializedObject(component);
            SetObjectReference(
                serialized,
                component.transform,
                "rootTransform",
                "_rootTransform");
            SetEnum(serialized, "Capsule", "shapeType", "_shapeType");
            SetFloat(serialized, Mathf.Max(0.001f, draft.Radius), "radius", "_radius");
            SetFloat(
                serialized,
                Mathf.Max(draft.Radius * 2f, draft.Height),
                "height",
                "_height");
            SetVector3(serialized, draft.Position, "position", "_position");
            SetRotation(serialized, draft.Rotation, "rotation", "_rotation");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        private static void ConfigureRoot(GameObject root, string rootName)
        {
            root.name = rootName;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            for (var index = root.transform.childCount - 1; index >= 0; index--)
            {
                Undo.DestroyObjectImmediate(root.transform.GetChild(index).gameObject);
            }

            EditorUtility.SetDirty(root);
        }

        private static void ConfigureProxy(
            Component proxy,
            Transform avatar,
            Transform target)
        {
            var path = AnimationUtility.CalculateTransformPath(target, avatar);
            var serialized = new SerializedObject(proxy);
            SetEnum(serialized, "LastBone", "boneReference");
            SetString(serialized, path, "subPath");
            SetEnum(serialized, "AsChildAtRoot", "attachmentMode");
            SetBool(serialized, true, "matchScale");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(proxy);
            PrefabUtility.RecordPrefabInstancePropertyModifications(proxy);
        }

        private static Transform ResolveProxyTarget(
            Transform avatar,
            Component proxy)
        {
            if (proxy == null)
            {
                return null;
            }

            var serialized = new SerializedObject(proxy);
            var boneReference = Find(serialized, "boneReference");
            var subPath = Find(serialized, "subPath");
            if (boneReference == null ||
                boneReference.propertyType != SerializedPropertyType.Enum ||
                subPath == null ||
                subPath.propertyType != SerializedPropertyType.String ||
                !string.Equals(
                    boneReference.enumNames[boneReference.enumValueIndex],
                    "LastBone",
                    StringComparison.Ordinal))
            {
                return null;
            }

            return string.IsNullOrEmpty(subPath.stringValue)
                ? avatar
                : avatar.Find(subPath.stringValue);
        }

        private void RemoveReferences(GameObject avatar, IReadOnlyCollection<Component> removed)
        {
            var physBoneType = ResolvePhysBoneType();
            if (physBoneType == null || removed.Count == 0)
            {
                return;
            }

            var removedSet = new HashSet<UnityEngine.Object>(removed.Cast<UnityEngine.Object>());
            foreach (var physBone in avatar.GetComponentsInChildren(physBoneType, true))
            {
                var serialized = new SerializedObject(physBone);
                var colliders = Find(serialized, "colliders", "_colliders");
                if (colliders == null || !colliders.isArray)
                {
                    continue;
                }

                Undo.RecordObject(physBone, "Update PhysBone Colliders");
                for (var index = colliders.arraySize - 1; index >= 0; index--)
                {
                    var reference = colliders.GetArrayElementAtIndex(index);
                    if (!removedSet.Contains(reference.objectReferenceValue))
                    {
                        continue;
                    }

                    colliders.DeleteArrayElementAtIndex(index);
                    if (index < colliders.arraySize &&
                        colliders.GetArrayElementAtIndex(index).objectReferenceValue == null)
                    {
                        colliders.DeleteArrayElementAtIndex(index);
                    }
                }

                serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(physBone);
            }
        }

        private void LoadAssignments(
            GameObject avatar,
            IReadOnlyDictionary<UnityEngine.Object, PhysBoneColliderDraft> owned)
        {
            var physBoneType = ResolvePhysBoneType();
            if (physBoneType == null || owned.Count == 0)
            {
                return;
            }

            foreach (var physBone in avatar.GetComponentsInChildren(physBoneType, true))
            {
                var serialized = new SerializedObject(physBone);
                var colliders = Find(serialized, "colliders", "_colliders");
                if (colliders == null || !colliders.isArray)
                {
                    continue;
                }

                var path = AnimationUtility.CalculateTransformPath(
                    ResolvePhysBoneTarget(physBone),
                    avatar.transform);
                for (var index = 0; index < colliders.arraySize; index++)
                {
                    var reference = colliders
                        .GetArrayElementAtIndex(index)
                        .objectReferenceValue;
                    if (reference != null && owned.TryGetValue(reference, out var draft))
                    {
                        draft.AssignedPhysBonePaths.Add(path);
                    }
                }
            }
        }

        private void AddReferences(
            GameObject avatar,
            IReadOnlyDictionary<Component, PhysBoneColliderDraft> created)
        {
            var physBoneType = ResolvePhysBoneType();
            if (physBoneType == null || created.Count == 0)
            {
                return;
            }

            foreach (var physBone in avatar.GetComponentsInChildren(physBoneType, true))
            {
                var serialized = new SerializedObject(physBone);
                var colliders = Find(serialized, "colliders", "_colliders");
                if (colliders == null || !colliders.isArray)
                {
                    continue;
                }

                var path = AnimationUtility.CalculateTransformPath(
                    ResolvePhysBoneTarget(physBone),
                    avatar.transform);
                Undo.RecordObject(physBone, "Assign PhysBone Colliders");
                foreach (var pair in created)
                {
                    if (!pair.Value.AssignedPhysBonePaths.Contains(path))
                    {
                        continue;
                    }

                    var index = colliders.arraySize;
                    colliders.InsertArrayElementAtIndex(index);
                    colliders.GetArrayElementAtIndex(index).objectReferenceValue = pair.Key;
                }

                serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(physBone);
            }
        }

        private Type ResolveColliderType()
        {
            return _colliderType ?? (_colliderType = ResolveComponentType(ColliderTypeName));
        }

        private static Transform ResolvePhysBoneTarget(Component physBone)
        {
            var serialized = new SerializedObject(physBone);
            var root = Find(serialized, "rootTransform", "_rootTransform");
            return root != null &&
                   root.propertyType == SerializedPropertyType.ObjectReference
                ? root.objectReferenceValue as Transform ?? physBone.transform
                : physBone.transform;
        }

        private static IReadOnlyList<string> CollectPhysBoneTransformPaths(
            Component physBone,
            Transform avatar)
        {
            var root = ResolvePhysBoneTarget(physBone);
            if (root == null)
            {
                return Array.Empty<string>();
            }

            var ignored = new HashSet<Transform>();
            var serialized = new SerializedObject(physBone);
            var ignoreTransforms = Find(
                serialized,
                "ignoreTransforms",
                "_ignoreTransforms");
            if (ignoreTransforms != null && ignoreTransforms.isArray)
            {
                for (var index = 0; index < ignoreTransforms.arraySize; index++)
                {
                    var ignoredTransform = ignoreTransforms
                        .GetArrayElementAtIndex(index)
                        .objectReferenceValue as Transform;
                    if (ignoredTransform != null)
                    {
                        ignored.Add(ignoredTransform);
                    }
                }
            }

            var paths = new List<string>();
            CollectPhysBoneTransformPaths(root, avatar, ignored, paths);
            return paths;
        }

        private static void CollectPhysBoneTransformPaths(
            Transform current,
            Transform avatar,
            ISet<Transform> ignored,
            ICollection<string> paths)
        {
            if (current == null ||
                ignored.Contains(current) ||
                (current != avatar && !current.IsChildOf(avatar)))
            {
                return;
            }

            paths.Add(AnimationUtility.CalculateTransformPath(current, avatar));
            for (var index = 0; index < current.childCount; index++)
            {
                CollectPhysBoneTransformPaths(
                    current.GetChild(index),
                    avatar,
                    ignored,
                    paths);
            }
        }

        private Type ResolvePhysBoneType()
        {
            return _physBoneType ?? (_physBoneType = ResolveComponentType(PhysBoneTypeName));
        }

        private Type ResolveBoneProxyType()
        {
            return _boneProxyType ??
                   (_boneProxyType = ResolveComponentType(BoneProxyTypeName));
        }

        private static Type ResolveComponentType(string fullName)
        {
            return TypeCache.GetTypesDerivedFrom<Component>()
                .FirstOrDefault(type => string.Equals(
                    type.FullName,
                    fullName,
                    StringComparison.Ordinal));
        }

        private static string GetOwnedObjectName(PhysBoneColliderDraft draft)
        {
            return OwnedObjectName + " " + Hash128.Compute(draft.LayoutId);
        }

        private static void SetString(
            SerializedObject serialized,
            string value,
            params string[] names)
        {
            var property = Find(serialized, names);
            if (property != null && property.propertyType == SerializedPropertyType.String)
            {
                property.stringValue = value;
            }
        }

        private static void SetBool(
            SerializedObject serialized,
            bool value,
            params string[] names)
        {
            var property = Find(serialized, names);
            if (property != null && property.propertyType == SerializedPropertyType.Boolean)
            {
                property.boolValue = value;
            }
        }

        private static SerializedProperty Find(
            SerializedObject serialized,
            params string[] names)
        {
            foreach (var name in names)
            {
                var property = serialized.FindProperty(name);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static void SetObjectReference(
            SerializedObject serialized,
            UnityEngine.Object value,
            params string[] names)
        {
            var property = Find(serialized, names);
            if (property != null &&
                property.propertyType == SerializedPropertyType.ObjectReference)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetFloat(
            SerializedObject serialized,
            float value,
            params string[] names)
        {
            var property = Find(serialized, names);
            if (property != null && property.propertyType == SerializedPropertyType.Float)
            {
                property.floatValue = value;
            }
        }

        private static void SetVector3(
            SerializedObject serialized,
            Vector3 value,
            params string[] names)
        {
            var property = Find(serialized, names);
            if (property != null && property.propertyType == SerializedPropertyType.Vector3)
            {
                property.vector3Value = value;
            }
        }

        private static void SetRotation(
            SerializedObject serialized,
            Quaternion value,
            params string[] names)
        {
            var property = Find(serialized, names);
            if (property == null)
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.Quaternion)
            {
                property.quaternionValue = value;
            }
            else if (property.propertyType == SerializedPropertyType.Vector3)
            {
                property.vector3Value = value.eulerAngles;
            }
        }

        private static void SetEnum(
            SerializedObject serialized,
            string value,
            params string[] names)
        {
            var property = Find(serialized, names);
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            var index = Array.IndexOf(property.enumNames, value);
            property.enumValueIndex = Mathf.Max(0, index);
        }
    }
}
