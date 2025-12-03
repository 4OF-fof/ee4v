using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Presenter;
using _4OF.ee4v.AssetManager.Services;
using _4OF.ee4v.AssetManager.Views.Components.AssetInfo;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views {
    public class AssetInfo : VisualElement {
        private readonly Actions _actions;
        private readonly Dependencies _dependencies;
        private readonly Identity _identity;
        private readonly VisualElement _multiSelectionContainer;
        private readonly Label _multiSelectionLabel;
        private readonly VisualElement _singleSelectionContainer;
        private readonly Stats _stats;
        private readonly Tags _tags;
        private readonly Thumbnail _thumbnail;

        private AssetInfoPresenter _presenter;

        public AssetInfo() {
            style.flexDirection = FlexDirection.Column;
            style.backgroundColor = ColorPreset.DefaultBackground;

            var scrollView = new ScrollView { style = { flexGrow = 1 } };
            scrollView.contentContainer.style.paddingLeft = 10;
            scrollView.contentContainer.style.paddingRight = 10;
            scrollView.contentContainer.style.paddingTop = 10;
            scrollView.contentContainer.style.paddingBottom = 10;
            Add(scrollView);

            _singleSelectionContainer = new VisualElement();
            scrollView.Add(_singleSelectionContainer);

            _thumbnail = new Thumbnail();
            _singleSelectionContainer.Add(_thumbnail);

            _identity = new Identity();
            _singleSelectionContainer.Add(_identity);

            _tags = new Tags();
            _singleSelectionContainer.Add(_tags);

            _dependencies = new Dependencies();
            _singleSelectionContainer.Add(_dependencies);

            _actions = new Actions();
            _singleSelectionContainer.Add(_actions);

            _multiSelectionContainer = new VisualElement {
                style = {
                    display = DisplayStyle.None, alignItems = Align.Center, marginTop = 20, marginBottom = 40
                }
            };
            _multiSelectionLabel = new Label {
                style = {
                    fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            _multiSelectionContainer.Add(_multiSelectionLabel);
            scrollView.Add(_multiSelectionContainer);

            _stats = new Stats();
            scrollView.Add(_stats);

            BindComponentEvents();
        }

        public event Action<string> OnNameChanged;
        public event Action<string> OnDescriptionChanged;
        public event Action<string> OnTagAdded;
        public event Action<string> OnTagRemoved;
        public event Action<string> OnTagClicked;
        public event Action<Ulid> OnDependencyAdded;
        public event Action<Ulid> OnDependencyRemoved;
        public event Action<Ulid> OnDependencyClicked;
        public event Action<Ulid> OnFolderClicked;
        public event Action<string> OnDownloadRequested;

        private void BindComponentEvents() {
            _identity.OnNameChanged += n => OnNameChanged?.Invoke(n);
            _identity.OnDescriptionChanged += d => OnDescriptionChanged?.Invoke(d);
            _identity.OnFolderClicked += id => OnFolderClicked?.Invoke(id);

            _tags.OnTagAdded += t => OnTagAdded?.Invoke(t);
            _tags.OnTagRemoved += t => OnTagRemoved?.Invoke(t);
            _tags.OnTagClicked += t => OnTagClicked?.Invoke(t);

            _dependencies.OnAddRequested += () => OnDependencyAdded?.Invoke(Ulid.Empty);
            _dependencies.OnRemoveRequested += id => OnDependencyRemoved?.Invoke(id);
            _dependencies.OnClicked += id => OnDependencyClicked?.Invoke(id);

            _actions.OnDownloadRequested += url => OnDownloadRequested?.Invoke(url);
        }

        public void Initialize(IAssetRepository repository, TextureService textureService) {
            _tags.SetRepository(repository);

            _presenter = new AssetInfoPresenter(repository, textureService);
            _presenter.AssetDataUpdated += OnAssetDataUpdated;
            _presenter.FolderDataUpdated += OnFolderDataUpdated;
            _presenter.LibraryDataUpdated += OnLibraryDataUpdated;
            _presenter.MultiSelectionUpdated += OnMultiSelectionUpdated;

            RegisterCallback<DetachFromPanelEvent>(_ => _presenter?.Dispose());
            UpdateSelection(null);
        }

        public void UpdateSelection(IReadOnlyList<object> selectedItems) {
            _presenter?.UpdateSelection(selectedItems);
        }

        private void SwitchToSingleView() {
            _singleSelectionContainer.style.display = DisplayStyle.Flex;
            _multiSelectionContainer.style.display = DisplayStyle.None;
            _stats.style.display = DisplayStyle.Flex;
        }

        private void SwitchToMultiView(string message) {
            _singleSelectionContainer.style.display = DisplayStyle.None;
            _multiSelectionContainer.style.display = DisplayStyle.Flex;
            _multiSelectionLabel.text = message;
            _stats.style.display = DisplayStyle.None;
            _stats.Clear();
        }

        private void OnAssetDataUpdated(AssetDisplayData data) {
            SwitchToSingleView();

            _presenter.LoadThumbnail(data.Id, false, tex => _thumbnail.SetImage(tex, false));

            _identity.SetData(data.Name, data.Description, data.FolderId, data.FolderName);
            _tags.SetTags(data.Tags);

            _dependencies.SetVisible(true);
            _dependencies.SetDependencies(data.Dependencies);

            _actions.SetDownloadUrl(data.DownloadUrl, data.HasPhysicalFile);

            _stats.Clear();
            _stats.SetRow("Size", FormatSize(data.Size));
            if (!string.IsNullOrEmpty(data.Extension)) _stats.SetRow("Type", data.Extension);
            _stats.SetRow("Modified", data.ModificationTime.ToString("yyyy/MM/dd HH:mm"));
            if (!string.IsNullOrEmpty(data.ShopName)) _stats.SetLinkRow("Shop", data.ShopName, data.ShopUrl);
            if (!string.IsNullOrEmpty(data.ItemId)) _stats.SetLinkRow("Item", data.ItemId, data.ItemUrl);
        }

        private void OnFolderDataUpdated(FolderDisplayData data) {
            SwitchToSingleView();

            _presenter.LoadThumbnail(data.Id, true, tex =>
                _thumbnail.SetImage(tex, true, data.SubFolderCount == 0 && data.AssetCount == 0));

            _identity.SetData(data.Name, data.Description, data.ParentFolderId, data.ParentFolderName);
            _tags.SetTags(data.Tags);

            _dependencies.SetVisible(false);
            _actions.SetDownloadUrl(null, true);

            _stats.Clear();
            if (data.IsFolder) _stats.SetRow("SubFolders", data.SubFolderCount.ToString());
            _stats.SetRow("Assets", data.AssetCount.ToString());
            _stats.SetRow("Modified", data.ModificationTime.ToString("yyyy/MM/dd HH:mm"));
            if (!string.IsNullOrEmpty(data.ShopName)) _stats.SetLinkRow("Shop", data.ShopName, data.ShopUrl);
            if (!string.IsNullOrEmpty(data.ItemId)) _stats.SetLinkRow("Item", data.ItemId, data.ItemUrl);
        }

        private void OnLibraryDataUpdated(LibraryDisplayData data) {
            SwitchToMultiView(I18N.Get("UI.AssetManager.AssetInfo.LibraryOverview"));

            _stats.style.display = DisplayStyle.Flex;
            _stats.Clear();
            _stats.SetRow("TotalAssets", data.TotalAssets.ToString());
            _stats.SetRow("TotalSize", FormatSize(data.TotalSize));
            _stats.SetRow("TotalTags", data.TotalTags.ToString());
        }

        private void OnMultiSelectionUpdated(int count) {
            SwitchToMultiView(I18N.Get("UI.AssetManager.AssetInfo.SelectedItems", count));
        }

        private static string FormatSize(long bytes) {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            var counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1) {
                number /= 1024;
                counter++;
            }

            return $"{number:n2} {suffixes[counter]}";
        }
    }
}