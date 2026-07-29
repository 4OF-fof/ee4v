using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Testing.Application;
using Ee4v.Testing.Contracts;

namespace Ee4v.Testing.Infrastructure.Unity
{
    internal static class FeatureTestRegistry
    {
        private static List<FeatureTestDescriptor> _cachedDescriptors;

        public static IReadOnlyList<FeatureTestDescriptor> GetDescriptors()
        {
            if (_cachedDescriptors == null)
            {
                Refresh();
            }

            return _cachedDescriptors;
        }

        public static IReadOnlyList<FeatureTestDescriptor> Refresh()
        {
            _cachedDescriptors = new FeatureTestDescriptorBuilder()
                .Build(DiscoverSuites())
                .ToList();
            return _cachedDescriptors;
        }

        private static IEnumerable<FeatureTestSuiteAttribute>
            DiscoverSuites()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                    assembly.GetCustomAttributes(
                            typeof(FeatureTestSuiteAttribute),
                            false)
                        .Cast<FeatureTestSuiteAttribute>());
        }
    }
}
