using System;
using Ee4v.Core.Testing;
using NUnit.Framework;

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
    }
}
