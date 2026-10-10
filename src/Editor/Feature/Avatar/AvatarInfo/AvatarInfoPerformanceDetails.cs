using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ee4v.AvatarInfo
{
    public static partial class AvatarInfoAnalysis
    {
        internal static void CapturePerformanceDetails(GameObject avatar, bool mobile,
            IReadOnlyList<PerformanceMetric> metrics, Func<UnityEngine.Object, PerformanceOrigin> origin)
        {
            var rows = metrics.ToDictionary(metric => metric.Category, _ => new List<PerformanceDetail>());
            foreach (var metric in metrics)
            {
                metric.DetailUnit = GetDetailUnit(metric.Category);
                metric.DetailFailures = 0;
                metric.ConstraintGraph = null;
            }
            var owners = new Dictionary<Component, HashSet<string>>();
            void Add(Component component, params string[] categories)
            {
                if (component == null) { return; }
                if (!owners.TryGetValue(component, out var set))
                    owners.Add(component, set = new HashSet<string>(StringComparer.Ordinal));
                foreach (var category in categories) { set.Add(category); }
            }
            void AddSdk(string typeName, params string[] categories)
            {
                var type = FindSdkType(typeName);
                if (type == null) { return; }
                foreach (var component in avatar.GetComponentsInChildren(type, true)) { Add(component, categories); }
            }
            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true))
            {
                Add(renderer, "MaterialCount");
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) { continue; }
                Add(renderer, "PolyCount",
                    renderer is SkinnedMeshRenderer ? "SkinnedMeshCount" : "MeshCount");
            }
            foreach (var component in avatar.GetComponentsInChildren<Animator>(true)) { Add(component, "AnimatorCount"); }
            foreach (var component in avatar.GetComponentsInChildren<ParticleSystem>(true))
                Add(component, "ParticleSystemCount", "ParticleTotalCount");
            foreach (var component in avatar.GetComponentsInChildren<Light>(true)) { Add(component, "LightCount"); }
            foreach (var component in avatar.GetComponentsInChildren<AudioSource>(true)) { Add(component, "AudioSourceCount"); }
            AddSdk("VRC.Dynamics.VRCPhysBoneBase", "PhysBoneComponentCount", "PhysBoneTransformCount", "PhysBoneCollisionCheckCount");
            AddSdk("VRC.Dynamics.ContactBase", "ContactCount");
            AddSdk("VRC.Dynamics.VRCConstraintBase", "ConstraintsCount");
            foreach (var component in avatar.GetComponentsInChildren<UnityEngine.Animations.IConstraint>(true)
                         .OfType<Component>()) { Add(component, "ConstraintsCount"); }

            var renderers = new List<Renderer>();
            var bones = new HashSet<Transform>();
            var constraints = new List<Component>();
            var colliderUsers = new Dictionary<Component, List<Component>>();
            var physBones = new Dictionary<string, List<(Component Component, PerformanceDetail Detail)>>();
            foreach (var owner in owners)
            {
                var particleSystem = owner.Key is ParticleSystemRenderer
                    ? owner.Key.GetComponent<ParticleSystem>() : null;
                if (!TryReadPerformance(avatar, mobile, out _, out var values, out _, component =>
                        component == owner.Key || particleSystem != null && component == particleSystem))
                {
                    foreach (var metric in metrics.Where(metric => owner.Value.Contains(metric.Category)))
                        metric.DetailFailures++;
                    if (owner.Key is Renderer)
                    {
                        foreach (var metric in metrics.Where(metric => metric.Category == "BoneCount" ||
                                     metric.Category == "TextureMegabytes")) { metric.DetailFailures++; }
                    }
                    continue;
                }
                foreach (var value in values.Where(value => owner.Value.Contains(value.Category) &&
                             value.Amount.GetValueOrDefault() > 0))
                {
                    if (rows.TryGetValue(value.Category, out var details))
                    {
                        var detail = CreateComponentDetail(avatar, owner.Key, value.Amount.Value,
                            FormatDetailValue(value), origin?.Invoke(owner.Key), value.Category == "ConstraintsCount");
                        if (GetDetailUnit(value.Category) == PerformanceDetailUnit.PhysBone)
                        {
                            if (!physBones.TryGetValue(value.Category, out var attachments))
                                physBones.Add(value.Category, attachments = new List<(Component, PerformanceDetail)>());
                            attachments.Add((owner.Key, detail));
                        }
                        else { details.Add(detail); }
                    }
                }
                if (owner.Key is Renderer renderer)
                {
                    if (values.Any(value => value.Category == "TextureMegabytes" && value.Amount.GetValueOrDefault() > 0))
                        renderers.Add(renderer);
                    if (renderer is SkinnedMeshRenderer skinned && values.Any(value =>
                            value.Category == "BoneCount" && value.Amount.GetValueOrDefault() > 0))
                        bones.UnionWith(skinned.bones.Where(bone => bone != null));
                }
                if (owner.Value.Contains("ConstraintsCount") && values.Any(value =>
                        value.Category == "ConstraintsCount" && value.Amount.GetValueOrDefault() > 0))
                    constraints.Add(owner.Key);
                if (owner.Value.Contains("PhysBoneComponentCount") && values.Any(value =>
                        value.Category == "PhysBoneComponentCount" && value.Amount.GetValueOrDefault() > 0) &&
                    ReadFieldPath(owner.Key, "colliders") is System.Collections.IEnumerable colliders)
                {
                    foreach (var collider in colliders.Cast<object>().OfType<Component>().Where(collider => collider != null).Distinct())
                    {
                        if (!colliderUsers.TryGetValue(collider, out var users))
                            colliderUsers.Add(collider, users = new List<Component>());
                        users.Add(owner.Key);
                    }
                }
            }
            CaptureBoneDetails(avatar, metrics, rows, bones, origin);
            CapturePhysBoneDetails(avatar, metrics, rows, physBones, origin);
            CaptureColliderDetails(avatar, metrics, rows, colliderUsers, origin);
            CaptureTextureDetails(avatar, mobile, metrics, rows, renderers, origin);
            CaptureConstraintDepthDetails(avatar, mobile, metrics, rows, constraints, origin);
            RefreshConstraintGroups(avatar);
            foreach (var metric in metrics)
            {
                if (physBones.ContainsKey(metric.Category)) { metric.DetailUnit = PerformanceDetailUnit.Prefab; }
                metric.Details = rows[metric.Category].OrderByDescending(row => row.Amount)
                    .ThenBy(row => row.Path, StringComparer.Ordinal).ToArray();
                var combined = metric.Category == "ConstraintDepth"
                    ? metric.Details.Select(row => row.Amount).DefaultIfEmpty(0).Max()
                    : metric.Details.Sum(row => row.Amount);
                if (metric.Amount.HasValue && metric.DetailFailures == 0 && Math.Abs(combined - metric.Amount.Value) >
                    Math.Max(0.00001, Math.Abs(metric.Amount.Value) * 0.000001)) { metric.DetailFailures++; }
            }
        }

        private static PerformanceDetailUnit GetDetailUnit(string category)
        {
            switch (category)
            {
                case "PolyCount": return PerformanceDetailUnit.Mesh;
                case "BoneCount": return PerformanceDetailUnit.Prefab;
                case "TextureMegabytes": return PerformanceDetailUnit.Texture;
                case "MaterialCount":
                case "SkinnedMeshCount":
                case "MeshCount": return PerformanceDetailUnit.Renderer;
                case "PhysBoneComponentCount":
                case "PhysBoneTransformCount":
                case "PhysBoneCollisionCheckCount": return PerformanceDetailUnit.PhysBone;
                case "PhysBoneColliderCount": return PerformanceDetailUnit.Collider;
                case "ContactCount": return PerformanceDetailUnit.Contact;
                case "ConstraintsCount": return PerformanceDetailUnit.Constraint;
                case "ConstraintDepth": return PerformanceDetailUnit.ConstraintPath;
                case "AnimatorCount": return PerformanceDetailUnit.Animator;
                case "ParticleSystemCount":
                case "ParticleTotalCount": return PerformanceDetailUnit.ParticleSystem;
                case "LightCount": return PerformanceDetailUnit.Light;
                default: return PerformanceDetailUnit.AudioSource;
            }
        }

        private static PerformanceDetail CreateComponentDetail(GameObject avatar, Component component,
            double amount, string value, PerformanceOrigin origin, bool captureConstraint = false)
        {
            var index = Array.IndexOf(component.GetComponents(component.GetType()), component);
            return new PerformanceDetail
            {
                Name = component.name,
                Path = GetPerformanceDetailPath(avatar.transform, component.transform) +
                    " / " + component.GetType().Name + "[" + index + "]",
                DisplayPath = GetPerformanceDetailPath(avatar.transform, component.transform, false),
                Amount = amount, Value = value,
                ItemName = origin?.Name, ItemPath = origin?.Path, ItemAssetPath = origin?.AssetPath,
                Constraint = captureConstraint ? CaptureConstraintRelation(avatar, component) : null
            };
        }

        private static PerformanceConstraintRelation CaptureConstraintRelation(GameObject avatar, Component component)
        {
            PerformanceConstraintEndpoint Endpoint(Transform transform, double? weight = null) =>
                new PerformanceConstraintEndpoint
                {
                    Name = transform != null ? transform.name : null,
                    Path = transform != null ? GetPerformanceDetailPath(avatar.transform, transform) : null,
                    DisplayPath = transform != null ? GetPerformanceDetailPath(avatar.transform, transform, false) : null,
                    Weight = weight
                };
            if (component is UnityEngine.Animations.IConstraint unityConstraint)
            {
                return new PerformanceConstraintRelation
                {
                    Type = component.GetType().Name,
                    Target = Endpoint(component.transform),
                    Sources = Enumerable.Range(0, unityConstraint.sourceCount).Select(index =>
                    {
                        var source = unityConstraint.GetSource(index);
                        return Endpoint(source.sourceTransform, source.weight);
                    }).ToArray()
                };
            }
            var vrcType = FindSdkType("VRC.Dynamics.VRCConstraintBase");
            if (vrcType == null || !vrcType.IsInstanceOfType(component)) { return null; }
            var target = ReadFieldPath(component, "TargetTransform") as Transform;
            if (target == null) { target = component.transform; }
            var sources = ReadFieldPath(component, "Sources") as System.Collections.IEnumerable;
            return new PerformanceConstraintRelation
            {
                Type = component.GetType().Name,
                Target = Endpoint(target),
                Sources = sources?.Cast<object>().Select(source => Endpoint(
                    ReadFieldPath(source, "SourceTransform") as Transform,
                    ReadFieldPath(source, "Weight") is float weight ? (double?)weight : null)).ToArray()
                    ?? Array.Empty<PerformanceConstraintEndpoint>()
            };
        }

        private static string FormatDetailValue(PerformanceMetric metric) => metric.Category == "TextureMegabytes"
            && metric.Amount.GetValueOrDefault() < 0.01
                ? metric.Amount.GetValueOrDefault().ToString("0.######", CultureInfo.InvariantCulture) + " MB"
                : metric.Value;

        private static void CaptureBoneDetails(GameObject avatar, IReadOnlyList<PerformanceMetric> metrics,
            Dictionary<string, List<PerformanceDetail>> rows, HashSet<Transform> bones,
            Func<UnityEngine.Object, PerformanceOrigin> origin)
        {
            var metric = metrics.FirstOrDefault(value => value.Category == "BoneCount");
            if (metric == null) { return; }
            if (metric.Amount.GetValueOrDefault() != bones.Count)
            {
                metric.DetailFailures++;
                return;
            }
            foreach (var group in bones.Select(bone => new { Bone = bone, Origin = origin?.Invoke(bone) })
                         .GroupBy(item => item.Origin?.Path))
            {
                var source = group.First().Origin;
                var used = new HashSet<Transform>(group.Select(item => item.Bone));
                var hierarchy = new HashSet<Transform>();
                foreach (var bone in used)
                {
                    for (var current = bone; current != null; current = current.parent)
                    {
                        if (current == avatar.transform && !used.Contains(current)) { break; }
                        hierarchy.Add(current);
                        if (current == avatar.transform) { break; }
                    }
                }
                var nodes = hierarchy.OrderBy(node => GetPerformanceDetailPath(avatar.transform, node), StringComparer.Ordinal)
                    .Select(node =>
                    {
                        var detail = CreateComponentDetail(avatar, node, used.Contains(node) ? 1 : 0,
                            used.Contains(node) ? "1" : "0", origin?.Invoke(node));
                        detail.Path = GetPerformanceDetailPath(avatar.transform, node);
                        detail.ParentPath = node.parent != null && hierarchy.Contains(node.parent)
                            ? GetPerformanceDetailPath(avatar.transform, node.parent) : string.Empty;
                        return detail;
                    }).ToArray();
                rows[metric.Category].Add(new PerformanceDetail
                {
                    Name = source?.Name, Path = source?.Path,
                    ItemName = source?.Name, ItemPath = source?.Path, ItemAssetPath = source?.AssetPath,
                    Amount = group.Count(), Value = group.Count().ToString("N0", CultureInfo.InvariantCulture),
                    Children = nodes
                });
            }
        }

        private static void CapturePhysBoneDetails(GameObject avatar, IReadOnlyList<PerformanceMetric> metrics,
            Dictionary<string, List<PerformanceDetail>> rows,
            Dictionary<string, List<(Component Component, PerformanceDetail Detail)>> attachments,
            Func<UnityEngine.Object, PerformanceOrigin> origin)
        {
            foreach (var category in attachments)
            {
                var metric = metrics.First(value => value.Category == category.Key);
                foreach (var group in category.Value.GroupBy(item => item.Detail.ItemPath))
                {
                    var roots = new Dictionary<Transform, List<PerformanceDetail>>();
                    foreach (var item in group)
                    {
                        var root = ReadFieldPath(item.Component, "rootTransform") as Transform ?? item.Component.transform;
                        if (root != avatar.transform && !root.IsChildOf(avatar.transform))
                        {
                            metric.DetailFailures++;
                            continue;
                        }
                        if (!roots.TryGetValue(root, out var components))
                            roots.Add(root, components = new List<PerformanceDetail>());
                        components.Add(item.Detail);
                    }
                    if (roots.Count == 0) { continue; }
                    var hierarchy = new HashSet<Transform>();
                    foreach (var root in roots.Keys)
                    {
                        for (var current = root; current != null; current = current.parent)
                        {
                            if (current == avatar.transform && !roots.ContainsKey(current)) { break; }
                            hierarchy.Add(current);
                            if (current == avatar.transform) { break; }
                        }
                    }
                    var nodes = hierarchy.OrderBy(node => GetPerformanceDetailPath(avatar.transform, node), StringComparer.Ordinal)
                        .Select(node =>
                        {
                            roots.TryGetValue(node, out var components);
                            var amount = components?.Sum(component => component.Amount) ?? 0;
                            var detail = CreateComponentDetail(avatar, node, amount,
                                amount.ToString("N0", CultureInfo.InvariantCulture), origin?.Invoke(node));
                            detail.Path = GetPerformanceDetailPath(avatar.transform, node);
                            detail.ParentPath = node.parent != null && hierarchy.Contains(node.parent)
                                ? GetPerformanceDetailPath(avatar.transform, node.parent) : string.Empty;
                            detail.Children = components?.ToArray();
                            return detail;
                        }).ToArray();
                    var source = group.First().Detail;
                    var total = nodes.Sum(node => node.Amount);
                    rows[category.Key].Add(new PerformanceDetail
                    {
                        Name = source.ItemName, Path = source.ItemPath,
                        ItemName = source.ItemName, ItemPath = source.ItemPath, ItemAssetPath = source.ItemAssetPath,
                        Amount = total, Value = total.ToString("N0", CultureInfo.InvariantCulture), Children = nodes
                    });
                }
            }
        }

        private static void CaptureConstraintDepthDetails(GameObject avatar, bool mobile,
            IReadOnlyList<PerformanceMetric> metrics, Dictionary<string, List<PerformanceDetail>> rows,
            IReadOnlyList<Component> constraints, Func<UnityEngine.Object, PerformanceOrigin> origin)
        {
            var metric = metrics.FirstOrDefault(value => value.Category == "ConstraintDepth");
            if (metric == null) { return; }
            RefreshConstraintGroups(avatar);
            var vrcType = FindSdkType("VRC.Dynamics.VRCConstraintBase");
            var dependentsField = vrcType?.GetField("_dependents",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dependentsField == null) { metric.DetailFailures++; return; }
            var indices = constraints.ToDictionary(component => component, component => component.GetType()
                .GetProperty("LatestValidExecutionGroupIndex")?.GetValue(component) as int?);
            var parents = constraints.ToDictionary(component => component, _ => new List<Component>());
            var terminals = new HashSet<Component>(constraints);
            foreach (var component in constraints)
            {
                if (!vrcType.IsInstanceOfType(component) || !indices[component].HasValue ||
                    !(dependentsField.GetValue(component) is System.Collections.IEnumerable dependents))
                {
                    metric.DetailFailures++;
                    return;
                }
                foreach (var dependent in dependents.Cast<object>().OfType<Component>().Where(parents.ContainsKey))
                {
                    if (indices[dependent] <= indices[component]) { metric.DetailFailures++; return; }
                    parents[dependent].Add(component);
                    terminals.Remove(component);
                }
            }
            var captured = constraints.ToDictionary(component => component, component =>
                CreateComponentDetail(avatar, component, 1, "1", origin?.Invoke(component), true));
            foreach (var component in constraints)
                captured[component].Constraint.Dependencies = parents[component]
                    .Select(parent => captured[parent].Path).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var chains = new Dictionary<Component, Component[]>();
            foreach (var component in constraints.OrderBy(component => indices[component])
                         .ThenBy(component => captured[component].Path, StringComparer.Ordinal))
            {
                var predecessor = parents[component].OrderByDescending(parent => chains[parent].Length)
                    .ThenBy(parent => captured[parent].Path, StringComparer.Ordinal).FirstOrDefault();
                chains.Add(component, (predecessor != null ? chains[predecessor] : Array.Empty<Component>())
                    .Concat(new[] { component }).ToArray());
            }
            foreach (var group in constraints.GroupBy(component => indices[component]))
            {
                if (!group.Key.HasValue) { metric.DetailFailures += group.Count(); continue; }
                var members = new HashSet<Component>(group);
                if (!TryReadPerformance(avatar, mobile, out _, out var values, out _, members.Contains))
                {
                    metric.DetailFailures++;
                    continue;
                }
                var depth = values.FirstOrDefault(value => value.Category == metric.Category);
                if (depth?.Amount.GetValueOrDefault() <= 0 || depth?.Amount == null) { continue; }
                foreach (var component in group)
                    captured[component].Constraint.Depth = (int)depth.Amount.Value;
                foreach (var terminal in group.Where(terminals.Contains))
                {
                    var chain = chains[terminal].Select(component => captured[component]).ToArray();
                    if (chain.Length != depth.Amount.Value) { metric.DetailFailures++; continue; }
                    var sources = chain[0].Constraint.Sources.Where(source => source.Name != null)
                        .Select(source => source.Name).Distinct().ToArray();
                    var names = chain.Select(detail => detail.Constraint.Target.Name ?? detail.Name);
                    var routeName = string.Join(" → ", (sources.Length > 0
                        ? new[] { string.Join(" / ", sources) } : Array.Empty<string>()).Concat(names));
                    rows[metric.Category].Add(new PerformanceDetail
                    {
                        Name = routeName, Path = string.Join("\n→ ", chain.Select(detail => detail.Path)),
                        Amount = depth.Amount.Value, Value = depth.Value,
                        Children = chain
                    });
                }
            }
            if (metric.DetailFailures == 0 && captured.Values.All(detail => detail.Constraint.Depth.HasValue))
                metric.ConstraintGraph = captured.Values.OrderBy(detail => detail.Path, StringComparer.Ordinal).ToArray();
        }

        private static void CaptureColliderDetails(GameObject avatar, IReadOnlyList<PerformanceMetric> metrics,
            Dictionary<string, List<PerformanceDetail>> rows, Dictionary<Component, List<Component>> colliderUsers,
            Func<UnityEngine.Object, PerformanceOrigin> origin)
        {
            var metric = metrics.FirstOrDefault(value => value.Category == "PhysBoneColliderCount");
            if (metric == null) { return; }
            if (metric.Amount.GetValueOrDefault() != colliderUsers.Count)
            {
                metric.DetailFailures++;
                return;
            }
            foreach (var item in colliderUsers)
            {
                var detail = CreateComponentDetail(avatar, item.Key, 1, "1", origin?.Invoke(item.Key));
                detail.Children = item.Value.Select(component => CreateComponentDetail(avatar, component, 1, "1",
                    origin?.Invoke(component))).OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
                rows[metric.Category].Add(detail);
            }
        }

        private sealed class TextureUsage
        {
            internal Material Material;
            internal string Property;
            internal readonly HashSet<Renderer> Renderers = new HashSet<Renderer>();
        }

        private static void CaptureTextureDetails(GameObject avatar, bool mobile,
            IReadOnlyList<PerformanceMetric> metrics, Dictionary<string, List<PerformanceDetail>> rows,
            IReadOnlyList<Renderer> renderers, Func<UnityEngine.Object, PerformanceOrigin> origin)
        {
            var metric = metrics.FirstOrDefault(value => value.Category == "TextureMegabytes");
            if (metric == null) { return; }
            var textures = new Dictionary<Texture, TextureUsage>();
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials.Where(material => material != null).Distinct())
            foreach (var property in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(property);
                if (texture == null) { continue; }
                if (!textures.TryGetValue(texture, out var usage))
                    textures.Add(texture, usage = new TextureUsage { Material = material, Property = property });
                usage.Renderers.Add(renderer);
            }
            if (textures.Count == 0) { return; }
            var scene = EditorSceneManager.NewPreviewScene();
            var mesh = new Mesh { name = "ee4v Texture Measurement" };
            Material isolated = null;
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2 };
                var probe = new GameObject("ee4v Texture Measurement");
                SceneManager.MoveGameObjectToScene(probe, scene);
                probe.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = probe.AddComponent<MeshRenderer>();
                var index = 0;
                foreach (var texture in textures.OrderBy(item => item.Key.name, StringComparer.Ordinal))
                {
                    if (isolated != null) { UnityEngine.Object.DestroyImmediate(isolated); }
                    isolated = new Material(texture.Value.Material);
                    foreach (var property in isolated.GetTexturePropertyNames()) { isolated.SetTexture(property, null); }
                    isolated.SetTexture(texture.Value.Property, texture.Key);
                    renderer.sharedMaterial = isolated;
                    if (!TryReadPerformance(probe, mobile, out _, out var values, out _))
                    {
                        metric.DetailFailures++;
                        continue;
                    }
                    var value = values.FirstOrDefault(entry => entry.Category == metric.Category);
                    var assetPath = origin?.Invoke(texture.Key)?.AssetPath;
                    if (origin == null && AssetDatabase.Contains(texture.Key))
                        assetPath = AssetDatabase.GetAssetPath(texture.Key);
                    if (value?.Amount.GetValueOrDefault() > 0)
                        rows[metric.Category].Add(new PerformanceDetail
                        {
                            Name = texture.Key.name,
                            Path = string.IsNullOrEmpty(assetPath) ? texture.Key.name + " [" + index + "]" : assetPath,
                            Amount = value.Amount.Value, Value = FormatDetailValue(value),
                            Children = texture.Value.Renderers.Select(user => CreateComponentDetail(avatar, user,
                                1, "1", origin?.Invoke(user))).OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray()
                        });
                    index++;
                }
            }
            finally
            {
                if (isolated != null) { UnityEngine.Object.DestroyImmediate(isolated); }
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
