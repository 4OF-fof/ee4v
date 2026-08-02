using System;
using System.IO;
using System.Linq;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;

[assembly: FeatureTestSuite(
    "AvatarModify Architecture",
    "AvatarModify",
    "Ee4v.AvatarModify.Tests.Editor",
    "AvatarModify の依存方向と外部境界を確認します。",
    order: 306)]

namespace Ee4v.AvatarModify.Tests
{
    public sealed class AvatarModifyArchitectureTests
    {
        [Test]
        [FeatureTestCase(
            "Domain と Application を Unity 非依存に保つ",
            "中心 assembly が Unity、filesystem、Git、VRChat の具象を参照しないことを確認します。",
            order: 1)]
        public void InnerAssemblies_DoNotReferenceExternalTechnologies()
        {
            var root = GetModuleRoot();
            var forbidden = new[]
            {
                "UnityEditor",
                "UnityEngine",
                "System.IO",
                "System.Diagnostics",
                "Newtonsoft",
                "VRC."
            };
            foreach (var layer in new[] { "Domain", "Application" })
            {
                var directory = Path.Combine(root, layer);
                var content = string.Join(
                    "\n",
                    Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                        .Select(File.ReadAllText));
                Assert.That(
                    forbidden.Where(content.Contains),
                    Is.Empty,
                    layer);
                Assert.That(
                    File.ReadAllText(Directory.GetFiles(directory, "*.asmdef").Single()),
                    Does.Match("\\\"noEngineReferences\\\"\\s*:\\s*true"),
                    layer);
            }
        }

        [Test]
        [FeatureTestCase(
            "VRChat SDKのDescriptorだけを任意接続する",
            "SDK非導入時にassemblyを無効化し、buildやbackupへ接続しないことを確認します。",
            order: 2)]
        public void VrchatBridge_OnlyDetectsAvatarDescriptor()
        {
            var directory = Path.Combine(
                GetModuleRoot(),
                "Infrastructure",
                "VRChat");
            var source = File.ReadAllText(Path.Combine(
                directory,
                "VrchatAvatarDescriptorBridge.cs"));
            var assembly = File.ReadAllText(Directory.GetFiles(directory, "*.asmdef").Single());

            Assert.That(source, Does.Contain("VRCAvatarDescriptor"));
            Assert.That(source, Does.Not.Contain("BuildEvent"));
            Assert.That(source, Does.Not.Contain("Backup"));
            Assert.That(source, Does.Not.Contain("System.Reflection"));
            Assert.That(assembly, Does.Contain("com.vrchat.avatars"));
            Assert.That(assembly, Does.Contain("EE4V_VRCSDK_AVATARS"));
        }

        [Test]
        [FeatureTestCase(
            "AvatarModifyがCollectionとbackupを所有しない",
            "module内にCollection選択、record store、Git gateway、独立windowが残っていないことを確認します。",
            order: 3)]
        public void Module_ContainsOnlyVariantCreationResponsibilities()
        {
            var root = GetModuleRoot();
            var productionLayers = new[]
            {
                "Domain",
                "Application",
                "Infrastructure",
                "Composition",
                "UI"
            };
            var sources = string.Join(
                "\n",
                productionLayers.SelectMany(layer =>
                        Directory.GetFiles(
                            Path.Combine(root, layer),
                            "*.cs",
                            SearchOption.AllDirectories))
                    .Select(File.ReadAllText));

            Assert.That(sources, Does.Not.Contain("GetCollections("));
            Assert.That(sources, Does.Not.Contain("BackupNow("));
            Assert.That(sources, Does.Not.Contain("[MenuItem("));
            Assert.That(sources, Does.Not.Contain("GitAvatarBackupGateway"));
        }

        private static string GetModuleRoot()
        {
            var path = AssetDatabase.FindAssets("AvatarModifyDomain t:Script")
                .Select(AssetDatabase.GUIDToAssetPath)
                .First(candidate => candidate.EndsWith(
                    "Editor/AvatarModify/Domain/AvatarModifyDomain.cs",
                    StringComparison.Ordinal));
            return Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                Path.GetDirectoryName(Path.GetDirectoryName(path))));
        }
    }
}
