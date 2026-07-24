using System.Linq;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ee4v.UI.Tests
{
    public sealed class CommaSeparatedListFieldTests
    {
        [Test]
        [FeatureTestCase(
            "カンマ区切りの保存値を改行区切りで表示する",
            "保存形式と編集UIを分離し、リストの各要素が複数行入力の1行として表示されることを確認します。",
            order: 250,
            category: FeatureTestCategory.Ui)]
        public void Constructor_CreatesOneMultilineInput()
        {
            var field = new CommaSeparatedListField(
                new CommaSeparatedListFieldState(
                    new[] { "Airi", "Manuka", "Moe" },
                    itemPlaceholder: "One item per line"));

            Assert.That(field.ItemCount, Is.EqualTo(3));
            Assert.That(
                field.ItemValues,
                Is.EqualTo(new[] { "Airi", "Manuka", "Moe" }));
            Assert.That(
                field.Query<InputField>().ToList().Count,
                Is.EqualTo(1));
            Assert.That(
                field.Query<InputField>().First().Value,
                Is.EqualTo("Airi\nManuka\nMoe"));
            Assert.That(
                field.Query<TextField>().First().multiline,
                Is.True);
            Assert.That(
                field.Query<Foldout>().ToList(),
                Is.Empty);
        }

        [Test]
        [FeatureTestCase(
            "区切り文字を貼り付けると要素ごとの入力欄へ展開する",
            "単一の入力欄へカンマ区切りの値を貼り付けても、各要素を独立した入力欄として編集できることを確認します。",
            order: 260,
            category: FeatureTestCategory.Ui)]
        public void InputField_PastedDelimitedValuesExpandToItems()
        {
            var field = new CommaSeparatedListField(
                new CommaSeparatedListFieldState(
                    new[] { "ee4v" }));
            var window =
                UnityEngine.ScriptableObject.CreateInstance<
                    UnityEditor.EditorWindow>();
            try
            {
                window.Show();
                window.rootVisualElement.Add(field);
                var editor = field.Query<InputField>().First();

                editor.Value = "ee4v,eagle,blm";
            }
            finally
            {
                window.Close();
            }

            Assert.That(field.ItemCount, Is.EqualTo(3));
            Assert.That(
                field.ItemValues,
                Is.EqualTo(new[] { "ee4v", "eagle", "blm" }));
            Assert.That(
                field.Query<InputField>().First().Value,
                Is.EqualTo("ee4v\neagle\nblm"));
        }
    }
}
