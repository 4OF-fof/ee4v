using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.PhysBoneCollider
{
    public sealed class PhysBoneColliderData
    {
        public string Id { get; set; }
        public string BonePath { get; set; }
        public string SuggestedBonePath { get; set; }
        public string TargetPath { get; set; }
        public float BoneLength { get; set; }
        public bool Enabled { get; set; }
        public float[] Position { get; set; }
        public float[] Rotation { get; set; }
        public float Radius { get; set; }
        public float Height { get; set; }
        public IReadOnlyList<string> AssignedPhysBonePaths { get; set; }
    }

    public sealed class PhysBoneTargetData
    {
        public string Path { get; set; }
        public int ComponentCount { get; set; }
        public IReadOnlyList<string> TransformPaths { get; set; }
    }

    public sealed class PhysBoneColliderPlanData
    {
        public bool CanApply { get; set; }
        public bool HasVrchatSdk { get; set; }
        public bool HasModularAvatar { get; set; }
        public string Preset { get; set; }
        public float MinimumBoneLength { get; set; }
        public string PrefabPath { get; set; }
        public IReadOnlyList<PhysBoneColliderData> Colliders { get; set; }
        public IReadOnlyList<PhysBoneTargetData> PhysBoneTargets { get; set; }
        public IReadOnlyList<string> Warnings { get; set; }
    }

    public sealed class PhysBoneColliderOverrideData
    {
        public string Id { get; set; }
        public bool? Enabled { get; set; }
        public string BonePath { get; set; }
        public float[] Position { get; set; }
        public float[] Rotation { get; set; }
        public float? Radius { get; set; }
        public float? Height { get; set; }
        public IReadOnlyList<string> AssignedPhysBonePaths { get; set; }
    }

    public sealed class PhysBoneColliderApplyResult
    {
        public bool Succeeded { get; set; }
        public string ErrorCode { get; set; }
        public int CreatedCount { get; set; }
        public string PrefabPath { get; set; }
    }

    public static class PhysBoneColliderApi
    {
        public static PhysBoneColliderPlanData Plan(
            GameObject avatar,
            string preset,
            float minimumBoneLength,
            IReadOnlyList<GameObject> searchRoots = null)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            var parsedPreset = ParsePreset(preset);
            minimumBoneLength = Mathf.Max(0.001f, minimumBoneLength);
            var drafts = BoneColliderLayout.Create(
                avatar,
                minimumBoneLength,
                parsedPreset);
            var gateway = new VrchatPhysBoneColliderGateway();
            gateway.LoadOwned(avatar, drafts);
            IReadOnlyList<GameObject> roots = searchRoots == null || searchRoots.Count == 0
                ? new[] { avatar }
                : searchRoots;
            var targets = gateway.FindPhysBoneTargets(avatar, roots);
            var warnings = new List<string>();
            if (!gateway.IsSdkAvailable)
            {
                warnings.Add("VRChat PhysBone Collider type was not found.");
            }

            if (!gateway.IsModularAvatarAvailable)
            {
                warnings.Add("Modular Avatar Bone Proxy type was not found.");
            }

            if (drafts.Count == 0)
            {
                warnings.Add("No collider candidates were found under the avatar's direct Armature.");
            }

            var paths = PhysBoneColliderGenerationPaths.Create(avatar);
            return new PhysBoneColliderPlanData
            {
                CanApply = gateway.IsAvailable &&
                           !EditorUtility.IsPersistent(avatar) &&
                           drafts.Count > 0,
                HasVrchatSdk = gateway.IsSdkAvailable,
                HasModularAvatar = gateway.IsModularAvatarAvailable,
                Preset = parsedPreset.ToString(),
                MinimumBoneLength = minimumBoneLength,
                PrefabPath = paths.PrefabPath,
                Colliders = drafts.Select(ToData).ToArray(),
                PhysBoneTargets = targets.Select(target => new PhysBoneTargetData
                {
                    Path = target.Path,
                    ComponentCount = target.ComponentCount,
                    TransformPaths = target.TransformPaths
                }).ToArray(),
                Warnings = warnings
            };
        }

        public static PhysBoneColliderApplyResult Apply(
            GameObject avatar,
            string preset,
            float minimumBoneLength,
            IReadOnlyList<GameObject> searchRoots,
            IReadOnlyList<PhysBoneColliderOverrideData> overrides,
            bool assignAllTargets)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar));
            }

            var parsedPreset = ParsePreset(preset);
            var drafts = BoneColliderLayout.Create(
                avatar,
                Mathf.Max(0.001f, minimumBoneLength),
                parsedPreset);
            var gateway = new VrchatPhysBoneColliderGateway();
            gateway.LoadOwned(avatar, drafts);
            IReadOnlyList<GameObject> roots = searchRoots == null || searchRoots.Count == 0
                ? new[] { avatar }
                : searchRoots;
            var targets = gateway.FindPhysBoneTargets(avatar, roots);
            var byId = drafts.ToDictionary(Id, StringComparer.Ordinal);
            foreach (var value in overrides ??
                         Array.Empty<PhysBoneColliderOverrideData>())
            {
                if (value == null || string.IsNullOrWhiteSpace(value.Id) ||
                    !byId.TryGetValue(value.Id, out var draft))
                {
                    throw new InvalidOperationException(
                        "A collider override id no longer matches the current plan.");
                }

                if (value.Enabled.HasValue)
                {
                    draft.Enabled = value.Enabled.Value;
                }

                if (!string.IsNullOrWhiteSpace(value.BonePath))
                {
                    var bone = avatar.transform.Find(value.BonePath);
                    if (bone == null)
                    {
                        throw new InvalidOperationException(
                            "Collider bone path was not found: " + value.BonePath);
                    }

                    draft.Rebind(bone, avatar.transform);
                }

                if (value.Position != null)
                {
                    draft.Position = Vector(value.Position, "position");
                }

                if (value.Rotation != null)
                {
                    draft.Rotation = Rotation(value.Rotation);
                }

                if (value.Radius.HasValue)
                {
                    draft.Radius = Mathf.Max(0.001f, value.Radius.Value);
                }

                if (value.Height.HasValue)
                {
                    draft.Height = Mathf.Max(draft.Radius * 2f, value.Height.Value);
                }

                if (value.AssignedPhysBonePaths != null)
                {
                    draft.AssignedPhysBonePaths.Clear();
                    foreach (var path in value.AssignedPhysBonePaths
                                 .Where(path => !string.IsNullOrWhiteSpace(path)))
                    {
                        draft.AssignedPhysBonePaths.Add(path);
                    }
                }
            }

            if (assignAllTargets)
            {
                var paths = targets.Select(target => target.Path).ToArray();
                foreach (var draft in drafts)
                {
                    draft.AssignedPhysBonePaths.Clear();
                    foreach (var path in paths)
                    {
                        draft.AssignedPhysBonePaths.Add(path);
                    }
                }
            }

            var succeeded = gateway.TryApply(
                avatar,
                drafts,
                out var createdCount,
                out var error);
            return new PhysBoneColliderApplyResult
            {
                Succeeded = succeeded,
                ErrorCode = error ?? string.Empty,
                CreatedCount = createdCount,
                PrefabPath = PhysBoneColliderGenerationPaths.Create(avatar).PrefabPath
            };
        }

        private static PhysBoneColliderData ToData(PhysBoneColliderDraft draft)
        {
            return new PhysBoneColliderData
            {
                Id = Id(draft),
                BonePath = draft.Path,
                SuggestedBonePath = draft.SuggestedPath,
                TargetPath = draft.TargetPath,
                BoneLength = draft.BoneLength,
                Enabled = draft.Enabled,
                Position = new[]
                {
                    draft.Position.x,
                    draft.Position.y,
                    draft.Position.z
                },
                Rotation = new[]
                {
                    draft.Rotation.x,
                    draft.Rotation.y,
                    draft.Rotation.z,
                    draft.Rotation.w
                },
                Radius = draft.Radius,
                Height = draft.Height,
                AssignedPhysBonePaths = draft.AssignedPhysBonePaths
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        private static string Id(PhysBoneColliderDraft draft)
        {
            return Hash128.Compute(draft.LayoutId).ToString();
        }

        private static ColliderLayoutPreset ParsePreset(string value)
        {
            if (Enum.TryParse(value ?? "Standard", true, out ColliderLayoutPreset preset))
            {
                return preset;
            }

            throw new ArgumentException(
                "preset must be Lightweight, Standard, or Full.",
                nameof(value));
        }

        private static Vector3 Vector(float[] values, string name)
        {
            if (values.Length != 3 || values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
            {
                throw new ArgumentException(name + " must contain three finite numbers.");
            }

            return new Vector3(values[0], values[1], values[2]);
        }

        private static Quaternion Rotation(float[] values)
        {
            if (values.Length != 4 || values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
            {
                throw new ArgumentException("rotation must contain four finite quaternion values.");
            }

            var rotation = new Quaternion(values[0], values[1], values[2], values[3]);
            var squaredMagnitude = rotation.x * rotation.x +
                                   rotation.y * rotation.y +
                                   rotation.z * rotation.z +
                                   rotation.w * rotation.w;
            if (Mathf.Approximately(squaredMagnitude, 0f))
            {
                throw new ArgumentException("rotation quaternion cannot be zero.");
            }

            return rotation.normalized;
        }
    }
}
