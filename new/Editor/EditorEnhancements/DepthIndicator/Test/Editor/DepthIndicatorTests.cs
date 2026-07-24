using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.DepthIndicator.Tests
{
    public sealed class DepthIndicatorTests
    {
        [Test]
        [FeatureTestCase(
            "DepthIndicator の描画領域を計算できる",
            "Hierarchy item の矩形から枝線と終端線の描画領域を計算できることを確認します。",
            order: 10)]
        public void Geometry_CreatesBranchRects()
        {
            var itemRect = new Rect(80f, 20f, 200f, 16f);

            var firstCell = DepthIndicatorGeometry.GetFirstCell(itemRect);
            var parentCell =
                DepthIndicatorGeometry.MoveToParentCell(firstCell);
            var horizontal =
                DepthIndicatorGeometry.GetBranchHorizontalLine(parentCell);
            var branchEnd =
                DepthIndicatorGeometry.GetBranchEndVerticalLine(parentCell);

            Assert.That(firstCell.x, Is.EqualTo(64f));
            Assert.That(parentCell.x, Is.EqualTo(50f));
            Assert.That(horizontal.y, Is.EqualTo(27f));
            Assert.That(horizontal.height, Is.EqualTo(2f));
            Assert.That(branchEnd.yMax, Is.EqualTo(29f));
        }

        [Test]
        [FeatureTestCase(
            "DepthIndicator の設定は既定で有効",
            "DepthIndicator が初回起動時に有効であることを確認します。",
            order: 20)]
        public void Definition_IsEnabledByDefault()
        {
            Assert.That(
                DepthIndicatorDefinitions.Enabled.DefaultValue,
                Is.EqualTo(true));
        }
    }
}
