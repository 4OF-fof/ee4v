(function () {
  "use strict";

  const POPUP_WIDTH = 380;
  const POPUP_HEIGHT = 196;

  interface PluginState {
    rootFolder: EagleFolder | null;
    isBusy: boolean;
    isDomReady: boolean;
    isPluginReady: boolean;
    errorMessage: string;
    libraryRevision: number;
  }

  interface PluginElements {
    windowTitle: HTMLElement;
    itemUrlLabel: HTMLElement;
    itemUrlInput: HTMLInputElement;
    statusMessage: HTMLElement;
    createButton: HTMLButtonElement;
    cancelButton: HTMLButtonElement;
  }

  interface RefreshResult {
    name: string;
    thumbnailError: string;
    error: string;
  }

  const state: PluginState = {
    rootFolder: null,
    isBusy: false,
    isDomReady: false,
    isPluginReady: false,
    errorMessage: "",
    libraryRevision: 0
  };

  const elements = {} as PluginElements;

  window.addEventListener("DOMContentLoaded", () => {
    cacheElements();
    state.isDomReady = true;
    localizeDocument();
    bindEvents();
    render();
  });

  eagle.onPluginCreate(async () => {
    state.isPluginReady = true;
    localizeDocument();
    await applyTheme(await Promise.resolve(eagle.app.theme));
    eagle.onThemeChanged(theme => {
      applyTheme(theme).catch(console.error);
    });
    eagle.onLibraryChanged(() => {
      state.libraryRevision += 1;
      state.rootFolder = null;
      state.errorMessage = "";
      reloadState().catch(console.error);
    });
    window.addEventListener("keydown", handleWindowKeydown);
    await configurePopupWindow();
    await reloadState();
  });

  eagle.onPluginRun(() => {
    if (state.isPluginReady) {
      handlePluginRun().catch(console.error);
    }
  });

  async function handlePluginRun(): Promise<void> {
    if (state.isBusy) {
      return;
    }

    const selectedItems = await eagle.item.getSelected();
    const selectedIds = Array.from(new Set(selectedItems.map(item => item.id)));
    const boothMetaItems: EagleItem[] = [];
    for (const id of selectedIds) {
      const item = await eagle.item.getById(id);
      if (core().isBoothMetaItem(item)) {
        boothMetaItems.push(item);
      }
    }
    if (boothMetaItems.length > 0) {
      await eagle.window.hide();
      await refreshSelectedBoothMetaItems(boothMetaItems, selectedIds.length - boothMetaItems.length);
      return;
    }

    resetForm();
    await centerPopupWindow();
    await reloadState();
    await eagle.window.show();
    focusUrlInput();
  }

  function cacheElements(): void {
    elements.windowTitle = requireElement("window-title", HTMLElement);
    elements.itemUrlLabel = requireElement("item-url-label", HTMLElement);
    elements.itemUrlInput = requireElement("item-url-input", HTMLInputElement);
    elements.statusMessage = requireElement("status-message", HTMLElement);
    elements.createButton = requireElement("create-button", HTMLButtonElement);
    elements.cancelButton = requireElement("cancel-button", HTMLButtonElement);
  }

  function bindEvents(): void {
    elements.createButton.addEventListener("click", () => {
      handleCreate().catch(console.error);
    });
    elements.cancelButton.addEventListener("click", () => {
      closeWindow().catch(console.error);
    });
    elements.itemUrlInput.addEventListener("input", () => {
      state.errorMessage = "";
      render();
    });
    elements.itemUrlInput.addEventListener("keydown", (event: KeyboardEvent) => {
      if (event.key === "Enter" && !elements.createButton.disabled) {
        event.preventDefault();
        handleCreate().catch(console.error);
      }
    });
  }

  function localizeDocument(): void {
    if (!state.isDomReady) {
      return;
    }

    const locale = normalizeLocale(eagle.app.locale);
    document.documentElement.lang = locale;
    document.title = t("window.title", "Add BOOTH item");
    elements.windowTitle.textContent = t("window.title", "Add BOOTH item");
    elements.itemUrlLabel.textContent = t("window.itemUrl", "BOOTH item URL");
    elements.itemUrlInput.setAttribute("aria-label", t("window.itemUrl", "BOOTH item URL"));
    elements.cancelButton.textContent = t("window.cancel", "Cancel");
    render();
  }

  async function applyTheme(theme: EagleTheme): Promise<void> {
    const normalizedTheme = core().safeString(theme).toUpperCase();
    if (normalizedTheme === "LIGHT" || normalizedTheme === "LIGHTGRAY") {
      document.body.setAttribute("theme", normalizedTheme);
      return;
    }

    if (normalizedTheme === "AUTO") {
      const isDark = await Promise.resolve(eagle.app.isDarkColors());
      document.body.setAttribute("theme", isDark ? "DARK" : "LIGHT");
      return;
    }

    document.body.setAttribute("theme", normalizedTheme || "DARK");
  }

  async function configurePopupWindow(): Promise<void> {
    await eagle.window.setAlwaysOnTop(true);
    await eagle.window.setResizable(false);
    await centerPopupWindow();
  }

  async function centerPopupWindow(): Promise<void> {
    const cursorPoint = await eagle.screen.getCursorScreenPoint();
    const display = await eagle.screen.getDisplayNearestPoint(cursorPoint);
    const bounds = display && display.workArea ? display.workArea : display.bounds;
    const x = Math.round(bounds.x + ((bounds.width - POPUP_WIDTH) / 2));
    const y = Math.round(bounds.y + ((bounds.height - POPUP_HEIGHT) / 2));
    await eagle.window.setBounds({
      x,
      y,
      width: POPUP_WIDTH,
      height: POPUP_HEIGHT
    });
  }

  async function reloadState(): Promise<void> {
    if (!state.isPluginReady || state.isBusy) {
      return;
    }

    state.isBusy = true;
    state.errorMessage = "";
    render();
    try {
      state.rootFolder = await core().findVrcAssetRootFolder();
    } catch (error) {
      console.error(error);
      state.rootFolder = null;
      state.errorMessage = errorMessage(error);
    } finally {
      state.isBusy = false;
      render();
    }
  }

  async function handleCreate(): Promise<void> {
    const boothRef = core().parseBoothItemReference(elements.itemUrlInput.value);
    if (!boothRef) {
      state.errorMessage = t("error.invalidItemUrl", "Enter a valid BOOTH item URL.");
      render();
      return;
    }

    if (!state.rootFolder) {
      state.errorMessage = t("window.rootFolderMissing", "Create one VRCAsset folder at the library root first.");
      render();
      return;
    }

    await runBusy(async () => {
      const result = await core().ensureBoothMetaForUrl(elements.itemUrlInput.value);
      elements.itemUrlInput.value = result.meta.itemUrl || boothRef.normalizedUrl;
      if (result.folder && result.folder.open) {
        await result.folder.open();
      }
      await eagle.item.select([result.item.id]);
      await closeWindow();
    });
  }

  async function refreshSelectedBoothMetaItems(items: EagleItem[], skippedCount: number): Promise<void> {
    const results: RefreshResult[] = [];
    const libraryRevision = state.libraryRevision;
    const succeeded = await runBusy(async () => {
      for (const selectedItem of items) {
        if (libraryRevision !== state.libraryRevision) {
          break;
        }
        try {
          const refreshed = await refreshBoothMetaItem(selectedItem, libraryRevision);
          results.push({ ...refreshed, error: "" });
        } catch (error) {
          console.error(error);
          results.push({
            name: core().safeString(selectedItem.name) || selectedItem.id,
            thumbnailError: "",
            error: errorMessage(error) || t("window.unknownError", "An unexpected error occurred.")
          });
        }
      }
    });

    let body: string;
    if (!succeeded) {
      body = t("notification.refreshFailed", "Could not refresh BOOTH information: {{message}}", {
        message: state.errorMessage || t("window.unknownError", "An unexpected error occurred.")
      });
    } else if (items.length === 1 && skippedCount === 0 && results.length === 1) {
      const result = results[0];
      body = result.error
        ? t("notification.refreshFailed", "Could not refresh BOOTH information: {{message}}", { message: result.error })
        : result.thumbnailError
          ? t("notification.refreshPartial", "Updated BOOTH information for {{name}}, but the thumbnail could not be updated: {{message}}", {
            name: result.name,
            message: result.thumbnailError
          })
          : t("notification.refreshComplete", "Updated BOOTH information and thumbnail for {{name}}.", { name: result.name });
    } else {
      const updatedCount = results.filter(result => !result.error).length;
      const thumbnailFailedCount = results.filter(result => result.thumbnailError).length;
      const failedCount = results.filter(result => result.error).length;
      body = t("notification.batchComplete", "BOOTH info updated: {{updated}}; thumbnail failures: {{thumbnailFailed}}; refresh failures: {{failed}}; other selected items: {{skipped}}; not processed: {{remaining}}.", {
        updated: updatedCount,
        thumbnailFailed: thumbnailFailedCount,
        failed: failedCount,
        skipped: skippedCount,
        remaining: items.length - results.length
      });
      const firstIssue = results.find(result => result.error || result.thumbnailError);
      if (firstIssue) {
        body += ` ${t("notification.batchFirstIssue", "First issue: {{name}} — {{message}}", {
          name: firstIssue.name,
          message: firstIssue.error || firstIssue.thumbnailError
        })}`;
      }
    }
    await eagle.notification.show({
      title: t("notification.title", "Booth Compat"),
      body,
      mute: true,
      duration: results.some(result => result.error || result.thumbnailError) || results.length < items.length ? 5000 : 3000
    });
  }

  async function refreshBoothMetaItem(selectedItem: EagleItem, libraryRevision: number): Promise<Pick<RefreshResult, "name" | "thumbnailError">> {
    const storedMeta = await core().loadMetaFromItem(selectedItem);
    const boothRef = core().parseBoothItemReference(selectedItem.url)
      || core().parseBoothItemReference(storedMeta.itemUrl);
    if (!boothRef) {
      throw new Error(t("error.invalidItemUrl", "Enter a valid BOOTH item URL."));
    }

    const snapshot = await core().fetchBoothSnapshot(boothRef);
    if (libraryRevision !== state.libraryRevision) {
      throw new Error(t("notification.libraryChanged", "The Eagle library changed during refresh."));
    }
    const item = await eagle.item.getById(selectedItem.id);
    if (!core().isBoothMetaItem(item)) {
      throw new Error(t("notification.itemUnavailable", "The selected BoothMeta item is no longer available."));
    }

    const latestMeta = await core().loadMetaFromItem(item);
    if (libraryRevision !== state.libraryRevision) {
      throw new Error(t("notification.libraryChanged", "The Eagle library changed during refresh."));
    }
    const nextMeta = core().normalizeMeta({
      ...latestMeta,
      ...snapshot,
      name: snapshot.name || core().safeString(item.name).trim() || latestMeta.name,
      attachedAt: latestMeta.attachedAt || new Date().toISOString(),
      downloads: latestMeta.downloads
    });

    const normalizedTags = core().ensureBoothMetaTag(item.tags);
    if (item.name !== nextMeta.name || item.url !== nextMeta.itemUrl
      || item.annotation !== nextMeta.description
      || JSON.stringify(item.tags) !== JSON.stringify(normalizedTags)) {
      item.name = nextMeta.name;
      item.url = nextMeta.itemUrl;
      item.annotation = nextMeta.description;
      item.tags = normalizedTags;
      await item.save();
    }
    await core().saveMetaToItem(item, nextMeta);

    let thumbnailError = "";
    if (!nextMeta.thumbnailUrl) {
      thumbnailError = t("notification.thumbnailMissing", "No thumbnail URL was found.");
    } else {
      try {
        await core().applyThumbnailToItem(item, nextMeta.thumbnailUrl, await Promise.resolve(eagle.app.getPath("temp")), true);
      } catch (error) {
        console.error(error);
        thumbnailError = errorMessage(error) || t("window.unknownError", "An unexpected error occurred.");
      }
    }
    return { name: nextMeta.name, thumbnailError };
  }

  async function handleWindowKeydown(event: KeyboardEvent): Promise<void> {
    if (event.key !== "Escape") {
      return;
    }

    event.preventDefault();
    await closeWindow();
  }

  async function closeWindow(): Promise<void> {
    resetForm();
    await eagle.window.hide();
  }

  function resetForm(): void {
    if (state.isDomReady) {
      elements.itemUrlInput.value = "";
    }
    state.errorMessage = "";
    render();
  }

  function render(): void {
    if (!state.isDomReady) {
      return;
    }

    const hasRootFolder = Boolean(state.rootFolder);
    const inputValue = elements.itemUrlInput.value.trim();
    const hasValidUrl = Boolean(core().parseBoothItemReference(inputValue));
    const isInteractive = state.isPluginReady && !state.isBusy;

    elements.createButton.textContent = state.isBusy
      ? t("window.creating", "Creating…")
      : t("window.create", "Create");
    elements.createButton.disabled = !isInteractive || !hasRootFolder || !hasValidUrl;
    elements.createButton.setAttribute("aria-busy", String(state.isBusy));
    elements.cancelButton.disabled = !isInteractive;

    let status = "";
    let isError = false;
    if (state.errorMessage) {
      status = state.errorMessage;
      isError = true;
    } else if (!state.isBusy && !hasRootFolder) {
      status = t("window.rootFolderMissing", "Create one VRCAsset folder at the library root first.");
      isError = true;
    } else if (!inputValue) {
      status = t("window.ready", "Enter the URL of a BOOTH item.");
    } else if (!hasValidUrl) {
      status = t("window.invalidUrl", "Use a valid booth.pm item URL.");
      isError = true;
    }

    elements.statusMessage.textContent = status;
    elements.statusMessage.classList.toggle("is-error", isError);
  }

  async function runBusy(action: () => Promise<void>): Promise<boolean> {
    if (state.isBusy) {
      return false;
    }

    state.isBusy = true;
    state.errorMessage = "";
    render();
    try {
      await action();
      return true;
    } catch (error) {
      console.error(error);
      state.errorMessage = errorMessage(error) || t("window.unknownError", "An unexpected error occurred.");
      return false;
    } finally {
      state.isBusy = false;
      render();
    }
  }

  function focusUrlInput(): void {
    if (!state.isDomReady) {
      return;
    }
    window.requestAnimationFrame(() => elements.itemUrlInput.focus());
  }

  function normalizeLocale(locale: unknown): string {
    return core().safeString(locale).replace("_", "-") || "en";
  }

  function errorMessage(error: unknown): string {
    return error instanceof Error ? error.message : core().safeString(error);
  }

  function t(key: string, fallback: string, options?: Record<string, unknown>): string {
    return core().t(key, fallback, options);
  }

  function core(): BoothCompatCore {
    return window.BoothCompatCore;
  }

  function requireElement<T extends HTMLElement>(id: string, constructor: { new(): T }): T {
    const element = document.getElementById(id);
    if (!(element instanceof constructor)) {
      throw new Error(`Element #${id} was not found.`);
    }
    return element;
  }
})();
