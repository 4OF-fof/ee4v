using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEngine.UIElements;

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

            var value = I18N.Get("settings.language.label");

            Assert.That(value, Is.Not.Null.And.Not.Empty);
            Assert.That(value, Is.Not.EqualTo("settings.language.label"));
        }

        [Test]
        [FeatureTestCase(
            "I18N.TryGet が caller file から scope を解決する",
            "I18N.TryGet が Core.Tests 名前空間の呼び出し元から Core scope を解決し、翻訳取得に成功することを確認します。",
            order: 1)]
        public void I18N_TryGet_ResolvesScope_FromCallerFilePath()
        {
            Ee4vCoreTestReset.RecoverEditorState();

            var found = I18N.TryGet(
                "settings.fallbackLanguage.label",
                out var value);

            Assert.That(found, Is.True);
            Assert.That(value, Is.Not.Null.And.Not.Empty);
            Assert.That(
                value,
                Is.Not.EqualTo("settings.fallbackLanguage.label"));
        }

        [Test]
        [FeatureTestCase(
            "設定 field は UI Toolkit で値変更を通知する",
            "標準の設定 field が IMGUI ではなく UI Toolkit の field を生成し、変更値を callback へ通知することを確認します。",
            order: 3)]
        public void SettingFieldRenderer_Create_UsesUiToolkitField()
        {
            object changedValue = null;

            var element = SettingFieldRenderer.Create(
                typeof(int),
                "Count",
                "Item count",
                3,
                value => changedValue = value);

            var field = element as IntegerField;
            Assert.That(field, Is.Not.Null);
            Assert.That(field.value, Is.EqualTo(3));

            var window = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.EditorWindow>();
            try
            {
                window.Show();
                window.rootVisualElement.Add(field);
                field.value = 7;
            }
            finally
            {
                window.Close();
            }

            Assert.That(changedValue, Is.EqualTo(7));
        }

        [Test]
        [FeatureTestCase(
            "設定画面は UI Toolkit のみで構築する",
            "SettingsUiRenderer が設定画面を VisualElement で構築し、IMGUIContainer を含めないことを確認します。",
            order: 4)]
        public void SettingsUiRenderer_BuildScope_DoesNotCreateImguiContainer()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var root = new VisualElement();

            SettingsUiRenderer.BuildScope(
                root,
                CoreSettings.Current,
                SettingScope.User,
                string.Empty);

            Assert.That(root.childCount, Is.GreaterThan(0));
            Assert.That(root.Query<IMGUIContainer>().ToList(), Is.Empty);
        }
    }
}
