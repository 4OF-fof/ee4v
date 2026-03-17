using System;

namespace Ee4v.Core.Testing
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class FeatureTestCaseAttribute : Attribute
    {
        public FeatureTestCaseAttribute(
            string title,
            string description = "",
            int order = 0,
            FeatureTestCategory category = FeatureTestCategory.Standard)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Order = order;
            Category = category;
        }

        public string Title { get; }

        public string Description { get; }

        public int Order { get; }

        public FeatureTestCategory Category { get; }
    }
}
