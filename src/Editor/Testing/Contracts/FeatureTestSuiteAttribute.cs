using System;

namespace Ee4v.Testing.Contracts
{
    [AttributeUsage(
        AttributeTargets.Assembly,
        AllowMultiple = false)]
    public sealed class FeatureTestSuiteAttribute : Attribute
    {
        public FeatureTestSuiteAttribute(
            string featureScope,
            string displayName,
            string assemblyName,
            string description = "",
            int order = 0,
            FeatureTestCategory category =
                FeatureTestCategory.Standard)
        {
            FeatureScope = featureScope ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            AssemblyName = assemblyName ?? string.Empty;
            Description = description ?? string.Empty;
            Order = order;
            Category = category;
        }

        public string FeatureScope { get; }
        public string DisplayName { get; }
        public string AssemblyName { get; }
        public string Description { get; }
        public int Order { get; }
        public FeatureTestCategory Category { get; }
    }
}
