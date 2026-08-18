using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.FolderContentOverlay.Tests
{
    public sealed class FolderContentOverlayTests
    {
        [Test]
        public void AssetPostprocessor_CollectsAncestorFolders()
        {
            var affectedFolders = new HashSet<string>();

            FolderContentOverlayAssetPostprocessor
                .CollectFolderAndAncestors(
                    "Assets/Avatar/Animation",
                    affectedFolders);

            Assert.That(
                affectedFolders,
                Is.EquivalentTo(new[]
                {
                    "Assets/Avatar/Animation",
                    "Assets/Avatar",
                    "Assets"
                }));
        }

        [Test]
        public void RepresentativeIcon_RequiresStrictMajorityToPropagate()
        {
            var primary = new Texture2D(1, 1)
            {
                name = "Primary"
            };
            var secondary = new Texture2D(1, 1)
            {
                name = "Secondary"
            };

            try
            {
                Assert.That(
                    FolderContentOverlayIconCache
                        .SummarizeIcons(new Texture[]
                        {
                            primary,
                            secondary
                        })
                        .PropagatedIcon,
                    Is.Null);
                Assert.That(
                    FolderContentOverlayIconCache
                        .SummarizeIcons(new Texture[]
                        {
                            primary,
                            primary,
                            secondary
                        })
                        .PropagatedIcon,
                    Is.EqualTo(primary));
            }
            finally
            {
                Object.DestroyImmediate(primary);
                Object.DestroyImmediate(secondary);
            }
        }

        [Test]
        public void RepresentativeIcon_ReturnsNullWhenMostCommonIsTied()
        {
            var primary = new Texture2D(1, 1)
            {
                name = "Primary"
            };
            var secondary = new Texture2D(1, 1)
            {
                name = "Secondary"
            };

            try
            {
                Assert.That(
                    FolderContentOverlayIconCache.SummarizeIcons(
                            new Texture[]
                            {
                                primary,
                                secondary
                            })
                        .DisplayIcon,
                    Is.Null);
                Assert.That(
                    FolderContentOverlayIconCache.SummarizeIcons(
                            new Texture[]
                            {
                                primary,
                                primary,
                                secondary
                            })
                        .DisplayIcon,
                    Is.EqualTo(primary));
            }
            finally
            {
                Object.DestroyImmediate(primary);
                Object.DestroyImmediate(secondary);
            }
        }
    }
}
