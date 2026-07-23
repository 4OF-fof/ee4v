using System;
using Ee4v.Core.Settings;
using Ee4v.Core.Testing;
using NUnit.Framework;

namespace Ee4v.Phase1.Tests
{
    public sealed class Phase1Tests
    {
        [SetUp]
        public void SetUp()
        {
            Ee4vPhase1TestReset.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            Ee4vPhase1TestReset.ResetAll();
            Ee4vPhase1TestReset.RecoverEditorState();
        }

        [Test]
        [FeatureTestCase(
            "Phase1 の設定定義が登録される",
            "Phase1Definitions.RegisterAll が User / Project の各設定定義を想定件数で登録することを確認します。",
            order: 20)]
        public void Phase1Definitions_RegisterAll_RegistersExpectedSettings()
        {
            var settings = CoreSettings.Current;
            Phase1Definitions.RegisterAll(settings);

            var definitions = settings.GetDefinitions(SettingScope.User);
            var projectDefinitions = settings.GetDefinitions(SettingScope.Project);

            Assert.That(definitions.Count, Is.EqualTo(3));
            Assert.That(projectDefinitions.Count, Is.EqualTo(5));
        }

        [Test]
        [FeatureTestCase(
            "Phase1 のデフォルト値を取得できる",
            "Phase1 の代表的な設定値をISettingsService経由で既定値として取得できることを確認します。",
            order: 21)]
        public void Phase1Definitions_DefaultValues_AreAvailableThroughSettingsService()
        {
            var settings = CoreSettings.Current;
            Phase1Definitions.RegisterAll(settings);

            Assert.That(settings.Get(Phase1Definitions.EnableHierarchyItemStub), Is.True);
            Assert.That(settings.Get(Phase1Definitions.HierarchyBadgeText), Is.EqualTo("stub"));
            Assert.That(settings.Get(Phase1Definitions.ToolbarButtonWidth), Is.EqualTo(96));
        }

        [Test]
        [FeatureTestCase(
            "Phase1 の不正値はバリデーションで拒否される",
            "Phase1Definitions の validator が空文字のような不正値を拒否することを確認します。",
            order: 22)]
        public void Phase1Definitions_InvalidProjectValue_ThrowsValidationError()
        {
            var settings = CoreSettings.Current;
            Phase1Definitions.RegisterAll(settings);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                settings.Set(Phase1Definitions.HierarchyBadgeText, string.Empty, saveImmediately: false));

            Assert.That(exception.Message, Is.Not.Empty);
        }

        [Test]
        [FeatureTestCase(
            "reset 後に Phase1 bootstrap を再実行できる",
            "Ee4vPhase1TestReset のあとで Phase1 の初期化状態が戻り、再度 bootstrap 可能になることを確認します。",
            order: 23)]
        public void Ee4vPhase1TestReset_AllowsBootstrapToRunAgain()
        {
            Phase1Bootstrap.EnsureInitialized();
            Assert.That(
                CoreSettings.Current.GetDefinitions(SettingScope.User).Count,
                Is.GreaterThan(0));

            Ee4vPhase1TestReset.ResetAll();

            Assert.That(
                CoreSettings.Current.GetDefinitions(SettingScope.User).Count,
                Is.EqualTo(0));
            Assert.That((bool)ReflectionReset.GetStaticField(typeof(Phase1Bootstrap), "_initialized"), Is.False);
        }
    }
}
