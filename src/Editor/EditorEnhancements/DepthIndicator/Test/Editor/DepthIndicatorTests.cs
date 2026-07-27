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

        [Test]
        [FeatureTestCase(
            "非表示の後続要素を枝線の判定から除外する",
            "後続の兄弟がHierarchyから非表示なら、現在の要素を最後の表示兄弟として扱うことを確認します。",
            order: 30)]
        public void Hierarchy_HiddenFollowingSiblingIsIgnored()
        {
            var parent = new GameObject("Parent");
            var visible = new GameObject("Visible");
            var hidden = new GameObject("Hidden");
            try
            {
                visible.transform.SetParent(parent.transform);
                hidden.transform.SetParent(parent.transform);
                hidden.hideFlags |=
                    HideFlags.HideInHierarchy;

                Assert.That(
                    DepthIndicatorHierarchy
                        .IsLastVisibleSibling(
                            visible.transform),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        [FeatureTestCase(
            "非表示の子だけを持つ要素を葉として扱う",
            "Hierarchyから非表示の子を表示中の子として数えないことを確認します。",
            order: 40)]
        public void Hierarchy_HiddenOnlyChildIsIgnored()
        {
            var parent = new GameObject("Parent");
            var hidden = new GameObject("Hidden");
            try
            {
                hidden.transform.SetParent(parent.transform);
                hidden.hideFlags |=
                    HideFlags.HideInHierarchy;

                Assert.That(
                    DepthIndicatorHierarchy
                        .HasVisibleChild(
                            parent.transform),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
