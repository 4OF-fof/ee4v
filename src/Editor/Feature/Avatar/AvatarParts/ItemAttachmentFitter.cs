using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ee4v.AvatarParts
{
    /// <summary>Estimates the rounded mounting surface, excluding unrelated decorations by surface support.</summary>
    internal static class ItemAttachmentFitter
    {
        private const int SampleCount = 3000;
        private struct Sample { public Vector3 Point, Normal; }
        private struct Triangle { public Sample A, B, C; public float EndArea; }
        private struct Sphere { public Vector3 Center; public float Radius; }

        public static bool TryFit(Transform item, Transform avatar, Transform head,
            Func<Transform, bool> isBase, Func<Transform, bool> isVisible,
            out Vector3 position, out Vector3 scale)
        {
            position = item.localPosition;
            scale = item.localScale;
            if (head == null) return false;
            var inverse = Quaternion.Inverse(avatar.rotation);
            var source = ReadSurface(item.GetComponentsInChildren<Renderer>(true), inverse, avatar.position, null);
            var target = ReadSurface(avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => isBase(renderer.transform)), inverse, avatar.position, head);
            if (!TrySphere(source, false, out var sourceSphere) || !TrySphere(target, true, out var targetSphere)) return false;
            var existing = ReadSurface(avatar.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => !renderer.transform.IsChildOf(item) && isVisible(renderer.transform)),
                inverse, avatar.position, null, false, point => {
                    var delta = point - targetSphere.Center;
                    return delta.y >= targetSphere.Radius * 0.35f && delta.sqrMagnitude < targetSphere.Radius * targetSphere.Radius * 4f;
                });
            if (existing.Length > 0) targetSphere = FitMountingVolume(existing, targetSphere);
            // The estimated shell is an outer surface. Leave room for its thickness and small shape differences.
            var factor = targetSphere.Radius * 1.14f / sourceSphere.Radius;
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor < 0.1f || factor > 10f) return false;
            var sourceCenter = avatar.position + avatar.rotation * sourceSphere.Center;
            var targetCenter = avatar.position + avatar.rotation * targetSphere.Center;
            position = item.parent.InverseTransformPoint(targetCenter - (sourceCenter - item.position) * factor);
            scale = item.localScale * factor;
            return true;
        }

        private static Sample[] ReadSurface(IEnumerable<Renderer> renderers, Quaternion inverse,
            Vector3 origin, Transform bone, bool largest = true, Func<Vector3, bool> include = null)
        {
            var best = new List<Triangle>();
            var bestArea = 0f;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Mesh mesh = null;
                var owned = false;
                var skinned = renderer as SkinnedMeshRenderer;
                try
                {
                    if (skinned != null && skinned.sharedMesh != null)
                    {
                        mesh = new Mesh();
                        owned = true;
                        // true returns renderer-local vertices, including imported rigs with 100x child scale.
                        skinned.BakeMesh(mesh, true);
                    }
                    else if (bone == null && renderer is MeshRenderer) mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    var vertices = mesh.vertices;
                    var normals = mesh.normals;
                    if (normals.Length != vertices.Length) continue;
                    var accepted = bone == null ? null : DirectInfluence(skinned, bone, vertices.Length);
                    if (bone != null && accepted == null) continue;
                    var matrix = renderer.localToWorldMatrix;
                    var normalMatrix = matrix.inverse.transpose;
                    var points = new Sample[vertices.Length];
                    for (var i = 0; i < vertices.Length; i++)
                        points[i] = new Sample {
                            Point = inverse * (matrix.MultiplyPoint3x4(vertices[i]) - origin),
                            Normal = (inverse * normalMatrix.MultiplyVector(normals[i])).normalized
                        };
                    var indices = mesh.triangles;
                    var triangles = new List<Triangle>();
                    var area = 0f;
                    for (var i = 0; i + 2 < indices.Length; i += 3)
                    {
                        var a = indices[i]; var b = indices[i + 1]; var c = indices[i + 2];
                        if (accepted != null && (!accepted[a] || !accepted[b] || !accepted[c])) continue;
                        if (include != null && !include((points[a].Point + points[b].Point + points[c].Point) / 3f)) continue;
                        var size = Vector3.Cross(points[b].Point - points[a].Point, points[c].Point - points[a].Point).magnitude;
                        if (!(size > 0f) || float.IsInfinity(size)) continue;
                        area += size;
                        triangles.Add(new Triangle { A = points[a], B = points[b], C = points[c], EndArea = area });
                    }
                    if (largest)
                    {
                        if (area <= bestArea) continue;
                        best = triangles;
                        bestArea = area;
                    }
                    else
                    {
                        foreach (var triangle in triangles)
                        {
                            var next = triangle;
                            next.EndArea += bestArea;
                            best.Add(next);
                        }
                        bestArea += area;
                    }
                }
                catch (UnityException) { /* Unreadable optional geometry cannot provide a fitting surface. */ }
                finally { if (owned && mesh != null) UnityEngine.Object.DestroyImmediate(mesh); }
            }
            if (best.Count < 8) return Array.Empty<Sample>();
            var random = new System.Random(41);
            var result = new Sample[SampleCount];
            for (var i = 0; i < result.Length; i++)
            {
                var value = (float)random.NextDouble() * bestArea;
                var low = 0; var high = best.Count - 1;
                while (low < high)
                {
                    var mid = (low + high) / 2;
                    if (best[mid].EndArea < value) low = mid + 1; else high = mid;
                }
                var triangle = best[low];
                var u = (float)random.NextDouble(); var v = (float)random.NextDouble();
                if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                var w = 1f - u - v;
                result[i] = new Sample { Point = triangle.A.Point * u + triangle.B.Point * v + triangle.C.Point * w,
                    Normal = (triangle.A.Normal * u + triangle.B.Normal * v + triangle.C.Normal * w).normalized };
            }
            return result;
        }

        private static Sphere FitMountingVolume(Sample[] points, Sphere baseline)
        {
            var best = baseline;
            var bestScore = float.PositiveInfinity;
            var distances = new float[points.Length];
            // The densest mounting volume should determine size. Sparse strands must not inflate it.
            // Penalize lifting the center merely to obtain a smaller radius, which leaves visible air gaps.
            for (var y = 0; y <= 6; y++)
                for (var z = -3; z <= 3; z++)
                {
                    var offset = new Vector3(0f, y * baseline.Radius * 0.1f, z * baseline.Radius * 0.1f);
                    var center = baseline.Center + offset;
                    for (var i = 0; i < points.Length; i++) distances[i] = (points[i].Point - center).magnitude;
                    Array.Sort(distances);
                    var radius = Mathf.Max(baseline.Radius, distances[Mathf.Min(distances.Length - 1, Mathf.FloorToInt(distances.Length * 0.99f))]);
                    var score = radius + offset.magnitude * 0.15f;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = new Sphere { Center = center, Radius = radius };
                }
            return best;
        }

        private static bool[] DirectInfluence(SkinnedMeshRenderer renderer, Transform bone, int count)
        {
            if (renderer == null) return null;
            var bones = renderer.bones;
            var counts = renderer.sharedMesh.GetBonesPerVertex();
            var weights = renderer.sharedMesh.GetAllBoneWeights();
            try
            {
                if (counts.Length != count) return null;
                var result = new bool[count];
                var offset = 0;
                for (var i = 0; i < count; i++)
                {
                    var influence = 0f;
                    for (var j = 0; j < counts[i]; j++)
                    {
                        var weight = weights[offset++];
                        if (weight.boneIndex < bones.Length && bones[weight.boneIndex] == bone) influence += weight.weight;
                    }
                    result[i] = influence >= 0.8f;
                }
                return result;
            }
            finally { counts.Dispose(); weights.Dispose(); }
        }

        private static bool TrySphere(Sample[] points, bool body, out Sphere sphere)
        {
            sphere = default;
            if (points.Length < 100) return false;
            var minimum = points[0].Point; var maximum = minimum;
            foreach (var sample in points) { minimum = Vector3.Min(minimum, sample.Point); maximum = Vector3.Max(maximum, sample.Point); }
            var extent = maximum - minimum;
            var diameter = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));
            if (!(diameter > 0.00001f)) return false;
            var heights = points.Select(sample => sample.Point.y).OrderBy(value => value).ToArray();
            var centerX = (minimum.x + maximum.x) * 0.5f;
            var best = 0f;
            var random = new System.Random(817);
            var bins = new int[36];
            for (var iteration = 0; iteration < 5000; iteration++)
            {
                var a = points[random.Next(points.Length)]; var b = points[random.Next(points.Length)];
                var normalDifference = a.Normal - b.Normal;
                if (normalDifference.sqrMagnitude < 0.05f) continue;
                var radius = Vector3.Dot(a.Point - b.Point, normalDifference) / normalDifference.sqrMagnitude;
                // Use outward rounded surfaces; folds and inward decorations do not define the mounting shell.
                if (radius < diameter * 0.04f || radius > diameter * 0.75f) continue;
                var center = (a.Point + b.Point - radius * (a.Normal + b.Normal)) * 0.5f;
                if (Mathf.Abs(center.x - centerX) > extent.x * 0.15f ||
                    center.y < heights[heights.Length / 4] || center.y > maximum.y ||
                    center.z < minimum.z || center.z > maximum.z) continue;
                Array.Clear(bins, 0, bins.Length);
                var support = 0; var outside = 0; var upper = 0;
                foreach (var point in points)
                {
                    var delta = point.Point - center;
                    var distance = delta.magnitude;
                    if (body && point.Point.y > center.y)
                    {
                        upper++;
                        if (distance > radius * 1.1f) outside++;
                    }
                    if (distance < 0.000001f || Mathf.Abs(distance - radius) > radius * 0.06f) continue;
                    var unit = delta / distance;
                    if (unit.y < -0.35f || Vector3.Dot(unit, point.Normal) < 0.85f) continue;
                    var azimuth = Mathf.FloorToInt((Mathf.Atan2(unit.x, unit.z) + Mathf.PI) * (12f / (2f * Mathf.PI))) % 12;
                    var elevation = Mathf.FloorToInt(Mathf.Clamp(unit.y, 0f, 0.999f) * 3f);
                    bins[azimuth + elevation * 12]++;
                    support++;
                }
                if (support < points.Length * 0.035f || body && upper > 0 && outside > upper * 0.1f) continue;
                var score = 0f; var coverage = 0;
                foreach (var bin in bins) { score += Mathf.Sqrt(bin); if (bin > 0) coverage++; }
                if (coverage < 12 || score <= best) continue;
                best = score;
                sphere = new Sphere { Center = center, Radius = radius };
            }
            return best > 0f;
        }
    }
}
