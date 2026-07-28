using System.Linq;
using System.Reflection;
using Ee4v.AssetManager.Contracts;
using Ee4v.UI;
using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI.Tests
{
    public sealed class NavigationTests
    {
        [Test]
        public void Catalog_ContainsRequestedNavigationItemsInOrder()
        {
            Assert.That(
                AssetManagerNavigationCatalog.Items.Select(item => item.Id).ToArray(),
                Is.EqualTo(new[]
                {
                    "all-assets",
                    "booth-items",
                    "uncategorized",
                    "tags"
                }));
        }

        [Test]
        public void Catalog_UsesPackageAndStoreIcons()
        {
            var items = AssetManagerNavigationCatalog.Items;

            Assert.That(
                items[0].IconState.BuiltinIcon,
                Is.EqualTo(UiBuiltinIcon.Package));
            Assert.That(
                items[1].IconState.BuiltinIcon,
                Is.EqualTo(UiBuiltinIcon.Store));
        }

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
        public void CollectionNavigationList_RendersRegularAndSmartTheSameWay()
        {
            var list = new CollectionNavigationList(null);

            list.SetState(new[]
            {
                new AssetCollection
                {
                    Id = "regular",
                    Name = "Regular",
                    Icon = AssetCollectionIcon.Folder
                },
                new AssetCollection
                {
                    Id = "smart",
                    Name = "Smart",
                    Icon = AssetCollectionIcon.Folder,
                    IsSmartCollection = true
                }
            }, string.Empty);

            var buttons = list.Query<Button>(
                    className:
                    "ee4v-asset-manager-collection-list__button")
                .ToList();
            Assert.That(buttons.Count, Is.EqualTo(2));
            Assert.That(
                buttons.All(button =>
                    button.GetClasses().Contains(
                        "ee4v-asset-manager-collection-list__button")),
                Is.True);
        }

        [Test]
        public void NavigationPanel_SeparatesRegularAndSmartCollections()
        {
            var panel = new NavigationPanel();
            panel.SetCollections(new[]
            {
                new AssetCollection
                {
                    Id = "regular",
                    Name = "Regular"
                },
                new AssetCollection
                {
                    Id = "smart",
                    Name = "Smart",
                    IsSmartCollection = true
                }
            }, string.Empty);

            var lists = panel.Query<CollectionNavigationList>()
                .ToList();
            Assert.That(lists.Count, Is.EqualTo(2));
            Assert.That(
                lists[0].Query<Button>(
                        className:
                        "ee4v-asset-manager-collection-list__button")
                    .ToList().Count,
                Is.EqualTo(1));
            Assert.That(
                lists[1].Query<Button>(
                        className:
                        "ee4v-asset-manager-collection-list__button")
                    .ToList().Count,
                Is.EqualTo(1));
        }

        [Test]
        public void NavigationPanel_CollectionSectionsAreIndependentFoldouts()
        {
            var panel = new NavigationPanel();
            var sections = panel.Query<Foldout>(
                    className:
                    "ee4v-asset-manager-panel__collection-section")
                .ToList();

            Assert.That(sections.Count, Is.EqualTo(2));
            Assert.That(sections.All(section => section.value), Is.True);

            sections[0].value = false;

            Assert.That(sections[0].value, Is.False);
            Assert.That(sections[1].value, Is.True);
        }

        [Test]
        public void NavigationPanel_CollectionContentUsesNavigationInset()
        {
            var panel = new NavigationPanel();

            Assert.That(
                panel.Query<VisualElement>(
                        className:
                        "ee4v-asset-manager-panel__collections-content")
                    .ToList().Count,
                Is.EqualTo(1));
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
        public void SmartCollectionPopup_BuildsBeforeNativePositionExists()
        {
            var window =
                ScriptableObject.CreateInstance<
                    CollectionCreationWindow>();
            try
            {
                var smartField = typeof(CollectionCreationWindow)
                    .GetField(
                        "_smart",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                Assert.That(smartField, Is.Not.Null);
                smartField.SetValue(window, true);
                var createGui = typeof(CollectionCreationWindow)
                    .GetMethod(
                        "CreateGUI",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

                Assert.That(createGui, Is.Not.Null);
                Assert.DoesNotThrow(() =>
                    createGui.Invoke(window, null));

                var textElements = window.rootVisualElement
                    .Query<UiTextElement>()
                    .ToList();
                Assert.That(textElements, Is.Not.Empty);
                Assert.That(
                    textElements.All(element =>
                        element.GetType().Name ==
                        "ImguiUiTextElement"),
                    Is.True);
                var scrollView = window.rootVisualElement
                    .Query<ScrollView>()
                    .ToList()
                    .Single();
                Assert.That(
                    scrollView.verticalScrollerVisibility,
                    Is.EqualTo(ScrollerVisibility.Auto));
                Assert.That(
                    scrollView.horizontalScrollerVisibility,
                    Is.EqualTo(ScrollerVisibility.Hidden));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void CollectionPopupHeight_UsesNaturalHeightUntilMaximum()
        {
            Assert.That(
                CollectionCreationWindow.CalculatePopupHeight(
                    200f,
                    26f),
                Is.EqualTo(230f));
            Assert.That(
                CollectionCreationWindow.CalculatePopupHeight(
                    800f,
                    26f),
                Is.EqualTo(676f));
        }

        [Test]
        public void CollectionPopupResize_PreservesAnchoredOrigin()
        {
            var current = new Rect(
                480f,
                320f,
                400f,
                520f);

            var resized =
                CollectionCreationWindow
                    .CalculateResizedPopupRect(
                        current,
                        new Vector2(400f, 260f));

            Assert.That(resized.position, Is.EqualTo(current.position));
            Assert.That(
                resized.size,
                Is.EqualTo(new Vector2(400f, 260f)));
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
        public void CollectionIconSelector_UsesInlineIconCandidates()
        {
            var selector = new AssetCollectionIconSelector(
                AssetCollectionIcon.Search);

            Assert.That(
                selector.Query<Button>(
                        className:
                        "ee4v-collection-icon-selector__candidate")
                    .ToList().Count,
                Is.EqualTo(5));
            Assert.That(
                selector.Query<Button>(
                        className:
                        "ee4v-collection-icon-selector__candidate--selected")
                    .ToList().Count,
                Is.EqualTo(1));
            Assert.That(
                selector.Value,
                Is.EqualTo(AssetCollectionIcon.Search));
            Assert.That(
                selector.Query<ObjectField>().ToList().Count,
                Is.EqualTo(1));
        }

        [Test]
        public void SmartCollectionConditions_ExcludeSourceAndLifecycle()
        {
            Assert.That(
                System.Enum.GetNames(
                    typeof(SmartCollectionConditionField)),
                Is.EqualTo(new[]
                {
                    "Name",
                    "Description",
                    "Tag",
                    "FileName",
                    "Extension"
                }));
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

            Assert.That(page.Tags.Select(tag => tag.Name).ToArray(), Is.EqualTo(new[] { "Avatar", "Shader" }));
            Assert.That(
                page.Query<UiTextElement>().ToList().Any(text => text.Text == "Avatar"),
                Is.True);
            Assert.That(
                page.Query<UiTextElement>().ToList().Any(text => text.Text == "Shader"),
                Is.True);
        }
    }
}
