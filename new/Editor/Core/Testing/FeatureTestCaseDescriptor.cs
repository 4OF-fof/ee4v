using System;

namespace Ee4v.Core.Testing
{
    public sealed class FeatureTestCaseDescriptor
    {
        public FeatureTestCaseDescriptor(
            string title,
            string description = "",
            int order = 0,
            string resultKey = "",
            FeatureTestCategory category = FeatureTestCategory.Standard)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Test title is required.", nameof(title));
            }

            Title = title;
            Description = description ?? string.Empty;
            Order = order;
            ResultKey = resultKey ?? string.Empty;
            Category = category;
        }

        public string Title { get; }

        public string Description { get; }

        public int Order { get; }

        public string ResultKey { get; }

        public FeatureTestCategory Category { get; }
    }
}
