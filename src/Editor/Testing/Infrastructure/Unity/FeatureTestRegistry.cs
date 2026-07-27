using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Testing.Application;
using Ee4v.Testing.Contracts;
using UnityEditor;

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
                .Build(DiscoverRegistrarTypes())
                .ToList();
            return _cachedDescriptors;
        }

        private static IEnumerable<Type> DiscoverRegistrarTypes()
        {
            return TypeCache.GetTypesDerivedFrom<IFeatureTestRegistrar>()
                .Where(type => type != null &&
                    !type.IsAbstract &&
                    type.Name.EndsWith("TestRegistrar", StringComparison.Ordinal));
        }

        internal static List<FeatureTestDescriptor> BuildDescriptors(
            IEnumerable<Type> registrarTypes)
        {
            return new FeatureTestDescriptorBuilder()
                .Build(registrarTypes)
                .ToList();
        }
    }
}
