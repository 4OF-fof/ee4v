using System;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ee4v.UI.Tests
{
    public sealed class UiIconTests
    {
        [Test]
        [FeatureTestCase(
            "登録済み内蔵アイコンをすべて解決できる",
            "UiBuiltinIcon enum に登録された全ての Unity 内蔵アイコンが現在の Unity version で取得可能であることを確認します。",
            order: 200)]
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
        [FeatureTestCase(
            "登録済みFluentアイコンをすべて解決できる",
            "厳選して同梱したFluent SVGから生成した512px PNGがUI Toolkit描画用に取得可能であることを確認します。",
            order: 201)]
        public void UiFluentIconResolver_TryResolve_AllRegisteredIcons()
        {
            var iconCount = 0;
            foreach (UiFluentIcon fluentIcon in
                     Enum.GetValues(typeof(UiFluentIcon)))
            {
                var resolved =
                    UiFluentIconResolver.TryResolve(
                        fluentIcon,
                        out var texture);

                Assert.That(
                    resolved,
                    Is.True,
                    fluentIcon.ToString());
                Assert.That(
                    texture,
                    Is.Not.Null,
                    fluentIcon.ToString());
                Assert.That(
                    texture.width,
                    Is.EqualTo(512),
                    fluentIcon.ToString());
                Assert.That(
                    texture.height,
                    Is.EqualTo(512),
                    fluentIcon.ToString());
                iconCount++;
            }

            Assert.That(iconCount, Is.EqualTo(102));
        }

        [Test]
        public void FluentPngs_UseCrispUiImportSettings()
        {
            foreach (UiFluentIcon fluentIcon in
                     Enum.GetValues(typeof(UiFluentIcon)))
            {
                var path =
                    UiFluentIconResolver.GetAssetPath(
                        fluentIcon);
                var importer =
                    AssetImporter.GetAtPath(path)
                        as TextureImporter;

                Assert.That(
                    importer,
                    Is.Not.Null,
                    fluentIcon.ToString());
                Assert.That(
                    importer.mipmapEnabled,
                    Is.False,
                    fluentIcon.ToString());
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(
                        TextureImporterCompression
                            .Uncompressed),
                    fluentIcon.ToString());
                Assert.That(
                    importer.alphaIsTransparency,
                    Is.True,
                    fluentIcon.ToString());
                Assert.That(
                    importer.wrapMode,
                    Is.EqualTo(TextureWrapMode.Clamp),
                    fluentIcon.ToString());
                Assert.That(
                    importer.filterMode,
                    Is.EqualTo(FilterMode.Bilinear),
                    fluentIcon.ToString());
                Assert.That(
                    importer.maxTextureSize,
                    Is.EqualTo(512),
                    fluentIcon.ToString());
            }
        }

        [TestCase("FolderZip", "folder_zip")]
        [TestCase("FolderBranchFork", "folder_branch_fork")]
        [TestCase("FolderLayer", "folder_layer")]
        [TestCase("MusicNote2", "music_note_2")]
        [TestCase("CloudArrowDown", "cloud_arrow_down")]
        public void UiFluentIconResolver_UsesStableSnakeCaseNames(
            string iconName,
            string expectedStem)
        {
            Assert.That(
                UiFluentIconResolver.ToSnakeCase(
                    iconName),
                Is.EqualTo(expectedStem));
        }

    }
}
