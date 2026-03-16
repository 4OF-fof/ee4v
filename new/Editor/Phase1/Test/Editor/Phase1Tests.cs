using System;
using System.Collections;
using Ee4v.Core.Settings;
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
        public void Phase1Definitions_RegisterAll_RegistersExpectedSettings()
        {
            Phase1Definitions.RegisterAll();

            var definitions = SettingApi.GetDefinitions(SettingScope.User);
            var projectDefinitions = SettingApi.GetDefinitions(SettingScope.Project);

            Assert.That(definitions.Count, Is.EqualTo(4));
            Assert.That(projectDefinitions.Count, Is.EqualTo(6));
        }

        [Test]
        public void Phase1Definitions_DefaultValues_AreAvailableThroughSettingApi()
        {
            Phase1Definitions.RegisterAll();

            Assert.That(SettingApi.Get(Phase1Definitions.EnableHierarchyItemStub), Is.True);
            Assert.That(SettingApi.Get(Phase1Definitions.HierarchyBadgeText), Is.EqualTo("stub"));
            Assert.That(SettingApi.Get(Phase1Definitions.ToolbarButtonWidth), Is.EqualTo(96));
        }

        [Test]
        public void Phase1Definitions_InvalidProjectValue_ThrowsValidationError()
        {
            Phase1Definitions.RegisterAll();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                SettingApi.Set(Phase1Definitions.HierarchyBadgeText, string.Empty, saveImmediately: false));

            Assert.That(exception.Message, Is.Not.Empty);
        }

        [Test]
        public void Ee4vPhase1TestReset_AllowsBootstrapToRunAgain()
        {
            Phase1Bootstrap.EnsureInitialized();
            Assert.That(((IDictionary)ReflectionReset.GetStaticField(typeof(SettingApi), "Definitions")).Count, Is.GreaterThan(0));

            Ee4vPhase1TestReset.ResetAll();

            Assert.That(((IDictionary)ReflectionReset.GetStaticField(typeof(SettingApi), "Definitions")).Count, Is.EqualTo(0));
            Assert.That((bool)ReflectionReset.GetStaticField(typeof(Phase1Bootstrap), "_initialized"), Is.False);
            Assert.That((bool)ReflectionReset.GetStaticField(typeof(Phase1Definitions), "_registered"), Is.False);
        }
    }
}
