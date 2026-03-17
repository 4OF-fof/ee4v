using System;

namespace Ee4v.Core.Internal
{
    internal static class FeatureBootstrapContract
    {
        public static void Initialize(
            string featureScope,
            Type definitionsType,
            Action registerDefinitions,
            Action registerFeature)
        {
            if (string.IsNullOrWhiteSpace(featureScope))
            {
                throw new ArgumentException("Feature scope is required.", nameof(featureScope));
            }

            if (definitionsType == null)
            {
                throw new ArgumentNullException(nameof(definitionsType));
            }

            var expectedTypeName = featureScope + "Definitions";
            if (!string.Equals(definitionsType.Name, expectedTypeName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Definitions type must be named '" + expectedTypeName + "' but was '" + definitionsType.Name + "'.");
            }

            var resolvedScope = PackagePathUtility.GetScopeNameForNamespace(definitionsType.Namespace);
            if (!string.Equals(resolvedScope, featureScope, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Definitions namespace must resolve to scope '" + featureScope + "' but was '" +
                    (resolvedScope ?? "(null)") + "'.");
            }

            if (registerDefinitions == null)
            {
                throw new ArgumentNullException(nameof(registerDefinitions));
            }

            registerDefinitions();
            registerFeature?.Invoke();
        }
    }
}
