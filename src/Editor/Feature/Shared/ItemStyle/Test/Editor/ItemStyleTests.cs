using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.ItemStyle.Tests
{
    public sealed class ItemStyleServiceTests
    {
        [Test]
        public void Update_DeduplicatesIdentityAndSeparatesScopes()
        {
            var repository = new MemoryItemStyleRepository();
            var project = new ItemStyleService("project", repository);
            var hierarchy = new ItemStyleService(
                "hierarchy",
                repository);

            project.SetColor(
                new[] { "same", "same", string.Empty },
                Color.red);

            Assert.That(repository.PutCount, Is.EqualTo(1));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(project.Get("same").Color, Is.EqualTo(Color.red));
            Assert.That(hierarchy.Get("same").IsEmpty, Is.True);
        }

        [Test]
        public void SetIcon_UsesBoundedRecentHistoryContract()
        {
            var repository = new MemoryItemStyleRepository();
            var service = new ItemStyleService("project", repository);

            service.SetIcon(new[] { "folder" }, "icon-guid");

            Assert.That(repository.RecordedScope, Is.EqualTo("project"));
            Assert.That(repository.RecordedIcon, Is.EqualTo("icon-guid"));
            Assert.That(repository.MaximumRecentCount, Is.EqualTo(8));
        }

        private sealed class MemoryItemStyleRepository
            : IItemStyleRepository
        {
            private readonly Dictionary<string, ItemStyleValue> _styles =
                new Dictionary<string, ItemStyleValue>(
                    StringComparer.Ordinal);

            public int PutCount { get; private set; }
            public int SaveCount { get; private set; }
            public string RecordedScope { get; private set; }
            public string RecordedIcon { get; private set; }
            public int MaximumRecentCount { get; private set; }

            public ItemStyleValue Get(string scope, string identity)
            {
                _styles.TryGetValue(Key(scope, identity), out var style);
                return style;
            }

            public void Put(string scope, ItemStyleValue style)
            {
                PutCount++;
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
                RecordedScope = scope;
                RecordedIcon = iconGuid;
                MaximumRecentCount = maximumCount;
            }

            public bool RemoveRecentIcon(
                string scope,
                string iconGuid)
            {
                return false;
            }

            public void Save()
            {
                SaveCount++;
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
