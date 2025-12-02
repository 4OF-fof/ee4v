using System;

namespace _4OF.ee4v.Core.Attributes {
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class ExportsHierarchyExtensionComponent : Attribute {
        public ExportsHierarchyExtensionComponent(params Type[] types) {
            Types = types;
        }

        public Type[] Types { get; }
    }
}