using System;

namespace Ee4v.Core.Testing
{
    public sealed class FeatureTestCaseDescriptor
    {
        public FeatureTestCaseDescriptor(string title, string description = "", int order = 0, string resultKey = "")
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Test title is required.", nameof(title));
            }

            Title = title;
            Description = description ?? string.Empty;
            Order = order;
            ResultKey = resultKey ?? string.Empty;
        }

        public string Title { get; }

        public string Description { get; }

        public int Order { get; }

        public string ResultKey { get; }
    }
}
