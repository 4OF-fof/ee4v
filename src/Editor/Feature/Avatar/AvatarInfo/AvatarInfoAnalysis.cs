using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using nadena.dev.modular_avatar.core;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarInfo
{
    public static class AvatarInfoAnalysis
    {
        public sealed class PerformanceReport
        {
            [JsonProperty]
            public string Rating { get; set; }
            [JsonProperty]
            public IReadOnlyList<PerformanceMetric> Metrics { get; set; }
        }

        public sealed class PerformanceMetric
        {
            [JsonProperty]
            public string Category { get; set; }
            [JsonProperty]
            public string Value { get; set; }
            [JsonProperty]
            public string Rating { get; set; }
            [JsonProperty]
            public double? Amount { get; set; }
            [JsonProperty]
            public string TargetRating { get; set; }
            [JsonProperty]
            public double? TargetLimit { get; set; }
            [JsonProperty]
            public string TargetLimitLabel { get; set; }
        }

        private static readonly (string Category, string Field)[] Metrics =
        {
            ("PolyCount", "polyCount"),
            ("SkinnedMeshCount", "skinnedMeshCount"),
            ("MeshCount", "meshCount"),
            ("MaterialCount", "materialCount"),
            ("BoneCount", "boneCount"),
            ("TextureMegabytes", "textureMegabytes"),
            ("AnimatorCount", "animatorCount"),
            ("PhysBoneComponentCount", "physBone.componentCount"),
            ("PhysBoneTransformCount", "physBone.transformCount"),
            ("PhysBoneColliderCount", "physBone.colliderCount"),
            ("PhysBoneCollisionCheckCount", "physBone.collisionCheckCount"),
            ("ContactCount", "contactCount"),
            ("ConstraintsCount", "constraintsCount"),
            ("ConstraintDepth", "constraintDepth"),
            ("ParticleSystemCount", "particleSystemCount"),
            ("ParticleTotalCount", "particleTotalCount"),
            ("LightCount", "lightCount"),
            ("AudioSourceCount", "audioSourceCount")
        };

        public static IReadOnlyList<GameObject> FindAttachmentWarnings(
            GameObject avatar)
        {
            var warnings = new List<GameObject>();
            var variant = PrefabUtility.GetCorrespondingObjectFromSource(avatar);
            var basePrefab = variant != null
                ? PrefabUtility.GetCorrespondingObjectFromSource(variant) : null;
            var basePath = basePrefab != null
                ? AssetDatabase.GetAssetPath(basePrefab) : string.Empty;
            foreach (Transform transform in avatar.transform)
            {
                var child = transform.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child) ||
                    !string.IsNullOrEmpty(basePath) &&
                    PrefabUtility.GetCorrespondingObjectFromSourceAtPath(
                        child, basePath) != null)
                {
                    continue;
                }
                if (child.GetComponentInChildren<ModularAvatarMergeArmature>(true) == null &&
                    child.GetComponentInChildren<ModularAvatarBoneProxy>(true) == null)
                {
                    warnings.Add(child);
                }
            }
            return warnings;
        }

        public static bool TryReadPerformance(
            GameObject avatar,
            bool mobile,
            out string overallRating,
            out IReadOnlyList<PerformanceMetric> metrics,
            out string error)
        {
            overallRating = null;
            metrics = Array.Empty<PerformanceMetric>();
            error = null;
            var performanceType = FindSdkType(
                "VRC.SDKBase.Validation.Performance.AvatarPerformance");
            var statsType = FindSdkType(
                "VRC.SDKBase.Validation.Performance.Stats.AvatarPerformanceStats");
            if (performanceType == null || statsType == null)
            {
                return false;
            }
            try
            {
                var calculate = performanceType.GetMethod("CalculatePerformanceStats",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(string), typeof(GameObject), statsType, typeof(bool) }, null);
                var ratingMethod = statsType.GetMethods().Single(method =>
                    method.Name == "GetPerformanceRatingForCategory" &&
                    method.GetParameters().Length == 1);
                if (calculate == null)
                {
                    throw new MissingMethodException(performanceType.FullName,
                        "CalculatePerformanceStats");
                }
                var stats = System.Activator.CreateInstance(statsType, new object[] { mobile });
                RefreshConstraintGroups(avatar);
                calculate.Invoke(null, new[] { avatar.name, (object)avatar, stats, mobile });
                var categoryType = ratingMethod.GetParameters()[0].ParameterType;
                string ReadRating(string category) => ratingMethod.Invoke(stats,
                    new[] { Enum.Parse(categoryType, category) })?.ToString();
                overallRating = ReadRating("Overall");
                var limitMethod = statsType.GetMethod("GetStatLevelForRating",
                    BindingFlags.Public | BindingFlags.Static);
                var limitsByRating = new Dictionary<string, object>(StringComparer.Ordinal);
                var result = new List<PerformanceMetric>();
                foreach (var metric in Metrics)
                {
                    if (!Enum.IsDefined(categoryType, metric.Category))
                    {
                        continue;
                    }
                    var value = ReadFieldPath(stats, metric.Field);
                    var rating = ReadRating(metric.Category);
                    var targetRating = GetTargetRating(rating);
                    object limits = null;
                    if (targetRating != null && limitMethod != null &&
                        !limitsByRating.TryGetValue(targetRating, out limits))
                    {
                        limits = limitMethod.Invoke(null, new[]
                        {
                            Enum.Parse(ratingMethod.ReturnType, targetRating), (object)mobile
                        });
                        limitsByRating[targetRating] = limits;
                    }
                    var limit = ReadFieldPath(limits, metric.Field);
                    result.Add(new PerformanceMetric
                    {
                        Category = metric.Category,
                        Value = FormatValue(value, metric.Category == "TextureMegabytes"),
                        Rating = rating,
                        Amount = ReadNumber(value),
                        TargetRating = targetRating,
                        TargetLimit = ReadNumber(limit),
                        TargetLimitLabel = FormatValue(limit,
                            metric.Category == "TextureMegabytes")
                    });
                }
                metrics = result;
                return true;
            }
            catch (Exception exception)
            {
                error = (exception is TargetInvocationException invocation &&
                    invocation.InnerException != null
                        ? invocation.InnerException : exception).Message;
                return false;
            }
        }

        public static bool HasAaoComponents(GameObject avatar)
        {
            var componentType = FindSdkType("Anatawa12.AvatarOptimizer.AvatarTagComponent");
            return componentType != null && avatar.GetComponentInChildren(componentType, true) != null;
        }
        private static Type FindSdkType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name, false))
                .FirstOrDefault(type => type != null);
        }

        private static string GetTargetRating(string rating)
        {
            switch (rating)
            {
                case "VeryPoor": return "Poor";
                case "Poor": return "Medium";
                case "Medium": return "Good";
                case "Good":
                case "Excellent": return "Excellent";
                default: return null;
            }
        }

        private static void RefreshConstraintGroups(GameObject avatar)
        {
            var managerType = FindSdkType("VRC.Dynamics.VRCConstraintManager");
            var refresh = managerType?.GetMethod("Sdk_ManuallyRefreshGroups",
                BindingFlags.Public | BindingFlags.Static);
            if (refresh == null) { return; }
            var componentType = refresh.GetParameters()[0].ParameterType.GetElementType();
            var components = avatar.GetComponentsInChildren(componentType, true);
            var typedComponents = Array.CreateInstance(componentType, components.Length);
            Array.Copy(components, typedComponents, components.Length);
            refresh.Invoke(null, new object[] { typedComponents });
        }

        private static object ReadFieldPath(object target, string path)
        {
            foreach (var name in path.Split('.'))
            {
                if (target == null) { return null; }
                var type = target.GetType();
                var field = type.GetField(name);
                target = field != null ? field.GetValue(target)
                    : type.GetProperty(name)?.GetValue(target);
            }
            return target;
        }

        private static string FormatValue(object value, bool megabytes)
        {
            if (value == null) { return "—"; }
            return value is IFormattable number
                ? number.ToString(megabytes ? "N2" : "N0", CultureInfo.CurrentCulture) +
                    (megabytes ? " MB" : string.Empty)
                : value.ToString();
        }

        private static double? ReadNumber(object value)
        {
            if (!(value is IConvertible)) { return null; }
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
    }
}
