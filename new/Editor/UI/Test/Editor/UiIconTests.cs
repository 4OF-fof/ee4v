using System;
using Ee4v.Core.Testing;
using NUnit.Framework;
using UnityEngine;
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
            "Texture source をそのまま表示する",
            "Icon が custom texture source を優先して表示し、非表示状態にならないことを確認します。",
            order: 201)]
        public void Icon_SetState_WithTextureSource_UsesProvidedTexture()
        {
            var icon = new Icon();

            icon.SetState(IconState.FromTexture(Texture2D.blackTexture, size: 20f));

            var image = icon.Q<Image>();

            Assert.That(icon.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(icon.style.width.value.value, Is.EqualTo(20f));
            Assert.That(image, Is.Not.Null);
            Assert.That(image.image, Is.SameAs(Texture2D.blackTexture));
        }

        [Test]
        [FeatureTestCase(
            "enum 登録済み内蔵アイコンを component で表示できる",
            "Icon component が UiBuiltinIcon enum の全値をそのまま受け取り、非 null の image として適用できることを確認します。",
            order: 202)]
        public void Icon_SetState_WithBuiltinSource_UsesResolvedTextureForAllRegisteredIcons()
        {
            foreach (UiBuiltinIcon builtinIcon in Enum.GetValues(typeof(UiBuiltinIcon)))
            {
                var icon = new Icon();
                icon.SetState(IconState.FromBuiltinIcon(builtinIcon, size: 18f));

                var image = icon.Q<Image>();

                Assert.That(icon.style.display.value, Is.EqualTo(DisplayStyle.Flex), builtinIcon.ToString());
                Assert.That(image, Is.Not.Null, builtinIcon.ToString());
                Assert.That(image.image, Is.Not.Null, builtinIcon.ToString());
            }
        }

        [Test]
        [FeatureTestCase(
            "Texture source は null texture を許可しない",
            "IconState が Texture source を使う場合は null texture を受け付けず、source 未設定状態に戻らないことを確認します。",
            order: 203)]
        public void IconState_FromTexture_RejectsNullTexture()
        {
            Assert.Throws<ArgumentNullException>(() => IconState.FromTexture(null));
        }
    }
}
