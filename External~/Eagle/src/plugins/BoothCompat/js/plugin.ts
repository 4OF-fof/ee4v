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
  }

  interface PluginElements {
    windowTitle: HTMLElement;
    itemUrlLabel: HTMLElement;
    itemUrlInput: HTMLInputElement;
    statusMessage: HTMLElement;
    createButton: HTMLButtonElement;
    cancelButton: HTMLButtonElement;
  }

  const state: PluginState = {
    rootFolder: null,
    isBusy: false,
    isDomReady: false,
    isPluginReady: false,
    errorMessage: ""
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
