using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ee4v.Core.Internal;
using Ee4v.Core.Testing;
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

        private static void AssertAsmdef(
            string relativePath,
            IReadOnlyCollection<string> expectedReferences,
            bool requireNoEngineReferences)
        {
            var path = Path.Combine(
                GetCoreRoot(),
                relativePath.Replace('/', Path.DirectorySeparatorChar));
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
