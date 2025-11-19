using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using _4OF.ee4v.Core.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace _4OF.ee4v.AssetManager.Data {
    public class AllowedTypesBinder : SerializationBinder, ISerializationBinder {
        private readonly HashSet<Type> _allowedBaseTypes = new();

        public AllowedTypesBinder(IEnumerable<Type> allowedBaseTypes = null) {
            _allowedBaseTypes.Add(typeof(LibraryMetadata));
            _allowedBaseTypes.Add(typeof(BaseFolder));
            _allowedBaseTypes.Add(typeof(Folder));
            _allowedBaseTypes.Add(typeof(BoothItemFolder));
            _allowedBaseTypes.Add(typeof(AssetMetadata));
            _allowedBaseTypes.Add(typeof(BoothMetadata));
            _allowedBaseTypes.Add(typeof(Ulid));

            if (allowedBaseTypes == null) return;
            foreach (var t in allowedBaseTypes) _allowedBaseTypes.Add(t);
        }

        public override Type BindToType(string assemblyName, string typeName) {
            Type resolved = null;
            if (!string.IsNullOrEmpty(assemblyName))
                try {
                    resolved = Type.GetType($"{typeName}, {assemblyName}");
                }
                catch { /* ignore */ }

            if (resolved == null)
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    try {
                        var t = asm.GetType(typeName, false, false);
                        if (t == null) continue;
                        resolved = t;
                        break;
                    }
                    catch { /* ignore */ }

            if (resolved == null)
                throw new JsonSerializationException($"Type not found: {typeName} (assembly: {assemblyName})");

            return !IsAllowed(resolved) ? throw new JsonSerializationException($"Deserializing type {resolved.FullName} is not allowed.") : resolved;
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName) {
            assemblyName = serializedType.Assembly.FullName;
            typeName = serializedType.FullName;
        }

        private bool IsAllowed(Type t) {
            if (t == null) return false;
            return _allowedBaseTypes.Contains(t) || _allowedBaseTypes.Any(baseType => baseType.IsAssignableFrom(t));
        }
    }
}