using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI.Tests
{
    public sealed class NavigationTests
    {
        [Test]
        public void CreateQuery_UsesBoothInformationInsteadOfSourceType()
        {
            var query = MainViewController.CreateQuery(
                new MainViewRequest("booth-items"));

            Assert.That(query.HasBoothInformation, Is.True);
            Assert.That(query.SourceTypes, Is.Null);
        }

        [Test]
        public void CreateQuery_RequestsOnlyUncategorizedItems()
        {
            var query = MainViewController.CreateQuery(
                new MainViewRequest("uncategorized"));

            Assert.That(query.UncategorizedOnly, Is.True);
        }

        [Test]
        public void CreateQuery_UsesCollectionIdForCollectionView()
        {
            var query = MainViewController.CreateQuery(
                new MainViewRequest(
                    AssetManagerCollectionViewId.Encode("collection-1")));

            Assert.That(query.CollectionId, Is.EqualTo("collection-1"));
        }

        [Test]
        public void PendingCollectionNavigation_IsPreservedUntilCollectionsLoad()
        {
            var viewId =
                AssetManagerCollectionViewId.Encode(
                    "new-smart-collection");
            var pendingItem =
                MainViewController
                    .CreateCollectionNavigationItem(
                        viewId,
                        "new-smart-collection");

            Assert.That(
                MainViewController.ResolveNavigationItemId(
                    viewId,
                    collectionExists: false),
                Is.EqualTo(viewId));
            Assert.That(pendingItem.Id, Is.EqualTo(viewId));
            Assert.That(
                MainViewController.CreateQuery(
                        new MainViewRequest(pendingItem.Id))
                    .CollectionId,
                Is.EqualTo("new-smart-collection"));
            Assert.That(
                MainViewController.ResolveNavigationItemId(
                    "missing-fixed-item",
                    collectionExists: false),
                Is.EqualTo(
                    AssetManagerNavigationCatalog.DefaultItemId));
        }

        [Test]
        public void SmartCollectionRuleChange_InvalidatesAffectedViews()
        {
            var change = new AssetManagerChange(
                AssetManagerChangeKind.SmartCollectionRule,
                "smart");

            Assert.That(
                MainViewController
                    .ShouldInvalidateForSmartCollectionRule(
                        change,
                        AssetManagerCollectionViewId.Encode(
                            "smart")),
                Is.True);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForSmartCollectionRule(
                        change,
                        "uncategorized"),
                Is.True);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForSmartCollectionRule(
                        change,
                        AssetManagerCollectionViewId.Encode(
                            "other")),
                Is.False);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForSmartCollectionRule(
                        change,
                        "all-assets"),
                Is.False);
        }

        [Test]
        public void ItemCollectionChange_InvalidatesOnlyAffectedViews()
        {
            var change = new AssetManagerChange(
                AssetManagerChangeKind.ItemCollections,
                relatedId: "target");

            Assert.That(
                MainViewController
                    .ShouldInvalidateForItemCollections(
                        change,
                        AssetManagerCollectionViewId.Encode(
                            "target")),
                Is.True);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForItemCollections(
                        change,
                        "uncategorized"),
                Is.True);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForItemCollections(
                        change,
                        "all-assets"),
                Is.False);
            Assert.That(
                MainViewController
                    .ShouldInvalidateForItemCollections(
                        change,
                        AssetManagerCollectionViewId.Encode(
                            "source")),
                Is.False);
        }

        [Test]
        public void GetChildCollections_ReturnsDirectChildrenInDisplayOrder()
        {
            var children =
                MainViewController.GetChildCollections(
                    new[]
                    {
                        new AssetCollection
                        {
                            Id = "second",
                            Name = "Second",
                            ParentCollectionId = "parent",
                            SortOrder = 1
                        },
                        new AssetCollection
                        {
                            Id = "grandchild",
                            Name = "Grandchild",
                            ParentCollectionId = "second",
                            SortOrder = 0
                        },
                        new AssetCollection
                        {
                            Id = "first",
                            Name = "First",
                            ParentCollectionId = "parent",
                            SortOrder = 0
                        },
                        new AssetCollection
                        {
                            Id = "other",
                            Name = "Other",
                            ParentCollectionId = "other-parent",
                            SortOrder = 0
                        }
                    },
                    "parent",
                    "s");

            Assert.That(
                children.Select(collection => collection.Id),
                Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void CreateHistoryViewPath_ReturnsCollectionAncestors()
        {
            var path =
                MainViewController.CreateHistoryViewPath(
                    new[]
                    {
                        new AssetCollection
                        {
                            Id = "hoge",
                            Name = "hoge"
                        },
                        new AssetCollection
                        {
                            Id = "fuga",
                            Name = "fuga",
                            ParentCollectionId = "hoge"
                        },
                        new AssetCollection
                        {
                            Id = "child",
                            Name = "child",
                            ParentCollectionId = "fuga"
                        }
                    },
                    AssetManagerCollectionViewId.Encode("child"),
                    "child");

            Assert.That(
                path.Select(item => item.Id),
                Is.EqualTo(new[]
                {
                    AssetManagerCollectionViewId.Encode("hoge"),
                    AssetManagerCollectionViewId.Encode("fuga"),
                    AssetManagerCollectionViewId.Encode("child")
                }));
            Assert.That(
                path.Select(item => item.Label),
                Is.EqualTo(new[] { "hoge", "fuga", "child" }));
        }

        [Test]
        public void NavigationTypography_UsesImguiFontCacheWorkaround()
        {
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.SectionTitle).Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.NavigationItemLabel).Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.CollectionNavigationLabel)
                    .Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.ButtonLabel).Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.ButtonMeta).Style.RequiresImgui,
                Is.True);

            var panel = new NavigationPanel();
            panel.SetCollections(new[]
            {
                new AssetCollection
                {
                    Id = "collection",
                    Name = "Collection"
                }
            }, string.Empty);
            var textElements = panel.Query<UiTextElement>().ToList();

            Assert.That(textElements, Is.Not.Empty);
            Assert.That(
                textElements.All(element =>
                    element.GetType().Name == "ImguiUiTextElement"),
                Is.True);
            Assert.That(
                panel.Query<Button>().ToList()
                    .All(button => button is UiButton),
                Is.True);
        }

        [Test]
        public void FormTypography_UsesImguiFontCacheWorkaround()
        {
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.InputPlaceholder).Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.FormLabel).Style.RequiresImgui,
                Is.True);
            Assert.That(
                TypographyStyleResolver.Resolve(
                    UiClassNames.FormError).Style.RequiresImgui,
                Is.True);
        }

        [Test]
        public void StandaloneSession_SharesNavigationSelection()
        {
            var session = new StandaloneAssetManagerViewSession();
            string changedItemId = null;
            session.NavigationChanged +=
                itemId => changedItemId = itemId;

            session.SetNavigation("tags");

            Assert.That(
                session.SelectedNavigationItemId,
                Is.EqualTo("tags"));
            Assert.That(changedItemId, Is.EqualTo("tags"));
        }

        [Test]
        public void TagListPage_RendersTagsWithUiTextElements()
        {
            var page = new TagListPage();

            page.SetTags(new[]
            {
                new AssetTag { Id = "tag-1", Name = "Avatar" },
                new AssetTag { Id = "tag-2", Name = "Shader" }
            });

            Assert.That(
                page.Tags.Select(tag => tag.Name).ToArray(),
                Is.EqualTo(new[] { "Avatar", "Shader" }));
            Assert.That(
                page.Query<UiTextElement>()
                    .ToList()
                    .Any(text => text.Text == "Avatar"),
                Is.True);
            Assert.That(
                page.Query<UiTextElement>()
                    .ToList()
                    .Any(text => text.Text == "Shader"),
                Is.True);
        }
    }
}
