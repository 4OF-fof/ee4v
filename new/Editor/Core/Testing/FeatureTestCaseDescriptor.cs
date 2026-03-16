using System;

namespace Ee4v.Core.Testing
{
    public sealed class FeatureTestCaseDescriptor
    {
        public FeatureTestCaseDescriptor(string title, string description = "", int order = 0)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Test title is required.", nameof(title));
            }

            Title = title;
            Description = description ?? string.Empty;
            Order = order;
        }

        public string Title { get; }

        public string Description { get; }

        public int Order { get; }
    }
}
