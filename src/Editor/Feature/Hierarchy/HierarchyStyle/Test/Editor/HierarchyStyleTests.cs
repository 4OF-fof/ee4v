using System.Linq;
using Ee4v.HiddenObjects;
using Ee4v.ItemStyle;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.HierarchyStyle.Tests
{
    public sealed class HierarchyStyleTests
    {
        [Test]
        public void Inheritance_UsesNearestExplicitColor()
        {
            var parent = new Node(
                null,
                new ItemStyleValue(
                    "parent",
                    true,
                    Color.red,
                    string.Empty));
            var child = new Node(
                parent,
                new ItemStyleValue(
                    "child",
                    true,
                    Color.blue,
                    string.Empty));

            var found = HierarchyStyleInheritance
                .TryResolveBackgroundColor(
                    child,
                    node => node.Parent,
                    node => node.Style,
                    out var color);

            Assert.That(found, Is.True);
            Assert.That(color, Is.EqualTo(Color.blue));
        }

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

        private sealed class Node
        {
            public Node(Node parent, ItemStyleValue style)
            {
                Parent = parent;
                Style = style;
            }

            public Node Parent { get; }
            public ItemStyleValue Style { get; }
        }
    }

    public sealed class HiddenObjectsTests
    {
        [Test]
        public void Exclusion_RemovesMatchedObjectAndItsDescendants()
        {
            var snapshot = new[]
            {
                new HiddenObjectSnapshotItem(
                    1,
                    0,
                    10,
                    "Scene",
                    "Preview Root",
                    false,
                    0),
                new HiddenObjectSnapshotItem(
                    2,
                    1,
                    10,
                    "Scene",
                    "Hidden Child",
                    true,
                    1),
                new HiddenObjectSnapshotItem(
                    3,
                    0,
                    10,
                    "Scene",
                    "Keep",
                    true,
                    2)
            };
            var rules = new HiddenObjectExclusionRules(
                null,
                new[] { "Preview*" });

            var filtered = HiddenObjectExclusionPolicy.Apply(
                snapshot,
                rules);

            Assert.That(
                filtered.Select(item => item.InstanceId),
                Is.EqualTo(new[] { 3 }));
        }
    }
}
