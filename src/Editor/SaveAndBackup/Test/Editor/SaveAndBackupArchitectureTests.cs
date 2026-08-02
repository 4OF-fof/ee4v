using System;
using System.IO;
using System.Linq;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;

[assembly: FeatureTestSuite(
    "SaveAndBackup Architecture",
    "SaveAndBackup",
    "Ee4v.SaveAndBackup.Tests.Editor",
    "SaveAndBackupの独立性とGit境界を確認します。",
    order: 309)]

namespace Ee4v.SaveAndBackup.Tests
{
    public sealed class SaveAndBackupArchitectureTests
    {
        [Test]
        public void Module_DoesNotReferenceAvatarModify()
        {
            var root = GetModuleRoot();
            var content = string.Join(
                "\n",
                new[]
                    {
                        "Domain",
                        "Application",
                        "Infrastructure",
                        "Composition"
                    }
                    .SelectMany(layer =>
                        Directory.GetFiles(
                            Path.Combine(root, layer),
                            "*.cs",
                            SearchOption.AllDirectories))
                    .Select(File.ReadAllText));

            Assert.That(
                content,
                Does.Not.Contain("Ee4v.AvatarModify"));
        }

        [Test]
        public void GitGateway_DoesNotMutateGlobalOrRemoteState()
        {
            var source = File.ReadAllText(Path.Combine(
                GetModuleRoot(),
                "Infrastructure",
                "Git",
                "GitSaveAndBackupGateway.cs"));

            Assert.That(source, Does.Contain("\"--local\""));
            Assert.That(source, Does.Not.Contain("\"--global\""));
            Assert.That(source, Does.Not.Contain("\"push\""));
            Assert.That(source, Does.Not.Contain("\"remote\""));
        }

        private static string GetModuleRoot()
        {
            var path = AssetDatabase.FindAssets(
                    "SaveAndBackupDomain t:Script")
                .Select(AssetDatabase.GUIDToAssetPath)
                .First(candidate => candidate.EndsWith(
                    "Editor/SaveAndBackup/Domain/SaveAndBackupDomain.cs",
                    StringComparison.Ordinal));
            return Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                Path.GetDirectoryName(
                    Path.GetDirectoryName(path))));
        }
    }
}
