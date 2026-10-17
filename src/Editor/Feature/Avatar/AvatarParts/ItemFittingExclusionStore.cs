using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AvatarParts
{
    [FilePath("UserSettings/ee4v.item-fitting.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ItemFittingExclusionStore : ScriptableSingleton<ItemFittingExclusionStore>
    {
        [Serializable]
        private sealed class ObjectReference
        {
            public string Id;
            public string Path;
        }

        [Serializable]
        private sealed class Entry
        {
            public string Scope;
            public string RootId;
            public ObjectReference Item;
            public List<ObjectReference> Excluded = new List<ObjectReference>();
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        internal HashSet<Transform> Read(GameObject prefab, Transform root, Transform item)
        {
            var scope = Scope(prefab, root);
            var rootId = ObjectId(root);
            var entry = Find(scope, rootId, root, item);
            if (entry == null) return new HashSet<Transform>();
            return new HashSet<Transform>(entry.Excluded
                .Select(reference => Resolve(reference, entry.RootId != rootId, root))
                .Where(target => target != null && target != root && !target.IsChildOf(item)));
        }

        internal void Write(GameObject prefab, Transform root, Transform item, IEnumerable<Transform> excluded)
        {
            var scope = Scope(prefab, root);
            if (scope == null || item == null || item == root || !item.IsChildOf(root)) return;
            var rootId = ObjectId(root);
            var entry = Find(scope, rootId, root, item);
            var references = excluded.Where(target => target != null && target != root &&
                    target.IsChildOf(root) && !target.IsChildOf(item)).Distinct()
                .Select(target => Capture(target, root)).OrderBy(reference => reference.Path, StringComparer.Ordinal).ToList();
            if (references.Count == 0)
            {
                if (entry == null) return;
                _entries.Remove(entry);
            }
            else
            {
                if (entry == null)
                {
                    entry = new Entry { Scope = scope };
                    _entries.Add(entry);
                }
                entry.RootId = rootId;
                entry.Item = Capture(item, root);
                entry.Excluded = references;
            }
            Save(true);
        }

        private Entry Find(string scope, string rootId, Transform root, Transform item) =>
            scope == null ? null : _entries.FirstOrDefault(entry => entry.Scope == scope &&
                Resolve(entry.Item, entry.RootId != rootId, root) == item);

        private static string Scope(GameObject prefab, Transform root)
        {
            if (prefab != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out string guid, out long _))
                return "prefab:" + guid;
            var id = ObjectId(root);
            return id == null ? null : "scene:" + id;
        }

        private static string ObjectId(Transform target)
        {
            if (target == null) return null;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(target);
            return id.identifierType != 0 && id.targetObjectId != 0 &&
                id.assetGUID.ToString() != "00000000000000000000000000000000" ? id.ToString() : null;
        }

        private static ObjectReference Capture(Transform target, Transform root)
        {
            var segments = new List<string>();
            for (var current = target; current != root; current = current.parent)
            {
                var occurrence = 0;
                for (var index = 0; index < current.GetSiblingIndex(); index++)
                    if (current.parent.GetChild(index).name == current.name) occurrence++;
                segments.Add(Uri.EscapeDataString(current.name) + "#" + occurrence);
            }
            segments.Reverse();
            return new ObjectReference { Id = ObjectId(target), Path = string.Join("/", segments) };
        }

        private static Transform Resolve(ObjectReference reference, bool allowPath, Transform root)
        {
            if (reference == null || root == null) return null;
            if (!string.IsNullOrEmpty(reference.Id) && GlobalObjectId.TryParse(reference.Id, out var id))
            {
                var target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as Transform;
                if (target != null && target != root && target.IsChildOf(root)) return target;
                if (!allowPath) return null;
            }
            if (string.IsNullOrEmpty(reference.Path)) return null;
            var current = root;
            foreach (var segment in reference.Path.Split('/'))
            {
                var separator = segment.LastIndexOf('#');
                if (separator < 0 || !int.TryParse(segment.Substring(separator + 1), out var occurrence) || occurrence < 0)
                    return null;
                string name;
                try { name = Uri.UnescapeDataString(segment.Substring(0, separator)); }
                catch (UriFormatException) { return null; }
                Transform match = null;
                foreach (Transform child in current)
                {
                    if (child.name != name) continue;
                    if (occurrence-- == 0) { match = child; break; }
                }
                if (match == null) return null;
                current = match;
            }
            return current;
        }
    }
}
