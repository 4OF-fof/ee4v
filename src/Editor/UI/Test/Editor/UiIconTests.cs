using System;
using System.IO;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using NUnit.Framework;
using UnityEditor;

namespace Ee4v.UI.Tests
{
    public sealed class UiIconTests
    {
        [Test]
        public void UiBuiltinIconResolver_TryResolve_AllRegisteredIcons()
        {
            foreach (UiBuiltinIcon builtinIcon in Enum.GetValues(typeof(UiBuiltinIcon)))
            {
                var resolved = UiBuiltinIconResolver.TryResolve(builtinIcon, out var texture);

                Assert.That(resolved, Is.True, builtinIcon.ToString());
                Assert.That(texture, Is.Not.Null, builtinIcon.ToString());
            }
        }

        [Test]
        public void FluentUiIcons_LoadsBundledRuntimeIcons()
        {
            var directory = PackageAssetApi.GetPackageRootAssetPath() +
                            "/Editor/ThirdParty/" +
                            "FluentUiSystemIcons/Png512";
            var iconFileNames = AssetDatabase.FindAssets(
                    "t:Texture2D",
                    new[] { directory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(Path.GetFileName)
                .ToArray();

            Assert.That(iconFileNames, Is.Not.Empty);

            foreach (var iconFileName in iconFileNames)
            {
                Assert.That(
                    FluentUiIcons.LoadTexture(iconFileName),
                    Is.Not.Null,
                    iconFileName);
            }
        }

    }
}
