using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.Core.Testing
{
    public sealed class FeatureTestDescriptor
    {
        public FeatureTestDescriptor(
            string featureScope,
            string displayName,
            string assemblyName,
            string description = "",
            int order = 0,
            IReadOnlyList<FeatureTestCaseDescriptor> testCases = null)
        {
            if (string.IsNullOrWhiteSpace(featureScope))
            {
                throw new ArgumentException("Feature scope is required.", nameof(featureScope));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name is required.", nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                throw new ArgumentException("Assembly name is required.", nameof(assemblyName));
            }

            FeatureScope = featureScope;
            DisplayName = displayName;
            AssemblyName = assemblyName;
            Description = description ?? string.Empty;
            Order = order;
            TestCases = (testCases ?? Array.Empty<FeatureTestCaseDescriptor>())
                .OrderBy(testCase => testCase.Order)
                .ThenBy(testCase => testCase.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string FeatureScope { get; }

        public string DisplayName { get; }

        public string AssemblyName { get; }

        public string Description { get; }

        public int Order { get; }

        public IReadOnlyList<FeatureTestCaseDescriptor> TestCases { get; }
    }
}
