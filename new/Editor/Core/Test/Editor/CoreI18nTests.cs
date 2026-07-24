using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.Testing.Contracts;
using Ee4v.UI;
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
            "I18N.Get が caller namespace から scope を解決する",
            "I18N.Get が Core.Tests 名前空間の call stack から Core scope を解決し、キー文字列ではなく翻訳値を返すことを確認します。",
            order: 0)]
        public void I18N_Get_ResolvesScope_FromCallerNamespace()
        {
            Ee4vCoreTestReset.RecoverEditorState();

            var value = I18N.Get("settings.language.label");

            Assert.That(value, Is.Not.Null.And.Not.Empty);
            Assert.That(value, Is.Not.EqualTo("settings.language.label"));
        }

        [Test]
        [FeatureTestCase(
            "I18N.TryGet が caller namespace から scope を解決する",
            "I18N.TryGet が Core.Tests 名前空間の call stack から Core scope を解決し、翻訳取得に成功することを確認します。",
            order: 1)]
        public void I18N_TryGet_ResolvesScope_FromCallerNamespace()
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
            "I18N.Get は string 引数を書式展開する",
            "string の書式引数が scope 解決用引数として誤解釈されず、翻訳文へ埋め込まれることを確認します。",
            order: 2)]
        public void I18N_Get_FormatsStringArgument()
        {
            Ee4vCoreTestReset.RecoverEditorState();

            var value = I18N.Get("settings.unsupportedType", "SampleType");

            Assert.That(value, Does.Contain("SampleType"));
            Assert.That(value, Does.Not.Contain("{0}"));
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
                "Item count",
                3,
                value => changedValue = value);

            var field = element as IntegerField;
            Assert.That(field, Is.Not.Null);
            Assert.That(field.value, Is.EqualTo(3));
            Assert.That(field.label, Is.Empty);

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
            "設定画面の IMGUI fallback は UiTextFactory 内に限定する",
            "フォントキャッシュ対策用のIMGUIContainerがUiTextElementの外へ作られないことを確認します。",
            order: 4)]
        public void SettingsUiRenderer_BuildScope_ContainsImguiOnlyInsideUiTextElements()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var root = new VisualElement();

            SettingsUiRenderer.BuildScope(
                root,
                CoreSettings.Current,
                SettingScope.User,
                string.Empty);

            Assert.That(root.childCount, Is.GreaterThan(0));
            var allImguiContainers =
                root.Query<IMGUIContainer>().ToList();
            var uiTextImguiContainers =
                root.Query<UiTextElement>()
                    .ToList()
                    .SelectMany(text =>
                        text.Query<IMGUIContainer>().ToList())
                    .ToList();
            Assert.That(
                uiTextImguiContainers,
                Is.EquivalentTo(allImguiContainers));
        }

        [Test]
        [FeatureTestCase(
            "設定画面は内容を縮めずスクロールできる",
            "設定項目が増えても行を圧縮せず、共通の縦スクロール領域内へ配置されることを確認します。",
            order: 5)]
        public void SettingsUiRenderer_BuildScope_UsesNonShrinkingScrollableContent()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var root = new VisualElement();

            SettingsUiRenderer.BuildScope(
                root,
                CoreSettings.Current,
                SettingScope.User,
                string.Empty);

            Assert.That(root.childCount, Is.EqualTo(1));
            var scrollView = root[0] as ScrollView;
            Assert.That(scrollView, Is.Not.Null);
            Assert.That(scrollView.mode, Is.EqualTo(ScrollViewMode.Vertical));
            Assert.That(scrollView.contentContainer.childCount, Is.GreaterThan(0));

            foreach (var section in scrollView.contentContainer.Children())
            {
                Assert.That(section, Is.TypeOf<Foldout>());
                Assert.That(section.style.flexShrink.value, Is.EqualTo(0f));

                foreach (var row in section.Children())
                {
                    Assert.That(row.style.flexShrink.value, Is.EqualTo(0f));
                }
            }
        }

        [Test]
        [FeatureTestCase(
            "設定項目のラベルは UiTextFactory で描画する",
            "Unity標準fieldのlabelを空にし、フォントキャッシュ対策済みのUiTextElementが項目名を表示することを確認します。",
            order: 6,
            category: FeatureTestCategory.Ui)]
        public void SettingsUiRenderer_BuildScope_UsesUiTextFactoryLabels()
        {
            Ee4vCoreTestReset.RecoverEditorState();
            var root = new VisualElement();

            SettingsUiRenderer.BuildScope(
                root,
                CoreSettings.Current,
                SettingScope.User,
                string.Empty);

            var expectedLabel = I18N.Get(
                "settings.language.label");
            var expectedSection = I18N.Get(
                "settings.section.localization");
            Assert.That(
                root.Query<UiTextElement>()
                    .ToList()
                    .Any(label => label.Text == expectedLabel),
                Is.True);
            Assert.That(
                root.Query<UiTextElement>()
                    .ToList()
                    .Any(label => label.Text == expectedSection),
                Is.True);
            Assert.That(
                root.Query<Foldout>()
                    .ToList()
                    .All(foldout => string.IsNullOrEmpty(foldout.text)),
                Is.True);
            Assert.That(
                root.Query<TextField>()
                    .ToList()
                    .All(field => string.IsNullOrEmpty(field.label)),
                Is.True);
            Assert.That(
                root.Query<PopupField<string>>()
                    .ToList()
                    .All(field => string.IsNullOrEmpty(field.label)),
                Is.True);
        }

        [Test]
        [FeatureTestCase(
            "リスト設定をカンマ区切りで保存する",
            "要素ごとの入力値を正規化し、永続化へ渡す文字列がカンマ区切りになることを確認します。",
            order: 7)]
        public void CommaSeparatedListSettingDrawer_SerializesItems()
        {
            var serialized =
                CommaSeparatedListSettingDrawer.SerializeItems(
                    new[] { " ee4v ", "eagle;blm", string.Empty });

            Assert.That(
                serialized,
                Is.EqualTo("ee4v,eagle,blm"));
        }
    }
}
