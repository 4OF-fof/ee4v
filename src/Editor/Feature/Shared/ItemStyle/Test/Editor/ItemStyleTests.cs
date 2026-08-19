using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.ItemStyle.Tests
{
    public sealed class ItemStyleServiceTests
    {
        [Test]
        public void Scopes_KeepProjectAndHierarchyValuesSeparate()
        {
            var repository = new MemoryItemStyleRepository();
            var project = new ItemStyleService("project", repository);
            var hierarchy = new ItemStyleService(
                "hierarchy",
                repository);

            project.SetColor(
                new[] { "same" },
                Color.red);

            Assert.That(project.Get("same").Color, Is.EqualTo(Color.red));
            Assert.That(hierarchy.Get("same").IsEmpty, Is.True);
        }

        private sealed class MemoryItemStyleRepository
            : IItemStyleRepository
        {
            private readonly Dictionary<string, ItemStyleValue> _styles =
                new Dictionary<string, ItemStyleValue>(
                    StringComparer.Ordinal);


            public ItemStyleValue Get(string scope, string identity)
            {
                _styles.TryGetValue(Key(scope, identity), out var style);
                return style;
            }

            public void Put(string scope, ItemStyleValue style)
            {
                _styles[Key(scope, style.Identity)] = style;
            }

            public IReadOnlyList<string> GetRecentIconGuids(
                string scope)
            {
                return Array.Empty<string>();
            }

            public void RecordRecentIcon(
                string scope,
                string iconGuid,
                int maximumCount)
            {
            }

            public bool RemoveRecentIcon(
                string scope,
                string iconGuid)
            {
                return false;
            }

            public void Save()
            {
            }

            private static string Key(string scope, string identity)
            {
                return scope + "|" + identity;
            }
        }
    }

    public sealed class ItemStyleInteractionTests
    {
        [Test]
        public void Selection_UsesGroupOnlyWhenHoveredFolderIsSelected()
        {
            var grouped = ItemStyleSelection.Resolve(
                "b",
                new[] { "a", "b", "b" });
            var isolated = ItemStyleSelection.Resolve(
                "c",
                new[] { "a", "b" });

            Assert.That(grouped, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(isolated, Is.EqualTo(new[] { "c" }));
        }
    }
}
