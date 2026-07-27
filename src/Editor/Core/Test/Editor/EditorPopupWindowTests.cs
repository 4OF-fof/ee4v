using Ee4v.Core.Internal.EditorAPI.Backends;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class EditorPopupWindowTests
    {
        [TestCase("UnityEditor.ColorPicker")]
        [TestCase("UnityEditor.ObjectSelector")]
        [FeatureTestCase(
            "popup編集用の一時pickerを判定できる",
            "ColorFieldとObjectFieldへfocusを移しても編集popupを閉じないため、Unity internal pickerの型名をbackendだけで判定できることを確認します。",
            order: 175)]
        public void TransientPickerTypeNames_AreRecognized(
            string typeName)
        {
            Assert.That(
                EditorPopupWindowBackend
                    .IsTransientPickerTypeName(typeName),
                Is.True);
        }

        [Test]
        [FeatureTestCase(
            "通常のEditorWindowを一時pickerとして扱わない",
            "Project windowなどへfocusが移った場合は編集popupを閉じられることを確認します。",
            order: 176)]
        public void OtherTypeNames_AreRejected()
        {
            Assert.That(
                EditorPopupWindowBackend
                    .IsTransientPickerTypeName(
                        "UnityEditor.ProjectBrowser"),
                Is.False);
        }

        [Test]
        [FeatureTestCase(
            "開いている一時pickerの有無を安全に確認できる",
            "Color Pickerのスポイト操作でfocusが別windowへ移っても、pickerの生存期間を確認するadapterが例外なく利用できることを確認します。",
            order: 177)]
        public void OpenTransientPickerQuery_IsSafe()
        {
            Assert.DoesNotThrow(
                () => EditorPopupWindowBackend
                    .HasOpenTransientPicker());
        }

        [Test]
        [FeatureTestCase(
            "直接起動したスポイトの状態を安全に確認できる",
            "ColorField右端からColor Picker windowを介さず起動したEyeDropperも、一時操作として判定するadapterが利用できることを確認します。",
            order: 178)]
        public void EyeDropperQuery_IsSafe()
        {
            Assert.DoesNotThrow(
                () => EditorPopupWindowBackend
                    .IsEyeDropperOpen());
        }
    }
}
