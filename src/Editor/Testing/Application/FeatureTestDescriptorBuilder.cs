using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Testing.Contracts;

namespace Ee4v.Testing.Application
{
    public sealed class FeatureTestDescriptorBuilder
    {
        public IReadOnlyList<FeatureTestDescriptor> Build(
            IEnumerable<FeatureTestSuiteAttribute> suites)
        {
            if (suites == null)
            {
                throw new ArgumentNullException(nameof(suites));
            }

            var descriptors = suites
                .Where(suite => suite != null)
                .Select(suite =>
                {
                    var discoveredCases =
                        FeatureTestCaseDiscovery.Discover(
                            suite.AssemblyName);
                    return new FeatureTestDescriptor(
                        suite.FeatureScope,
                        suite.DisplayName,
                        suite.AssemblyName,
                        suite.Description,
                        suite.Order,
                        discoveredCases,
                        suite.Category);
                })
                .ToArray();
            return OrderAndValidate(descriptors);
        }

        private static IReadOnlyList<FeatureTestDescriptor>
            OrderAndValidate(
                IReadOnlyList<FeatureTestDescriptor> descriptors)
        {
            ValidateNoDuplicates(
                descriptors,
                descriptor => descriptor.FeatureScope,
                "feature scope");
            ValidateNoDuplicates(
                descriptors,
                descriptor => descriptor.AssemblyName,
                "assembly name");

            return descriptors
                .OrderBy(descriptor => descriptor.Order)
                .ThenBy(
                    descriptor => descriptor.DisplayName,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    descriptor => descriptor.FeatureScope,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    descriptor => descriptor.AssemblyName,
                    StringComparer.Ordinal)
                .ToArray();
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

            var scopes = string.Join(
                ", ",
                duplicate
                    .Select(item => item.FeatureScope)
                    .OrderBy(scope => scope, StringComparer.Ordinal));
            throw new InvalidOperationException(
                "Duplicate feature test " +
                fieldName +
                " '" +
                duplicate.Key +
                "' detected in: " +
                scopes +
                ".");
        }
    }
}
