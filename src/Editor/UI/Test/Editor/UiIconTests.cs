using System;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEngine.UIElements;

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

            Assert.That(iconCount, Is.EqualTo(100));
        }

        [TestCase("FolderZip", "folder_zip")]
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

        [Test]
        public void FolderZip_UsesGeneratedPngAsset()
        {
            var resolved =
                UiFluentIconResolver.TryResolve(
                    UiFluentIcon.FolderZip,
                    out var texture);

            Assert.That(resolved, Is.True);
            Assert.That(
                texture.width,
                Is.EqualTo(512));
            Assert.That(
                UiFluentIconResolver.GetAssetPath(
                    UiFluentIcon.FolderZip),
                Does.EndWith(
                    "/FluentUiSystemIcons/Png512/" +
                    "folder_zip.png"));
        }

        [Test]
        public void Icon_UsesGeneratedTextureForFluentSource()
        {
            UiFluentIconResolver.TryResolve(
                UiFluentIcon.FolderZip,
                out var expectedTexture);
            var icon = new Icon(
                IconState.FromFluentIcon(
                    UiFluentIcon.FolderZip,
                    size: 44f));

            var image = icon.Q<Image>();

            Assert.That(
                image,
                Is.Not.Null);
            Assert.That(
                image.image,
                Is.SameAs(expectedTexture));
        }
    }
}
