using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.AssetManager.UI.Tests
{
    public sealed class SourcePrioritySettingDrawerTests
    {
        [Test]
        [FeatureTestCase(
            "データソース優先順位を固定3項目へ正規化する",
            "欠損・重複・未知値があっても編集可能な固定3項目の順序へ復元することを確認します。",
            order: 180)]
        public void NormalizeOrder_ReturnsEachKnownSourceExactlyOnce()
        {
            var order =
                SourcePrioritySettingDrawer.NormalizeOrder(
                    "eagle,unknown,eagle");

            Assert.That(
                order,
                Is.EqualTo(new[] { "eagle", "ee4v", "blm" }));
            Assert.That(
                SourcePrioritySettingDrawer.SerializeOrder(order),
                Is.EqualTo("eagle,ee4v,blm"));
        }
    }
}
