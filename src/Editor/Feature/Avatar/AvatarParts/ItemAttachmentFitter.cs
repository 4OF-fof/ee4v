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
        private struct Constraint { public Vector4 Weights; public float Limit; }

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
                    return delta.y >= -targetSphere.Radius * 0.35f && delta.sqrMagnitude < targetSphere.Radius * targetSphere.Radius * 4f;
                });
            if (!TryFitShell(source, target, existing, sourceSphere, targetSphere, out var center, out var factor))
            {
                var upper = existing.Where(sample => sample.Point.y >= targetSphere.Center.y + targetSphere.Radius * 0.35f).ToArray();
                if (upper.Length > 0) targetSphere = FitMountingVolume(upper, targetSphere);
                factor = targetSphere.Radius * 1.02f / sourceSphere.Radius;
                center = targetSphere.Center;
            }
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor < 0.1f || factor > 10f) return false;
            var sourceCenter = avatar.position + avatar.rotation * sourceSphere.Center;
            var targetCenter = avatar.position + avatar.rotation * center;
            position = item.parent.InverseTransformPoint(targetCenter - (sourceCenter - item.position) * factor);
            scale = item.localScale * factor;
            return true;
        }

        private static bool TryFitShell(Sample[] source, Sample[] target, Sample[] existing,
            Sphere shell, Sphere baseline, out Vector3 center, out float factor)
        {
            center = baseline.Center;
            factor = 1f;
            target = target.Where(sample => {
                var delta = sample.Point - baseline.Center;
                return delta.y >= -baseline.Radius * 0.35f && delta.sqrMagnitude < baseline.Radius * baseline.Radius * 4f;
            }).ToArray();
            if (target.Length < 100) return false;
            var outside = new List<Vector3>();
            var inside = new List<Vector3>();
            foreach (var sample in source)
            {
                var delta = sample.Point - shell.Center;
                var distance = delta.magnitude;
                if (distance < shell.Radius * 0.65f || delta.y < -shell.Radius * 0.35f) continue;
                var alignment = Vector3.Dot(delta / distance, sample.Normal);
                if (Mathf.Abs(distance - shell.Radius) < shell.Radius * 0.15f && alignment > 0.7f) outside.Add(delta);
                if (distance < shell.Radius * 1.05f && alignment < -0.7f) inside.Add(delta);
            }
            if (outside.Count < source.Length * 0.15f) return false;
            var constraints = new List<Constraint>();
            for (var elevation = 0; elevation < 4; elevation++)
            {
                var height = Mathf.Min(1f, elevation * 0.4f);
                var horizontal = Mathf.Sqrt(1f - height * height);
                for (var azimuth = 0; azimuth < (elevation == 3 ? 1 : 12); azimuth++)
                {
                    var angle = azimuth * Mathf.PI / 6f;
                    var direction = new Vector3(Mathf.Sin(angle) * horizontal, height, Mathf.Cos(angle) * horizontal);
                    if (outside.Count(point => Vector3.Dot(point.normalized, direction) > 0.85f) < 8) continue;
                    var outerSupport = Quantile(outside.Select(point => Vector3.Dot(point, direction)), 0.98f);
                    var interior = inside.Where(point => Vector3.Dot(point.normalized, direction) > 0.85f).ToArray();
                    var sourceSupport = interior.Length >= 8
                        ? Mathf.Clamp(Quantile(interior.Select(point => Vector3.Dot(point, direction)), 0.98f), outerSupport * 0.8f, outerSupport)
                        : outerSupport;
                    if (!(sourceSupport > shell.Radius * 0.2f)) continue;
                    var targetSupport = Quantile(target.Select(sample => Vector3.Dot(sample.Point - baseline.Center, direction)), 0.98f);
                    if (existing.Length > 0)
                        targetSupport = Mathf.Max(targetSupport, Quantile(existing.Select(sample => Vector3.Dot(sample.Point - baseline.Center, direction)), 0.98f));
                    constraints.Add(new Constraint {
                        Weights = new Vector4(direction.x, direction.y, direction.z, sourceSupport / shell.Radius),
                        Limit = targetSupport / baseline.Radius + 0.02f
                    });
                }
            }
            if (constraints.Count < 24) return false;
            var solution = new Vector4(0f, 0f, 0f, 1f);
            for (var iteration = 0; iteration < 32; iteration++)
            {
                var system = new double[4, 5];
                for (var axis = 0; axis < 3; axis++) system[axis, axis] = 2;
                foreach (var constraint in constraints)
                {
                    var weight = Vector4.Dot(constraint.Weights, solution) < constraint.Limit ? 200d : 1d;
                    for (var row = 0; row < 4; row++)
                    {
                        for (var column = 0; column < 4; column++)
                            system[row, column] += weight * constraint.Weights[row] * constraint.Weights[column];
                        system[row, 4] += weight * constraint.Weights[row] * constraint.Limit;
                    }
                }
                if (!TrySolve(system, out var next)) return false;
                var cost = ShellCost(constraints, solution);
                var step = 1f;
                while (step > 0.001f && ShellCost(constraints, Vector4.LerpUnclamped(solution, next, step)) > cost) step *= 0.5f;
                var change = (next - solution) * step;
                solution += change;
                if (change.sqrMagnitude < 0.00000001f) break;
            }
            center += new Vector3(solution.x, solution.y, solution.z) * baseline.Radius;
            factor = solution.w * baseline.Radius / shell.Radius;
            return factor > 0f && !float.IsNaN(factor) && !float.IsInfinity(factor);
        }

        private static float Quantile(IEnumerable<float> values, float quantile)
        {
            var ordered = values.OrderBy(value => value).ToArray();
            var index = (ordered.Length - 1) * quantile;
            var lower = Mathf.FloorToInt(index);
            return Mathf.Lerp(ordered[lower], ordered[Mathf.Min(lower + 1, ordered.Length - 1)], index - lower);
        }

        private static double ShellCost(List<Constraint> constraints, Vector4 solution)
        {
            var cost = 2d * (solution.x * solution.x + solution.y * solution.y + solution.z * solution.z);
            foreach (var constraint in constraints)
            {
                var residual = Vector4.Dot(constraint.Weights, solution) - constraint.Limit;
                cost += residual * residual * (residual < 0f ? 200d : 1d);
            }
            return cost;
        }

        private static bool TrySolve(double[,] system, out Vector4 solution)
        {
            solution = Vector4.zero;
            for (var column = 0; column < 4; column++)
            {
                var pivot = column;
                for (var row = column + 1; row < 4; row++)
                    if (Math.Abs(system[row, column]) > Math.Abs(system[pivot, column])) pivot = row;
                if (Math.Abs(system[pivot, column]) < 0.000000001d) return false;
                for (var entry = column; entry < 5; entry++)
                {
                    var saved = system[column, entry];
                    system[column, entry] = system[pivot, entry];
                    system[pivot, entry] = saved;
                }
                var divisor = system[column, column];
                for (var entry = column; entry < 5; entry++) system[column, entry] /= divisor;
                for (var row = 0; row < 4; row++)
                {
                    if (row == column) continue;
                    var multiplier = system[row, column];
                    for (var entry = column; entry < 5; entry++) system[row, entry] -= multiplier * system[column, entry];
                }
            }
            for (var axis = 0; axis < 4; axis++) solution[axis] = (float)system[axis, 4];
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
