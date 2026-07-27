using System;
using Ee4v.Core.I18n;
using Ee4v.Testing.Application;

namespace Ee4v.Testing.UI
{
    internal static class TestingUiDependencies
    {
        private static IFeatureTestCatalog _catalog;
        private static IFeatureTestRunner _runner;

        internal static IFeatureTestCatalog Catalog =>
            _catalog ?? throw new InvalidOperationException(
                I18N.Get("testing.window.dependenciesNotConfigured"));

        internal static IFeatureTestRunner Runner =>
            _runner ?? throw new InvalidOperationException(
                I18N.Get("testing.window.dependenciesNotConfigured"));

        internal static void Configure(
            IFeatureTestCatalog catalog,
            IFeatureTestRunner runner)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }
    }
}
