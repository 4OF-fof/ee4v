using System.Linq;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI.Tests
{
    public sealed class DecorationStyleEditorTests
    {
        [Test]
        [FeatureTestCase(
            "装飾編集UIは独立したUiTextElementを使う",
            "標準fieldの内部labelを空にし、色とアイコンの項目名をUiTextFactory由来の要素で表示することを確認します。",
            order: 290,
            category: FeatureTestCategory.Ui)]
        public void Constructor_UsesIndependentTextElements()
        {
            var editor = new DecorationStyleEditor(
                CreateText(),
                new DecorationStyleEditorState(
                    Color.red,
                    null));

            var labels = editor
                .Query<UiTextElement>()
                .ToList()
                .Select(element => element.Text)
                .ToArray();

            Assert.That(labels, Does.Contain("Color"));
            Assert.That(labels, Does.Contain("Icon"));
            Assert.That(editor.ColorField.label, Is.Empty);
            Assert.That(editor.IconField.label, Is.Empty);
            Assert.That(
                editor.Query<ColorField>().ToList().Count,
                Is.EqualTo(1));
            Assert.That(
                editor.Query<ObjectField>().ToList().Count,
                Is.EqualTo(1));
        }

        [Test]
        [FeatureTestCase(
            "装飾編集UIは複数値の混在を表示できる",
            "複数対象で色またはアイコンが異なる場合に標準fieldのmixed表示へ反映されることを確認します。",
            order: 300,
            category: FeatureTestCategory.Ui)]
        public void SetState_AppliesMixedValues()
        {
            var editor = new DecorationStyleEditor(
                CreateText());

            editor.SetState(
                new DecorationStyleEditorState(
                    Color.clear,
                    null,
                    colorIsMixed: true,
                    iconIsMixed: true));

            Assert.That(
                editor.ColorField.showMixedValue,
                Is.True);
            Assert.That(
                editor.IconField.showMixedValue,
                Is.True);
        }

        [Test]
        [FeatureTestCase(
            "未設定の任意色fieldを黒い帯として表示しない",
            "色が未設定の場合も任意色pickerには視認できる既定色を表示し、解除状態はpalette側で表現することを確認します。",
            order: 305,
            category: FeatureTestCategory.Ui)]
        public void SetState_UsesVisibleCustomColorWhenUnset()
        {
            var editor = new DecorationStyleEditor(
                CreateText(),
                new DecorationStyleEditorState(
                    Color.clear,
                    null));

            Assert.That(
                editor.ColorField.value,
                Is.Not.EqualTo(Color.clear));
            Assert.That(
                editor.ColorField.value.a,
                Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        [FeatureTestCase(
            "装飾編集UIへ色presetと最近使ったiconを表示する",
            "featureから渡された候補だけを表示し、共通UI自身が履歴やpreset内容を所有しないことを確認します。",
            order: 310,
            category: FeatureTestCategory.Ui)]
        public void SetState_RendersProvidedCandidates()
        {
            var first = new Texture2D(1, 1);
            var second = new Texture2D(1, 1);
            try
            {
                var editor = new DecorationStyleEditor(
                    CreateText(),
                    new DecorationStyleEditorState(
                        Color.red,
                        null,
                        new[]
                        {
                            new DecorationColorPresetState(
                                Color.red),
                            new DecorationColorPresetState(
                                Color.blue)
                        },
                        new[]
                        {
                            new DecorationIconCandidateState(
                                first,
                                isApplied: true,
                                canRemove: false),
                            new DecorationIconCandidateState(
                                second)
                        },
                        iconIsMixed: true));

                Assert.That(
                    editor.ColorPresetCount,
                    Is.EqualTo(2));
                Assert.That(
                    editor.RecentIconCount,
                    Is.EqualTo(2));
                Assert.That(
                    editor.RemovableRecentIconCount,
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        [FeatureTestCase(
            "アイコン履歴の先頭へ解除候補を常設する",
            "履歴がない場合も色paletteと同じ解除候補を表示し、履歴行の高さを変えないことを確認します。",
            order: 315,
            category: FeatureTestCategory.Ui)]
        public void SetState_AlwaysRendersIconResetCandidate()
        {
            var editor = new DecorationStyleEditor(
                CreateText(),
                new DecorationStyleEditorState(
                    Color.clear,
                    null));

            Assert.That(
                editor.RecentIconCount,
                Is.Zero);
            Assert.That(
                editor.HasIconResetCandidate,
                Is.True);
            Assert.That(
                editor.IconResetUsesSwatchStyle,
                Is.True);
            Assert.That(
                editor.RemovableRecentIconCount,
                Is.Zero);
        }

        private static DecorationStyleEditorText CreateText()
        {
            return new DecorationStyleEditorText(
                "Color",
                "Color tooltip",
                "Custom",
                "Clear color",
                "Icon",
                "Icon tooltip",
                "Recently used",
                "Texture",
                "Clear icon");
        }
    }
}
