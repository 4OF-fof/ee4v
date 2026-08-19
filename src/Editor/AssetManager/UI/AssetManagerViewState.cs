using System;

namespace Ee4v.AssetManager.UI
{
    internal enum AssetManagerViewMode
    {
        Combined,
        Navigation,
        Main,
        Information
    }

    internal enum AssetManagerPage
    {
        Library,
        Archived,
        Collection,
        UnassignedFiles,
        Sources
    }

    internal enum AssetManagerViewStateChange
    {
        Navigation,
        ItemSelection,
        FileSelection,
        Information
    }

    internal enum AssetManagerInformationContent
    {
        Selection,
        NewItem,
        CollectionEditor
    }

    internal sealed class AssetManagerViewState
    {
        public AssetManagerPage Page { get; private set; }
        public string CollectionId { get; private set; }
        public string SelectedItemId { get; private set; }
        public string SelectedFileId { get; private set; }
        public AssetManagerInformationContent InformationContent
        {
            get;
            private set;
        }
        public string EditingCollectionId { get; private set; }

        public event Action<AssetManagerViewStateChange> Changed;

        public void SelectPage(AssetManagerPage page)
        {
            Page = page;
            CollectionId = null;
            SelectedItemId = null;
            SelectedFileId = null;
            InformationContent = AssetManagerInformationContent.Selection;
            EditingCollectionId = null;
            Changed?.Invoke(AssetManagerViewStateChange.Navigation);
        }

        public void SelectCollection(string collectionId)
        {
            Page = AssetManagerPage.Collection;
            CollectionId = collectionId;
            SelectedItemId = null;
            SelectedFileId = null;
            InformationContent = AssetManagerInformationContent.Selection;
            EditingCollectionId = null;
            Changed?.Invoke(AssetManagerViewStateChange.Navigation);
        }

        public void SelectItem(string itemId)
        {
            SelectedItemId = itemId;
            SelectedFileId = null;
            InformationContent = AssetManagerInformationContent.Selection;
            EditingCollectionId = null;
            Changed?.Invoke(AssetManagerViewStateChange.ItemSelection);
        }

        public void SelectFile(string fileId)
        {
            SelectedFileId = fileId;
            InformationContent = AssetManagerInformationContent.Selection;
            EditingCollectionId = null;
            Changed?.Invoke(AssetManagerViewStateChange.FileSelection);
        }

        public void ShowNewItem()
        {
            InformationContent = AssetManagerInformationContent.NewItem;
            EditingCollectionId = null;
            Changed?.Invoke(AssetManagerViewStateChange.Information);
        }

        public void ShowCollectionEditor(string collectionId)
        {
            InformationContent =
                AssetManagerInformationContent.CollectionEditor;
            EditingCollectionId = collectionId;
            Changed?.Invoke(AssetManagerViewStateChange.Information);
        }
    }
}
