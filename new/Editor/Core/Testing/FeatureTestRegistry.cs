using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Ee4v.Core.Testing
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
            _cachedDescriptors = BuildDescriptors(DiscoverRegistrarTypes());
            return _cachedDescriptors;
        }

        private static IEnumerable<Type> DiscoverRegistrarTypes()
        {
            return TypeCache.GetTypesDerivedFrom<IFeatureTestRegistrar>()
                .Where(type => type != null &&
                    !type.IsAbstract &&
                    type.Name.EndsWith("TestRegistrar", StringComparison.Ordinal));
        }

        internal static List<FeatureTestDescriptor> BuildDescriptors(IEnumerable<Type> registrarTypes)
        {
            if (registrarTypes == null)
            {
                throw new ArgumentNullException(nameof(registrarTypes));
            }

            var descriptors = new List<FeatureTestDescriptor>();

            foreach (var registrarType in registrarTypes
                .Where(type => type != null && !type.IsAbstract && typeof(IFeatureTestRegistrar).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                IFeatureTestRegistrar registrar;
                try
                {
                    registrar = (IFeatureTestRegistrar)Activator.CreateInstance(registrarType);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        "Failed to instantiate feature test registrar '" + registrarType.FullName + "'.",
                        exception);
                }

                var descriptor = registrar.CreateDescriptor();
                if (descriptor == null)
                {
                    throw new InvalidOperationException(
                        "Feature test registrar '" + registrarType.FullName + "' returned null descriptor.");
                }

                var discoveredCases = FeatureTestCaseDiscovery.Discover(descriptor.AssemblyName);
                descriptors.Add(new FeatureTestDescriptor(
                    descriptor.FeatureScope,
                    descriptor.DisplayName,
                    descriptor.AssemblyName,
                    descriptor.Description,
                    descriptor.Order,
                    discoveredCases.Count > 0 ? discoveredCases : descriptor.TestCases));
            }

            ValidateNoDuplicates(
                descriptors,
                descriptor => descriptor.FeatureScope,
                "feature scope");

            ValidateNoDuplicates(
                descriptors,
                descriptor => descriptor.AssemblyName,
                "assembly name");

            descriptors.Sort(CompareDescriptors);
            return descriptors;
        }

        private static void ValidateNoDuplicates(
            IEnumerable<FeatureTestDescriptor> descriptors,
            Func<FeatureTestDescriptor, string> selector,
            string fieldName)
        {
            var duplicate = descriptors
                .GroupBy(selector, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicate == null)
            {
                return;
            }

            var scopes = string.Join(", ", duplicate.Select(item => item.FeatureScope).OrderBy(scope => scope, StringComparer.Ordinal));
            throw new InvalidOperationException(
                "Duplicate feature test " + fieldName + " '" + duplicate.Key + "' detected in: " + scopes + ".");
        }

        private static int CompareDescriptors(FeatureTestDescriptor left, FeatureTestDescriptor right)
        {
            var orderCompare = left.Order.CompareTo(right.Order);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            var displayCompare = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (displayCompare != 0)
            {
                return displayCompare;
            }

            var scopeCompare = string.Compare(left.FeatureScope, right.FeatureScope, StringComparison.OrdinalIgnoreCase);
            if (scopeCompare != 0)
            {
                return scopeCompare;
            }

            return string.Compare(left.AssemblyName, right.AssemblyName, StringComparison.Ordinal);
        }
    }
}
