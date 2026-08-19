using NUnit.Framework;
using UnityEngine;

namespace Ee4v.HierarchyStyle.Tests
{
    public sealed class HierarchyStyleTests
    {
        [Test]
        public void VisibilityApi_RestoresActiveStateAndTag()
        {
            var gameObject = new GameObject("VisibilityApiTarget");
            try
            {
                var instanceIds = new[]
                {
                    gameObject.GetInstanceID()
                };

                Assert.That(
                    HierarchyStyleApi.Hide(
                        instanceIds,
                        "Hide test object"),
                    Is.EqualTo(1));
                Assert.That(gameObject.activeSelf, Is.False);
                Assert.That(gameObject.tag, Is.EqualTo("EditorOnly"));
                Assert.That(
                    gameObject.hideFlags & HideFlags.HideInHierarchy,
                    Is.EqualTo(HideFlags.HideInHierarchy));

                Assert.That(
                    HierarchyStyleApi.Reveal(
                        instanceIds,
                        "Reveal test object"),
                    Is.EqualTo(1));
                Assert.That(gameObject.activeSelf, Is.True);
                Assert.That(gameObject.tag, Is.EqualTo("Untagged"));
                Assert.That(
                    gameObject.hideFlags & HideFlags.HideInHierarchy,
                    Is.EqualTo(HideFlags.None));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

    }
}
