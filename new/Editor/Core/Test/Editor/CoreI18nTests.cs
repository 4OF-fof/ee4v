using Ee4v.Core.I18n;
using Ee4v.Core.Testing;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class CoreI18nTests
    {
        [SetUp]
        public void SetUp()
        {
            Ee4vCoreTestReset.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            Ee4vCoreTestReset.ResetAll();
            Ee4vCoreTestReset.RecoverEditorState();
        }

        [Test]
        [FeatureTestCase(
            "I18N.Get が caller file から scope を解決する",
            "I18N.Get が Core.Tests 名前空間の呼び出し元から Core scope を解決し、キー文字列ではなく翻訳値を返すことを確認します。",
            order: 0)]
        public void I18N_Get_ResolvesScope_FromCallerFilePath()
        {
            Ee4vCoreTestReset.RecoverEditorState();

            var value = I18N.Get("testing.window.title");

            Assert.That(value, Is.Not.Null.And.Not.Empty);
            Assert.That(value, Is.Not.EqualTo("testing.window.title"));
        }

        [Test]
        [FeatureTestCase(
            "I18N.TryGet が caller file から scope を解決する",
            "I18N.TryGet が Core.Tests 名前空間の呼び出し元から Core scope を解決し、翻訳取得に成功することを確認します。",
            order: 1)]
        public void I18N_TryGet_ResolvesScope_FromCallerFilePath()
        {
            Ee4vCoreTestReset.RecoverEditorState();

            var found = I18N.TryGet("testing.window.searchPlaceholder", out var value);

            Assert.That(found, Is.True);
            Assert.That(value, Is.Not.Null.And.Not.Empty);
            Assert.That(value, Is.Not.EqualTo("testing.window.searchPlaceholder"));
        }
    }
}
