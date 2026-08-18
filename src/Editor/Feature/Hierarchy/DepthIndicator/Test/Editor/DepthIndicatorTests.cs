using NUnit.Framework;
using UnityEngine;

namespace Ee4v.DepthIndicator.Tests
{
    public sealed class DepthIndicatorTests
    {
        [Test]
        public void Hierarchy_HiddenFollowingSiblingIsIgnored()
        {
            var parent = new GameObject("Parent");
            var visible = new GameObject("Visible");
            var hidden = new GameObject("Hidden");
            try
            {
                visible.transform.SetParent(parent.transform);
                hidden.transform.SetParent(parent.transform);
                hidden.hideFlags |= HideFlags.HideInHierarchy;

                Assert.That(
                    DepthIndicatorRenderer.IsLastVisibleSibling(
                        visible.transform),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Hierarchy_HiddenOnlyChildIsIgnored()
        {
            var parent = new GameObject("Parent");
            var hidden = new GameObject("Hidden");
            try
            {
                hidden.transform.SetParent(parent.transform);
                hidden.hideFlags |= HideFlags.HideInHierarchy;

                Assert.That(
                    DepthIndicatorRenderer.HasVisibleChild(
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
