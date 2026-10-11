using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.EditorIntegration
{
    /// <summary>Evidence of material use on triangle surfaces with identical UV0 layouts.</summary>
    public sealed class MaterialUvUsageIndex
    {
        private static readonly HashSet<string> EmptyLayouts = new HashSet<string>(StringComparer.Ordinal);
        private readonly MaterialUvUsageIndex _projectUsage;
        private readonly Dictionary<string, HashSet<string>> _layouts =
            new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<Mesh, Dictionary<int, string>> _meshLayouts =
            new Dictionary<Mesh, Dictionary<int, string>>();
        private readonly Dictionary<string, HashSet<string>> _usageNames =
            new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, HashSet<string>> _effectiveLayouts =
            new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<Mesh, ulong[]> _meshPoints = new Dictionary<Mesh, ulong[]>();
        private readonly int _cacheVersion = AssetPickerCache.Version;
        private int _revision;
        private int _projectRevision;

        [Serializable]
        internal sealed class UsageData
        {
            public string MaterialKey;
            public string[] Layouts;
            public string[] Names;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float Value;
            [FieldOffset(0)] public uint Bits;
        }

        private struct Triangle : IComparable<Triangle>
        {
            internal ulong A, B, C;

            public int CompareTo(Triangle other)
            {
                var comparison = A.CompareTo(other.A);
                if (comparison == 0) comparison = B.CompareTo(other.B);
                return comparison == 0 ? C.CompareTo(other.C) : comparison;
            }
        }

        public MaterialUvUsageIndex() { }

        private MaterialUvUsageIndex(MaterialUvUsageIndex projectUsage) => _projectUsage = projectUsage;

        public MaterialUvUsageIndex CreateContextIndex() => new MaterialUvUsageIndex(this);

        public IEnumerable<bool> ScanContext(GameObject context) => ScanRoot(context);

        public string DescribeUsage(Material material)
            => DescribeUsage(AssetPickerCache.GetAssetKey(material));

        public string DescribeUsage(string materialKey)
        {
            if (string.IsNullOrEmpty(materialKey)) return string.Empty;
            var names = new HashSet<string>(StringComparer.Ordinal);
            CollectNames(materialKey, names);
            return string.Join(", ", names.OrderBy(name => name, StringComparer.Ordinal));
        }

        private void CollectNames(string material, HashSet<string> result)
        {
            _projectUsage?.CollectNames(material, result);
            if (_usageNames.TryGetValue(material, out var names)) result.UnionWith(names);
        }

        private HashSet<string> Layouts(string material)
        {
            if (_projectUsage != null && _projectRevision != _projectUsage._revision)
            {
                _effectiveLayouts.Clear();
                _projectRevision = _projectUsage._revision;
            }
            if (_effectiveLayouts.TryGetValue(material, out var cached)) return cached;
            _layouts.TryGetValue(material, out var layouts);
            var projectLayouts = _projectUsage?.Layouts(material);
            if ((layouts == null || layouts.Count == 0) && (projectLayouts == null || projectLayouts.Count == 0)) return EmptyLayouts;
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (layouts != null) result.UnionWith(layouts);
            if (projectLayouts != null) result.UnionWith(projectLayouts);
            _effectiveLayouts.Add(material, result);
            return result;
        }

        public bool HasSameUsage(Material reference, Material candidate)
            => HasSameUsage(AssetPickerCache.GetAssetKey(reference), AssetPickerCache.GetAssetKey(candidate));

        public bool HasSameUsage(string reference, string candidate)
        {
            return !string.IsNullOrEmpty(reference) && !string.IsNullOrEmpty(candidate) &&
                Layouts(reference).Overlaps(Layouts(candidate));
        }

        public void Add(GameObject root)
        {
            foreach (var _ in ScanRoot(root)) { }
        }

        private IEnumerable<bool> ScanRoot(GameObject root)
        {
            if (root == null) yield break;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = GetMesh(renderer);
                if (mesh == null) continue;
                var materials = renderer.sharedMaterials;
                for (var slot = 0; slot < Math.Min(materials.Length, mesh.subMeshCount); slot++)
                {
                    var material = materials[slot];
                    if (material == null) continue;
                    AddUsage(material, mesh, slot);
                }
                yield return true;
            }

            // Material swaps in clips are evidence even when the default prefab uses another colour.
            foreach (var clip in AnimationUtility.GetAnimationClips(root))
            {
                if (clip == null) continue;
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    const string prefix = "m_Materials.Array.data[";
                    if (!typeof(Renderer).IsAssignableFrom(binding.type) ||
                        !binding.propertyName.StartsWith(prefix, StringComparison.Ordinal) ||
                        !binding.propertyName.EndsWith("]", StringComparison.Ordinal)) continue;
                    var slotText = binding.propertyName.Substring(prefix.Length, binding.propertyName.Length - prefix.Length - 1);
                    if (!int.TryParse(slotText, out var slot)) continue;
                    var target = string.IsNullOrEmpty(binding.path) ? root.transform : root.transform.Find(binding.path);
                    var renderer = target == null ? null : target.GetComponent(binding.type) as Renderer;
                    var mesh = GetMesh(renderer);
                    if (mesh == null || slot < 0 || slot >= mesh.subMeshCount) continue;
                    foreach (var frame in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                        if (frame.value is Material material && material != null) AddUsage(material, mesh, slot);
                }
                yield return true;
            }
        }

        private static Mesh GetMesh(Renderer renderer)
        {
            if (renderer == null) return null;
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            var filter = renderer.GetComponent<MeshFilter>();
            return filter == null ? null : filter.sharedMesh;
        }

        private void AddUsage(Material material, Mesh mesh, int slot)
        {
            var key = AssetPickerCache.GetAssetKey(material);
            if (!_usageNames.TryGetValue(key, out var names))
                _usageNames.Add(key, names = new HashSet<string>(StringComparer.Ordinal));
            names.Add(mesh.name + " [" + slot + "]");
            var signature = GetLayout(mesh, slot);
            if (signature == null) return;
            if (!_layouts.TryGetValue(key, out var layouts))
                _layouts.Add(key, layouts = new HashSet<string>(StringComparer.Ordinal));
            if (layouts.Add(signature))
            {
                _effectiveLayouts.Remove(key);
                _revision++;
            }
        }

        public IEnumerable<bool> ScanProject(GameObject context = null)
        {
            foreach (var step in ScanRoot(context)) yield return step;
            // FindAssets combines type filters with OR; includes embedded model materials.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab t:Model"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetPickerCache.TryGetUsageFile(guid, path, out var cached)) Merge(cached);
                else
                {
                    var rootUsage = new MaterialUvUsageIndex(this);
                    foreach (var step in rootUsage.ScanRoot(AssetDatabase.LoadAssetAtPath<GameObject>(path))) yield return step;
                    var entries = rootUsage.Export();
                    Merge(entries);
                    AssetPickerCache.StoreUsageFile(guid, path, entries, _cacheVersion);
                }
                yield return true;
            }
        }

        private UsageData[] Export()
        {
            return _usageNames.Where(pair => !pair.Key.StartsWith("instance:", StringComparison.Ordinal)).Select(pair => new UsageData
            {
                MaterialKey = pair.Key, Names = pair.Value.ToArray(),
                Layouts = _layouts.TryGetValue(pair.Key, out var layouts) ? layouts.ToArray() : Array.Empty<string>()
            }).ToArray();
        }

        private void Merge(IEnumerable<UsageData> entries)
        {
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.MaterialKey)) continue;
                if (!_layouts.TryGetValue(entry.MaterialKey, out var layouts))
                    _layouts[entry.MaterialKey] = layouts = new HashSet<string>(StringComparer.Ordinal);
                layouts.UnionWith(entry.Layouts ?? Array.Empty<string>());
                if (!_usageNames.TryGetValue(entry.MaterialKey, out var names))
                    _usageNames[entry.MaterialKey] = names = new HashSet<string>(StringComparer.Ordinal);
                names.UnionWith(entry.Names ?? Array.Empty<string>());
                _effectiveLayouts.Remove(entry.MaterialKey);
                _revision++;
            }
        }

        private string GetLayout(Mesh mesh, int slot)
        {
            if (_projectUsage != null && _projectUsage._meshLayouts.TryGetValue(mesh, out var projectSlots) &&
                projectSlots.TryGetValue(slot, out var projectLayout)) return projectLayout;
            if (!_meshLayouts.TryGetValue(mesh, out var slots))
                _meshLayouts.Add(mesh, slots = new Dictionary<int, string>());
            if (slots.TryGetValue(slot, out var cached)) return cached;
            if (AssetPickerCache.TryGetMeshLayout(mesh, slot, out cached))
            {
                slots.Add(slot, cached);
                return cached;
            }
            string signature = null;
            // Editor access outside the game/render loop does not require Read/Write Enabled.
            if ((!EditorApplication.isPlaying || mesh.isReadable) && mesh.GetTopology(slot) == MeshTopology.Triangles)
            {
                if (!_meshPoints.TryGetValue(mesh, out var uv))
                    _meshPoints[mesh] = uv = mesh.uv.Select(Point).ToArray();
                var indices = mesh.GetIndices(slot);
                if (uv.Length == mesh.vertexCount && indices.Length > 0)
                {
                    var triangles = new Triangle[indices.Length / 3];
                    for (var i = 0; i + 2 < indices.Length; i += 3)
                    {
                        var a = uv[indices[i]];
                        var b = uv[indices[i + 1]];
                        var c = uv[indices[i + 2]];
                        if (a > b) Swap(ref a, ref b);
                        if (b > c) Swap(ref b, ref c);
                        if (a > b) Swap(ref a, ref b);
                        triangles[i / 3] = new Triangle { A = a, B = b, C = c };
                    }
                    Array.Sort(triangles);
                    using (var stream = new MemoryStream(triangles.Length * 24))
                    using (var writer = new BinaryWriter(stream))
                    using (var hash = SHA256.Create())
                    {
                        foreach (var triangle in triangles)
                        {
                            writer.Write(triangle.A);
                            writer.Write(triangle.B);
                            writer.Write(triangle.C);
                        }
                        writer.Flush();
                        stream.Position = 0;
                        signature = Convert.ToBase64String(hash.ComputeHash(stream));
                    }
                }
            }
            slots.Add(slot, signature);
            AssetPickerCache.StoreMeshLayout(mesh, slot, signature, _cacheVersion);
            return signature;
        }

        private static void Swap(ref ulong a, ref ulong b)
        {
            var temporary = a;
            a = b;
            b = temporary;
        }

        private static ulong Point(Vector2 value)
        {
            var x = new FloatBits { Value = value.x == 0f ? 0f : value.x };
            var y = new FloatBits { Value = value.y == 0f ? 0f : value.y };
            return ((ulong)x.Bits << 32) | y.Bits;
        }
    }
}
