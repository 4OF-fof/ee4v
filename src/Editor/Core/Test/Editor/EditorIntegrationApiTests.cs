using System;
using System.Linq;
using System.Reflection;
using Ee4v.Core.EditorIntegration;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class EditorIntegrationApiTests
    {
        private static readonly Type[] PublicApis =
        {
            typeof(ProjectBrowserApi),
            typeof(ProjectFavoritesApi),
            typeof(EditorPopupApi),
            typeof(InspectorApi),
            typeof(AssetImportApi),
            typeof(HierarchyItemApi),
            typeof(EditorTextFieldApi),
            typeof(PackageAssetApi)
        };

        [Test]
        public void PublicApis_DoNotExposeInternalBackendTypes()
        {
            var leakedTypes = PublicApis
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Static))
                .SelectMany(GetContractTypes)
                .Where(type =>
                    type.FullName != null &&
                    type.FullName.Contains(".Internal."))
                .Select(type => type.FullName)
                .Distinct()
                .ToArray();

            Assert.That(leakedTypes, Is.Empty);
        }

        [Test]
        public void PackageAssetApi_ResolvesInstalledPackage()
        {
            var packagePath = PackageAssetApi
                .GetPackageRootAssetPath()
                .Replace('\\', '/');
            Assert.That(
                packagePath.EndsWith("/src") ||
                packagePath.EndsWith("/dev.4of.ee4v"),
                Is.True,
                packagePath);
        }

        private static Type[] GetContractTypes(MethodInfo method)
        {
            return new[] { method.ReturnType }
                .Concat(
                    method.GetParameters()
                        .Select(parameter =>
                            parameter.ParameterType.IsByRef
                                ? parameter.ParameterType.GetElementType()
                                : parameter.ParameterType))
                .Where(type => type != null)
                .ToArray();
        }
    }
}
