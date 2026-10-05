using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.Core.Images;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetManagerView
    {
        private sealed class VariantPageState
        {
            internal string VariantDetailVariantId;
            internal string VariantDetailCurrentRevisionId;
            internal string SelectedVariantRevisionId;
            internal string SelectedVariantGalleryImageId;
            internal VariantGalleryView VariantGalleryView;
            internal string VariantGalleryOperationId;
            internal VisualElement HoveredVariantRevisionRow;
            internal Vector2 VariantRevisionPointerPosition;
            internal CancellationTokenSource VariantRevisionHoverCancellation;
            internal FileTreeImageTooltipWindow VariantRevisionTooltip;
            internal bool VariantBusy;
            internal Task VariantMetadataSaveTask = Task.CompletedTask;
            internal int VariantMetadataSaveCount;
            internal CancellationTokenSource VariantPreviewCancellation;
            internal readonly Dictionary<string, Task<AssetThumbnail>> VariantPreviewLoads =
            new Dictionary<string, Task<AssetThumbnail>>(StringComparer.Ordinal);
            internal readonly Dictionary<string, AssetVariantRevisionDetails> VariantRevisionDetails =
            new Dictionary<string, AssetVariantRevisionDetails>(StringComparer.Ordinal);
        }

        private void ShowVariantDetail()
        {
            _detail.Clear();
            var variant = GetVariants().FirstOrDefault(candidate =>
                candidate.VariantId == (_viewState.DetailVariantId ?? _viewState.SelectedVariantId));
            if (variant == null)
            {
                ShowEmptyDetail(I18N.Get("notice.selectVariant"));
                return;
            }
            if (_viewState.SelectedVariantIds.Count > 1)
            {
                _detail.Add(UiTextFactory.Create(
                    I18N.Get("variant.selectedCount", _viewState.SelectedVariantIds.Count),
                    UiClassNames.SelectionCount));
                _detail.Add(new InfoCard(new InfoCardState(variant.Name, variant.Description)));
            }
            else { AddVariantMetadataEditor(_detail, variant); }
        }

        private void AddVariantMetadataEditor(VisualElement detail, DerivedAssetInfo variant)
        {
            var latest = _variantManager?.GetRevisions(variant.VariantId)
                .OrderByDescending(revision => revision.Number).FirstOrDefault();
            var thumbnail = CreateVariantRevisionPreview(variant.VariantId, latest?.Id);
            thumbnail.AddToClassList("ee4v-asset-manager__variant-information-thumbnail");
            thumbnail.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var size = evt.newRect.width;
                if (float.IsNaN(size) || size <= 0f) { return; }
                thumbnail.style.height = size;
                thumbnail.style.minHeight = size;
                thumbnail.style.maxHeight = size;
            });
            detail.Add(thumbnail);
            var name = AssetManagerControls.CreateTextField(I18N.Get("field.name"),
                "ee4v-asset-manager__item-metadata-field", "ee4v-asset-manager__variant-name-field");
            name.value = variant.Name;
            var descriptionContainer = new VisualElement();
            descriptionContainer.AddToClassList("ee4v-asset-manager-control-field");
            descriptionContainer.AddToClassList("ee4v-asset-manager__item-metadata-field");
            descriptionContainer.Add(UiTextFactory.Create(I18N.Get("field.description"),
                UiClassNames.FormLabel, "ee4v-asset-manager-control-field__label"));
            var description = new InputField(new InputFieldState(variant.Description,
                multiline: true, maxHeight: 144f));
            description.AddToClassList("ee4v-asset-manager__description-field");
            description.AddToClassList("ee4v-asset-manager__variant-description-field");
            descriptionContainer.Add(description);
            name.isReadOnly = description.IsReadOnly = _variantManager == null || _variantState.VariantBusy;
            var metadataError = new VisualElement();
            Action saveMetadata = () =>
            {
                var normalizedName = name.value.Trim();
                if (normalizedName.Length == 0)
                {
                    ((InputField)name.Input).SetValueWithoutNotify(variant.Name);
                    return;
                }
                ((InputField)name.Input).SetValueWithoutNotify(normalizedName);
                SaveVariantMetadataAutomatically(variant, normalizedName, description.Value, metadataError);
            };
            name.RegisterCallback<FocusOutEvent>(_ => saveMetadata());
            description.RegisterCallback<FocusOutEvent>(_ => saveMetadata());
            detail.Add(name);
            detail.Add(descriptionContainer);
            detail.Add(metadataError);
        }

        private void BuildVariantDetailPage()
        {
            CancelGridThumbnails();
            CancelItemOverviewThumbnail();
            ClearItemOverviewThumbnail();
            _itemDetailPane = null;
            _fileTreeSelections = Array.Empty<FileTreeSelection>();
            _content.Clear();
            _search.style.display = DisplayStyle.None;
            _sortButton.style.display = DisplayStyle.None;
            _gridControls.style.display = DisplayStyle.None;
            var variant = GetVariants().FirstOrDefault(candidate =>
                candidate.VariantId == _viewState.DetailVariantId);
            if (variant == null)
            {
                _content.Add(AssetManagerControls.CreateNotice(I18N.Get("variant.missing")));
                return;
            }

            var detail = new VisualElement();
            detail.AddToClassList("ee4v-asset-manager__variant-detail-page");
            _content.Add(detail);
            var revisions = _variantManager?.GetRevisions(variant.VariantId) ??
                Array.Empty<AssetVariantRevision>();
            var latest = revisions.OrderByDescending(revision => revision.Number).FirstOrDefault();
            AssetVariantRevisionDetails savedDetails = null;
            IReadOnlyList<AssetVariantFileDependency> dependencies = null;
            string dependencyError = null;
            try
            {
                if (latest != null)
                {
                    if (!_variantState.VariantRevisionDetails.TryGetValue(latest.Id, out savedDetails))
                    {
                        savedDetails = _variantManager.GetRevisionDetails(variant.VariantId, latest.Id);
                        _variantState.VariantRevisionDetails.Add(latest.Id, savedDetails);
                    }
                    dependencies = savedDetails.Dependencies;
                }
                else
                {
                    dependencies = GetCurrentVariantDependencies(variant);
                }
            }
            catch (Exception exception)
            {
                dependencyError = exception.Message;
            }

            var currentRevisionId = variant.Prefab != null
                ? _variantManager?.GetCurrentRevisionId(variant.VariantId) : null;
            if (_variantState.VariantDetailVariantId != variant.VariantId || _variantState.VariantDetailCurrentRevisionId != currentRevisionId)
            {
                if (_variantState.VariantDetailVariantId != variant.VariantId) { _variantState.SelectedVariantGalleryImageId = null; }
                _variantState.VariantDetailVariantId = variant.VariantId;
                _variantState.VariantDetailCurrentRevisionId = currentRevisionId;
                _variantState.SelectedVariantRevisionId = null;
            }
            var selectedRevision = revisions.FirstOrDefault(revision => revision.Id == _variantState.SelectedVariantRevisionId)
                ?? revisions.FirstOrDefault(revision => revision.Id == currentRevisionId) ?? latest;
            _variantState.SelectedVariantRevisionId = selectedRevision?.Id;
            var layout = new VisualElement();
            layout.AddToClassList("ee4v-asset-manager__variant-detail-layout");
            var overview = new VisualElement();
            overview.AddToClassList("ee4v-asset-manager__variant-overview");
            var management = new VisualElement();
            management.AddToClassList("ee4v-asset-manager__variant-management");
            layout.Add(overview);
            layout.Add(management);
            detail.Add(layout);
            var selectRevision = AddVariantOverview(overview, variant, selectedRevision, latest, currentRevisionId, out var action);
            AddVariantHistory(management, variant, revisions, selectRevision, action);
            AddVariantDependencies(management, dependencies, dependencyError);
            var history = management.Q<ScrollView>(className: "ee4v-asset-manager__variant-history-scroll");
            var sources = management.Q<ScrollView>(className: "ee4v-asset-manager__variant-dependency-scroll");
            var historyHeader = history.parent.Q<SectionHeader>();
            var sourcesHeader = sources.parent.Q<SectionHeader>();
            Func<ScrollView, SectionHeader, float> sectionSpacing = (scroll, header) =>
            {
                var style = scroll.parent.resolvedStyle;
                return header.layout.height + header.resolvedStyle.marginTop + header.resolvedStyle.marginBottom +
                    style.marginTop + style.marginBottom + style.paddingTop + style.paddingBottom +
                    style.borderTopWidth + style.borderBottomWidth;
            };
            Action resizeSections = () =>
            {
                var available = layout.layout.height;
                var contentHeight = history.contentContainer.layout.height;
                if (available <= 0f || float.IsNaN(available) || float.IsNaN(contentHeight)) { return; }
                var historySpacing = sectionSpacing(history, historyHeader);
                var historyHeight = Mathf.Min(contentHeight, Mathf.Max(0f, available * .5f - historySpacing));
                history.style.height = historyHeight;
                sources.style.height = Mathf.Min(320f, Mathf.Max(0f,
                    available - historySpacing - historyHeight - sectionSpacing(sources, sourcesHeader)));
            };
            layout.RegisterCallback<GeometryChangedEvent>(_ => resizeSections());
            history.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => resizeSections());
            historyHeader.RegisterCallback<GeometryChangedEvent>(_ => resizeSections());
            sourcesHeader.RegisterCallback<GeometryChangedEvent>(_ => resizeSections());
        }

        private Action<AssetVariantRevision> AddVariantOverview(VisualElement detail, DerivedAssetInfo variant,
            AssetVariantRevision selectedRevision, AssetVariantRevision latestRevision, string currentRevisionId, out UiButton action)
        {
            var title = UiTextFactory.Create(variant.Name, UiClassNames.InfoCardTitle,
                "ee4v-asset-manager__variant-overview-title");
            title.SetFontSize(UiTypographyTokens.TitleFontSize);
            title.SetWhiteSpace(WhiteSpace.Normal);
            detail.Add(title);
            var descriptionSlot = new VisualElement();
            descriptionSlot.AddToClassList("ee4v-asset-manager__variant-overview-description-slot");
            descriptionSlot.style.display = string.IsNullOrWhiteSpace(variant.Description)
                ? DisplayStyle.None : DisplayStyle.Flex;
            var description = UiTextFactory.Create(variant.Description, UiClassNames.InfoCardDescription,
                "ee4v-asset-manager__variant-overview-description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            description.tooltip = variant.Description;
            descriptionSlot.Add(description);
            detail.Add(descriptionSlot);
            AddVariantGallery(detail, variant, latestRevision);
            var isImported = variant.Prefab != null;
            Func<bool> canModify = () => isImported && _modifyVariant != null &&
                (selectedRevision == null || selectedRevision.Id == currentRevisionId);
            var operation = AssetManagerControls.CreateButton(string.Empty, async () =>
            {
                var revision = selectedRevision;
                var modify = canModify();
                await PendingVariantMetadataSave;
                if (_disposed) { return; }
                if (modify) { ModifyVariant(variant.VariantId); }
                else if (revision != null) { RestoreVariant(variant.VariantId, revision.Id, confirm: isImported); }
            }, "ee4v-asset-manager__variant-overview-action");
            Action refreshAction = () =>
            {
                var modify = canModify();
                operation.SetLabel(I18N.Get(modify ? "variant.modify" : isImported ? "variant.restore" : "action.import"));
                operation.SetLabelColor(modify || !isImported ? UiColorTokens.TextOnState :
                    _variantState.VariantBusy ? UiColorTokens.TextPrimary : UiColorTokens.StatusRunningText);
                operation.EnableInClassList("ee4v-asset-manager__primary-action", modify || !isImported);
                operation.EnableInClassList("ee4v-asset-manager__variant-modify", modify);
                operation.EnableInClassList("ee4v-asset-manager__variant-import", !isImported);
                operation.EnableInClassList("ee4v-asset-manager__variant-restore", isImported && !modify);
                operation.style.display = modify || selectedRevision != null ? DisplayStyle.Flex : DisplayStyle.None;
                operation.SetEnabled(!_variantState.VariantBusy && (modify || selectedRevision != null));
            };
            refreshAction();
            if (_variantState.VariantGalleryView != null) { _variantState.VariantGalleryView.RefreshControls += refreshAction; }
            action = operation;
            return revision =>
            {
                if (_variantState.VariantBusy) { return; }
                selectedRevision = revision;
                _variantState.SelectedVariantRevisionId = revision.Id;
                refreshAction();
            };
        }

        private sealed class VariantGalleryEntry
        {
            internal string Id;
            internal string Key;
            internal string Label;
            internal Func<Task<AssetThumbnail>> Load;
        }

        private sealed class VariantGalleryView
        {
            internal string VariantId;
            internal Action RefreshControls;
            internal Func<IReadOnlyList<AssetVariantGalleryUpload>, Action> AddImages;
            internal Func<string, Action> RemoveImage;
            internal Func<string, Action> MoveToFront;
        }

        private void AddVariantGallery(VisualElement detail, DerivedAssetInfo variant,
            AssetVariantRevision latestRevision)
        {
            var gallery = new VisualElement();
            gallery.AddToClassList("ee4v-asset-manager__variant-gallery");
            detail.Add(gallery);
            var preview = CreateVariantPreview(null, null);
            preview.AddToClassList("ee4v-asset-manager__variant-gallery-preview");
            var stage = new VisualElement();
            stage.AddToClassList("ee4v-asset-manager__variant-gallery-stage");
            var images = new VisualElement();
            images.AddToClassList("ee4v-asset-manager__variant-gallery-images");
            images.Add(preview);
            stage.Add(images);
            gallery.Add(stage);
            var entries = new List<VariantGalleryEntry>();
            Func<AssetVariantRevision, VariantGalleryEntry> automatic = revision => new VariantGalleryEntry
            {
                Key = "variant-revision:" + revision.Id,
                Label = I18N.Get("variant.automaticImage"),
                Load = () => _variantManager.GetRevisionThumbnail(variant.VariantId, revision.Id)
            };
            try
            {
                foreach (var image in _variantManager?.GetGalleryImages(variant.VariantId) ??
                         Array.Empty<AssetVariantGalleryImage>())
                {
                    entries.Add(new VariantGalleryEntry
                    {
                        Id = image.Id, Key = "variant-gallery:" + image.Id, Label = image.FileName,
                        Load = () => _variantManager.GetGalleryImage(variant.VariantId, image.Id)
                    });
                }
            }
            catch (Exception exception) { gallery.Add(UiTextFactory.CreateHelpBox(exception.Message, HelpBoxMessageType.Error)); }
            if (latestRevision != null) { entries.Add(automatic(latestRevision)); }
            var controls = new VisualElement { pickingMode = PickingMode.Ignore };
            controls.AddToClassList("ee4v-asset-manager__variant-gallery-controls");
            Func<VariantGalleryEntry, string> entryId = entry => entry.Id ?? "automatic";
            var index = Math.Max(0, entries.FindIndex(entry => entryId(entry) == _variantState.SelectedVariantGalleryImageId));
            Action render = null;
            Action<int> select = next =>
            {
                if (entries.Count == 0) { return; }
                index = (next + entries.Count) % entries.Count;
                _variantState.SelectedVariantGalleryImageId = entryId(entries[index]);
                render();
            };
            RegisterVariantGalleryImageContextMenu(preview, variant.VariantId,
                () => entries.Count > 0 ? entries[index] : null);
            Action resize = () =>
            {
                var management = detail.parent.Q<VisualElement>(className: "ee4v-asset-manager__variant-management");
                var availableWidth = (detail.parent.layout.width - management.resolvedStyle.marginLeft) * .5f;
                var size = Mathf.Min(500f, availableWidth, stage.layout.height);
                if (size <= 0 || float.IsNaN(size)) { return; }
                detail.style.width = size;
                images.style.width = size;
                preview.style.height = size;
            };
            stage.RegisterCallback<GeometryChangedEvent>(_ => resize());
            detail.parent.RegisterCallback<GeometryChangedEvent>(_ => resize());
            var previous = AssetManagerControls.CreateIconButton(I18N.Get("variant.previousImage"),
                "arrow_left.png", () => select(index - 1), "ee4v-asset-manager__variant-gallery-previous");
            var nextButton = AssetManagerControls.CreateIconButton(I18N.Get("variant.nextImage"),
                "arrow_right.png", () => select(index + 1), "ee4v-asset-manager__variant-gallery-next");
            var position = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText,
                "ee4v-asset-manager__variant-gallery-position");
            position.SetTextAlign(TextAnchor.MiddleCenter);
            position.pickingMode = PickingMode.Ignore;
            var add = AssetManagerControls.CreateIconButton(I18N.Get("variant.addImages"), "add.png",
                UiSizeTokens.Size24, UiButtonVariant.Solid, () =>
            {
                var path = EditorUtility.OpenFilePanelWithFilters(I18N.Get("variant.addImages"), string.Empty,
                    new[] { I18N.Get("variant.imageFiles"), "png,jpg,jpeg" });
                if (!string.IsNullOrEmpty(path)) { AddVariantGalleryFiles(variant, new[] { path }); }
            }, "ee4v-asset-manager__variant-gallery-tile", "ee4v-asset-manager__variant-gallery-add");
            add.SetEnabled(_variantManager != null && !_variantState.VariantBusy && !string.IsNullOrEmpty(variant.ParentItemId));
            controls.Add(previous);
            controls.Add(position);
            controls.Add(nextButton);
            preview.Overlay.Add(controls);
            var thumbnails = new ScrollView(ScrollViewMode.Horizontal)
            {
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Auto
            };
            thumbnails.AddToClassList("ee4v-asset-manager__variant-gallery-thumbnails");
            thumbnails.contentContainer.AddToClassList("ee4v-asset-manager__variant-gallery-thumbnail-row");
            gallery.Add(thumbnails);
            var tiles = new List<UiButton>();
            var tileById = new Dictionary<string, UiButton>(StringComparer.Ordinal);
            thumbnails.Add(add);
            Action rebuildTiles = () =>
            {
                var ids = new HashSet<string>(entries.Select(entry => entry.Id ?? "automatic"), StringComparer.Ordinal);
                foreach (var removed in tileById.Keys.Where(id => !ids.Contains(id)).ToArray())
                {
                    tileById[removed].RemoveFromHierarchy();
                    tileById.Remove(removed);
                }
                tiles.Clear();
                for (var imageIndex = 0; imageIndex < entries.Count; imageIndex++)
                {
                    var entry = entries[imageIndex];
                    var id = entry.Id ?? "automatic";
                    if (!tileById.TryGetValue(id, out var tile))
                    {
                        tile = AssetManagerControls.CreateButton(string.Empty,
                            () => select(entries.FindIndex(candidate => (candidate.Id ?? "automatic") == id)),
                            "ee4v-asset-manager__variant-gallery-tile");
                        tile.tooltip = entry.Label;
                        var thumbnail = CreateVariantPreview(entry.Key, entry.Load);
                        thumbnail.AddToClassList("ee4v-asset-manager__variant-gallery-tile-image");
                        thumbnail.pickingMode = PickingMode.Ignore;
                        thumbnail.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
                        tile.Content.Add(thumbnail);
                        RegisterVariantGalleryImageContextMenu(tile, variant.VariantId,
                            () => entries.FirstOrDefault(candidate => (candidate.Id ?? "automatic") == id));
                        tileById.Add(id, tile);
                        thumbnails.Insert(imageIndex, tile);
                    }
                    if (thumbnails.contentContainer.IndexOf(tile) != imageIndex)
                    {
                        tile.PlaceBehind(thumbnails.contentContainer[imageIndex]);
                    }
                    tiles.Add(tile);
                }
            };
            render = () =>
            {
                position.SetText(entries.Count == 0 ? "0 / 0" : (index + 1) + " / " + entries.Count);
                previous.SetEnabled(entries.Count > 1);
                nextButton.SetEnabled(entries.Count > 1);
                for (var tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
                {
                    tiles[tileIndex].EnableInClassList("ee4v-asset-manager__variant-gallery-tile--selected", tileIndex == index);
                }
                if (entries.Count == 0) { SetVariantPreview(preview, null, null); return; }
                if (thumbnails.panel != null && thumbnails.layout.width > 0) { thumbnails.ScrollTo(tiles[index]); }
                var entry = entries[index];
                SetVariantPreview(preview, entry.Key, entry.Load);
            };
            Func<Action> checkpoint = () =>
            {
                var savedEntries = entries.ToArray();
                var savedIndex = index;
                var savedSelection = _variantState.SelectedVariantGalleryImageId;
                return () =>
                {
                    entries.Clear();
                    entries.AddRange(savedEntries);
                    index = savedIndex;
                    _variantState.SelectedVariantGalleryImageId = savedSelection;
                    rebuildTiles();
                    render();
                };
            };
            _variantState.VariantGalleryView = new VariantGalleryView
            {
                VariantId = variant.VariantId,
                RefreshControls = () =>
                {
                    add.SetEnabled(_variantManager != null && !_variantState.VariantBusy && !string.IsNullOrEmpty(variant.ParentItemId));
                    _content.Query<UiButton>(className: "ee4v-asset-manager__variant-revision-row")
                        .ForEach(row => row.SetEnabled(!_variantState.VariantBusy));
                },
                AddImages = uploads =>
                {
                    var rollback = checkpoint();
                    var selectedId = entries.Count > 0 ? entryId(entries[index]) : null;
                    foreach (var upload in uploads)
                    {
                        string id;
                        using (var hash = SHA256.Create())
                        {
                            id = BitConverter.ToString(hash.ComputeHash(upload.Data)).Replace("-", string.Empty).ToLowerInvariant();
                        }
                        if (entries.Any(entry => entry.Id == id)) { continue; }
                        var key = "variant-gallery:" + id;
                        _imageCache.SetSource(key, upload.Data);
                        entries.Insert(0, new VariantGalleryEntry
                        {
                            Id = id, Key = key, Label = upload.FileName,
                            Load = () => Task.FromResult(new AssetThumbnail { Found = true, Data = upload.Data })
                        });
                    }
                    index = Math.Max(0, entries.FindIndex(entry => entryId(entry) == selectedId));
                    _variantState.SelectedVariantGalleryImageId = entries.Count > 0 ? entryId(entries[index]) : null;
                    rebuildTiles();
                    render();
                    return rollback;
                },
                RemoveImage = id =>
                {
                    var rollback = checkpoint();
                    var selectedId = entries.Count > 0 ? entryId(entries[index]) : null;
                    entries.RemoveAll(entry => entry.Id == id);
                    index = selectedId == id ? Math.Min(index, Math.Max(0, entries.Count - 1))
                        : Math.Max(0, entries.FindIndex(entry => entryId(entry) == selectedId));
                    _variantState.SelectedVariantGalleryImageId = entries.Count > 0 ? entryId(entries[index]) : null;
                    rebuildTiles();
                    render();
                    return rollback;
                },
                MoveToFront = id =>
                {
                    var rollback = checkpoint();
                    var selectedId = entries.Count > 0 ? entryId(entries[index]) : null;
                    var target = entries.FirstOrDefault(entry => entry.Id == id);
                    if (target == null) { return rollback; }
                    entries.Remove(target);
                    entries.Insert(0, target);
                    index = Math.Max(0, entries.FindIndex(entry => entryId(entry) == selectedId));
                    rebuildTiles();
                    render();
                    return rollback;
                }
            };
            rebuildTiles();
            render();
            gallery.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.LeftArrow && evt.keyCode != KeyCode.RightArrow) { return; }
                select(index + (evt.keyCode == KeyCode.LeftArrow ? -1 : 1));
                evt.StopPropagation();
            });
            gallery.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (!add.enabledInHierarchy || GetDraggedGalleryPaths().Length == 0) { return; }
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            });
            gallery.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!add.enabledInHierarchy) { return; }
                var paths = GetDraggedGalleryPaths();
                if (paths.Length == 0) { return; }
                DragAndDrop.AcceptDrag();
                evt.StopPropagation();
                AddVariantGalleryFiles(variant, paths);
            });
        }

        private void RegisterVariantGalleryImageContextMenu(VisualElement target, string variantId,
            Func<VariantGalleryEntry> getEntry)
        {
            target.RegisterCallback<ContextClickEvent>(evt =>
            {
                var entry = getEntry();
                if (entry?.Id == null) { return; }
                var menu = new GenericMenu();
                var moveLabel = UiTextFactory.CreateGuiContent(I18N.Get("variant.moveImageToFront"));
                var removeLabel = UiTextFactory.CreateGuiContent(I18N.Get("variant.removeImage"));
                if (!_variantState.VariantBusy && _variantManager != null)
                {
                    menu.AddItem(moveLabel, false, () => MoveVariantGalleryImageToFront(variantId, entry.Id));
                    menu.AddItem(removeLabel, false, () => RemoveVariantGalleryImage(variantId, entry.Id));
                }
                else
                {
                    menu.AddDisabledItem(moveLabel);
                    menu.AddDisabledItem(removeLabel);
                }
                menu.ShowAsContext();
                evt.StopPropagation();
            });
        }

        private static string[] GetDraggedGalleryPaths() => DragAndDrop.paths
            .Concat(DragAndDrop.objectReferences.Select(AssetDatabase.GetAssetPath))
            .Where(path => !string.IsNullOrEmpty(path) && File.Exists(path) &&
                new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        private async void AddVariantGalleryFiles(DerivedAssetInfo variant, IReadOnlyList<string> paths)
        {
            IReadOnlyList<AssetVariantGalleryUpload> uploads = null;
            await RunVariantGalleryOperationAsync(variant.VariantId, "variant.addImages",
                async gallery =>
                {
                    uploads = await ReadVariantGalleryUploadsAsync(paths);
                    return !_disposed && ReferenceEquals(gallery, _variantState.VariantGalleryView)
                        ? gallery?.AddImages(uploads) : null;
                },
                () => _variantManager.AddGalleryImages(variant.VariantId, variant.ParentItemId, uploads));
        }

        private async void RemoveVariantGalleryImage(string variantId, string imageId)
        {
            await RunVariantGalleryOperationAsync(variantId, "variant.removeImage",
                gallery => Task.FromResult(gallery?.RemoveImage(imageId)),
                () => _variantManager.RemoveGalleryImage(variantId, imageId));
        }

        private async void MoveVariantGalleryImageToFront(string variantId, string imageId)
        {
            await RunVariantGalleryOperationAsync(variantId, "variant.moveImageToFront",
                gallery => Task.FromResult(gallery?.MoveToFront(imageId)),
                () => _variantManager.MoveGalleryImageToFront(variantId, imageId));
        }

        private async Task RunVariantGalleryOperationAsync(string variantId, string titleKey,
            Func<VariantGalleryView, Task<Action>> apply, Func<Task> save)
        {
            await PendingVariantMetadataSave;
            if (_disposed || _variantState.VariantBusy || _variantManager == null) { return; }
            _variantState.VariantBusy = true;
            _variantState.VariantGalleryOperationId = variantId;
            var gallery = _variantState.VariantGalleryView?.VariantId == variantId ? _variantState.VariantGalleryView : null;
            Action rollback = null;
            try
            {
                RefreshVariantGalleryControls();
                rollback = await apply(gallery);
                if (_disposed) { return; }
                RefreshVariantGalleryControls();
                await save();
            }
            catch (Exception exception)
            {
                if (!_disposed && ReferenceEquals(gallery, _variantState.VariantGalleryView)) { rollback?.Invoke(); }
                if (!_disposed) { EditorUtility.DisplayDialog(I18N.Get(titleKey), exception.Message, "OK"); }
            }
            finally
            {
                _variantState.VariantBusy = false;
                _variantState.VariantGalleryOperationId = null;
                if (!_disposed)
                {
                    if (gallery == null || !ReferenceEquals(gallery, _variantState.VariantGalleryView)) { Refresh(); }
                    else { RefreshVariantGalleryControls(); }
                }
            }
        }

        private static async Task<IReadOnlyList<AssetVariantGalleryUpload>> ReadVariantGalleryUploadsAsync(
            IReadOnlyList<string> paths)
        {
            var uploads = new List<AssetVariantGalleryUpload>();
            foreach (var path in paths)
            {
                var bytes = await Task.Run(() =>
                {
                    var file = new FileInfo(path);
                    if (file.Length > 16 * 1024 * 1024)
                    {
                        throw new InvalidDataException(I18N.Get("variant.imageTooLarge"));
                    }
                    return File.ReadAllBytes(path);
                });
                var texture = new Texture2D(2, 2);
                try
                {
                    if (!texture.LoadImage(bytes, true))
                    {
                        throw new InvalidDataException(I18N.Get("variant.invalidImage", Path.GetFileName(path)));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                uploads.Add(new AssetVariantGalleryUpload { FileName = Path.GetFileName(path), Data = bytes });
            }
            return uploads;
        }

        private void RefreshVariantGalleryControls()
        {
            _variantState.VariantGalleryView?.RefreshControls();
            if (ShowsInformation) { RefreshDetail(); }
        }

        private void AddVariantHistory(VisualElement detail, DerivedAssetInfo variant,
            IReadOnlyList<AssetVariantRevision> revisions, Action<AssetVariantRevision> selectRevision, UiButton action)
        {
            var section = new AssetDetailSection(I18N.Get("variant.history"));
            section.AddToClassList("ee4v-asset-manager__variant-history");
            section.Q<SectionHeader>().Actions.Add(action);
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList("ee4v-asset-manager__variant-history-scroll");
            section.Add(scroll);
            if (revisions.Count == 0)
            {
                scroll.Add(UiTextFactory.Create(I18N.Get("variant.noHistory"), UiClassNames.SecondaryText));
            }
            var list = new VisualElement();
            list.AddToClassList("ee4v-asset-manager__variant-revision-list");
            list.RegisterCallback<GeometryChangedEvent>(evt => list.EnableInClassList(
                "ee4v-asset-manager__variant-revision-list--compact", evt.newRect.width < 400f));
            var rows = new Dictionary<string, UiButton>(StringComparer.Ordinal);
            foreach (var revision in revisions)
            {
                var row = AssetManagerControls.CreateButton(string.Empty, () =>
                {
                    if (_variantState.VariantBusy) { return; }
                    selectRevision(revision);
                    foreach (var entry in rows)
                    {
                        entry.Value.EnableInClassList("ee4v-asset-manager__variant-revision-row--selected",
                            entry.Key == revision.Id);
                    }
                }, "ee4v-asset-manager__variant-revision-row");
                row.SetContentAlignment(Justify.FlexStart);
                row.EnableInClassList("ee4v-asset-manager__variant-revision-row--selected",
                    revision.Id == _variantState.SelectedVariantRevisionId);
                row.SetEnabled(!_variantState.VariantBusy);
                var number = UiTextFactory.Create("v" + revision.Number, UiClassNames.NavigationItemLabel,
                    "ee4v-asset-manager__variant-revision-number");
                number.pickingMode = PickingMode.Ignore;
                row.Content.Add(number);
                var memoSlot = new VisualElement { pickingMode = PickingMode.Ignore };
                memoSlot.AddToClassList("ee4v-asset-manager__variant-revision-memo-slot");
                var memo = UiTextFactory.Create(revision.Memo, UiClassNames.InfoCardDescription,
                    "ee4v-asset-manager__variant-revision-memo");
                memo.SetWhiteSpace(WhiteSpace.NoWrap);
                memo.SetTextAlign(TextAnchor.MiddleLeft);
                memo.pickingMode = PickingMode.Ignore;
                memoSlot.Add(memo);
                row.Content.Add(memoSlot);
                var date = UiTextFactory.Create(revision.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm"),
                    UiClassNames.SecondaryText, "ee4v-asset-manager__variant-revision-date");
                date.SetWhiteSpace(WhiteSpace.NoWrap);
                date.SetTextAlign(TextAnchor.MiddleRight);
                date.pickingMode = PickingMode.Ignore;
                row.Content.Add(date);
                row.RegisterCallback<PointerEnterEvent>(evt => BeginVariantRevisionTooltip(row, variant.VariantId,
                    revision, row.LocalToWorld(evt.localPosition)));
                row.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!ReferenceEquals(row, _variantState.HoveredVariantRevisionRow)) { return; }
                    _variantState.VariantRevisionPointerPosition = row.LocalToWorld(evt.localPosition);
                    _variantState.VariantRevisionTooltip?.SetPointerPosition(row, _variantState.VariantRevisionPointerPosition);
                });
                row.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (ReferenceEquals(row, _variantState.HoveredVariantRevisionRow)) { HideVariantRevisionTooltip(); }
                });
                row.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    if (ReferenceEquals(row, _variantState.HoveredVariantRevisionRow)) { HideVariantRevisionTooltip(); }
                });
                rows.Add(revision.Id, row);
                list.Add(row);
            }
            scroll.Add(list);
            detail.Add(section);
        }

        private void BeginVariantRevisionTooltip(VisualElement row, string variantId,
            AssetVariantRevision revision, Vector2 panelPosition)
        {
            if (_variantManager == null || ReferenceEquals(row, _variantState.HoveredVariantRevisionRow)) { return; }
            HideVariantRevisionTooltip();
            _variantState.HoveredVariantRevisionRow = row;
            _variantState.VariantRevisionPointerPosition = panelPosition;
            var cancellation = new CancellationTokenSource();
            _variantState.VariantRevisionHoverCancellation = cancellation;
            _ = ShowVariantRevisionTooltipAsync(row, variantId, revision, cancellation);
        }

        private async Task ShowVariantRevisionTooltipAsync(VisualElement row, string variantId,
            AssetVariantRevision revision, CancellationTokenSource cancellation)
        {
            try
            {
                var key = "variant-revision:" + revision.Id;
                if (!await EnsureVariantPreviewSourceAsync(key,
                        () => _variantManager.GetRevisionThumbnail(variantId, revision.Id), cancellation.Token) ||
                    !ReferenceEquals(row, _variantState.HoveredVariantRevisionRow) || row.panel == null) { return; }
                using (var image = new CachedImage(_imageCache))
                {
                    image.SetSource(key);
                    if (image.DisplayedTexture is Texture2D texture)
                    {
                        _variantState.VariantRevisionTooltip = FileTreeImageTooltipWindow.Show(row,
                            _variantState.VariantRevisionPointerPosition, texture, string.Empty, 240f);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                _variantState.VariantPreviewLoads.Remove("variant-revision:" + revision.Id);
                if (!_disposed && !cancellation.IsCancellationRequested) { Debug.LogException(exception); }
            }
            finally
            {
                if (ReferenceEquals(_variantState.VariantRevisionHoverCancellation, cancellation))
                {
                    _variantState.VariantRevisionHoverCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        private void HideVariantRevisionTooltip()
        {
            var cancellation = _variantState.VariantRevisionHoverCancellation;
            _variantState.VariantRevisionHoverCancellation = null;
            cancellation?.Cancel();
            if (_variantState.VariantRevisionTooltip != null)
            {
                _variantState.VariantRevisionTooltip.Close();
                _variantState.VariantRevisionTooltip = null;
            }
            _variantState.HoveredVariantRevisionRow = null;
        }

        private IReadOnlyList<AssetVariantFileDependency> GetCurrentVariantDependencies(DerivedAssetInfo variant)
        {
            if (variant.Prefab == null) { return Array.Empty<AssetVariantFileDependency>(); }
            var folder = Path.GetDirectoryName(variant.AssetPath).Replace('\\', '/');
            var sourceGuid = Infrastructure.DerivedAssetCatalog.Read(variant.AssetPath)?.SourceGuid;
            var guids = new HashSet<string>(AssetDatabase.GetDependencies(variant.AssetPath, true)
                .Concat(AssetDatabase.FindAssets(string.Empty, new[] { folder }).Select(AssetDatabase.GUIDToAssetPath))
                .Append(AssetDatabase.GUIDToAssetPath(sourceGuid ?? string.Empty))
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) && path != variant.AssetPath && path != folder)
                .Select(AssetDatabase.AssetPathToGUID), StringComparer.Ordinal);
            return _manager.GetImportedAssetAssociations(guids.ToArray())
                .GroupBy(association => association.AssetGuid)
                .Select(group => group.OrderByDescending(association => association.ImportedAt).First())
                .Select(association => association.FileId).Distinct(StringComparer.Ordinal)
                .Select(_manager.GetFile)
                .Select(file => new AssetVariantFileDependency
                {
                    FileId = file.Id, SourceType = file.SourceType, SourceId = file.SourceId,
                    TargetPaths = Array.Empty<string>()
                }).ToArray();
        }

        private void AddVariantDependencies(VisualElement detail,
            IReadOnlyList<AssetVariantFileDependency> dependencies, string error)
        {
            var section = new AssetDetailSection(I18N.Get("variant.dependencies"));
            section.AddToClassList("ee4v-asset-manager__variant-dependencies");
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList("ee4v-asset-manager__variant-dependency-scroll");
            section.Add(scroll);
            if (!string.IsNullOrEmpty(error))
            {
                scroll.Add(UiTextFactory.CreateHelpBox(error, HelpBoxMessageType.Error));
            }
            else if (dependencies == null || dependencies.Count == 0)
            {
                scroll.Add(UiTextFactory.Create(I18N.Get("common.none"), UiClassNames.SecondaryText));
            }
            else
            {
                IReadOnlyList<AssetFile> fallbackFiles = null;
                var grid = new VisualElement();
                grid.AddToClassList("ee4v-asset-manager__variant-card-grid");
                var groups = dependencies.Select(dependency => new
                    {
                        Dependency = dependency,
                        File = FindVariantDependencyFile(dependency, ref fallbackFiles)
                    })
                    .GroupBy(entry => !string.IsNullOrEmpty(entry.File?.ItemId)
                        ? "item:" + entry.File.ItemId
                        : entry.File != null ? "file:" + entry.File.Id
                        : "source:" + entry.Dependency.SourceType + ":" + entry.Dependency.SourceId,
                        StringComparer.Ordinal);
                foreach (var group in groups)
                {
                    var entries = group.GroupBy(entry =>
                            (entry.Dependency.SourceType, entry.Dependency.SourceId))
                        .Select(files => files.First()).ToArray();
                    var file = entries[0].File;
                    var item = FindVariantItem(file?.ItemId);
                    var title = item?.Name ?? (file == null ? I18N.Get("variant.dependencyMissing")
                        : file.IsArchived ? I18N.Get("detail.item.archived")
                        : I18N.Get("navigation.unassignedFiles"));
                    var fileNames = entries.Select(entry => entry.File?.FileName ??
                        Path.GetFileName(entry.Dependency.SourceId)).ToArray();
                    var description = fileNames.Length == 1 ? fileNames[0]
                        : I18N.Get("variant.dependencyFiles", fileNames[0], fileNames.Length - 1);
                    var archived = item != null && entries.Any(entry => entry.File?.IsArchived == true)
                        ? I18N.Get("detail.item.archived") : null;
                    var card = new InfoCard(new InfoCardState(title, description, badgeText: archived));
                    card.TitleText.SetWhiteSpace(WhiteSpace.Normal);
                    card.DescriptionText.SetWhiteSpace(WhiteSpace.Normal);
                    card.DescriptionText.tooltip = string.Join("\n", fileNames);
                    card.AddToClassList("ee4v-asset-manager__variant-card");
                    card.AddToClassList("ee4v-asset-manager__variant-dependency-card");
                    var preview = CreateVariantPreview(item?.Id, item == null ? null :
                        (Func<Task<AssetThumbnail>>)(() => _manager.GetThumbnail(item.Id)));
                    preview.AddToClassList("ee4v-asset-manager__variant-card-preview");
                    card.Insert(0, preview);
                    if (item != null)
                    {
                        card.Body.Add(AssetManagerControls.CreateButton(I18N.Get("variant.openSource"),
                            () => _viewState.OpenItemDetail(item.Id)));
                    }
                    var content = new VisualElement();
                    content.AddToClassList("ee4v-asset-manager__variant-dependency-content");
                    content.Add(card.Q<VisualElement>(className: "ee4v-ui-info-card__header"));
                    content.Add(card.Body);
                    card.Add(content);
                    grid.Add(card);
                }
                scroll.Add(grid);
            }
            detail.Add(section);
        }

        private AssetFile FindVariantDependencyFile(AssetVariantFileDependency dependency,
            ref IReadOnlyList<AssetFile> fallbackFiles)
        {
            AssetFile file = null;
            if (!string.IsNullOrEmpty(dependency.FileId))
            {
                try { file = _manager.GetFile(dependency.FileId); }
                catch (AssetManagerException exception) when (exception.Code == AssetManagerErrorCode.NotFound) { }
            }
            if (file != null && file.SourceType == dependency.SourceType && file.SourceId == dependency.SourceId)
            {
                return file;
            }
            if (fallbackFiles == null)
            {
                fallbackFiles = _manager.SearchItems(new AssetItemQuery { IncludeArchived = true }).Items
                    .SelectMany(item => _manager.GetFiles(item.Id, true))
                    .Concat(_manager.GetUnassignedFiles(true)).ToArray();
            }
            return fallbackFiles.FirstOrDefault(candidate => candidate.SourceType == dependency.SourceType &&
                candidate.SourceId == dependency.SourceId);
        }

        private AssetItem FindVariantItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) { return null; }
            try { return _manager.GetItem(itemId); }
            catch (AssetManagerException exception) when (exception.Code == AssetManagerErrorCode.NotFound) { return null; }
        }

        private void ModifyVariant(string variantId)
        {
            if (_variantState.VariantBusy || _modifyVariant == null) { return; }
            var variant = GetVariants().FirstOrDefault(candidate => candidate.VariantId == variantId);
            if (variant?.Prefab == null)
            {
                return;
            }
            _modifyVariant(variant);
        }

        private IReadOnlyList<DerivedAssetInfo> GetVariants()
        {
            var project = DerivedAssetCreator.FindAll().ToDictionary(
                variant => variant.VariantId, StringComparer.Ordinal);
            if (_variantManager != null)
            {
                foreach (var saved in _variantManager.GetVariants())
                {
                    if (project.TryGetValue(saved.Id, out var imported))
                    {
                        imported.Name = saved.Name;
                        imported.Description = saved.Description;
                        imported.UpdatedAt = saved.UpdatedAt;
                    }
                    else
                    {
                        project.Add(saved.Id, new DerivedAssetInfo
                        {
                            VariantId = saved.Id, Name = saved.Name, Description = saved.Description,
                            ParentItemId = saved.ParentItemId, AssetPath = saved.RootAssetPath,
                            UpdatedAt = saved.UpdatedAt
                        });
                    }
                }
            }
            var visibleItemIds = new HashSet<string>(
                _manager.SearchItems(new AssetItemQuery { IncludeArchived = true })
                    .Items.Select(item => item.Id), StringComparer.Ordinal);
            return project.Values.Where(variant =>
                visibleItemIds.Contains(variant.ParentItemId)).ToArray();
        }

        private async void RestoreVariant(string variantId, string revisionId, bool confirm = true)
        {
            await PendingVariantMetadataSave;
            if (_disposed) { return; }
            if (_variantState.VariantBusy || _variantManager == null || (confirm && !EditorUtility.DisplayDialog(I18N.Get("variant.restore"),
                    I18N.Get("variant.restoreConfirm"), I18N.Get("variant.restore"), I18N.Get("action.cancel"))))
            {
                return;
            }
            _variantState.VariantBusy = true;
            Refresh();
            try
            {
                await _variantManager.Restore(variantId, revisionId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!_disposed)
                {
                    EditorUtility.DisplayDialog(I18N.Get(confirm ? "variant.restore" : "action.import"), exception.Message, "OK");
                }
            }
            finally
            {
                _variantState.VariantBusy = false;
                if (!_disposed) { Refresh(); }
            }
        }

        private void OnVariantsChanged()
        {
            if (_variantState.VariantGalleryOperationId != null) { return; }
            _variantState.VariantRevisionDetails.Clear();
            _itemGrid?.ClearThumbnails();
            if (_variantState.VariantMetadataSaveCount > 0) { return; }
            if (!_disposed && (ShowsVariants || !string.IsNullOrEmpty(_viewState.DetailVariantId) ||
                !string.IsNullOrEmpty(_viewState.DetailItemId))) { Refresh(); }
        }

    }
}
