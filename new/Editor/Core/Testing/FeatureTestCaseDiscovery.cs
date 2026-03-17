using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ee4v.Core.Testing
{
    internal static class FeatureTestCaseDiscovery
    {
        private static readonly string[] NUnitTestAttributeNames =
        {
            "NUnit.Framework.TestAttribute",
            "NUnit.Framework.TestCaseAttribute",
            "NUnit.Framework.TestCaseSourceAttribute"
        };

        public static IReadOnlyList<FeatureTestCaseDescriptor> Discover(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return Array.Empty<FeatureTestCaseDescriptor>();
            }

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
            if (assembly == null)
            {
                return Array.Empty<FeatureTestCaseDescriptor>();
            }

            var results = new List<FeatureTestCaseDescriptor>();
            foreach (var type in assembly.GetTypes()
                .Where(candidate => candidate != null && candidate.IsClass && !candidate.IsAbstract)
                .OrderBy(candidate => candidate.FullName, StringComparer.Ordinal))
            {
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .OrderBy(candidate => candidate.MetadataToken))
                {
                    if (!HasNUnitTestAttribute(method))
                    {
                        continue;
                    }

                    var metadata = method.GetCustomAttribute<FeatureTestCaseAttribute>(inherit: false);
                    results.Add(new FeatureTestCaseDescriptor(
                        string.IsNullOrWhiteSpace(metadata?.Title) ? HumanizeMethodName(method.Name) : metadata.Title,
                        metadata?.Description ?? string.Empty,
                        metadata?.Order ?? results.Count,
                        BuildResultKey(method),
                        metadata?.Category ?? FeatureTestCategory.Standard));
                }
            }

            return results
                .OrderBy(testCase => testCase.Order)
                .ThenBy(testCase => testCase.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool HasNUnitTestAttribute(MethodInfo method)
        {
            return method.GetCustomAttributes(inherit: false)
                .Any(attribute => NUnitTestAttributeNames.Contains(attribute.GetType().FullName, StringComparer.Ordinal));
        }

        private static string HumanizeMethodName(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                return string.Empty;
            }

            return methodName.Replace("_", " ");
        }

        private static string BuildResultKey(MethodInfo method)
        {
            if (method == null)
            {
                return string.Empty;
            }

            var declaringType = method.DeclaringType != null
                ? method.DeclaringType.FullName
                : string.Empty;
            if (string.IsNullOrWhiteSpace(declaringType))
            {
                return method.Name ?? string.Empty;
            }

            return declaringType + "." + method.Name;
        }
    }
}
