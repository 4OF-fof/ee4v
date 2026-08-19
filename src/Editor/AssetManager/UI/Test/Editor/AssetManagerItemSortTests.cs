using System;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using NUnit.Framework;

namespace Ee4v.AssetManager.UI.Tests
{
    internal sealed class AssetManagerItemSortTests
    {
        [TestCase(
            AssetManagerItemSortField.Name,
            false,
            "alpha,beta,gamma")]
        [TestCase(
            AssetManagerItemSortField.Name,
            true,
            "gamma,beta,alpha")]
        [TestCase(
            AssetManagerItemSortField.CreatedAt,
            false,
            "beta,gamma,alpha")]
        [TestCase(
            AssetManagerItemSortField.CreatedAt,
            true,
            "alpha,gamma,beta")]
        [TestCase(
            AssetManagerItemSortField.UpdatedAt,
            false,
            "alpha,gamma,beta")]
        [TestCase(
            AssetManagerItemSortField.FileCount,
            false,
            "beta,gamma,alpha")]
        public void Apply_OrdersBySelectedField(
            AssetManagerItemSortField field,
            bool reverse,
            string expectedIds)
        {
            var items = new[]
            {
                CreateItem(
                    "alpha",
                    new DateTime(2026, 1, 3),
                    new DateTime(2026, 1, 1),
                    2),
                CreateItem(
                    "beta",
                    new DateTime(2026, 1, 1),
                    new DateTime(2026, 1, 3),
                    0),
                CreateItem(
                    "gamma",
                    new DateTime(2026, 1, 2),
                    new DateTime(2026, 1, 2),
                    1)
            };

            var sortedIds = AssetManagerItemSort.Apply(items, field, reverse)
                .Select(item => item.Id);

            Assert.That(sortedIds, Is.EqualTo(expectedIds.Split(',')));
        }

        [TestCase(
            AssetManagerItemSortField.Name,
            false,
            "alpha,beta,gamma")]
        [TestCase(
            AssetManagerItemSortField.CreatedAt,
            false,
            "beta,gamma,alpha")]
        [TestCase(
            AssetManagerItemSortField.UpdatedAt,
            true,
            "beta,gamma,alpha")]
        public void Apply_OrdersFilesByAvailableField(
            AssetManagerItemSortField field,
            bool reverse,
            string expectedIds)
        {
            var files = new[]
            {
                CreateFile(
                    "alpha",
                    new DateTime(2026, 1, 3),
                    new DateTime(2026, 1, 1)),
                CreateFile(
                    "beta",
                    new DateTime(2026, 1, 1),
                    new DateTime(2026, 1, 3)),
                CreateFile(
                    "gamma",
                    new DateTime(2026, 1, 2),
                    new DateTime(2026, 1, 2))
            };

            var sortedIds = AssetManagerItemSort.Apply(
                    files,
                    field,
                    reverse)
                .Select(file => file.Id);

            Assert.That(sortedIds, Is.EqualTo(expectedIds.Split(',')));
        }

        private static AssetItem CreateItem(
            string id,
            DateTime createdAt,
            DateTime updatedAt,
            int fileCount)
        {
            return new AssetItem
            {
                Id = id,
                Name = id,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                Files = Enumerable.Range(0, fileCount)
                    .Select(index => new AssetFile
                    {
                        Id = id + "-file-" + index
                    })
                    .ToArray()
            };
        }

        private static AssetFile CreateFile(
            string id,
            DateTime createdAt,
            DateTime updatedAt)
        {
            return new AssetFile
            {
                Id = id,
                FileName = id,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };
        }
    }
}
