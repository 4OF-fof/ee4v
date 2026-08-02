using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ee4v.Core.Internal;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class AssetManagerArchitectureTests
    {
        private static readonly string[] InnerLayerForbiddenTokens =
        {
            "UnityEngine",
            "UnityEditor",
            "SQLite",
            "System.IO",
            "System.Net",
            "CoreSettings",
            "ISettingsService",
            "SettingApi",
            "I18N"
        };

        [Test]
        [FeatureTestCase(
            "AssetManager の内側レイヤーは依存方向を守る",
            "Contracts、Domain、Application の asmdef と engine 非依存設定がクリーンアーキテクチャの境界を保つことを確認します。",
            order: 23,
            category: FeatureTestCategory.StaticAudit)]
        public void InnerLayerAsmdefs_HaveOnlyAllowedDependencies()
        {
            AssertAsmdef(
                "Contracts/Ee4v.AssetManager.Contracts.Editor.asmdef",
                Array.Empty<string>());
            AssertAsmdef(
                "Domain/Ee4v.AssetManager.Domain.Editor.asmdef",
                Array.Empty<string>());
            AssertAsmdef(
                "Application/Ee4v.AssetManager.Application.Editor.asmdef",
                new[]
                {
                    "Ee4v.AssetManager.Contracts.Editor",
                    "Ee4v.AssetManager.Domain.Editor"
                });
            AssertAsmdef(
                "UI/Ee4v.AssetManager.UI.Editor.asmdef",
                new[]
                {
                    "Ee4v.AssetManager.Contracts.Editor",
                    "Ee4v.AssetManager.Domain.Editor",
                    "Ee4v.Core.Contracts.Editor",
                    "Ee4v.Core.Editor",
                    "Ee4v.Core.Presentation.Editor",
                    "Ee4v.UI.Editor"
                },
                requireNoEngineReferences: false);
            AssertAsmdef(
                "Infrastructure/Ee4v.AssetManager.Infrastructure.Editor.asmdef",
                new[]
                {
                    "Ee4v.AssetManager.Application.Editor",
                    "Ee4v.AssetManager.Contracts.Editor",
                    "Ee4v.AssetManager.Domain.Editor",
                    "Ee4v.Core.Editor",
                    "Ee4v.SQLite.Editor"
                },
                requireNoEngineReferences: false);
            AssertAsmdef(
                "Composition/Ee4v.AssetManager.Composition.Editor.asmdef",
                new[]
                {
                    "Ee4v.AssetManager.Application.Editor",
                    "Ee4v.AssetManager.Contracts.Editor",
                    "Ee4v.AssetManager.Infrastructure.Editor",
                    "Ee4v.AssetManager.UI.Editor",
                    "Ee4v.Core.Contracts.Editor",
                    "Ee4v.Core.Editor",
                    "Ee4v.Core.Presentation.Editor",
                    "Ee4v.Core.Unity.Editor",
                    "Ee4v.UI.Editor"
                },
                requireNoEngineReferences: false);
        }

        [Test]
        [FeatureTestCase(
            "AssetManager の内側レイヤーは外部技術に依存しない",
            "Domain と Application に Unity、SQLite、filesystem、network、Setting、I18N 依存が入らないことを確認します。",
            order: 24,
            category: FeatureTestCategory.StaticAudit)]
        public void DomainAndApplicationSources_DoNotReferenceOuterLayerTechnologies()
        {
            var violations = new List<string>();
            foreach (var layer in new[] { "Domain", "Application" })
            {
                foreach (var filePath in GetAssetManagerSourceFiles(layer))
                {
                    var source = File.ReadAllText(filePath);
                    foreach (var token in InnerLayerForbiddenTokens.Where(source.Contains))
                    {
                        violations.Add(ToPackageRelativePath(filePath) + ": " + token);
                    }
                }
            }

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        [FeatureTestCase(
            "AssetManager の composition root は一箇所だけ",
            "static AssetManagerApi がなく、InitializeOnLoad が Composition の bootstrap 一箇所に限定されることを確認します。",
            order: 25,
            category: FeatureTestCategory.StaticAudit)]
        public void AssetManagerBootstrap_IsTheOnlyAutomaticEntryPoint()
        {
            var sourceFiles = Directory.GetFiles(GetAssetManagerRoot(), "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsTestSource(path))
                .ToArray();

            var staticApiReferences = sourceFiles
                .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\bAssetManagerApi\b"))
                .Select(ToPackageRelativePath)
                .ToArray();
            Assert.That(
                staticApiReferences,
                Is.Empty,
                "AssetManagerApi remains in:\n" + string.Join("\n", staticApiReferences));

            var initializeOnLoadSources = sourceFiles
                .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\[\s*InitializeOnLoad\s*\]"))
                .Select(ToPackageRelativePath)
                .ToArray();
            Assert.That(
                initializeOnLoadSources,
                Is.EqualTo(new[] { "Editor/AssetManager/Composition/AssetManagerBootstrap.cs" }));

            Assert.That(
                Directory.Exists(Path.Combine(GetAssetManagerRoot(), "api")),
                Is.False,
                "The legacy api directory must not remain.");
        }

        [Test]
        [FeatureTestCase(
            "AssetManager UI と Module 外部は実装 assembly に依存しない",
            "UI に storage/settings/datasource 依存がなく、package composition と開発用Catalog以外の外部asmdefがContracts以外を参照しないことを確認します。",
            order: 26,
            category: FeatureTestCategory.StaticAudit)]
        public void UiAndExternalAssemblies_ReferenceOnlyAllowedBoundaries()
        {
            var uiForbiddenTokens = new[]
            {
                "AssetManagerDatabase",
                "Ee4v.AssetManager.Infrastructure",
                "SQLite",
                "Datasources",
                "CoreSettings",
                "ISettingsService",
                "SettingApi",
                "Task.Run(",
                "EditorApplication.delayCall",
                "System.IO.Compression",
                "File.Open",
                "File.Exists(",
                "Directory.Exists(",
                "Directory.Enumerate",
                "new FileInfo(",
                "ZipArchive"
            };
            var uiViolations = GetAssetManagerSourceFiles("UI")
                .Where(path => !IsTestSource(path))
                .SelectMany(path =>
                {
                    var source = File.ReadAllText(path);
                    return uiForbiddenTokens
                        .Where(source.Contains)
                        .Select(token => ToPackageRelativePath(path) + ": " + token);
                })
                .ToArray();
            Assert.That(uiViolations, Is.Empty, string.Join("\n", uiViolations));

            var packageRoot = PackagePathUtility.GetPackageRootFullPath();
            var assetManagerRoot = GetAssetManagerRoot();
            var externalAsmdefViolations = Directory.GetFiles(
                    Path.Combine(packageRoot, "Editor"),
                    "*.asmdef",
                    SearchOption.AllDirectories)
                .Where(path => !path.StartsWith(
                    assetManagerRoot,
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    "Ee4v.UI.Catalog.Editor.asmdef",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    "Ee4v.Composition.Editor.asmdef",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path =>
                {
                    var content = File.ReadAllText(path);
                    return Regex.IsMatch(
                        content,
                        @"""Ee4v\.AssetManager\.(?!Contracts\.Editor"")[^""]+""");
                })
                .Select(ToPackageRelativePath)
                .ToArray();
            Assert.That(
                externalAsmdefViolations,
                Is.Empty,
                string.Join("\n", externalAsmdefViolations));
        }

        [Test]
        [FeatureTestCase(
            "AssetManager Infrastructure の技術別配置を固定する",
            "SQLite、filesystem、Unity adapter の実装と namespace が対応する外側レイヤーのディレクトリに置かれることを確認します。",
            order: 27,
            category: FeatureTestCategory.StaticAudit)]
        public void InfrastructureSources_MatchTechnologyDirectories()
        {
            var infrastructureRoot = Path.Combine(GetAssetManagerRoot(), "Infrastructure");
            var persistenceRoot = Path.Combine(infrastructureRoot, "Persistence", "SQLite");
            var filesRoot = Path.Combine(infrastructureRoot, "Files");
            var unityRoot = Path.Combine(infrastructureRoot, "Unity");

            Assert.That(Directory.Exists(persistenceRoot), Is.True);
            Assert.That(Directory.Exists(filesRoot), Is.True);
            Assert.That(Directory.Exists(unityRoot), Is.True);

            var misplacedDatabaseDeclarations = Directory.GetFiles(
                    infrastructureRoot,
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !IsTestSource(path))
                .Where(path => !path.StartsWith(
                    persistenceRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => Regex.IsMatch(
                    File.ReadAllText(path),
                    @"\bpartial\s+class\s+AssetManagerDatabase\b"))
                .Select(ToPackageRelativePath)
                .ToArray();
            Assert.That(
                misplacedDatabaseDeclarations,
                Is.Empty,
                string.Join("\n", misplacedDatabaseDeclarations));

            AssertSourcesUseNamespace(
                persistenceRoot,
                "Ee4v.AssetManager.Infrastructure.Persistence.SQLite");
            AssertSourcesUseNamespace(
                filesRoot,
                "Ee4v.AssetManager.Infrastructure.Files");
            AssertSourcesUseNamespace(
                unityRoot,
                "Ee4v.AssetManager.Infrastructure.Unity");
        }

        private static void AssertAsmdef(
            string relativePath,
            IReadOnlyCollection<string> expectedReferences,
            bool requireNoEngineReferences = true)
        {
            var asmdefPath = Path.Combine(GetAssetManagerRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
            var content = File.ReadAllText(asmdefPath);
            var referencesMatch = Regex.Match(
                content,
                @"""references""\s*:\s*\[(?<references>.*?)\]",
                RegexOptions.Singleline);
            Assert.That(referencesMatch.Success, Is.True, relativePath + " has no references array.");

            var actualReferences = Regex.Matches(referencesMatch.Groups["references"].Value, @"""(?<name>[^""]+)""")
                .Cast<Match>()
                .Select(match => match.Groups["name"].Value)
                .OrderBy(reference => reference, StringComparer.Ordinal)
                .ToArray();
            Assert.That(
                actualReferences,
                Is.EqualTo(expectedReferences.OrderBy(reference => reference, StringComparer.Ordinal).ToArray()),
                relativePath);
            if (requireNoEngineReferences)
            {
                Assert.That(
                    Regex.IsMatch(content, @"""noEngineReferences""\s*:\s*true"),
                    Is.True,
                    relativePath + " must set noEngineReferences to true.");
            }
        }

        private static IEnumerable<string> GetAssetManagerSourceFiles(string layer)
        {
            return Directory.GetFiles(
                Path.Combine(GetAssetManagerRoot(), layer),
                "*.cs",
                SearchOption.AllDirectories);
        }

        private static void AssertSourcesUseNamespace(string directory, string expectedNamespace)
        {
            var violations = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !Regex.IsMatch(
                    File.ReadAllText(path),
                    @"namespace\s+" + Regex.Escape(expectedNamespace) + @"\b"))
                .Select(ToPackageRelativePath)
                .ToArray();
            Assert.That(
                violations,
                Is.Empty,
                expectedNamespace + ":\n" + string.Join("\n", violations));
        }

        private static string GetAssetManagerRoot()
        {
            var packageRoot = PackagePathUtility.GetPackageRootFullPath();
            Assert.That(packageRoot, Is.Not.Null.And.Not.Empty);
            return Path.Combine(packageRoot, "Editor", "AssetManager");
        }

        private static bool IsTestSource(string filePath)
        {
            return filePath.Replace('\\', '/')
                .IndexOf("/Test/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ToPackageRelativePath(string filePath)
        {
            var packageRoot = PackagePathUtility.GetPackageRootFullPath();
            return filePath.Substring(packageRoot.Length + 1).Replace('\\', '/');
        }
    }
}
