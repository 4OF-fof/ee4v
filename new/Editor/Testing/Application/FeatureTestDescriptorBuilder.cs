using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Testing.Contracts;

namespace Ee4v.Testing.Application
{
    public sealed class FeatureTestDescriptorBuilder
    {
        public IReadOnlyList<FeatureTestDescriptor> Build(
            IEnumerable<Type> registrarTypes)
        {
            if (registrarTypes == null)
            {
                throw new ArgumentNullException(nameof(registrarTypes));
            }

            var descriptors = new List<FeatureTestDescriptor>();
            foreach (var registrarType in registrarTypes
                .Where(type =>
                    type != null &&
                    !type.IsAbstract &&
                    typeof(IFeatureTestRegistrar).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                var registrar = CreateRegistrar(registrarType);
                var descriptor = registrar.CreateDescriptor();
                if (descriptor == null)
                {
                    throw new InvalidOperationException(
                        "Feature test registrar '" +
                        registrarType.FullName +
                        "' returned null descriptor.");
                }

                var discoveredCases = FeatureTestCaseDiscovery.Discover(
                    descriptor.AssemblyName);
                descriptors.Add(new FeatureTestDescriptor(
                    descriptor.FeatureScope,
                    descriptor.DisplayName,
                    descriptor.AssemblyName,
                    descriptor.Description,
                    descriptor.Order,
                    discoveredCases.Count > 0
                        ? discoveredCases
                        : descriptor.TestCases,
                    descriptor.Category));
            }

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

        private static IFeatureTestRegistrar CreateRegistrar(Type type)
        {
            try
            {
                return (IFeatureTestRegistrar)Activator.CreateInstance(type);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Failed to instantiate feature test registrar '" +
                    type.FullName +
                    "'.",
                    exception);
            }
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
