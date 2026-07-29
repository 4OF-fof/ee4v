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
