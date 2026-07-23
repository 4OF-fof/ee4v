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
    public sealed class CoreArchitectureTests
    {
        [Test]
        [FeatureTestCase(
            "Core内層の依存方向をasmdefで固定する",
            "SettingsとLocalizationのContracts/ServicesがUnity非依存で、Unity adapterが内側のassemblyだけを参照することを確認します。",
            order: 28,
            category: FeatureTestCategory.StaticAudit)]
        public void CoreLayerAsmdefs_HaveOnlyAllowedDependencies()
        {
            AssertAsmdef(
                "Contracts/Ee4v.Core.Contracts.Editor.asmdef",
                Array.Empty<string>(),
                requireNoEngineReferences: true);
            AssertAsmdef(
                "Services/Ee4v.Core.Services.Editor.asmdef",
                new[] { "Ee4v.Core.Contracts.Editor" },
                requireNoEngineReferences: true);
            AssertAsmdef(
                "Unity/Ee4v.Core.Unity.Editor.asmdef",
                new[]
                {
                    "Ee4v.Core.Contracts.Editor",
                    "Ee4v.Core.Services.Editor"
                },
                requireNoEngineReferences: false);
        }

        [Test]
        [FeatureTestCase(
            "Coreの内側はUnityと保存技術に依存しない",
            "ContractsとServicesからUnity、Newtonsoft、filesystem、EditorPrefs依存が排除されていることを確認します。",
            order: 29,
            category: FeatureTestCategory.StaticAudit)]
        public void CoreInnerSources_DoNotReferenceOuterTechnologies()
        {
            var forbiddenTokens = new[]
            {
                "UnityEngine",
                "UnityEditor",
                "Newtonsoft",
                "System.IO",
                "EditorPrefs",
                "ProjectSettings"
            };
            var violations = new List<string>();

            foreach (var directory in new[] { "Contracts", "Services" })
            {
                var root = Path.Combine(GetCoreRoot(), directory);
                foreach (var filePath in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    var source = File.ReadAllText(filePath);
                    violations.AddRange(
                        forbiddenTokens
                            .Where(source.Contains)
                            .Select(token => ToPackageRelativePath(filePath) + ": " + token));
                }
            }

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        [FeatureTestCase(
            "旧SettingApiと定義内drawerを残さない",
            "Settings利用側がinstance契約へ移行し、純粋なSettingDefinitionがUI Toolkit型を参照しないことを確認します。",
            order: 30,
            category: FeatureTestCategory.StaticAudit)]
        public void LegacyStaticSettingsApi_DoesNotRemain()
        {
            var editorRoot = Path.Combine(
                PackagePathUtility.GetPackageRootFullPath(),
                "Editor");
            var violations = Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf(
                    Path.DirectorySeparatorChar + "Test" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path => Regex.IsMatch(
                    File.ReadAllText(path),
                    @"\bSettingApi\b|\bcustomDrawer\s*:"))
                .Select(ToPackageRelativePath)
                .ToArray();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        [FeatureTestCase(
            "Localization解決とpresentation adapterを分離する",
            "旧配置を廃止し、Unity非依存のserviceとUI assembly上のI18N adapterが存在することを確認します。",
            order: 31,
            category: FeatureTestCategory.StaticAudit)]
        public void LocalizationService_IsSeparatedFromPresentationAdapter()
        {
            var coreRoot = GetCoreRoot();
            Assert.That(
                File.Exists(Path.Combine(coreRoot, "I18n", "I18N.cs")),
                Is.False);
            Assert.That(
                File.Exists(Path.Combine(
                    coreRoot,
                    "Services",
                    "Localization",
                    "LocalizationService.cs")),
                Is.True);
            Assert.That(
                File.Exists(Path.Combine(
                    coreRoot,
                    "UI",
                    "Localization",
                    "I18N.cs")),
                Is.True);
        }

        [Test]
        [FeatureTestCase(
            "Injector registryとUnity presentationを分離する",
            "登録規則はServices、Unity callbackとhost同期はUI assemblyへ配置されることを確認します。",
            order: 35,
            category: FeatureTestCategory.StaticAudit)]
        public void InjectorRegistry_IsSeparatedFromUnityPresentation()
        {
            var coreRoot = GetCoreRoot();
            Assert.That(
                Directory.Exists(Path.Combine(coreRoot, "Injector")),
                Is.False);
            Assert.That(
                File.Exists(Path.Combine(
                    coreRoot,
                    "Services",
                    "Injector",
                    "InjectionRegistry.cs")),
                Is.True);
            Assert.That(
                File.Exists(Path.Combine(
                    coreRoot,
                    "UI",
                    "Injector",
                    "InjectionPresenter.cs")),
                Is.True);

            var presenterSource = File.ReadAllText(Path.Combine(
                coreRoot,
                "UI",
                "Injector",
                "InjectionPresenter.cs"));
            Assert.That(
                presenterSource,
                Does.Not.Contain("GetType(\"UnityEditor.ProjectBrowser\")"));
        }

        [Test]
        [FeatureTestCase(
            "Testingを独立Moduleと依存レイヤへ分離する",
            "Core配下の旧Testingを廃止し、Contracts/ApplicationがUnity非依存であることを確認します。",
            order: 36,
            category: FeatureTestCategory.StaticAudit)]
        public void TestingModule_HasIndependentLayerBoundaries()
        {
            var coreRoot = GetCoreRoot();
            var testingRoot = Path.Combine(
                Directory.GetParent(coreRoot).FullName,
                "Testing");

            Assert.That(
                Directory.Exists(Path.Combine(coreRoot, "Testing")),
                Is.False);
            Assert.That(
                Directory.Exists(Path.Combine(
                    coreRoot,
                    "UI",
                    "Testing")),
                Is.False);

            AssertAsmdefAt(
                Path.Combine(
                    testingRoot,
                    "Contracts",
                    "Ee4v.Testing.Contracts.Editor.asmdef"),
                Array.Empty<string>(),
                requireNoEngineReferences: true);
            AssertAsmdefAt(
                Path.Combine(
                    testingRoot,
                    "Application",
                    "Ee4v.Testing.Application.Editor.asmdef"),
                new[] { "Ee4v.Testing.Contracts.Editor" },
                requireNoEngineReferences: true);

            var violations = new List<string>();
            foreach (var layer in new[] { "Contracts", "Application" })
            {
                var layerRoot = Path.Combine(testingRoot, layer);
                foreach (var sourcePath in Directory.GetFiles(
                    layerRoot,
                    "*.cs",
                    SearchOption.AllDirectories))
                {
                    var source = File.ReadAllText(sourcePath);
                    foreach (var token in new[]
                    {
                        "UnityEngine",
                        "UnityEditor",
                        "SessionState",
                        "JsonUtility"
                    })
                    {
                        if (source.Contains(token))
                        {
                            violations.Add(
                                ToPackageRelativePath(sourcePath) +
                                ": " +
                                token);
                        }
                    }
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                string.Join("\n", violations));
        }

        [Test]
        [FeatureTestCase(
            "不要なstubと空集約assemblyを残さない",
            "Phase1、Ee4v.Editor、旧Background static APIを廃止し、AssetManager UI namespaceをレイヤ名へ一致させます。",
            order: 37,
            category: FeatureTestCategory.StaticAudit)]
        public void FinalModuleLayout_HasNoLegacyShells()
        {
            var editorRoot = Directory.GetParent(GetCoreRoot()).FullName;
            Assert.That(
                Directory.Exists(Path.Combine(editorRoot, "Phase1")),
                Is.False);
            Assert.That(
                File.Exists(Path.Combine(
                    editorRoot,
                    "Ee4v.Editor.asmdef")),
                Is.False);
            Assert.That(
                File.Exists(Path.Combine(
                    GetCoreRoot(),
                    "Background",
                    "BackgroundActivityApi.cs")),
                Is.False);
            Assert.That(
                File.Exists(Path.Combine(
                    GetCoreRoot(),
                    "Services",
                    "Background",
                    "BackgroundActivityTracker.cs")),
                Is.True);

            var uiRoot = Path.Combine(
                editorRoot,
                "AssetManager",
                "UI");
            var violations = Directory.GetFiles(
                    uiRoot,
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    "AssemblyInfo.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Select(path => new
                {
                    Path = path,
                    Namespace = PackagePathUtility.GetDeclaredNamespace(path)
                })
                .Where(item =>
                    !string.Equals(
                        item.Namespace,
                        "Ee4v.AssetManager.UI",
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        item.Namespace,
                        "Ee4v.AssetManager.UI.Tests",
                        StringComparison.Ordinal))
                .Select(item =>
                    ToPackageRelativePath(item.Path) +
                    ": " +
                    item.Namespace)
                .ToArray();

            Assert.That(
                violations,
                Is.Empty,
                string.Join("\n", violations));
        }

        private static void AssertAsmdef(
            string relativePath,
            IReadOnlyCollection<string> expectedReferences,
            bool requireNoEngineReferences)
        {
            var path = Path.Combine(
                GetCoreRoot(),
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            AssertAsmdefAt(
                path,
                expectedReferences,
                requireNoEngineReferences);
        }

        private static void AssertAsmdefAt(
            string path,
            IReadOnlyCollection<string> expectedReferences,
            bool requireNoEngineReferences)
        {
            var content = File.ReadAllText(path);
            var referencesMatch = Regex.Match(
                content,
                @"""references""\s*:\s*\[(?<references>.*?)\]",
                RegexOptions.Singleline);
            Assert.That(referencesMatch.Success, Is.True);

            var actualReferences = Regex.Matches(
                    referencesMatch.Groups["references"].Value,
                    @"""(?<reference>[^""]+)""")
                .Cast<Match>()
                .Select(match => match.Groups["reference"].Value)
                .OrderBy(reference => reference, StringComparer.Ordinal)
                .ToArray();
            Assert.That(
                actualReferences,
                Is.EqualTo(expectedReferences.OrderBy(
                    reference => reference,
                    StringComparer.Ordinal)));
            Assert.That(
                Regex.IsMatch(
                    content,
                    @"""noEngineReferences""\s*:\s*" +
                    (requireNoEngineReferences ? "true" : "false")),
                Is.True);
        }

        private static string GetCoreRoot()
        {
            return Path.Combine(
                PackagePathUtility.GetPackageRootFullPath(),
                "Editor",
                "Core");
        }

        private static string ToPackageRelativePath(string path)
        {
            var packageRoot = PackagePathUtility.GetPackageRootFullPath()
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path.Substring(packageRoot.Length + 1)
                .Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
