(function () {
  "use strict";

const POPUP_WIDTH = 380;
const POPUP_HEIGHT = 136;

interface PluginState {
  rootFolder: EagleFolder | null;
  isBusy: boolean;
  isPluginReady: boolean;
}

interface PluginElements {
  itemUrlInput: HTMLInputElement;
  createButton: HTMLButtonElement;
  cancelButton: HTMLButtonElement;
}

const state: PluginState = {
  rootFolder: null,
  isBusy: false,
  isPluginReady: false
};

const elements = {} as PluginElements;

window.addEventListener("DOMContentLoaded", () => {
  cacheElements();
  bindEvents();
  render();
});

eagle.onPluginCreate(async () => {
  state.isPluginReady = true;
  applyTheme(await Promise.resolve(eagle.app.theme));
  eagle.onThemeChanged(theme => applyTheme(theme));
  window.addEventListener("keydown", handleWindowKeydown);
  await configurePopupWindow();
  await reloadState();
});

eagle.onPluginRun(() => {
  if (state.isPluginReady) {
    handlePluginRun().catch(console.error);
  }
});

async function handlePluginRun() {
  const didSyncSelectedItem = await trySyncSelectedBoothMetaItem();
  if (didSyncSelectedItem) {
    return;
  }

  resetForm();
  await centerPopupWindow();
  await reloadState();
}

function cacheElements() {
  elements.itemUrlInput = requireElement("item-url-input", HTMLInputElement);
  elements.createButton = requireElement("create-button", HTMLButtonElement);
  elements.cancelButton = requireElement("cancel-button", HTMLButtonElement);
}

function bindEvents() {
  elements.createButton.addEventListener("click", handleCreate);
  elements.cancelButton.addEventListener("click", closeWindow);
  elements.itemUrlInput.addEventListener("input", render);
  elements.itemUrlInput.addEventListener("keydown", (event: KeyboardEvent) => {
    if (event.key === "Enter" && !elements.createButton.disabled) {
      event.preventDefault();
      handleCreate();
    }
  });
}

function applyTheme(theme: EagleTheme) {
  document.body.setAttribute("theme", theme || "LIGHT");
}

async function configurePopupWindow() {
  await eagle.window.setAlwaysOnTop(true);
  await eagle.window.setResizable(false);
  await centerPopupWindow();
}

async function centerPopupWindow() {
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

async function reloadState() {
  if (!state.isPluginReady) {
    return;
  }

  return runBusy(async () => {
    state.rootFolder = await core().requireVrcAssetRootFolder();
    render();
  }, false);
}

async function handleCreate() {
  return runBusy(async () => {
    const boothRef = core().parseBoothItemReference(elements.itemUrlInput.value);
    if (!boothRef) {
      throw new Error("有効な Booth item URL を入力してください。");
    }

    const result = await window.BoothCompatCore.ensureBoothMetaForUrl(elements.itemUrlInput.value);
    elements.itemUrlInput.value = result.meta.itemUrl || boothRef.normalizedUrl;
    if (result.folder && result.folder.open) {
      await result.folder.open();
    }
    await eagle.item.select([result.item.id]);
    await closeWindow();
  });
}

async function trySyncSelectedBoothMetaItem() {
  const selectedItems = await eagle.item.getSelected();
  if (selectedItems.length !== 1) {
    return false;
  }

  const item = await eagle.item.getById(selectedItems[0].id);
  if (!core().isBoothMetaItem(item)) {
    return false;
  }
  const boothMetaItem = item;

  await eagle.window.hide();

  return runBusy(async () => {
    const syncedMeta = await syncBoothMetaItem(boothMetaItem);
    await eagle.notification.show({
      title: "Booth Compat",
      body: syncedMeta.lastUpdatedAtUtc
        ? `${boothMetaItem.name} を更新しました。`
        : `${boothMetaItem.name} の更新対象が見つかりませんでした。`,
      mute: true,
      duration: 2500
    });
  }, false).then(() => true).catch(async error => {
    console.error(error);
    await eagle.notification.show({
      title: "Booth Compat",
      body: error instanceof Error ? error.message : "Booth metadata の更新に失敗しました。",
      mute: true,
      duration: 3500
    });
    return true;
  });
}

async function handleWindowKeydown(event: KeyboardEvent) {
  if (event.key !== "Escape") {
    return;
  }

  event.preventDefault();
  await closeWindow();
}

async function closeWindow() {
  resetForm();
  await eagle.window.hide();
}

function resetForm() {
  if (elements.itemUrlInput) {
    elements.itemUrlInput.value = "";
  }
  render();
}

async function syncBoothMetaItem(item: EagleItem): Promise<BoothMeta> {
  const storedMeta = await core().loadMetaFromItem(item);
  const itemUrl = normalizeItemUrl(item.url);
  const syncBase = core().normalizeMeta({
    ...storedMeta,
    itemUrl,
    name: core().safeString(item.name).trim(),
    description: core().safeString(item.annotation)
  });

  const boothRef = core().parseBoothItemReference(itemUrl || syncBase.itemUrl);
  if (!boothRef) {
    throw new Error("選択 item に有効な Booth item URL がありません。");
  }

  const snapshot = await core().fetchBoothSnapshot(boothRef);
  const nextItemName = core().safeString(snapshot.name).trim() || syncBase.name;
  const nextItemUrl = snapshot.itemUrl || boothRef.normalizedUrl;
  const nextItemDescription = core().safeString(snapshot.description);
  const nextMeta = core().normalizeMeta({
    ...syncBase,
    ...snapshot,
    itemUrl: nextItemUrl,
    name: nextItemName,
    description: nextItemDescription,
    attachedAt: syncBase.attachedAt || new Date().toISOString()
  });

  const originalTags = Array.isArray(item.tags) ? item.tags : [];
  const normalizedTags = core().ensureBoothMetaTag(originalTags);
  let shouldSaveItem = false;

  if (JSON.stringify(originalTags) !== JSON.stringify(normalizedTags)) {
    item.tags = normalizedTags;
    shouldSaveItem = true;
  }

  if (item.name !== nextItemName) {
    item.name = nextItemName;
    shouldSaveItem = true;
  }

  if (item.url !== nextItemUrl) {
    item.url = nextItemUrl;
    shouldSaveItem = true;
  }

  if (item.annotation !== nextItemDescription) {
    item.annotation = nextItemDescription;
    shouldSaveItem = true;
  }

  if (shouldSaveItem) {
    await item.save();
  }

  if (!core().isMetaEquivalent(storedMeta, nextMeta)) {
    await core().saveMetaToItem(item, nextMeta);
  }

  await core().applyThumbnailToItem(item, nextMeta.thumbnailUrl, await Promise.resolve(eagle.app.getPath("temp")));
  return nextMeta;
}

function render() {
  const isInteractive = state.isPluginReady && !state.isBusy;
  const hasRootFolder = Boolean(state.rootFolder);
  const hasValidUrl = Boolean(core().parseBoothItemReference(elements.itemUrlInput.value));
  elements.createButton.disabled = !isInteractive || !hasRootFolder || !hasValidUrl;
  elements.cancelButton.disabled = !isInteractive;
}

async function runBusy(action: () => Promise<void>, surfaceErrors = true): Promise<boolean> {
  if (state.isBusy) {
    return false;
  }

  state.isBusy = true;
  render();
  try {
    await action();
    return true;
  } catch (error) {
    console.error(error);
    if (surfaceErrors) {
      alert(error instanceof Error ? error.message : "不明なエラーが発生しました。");
    }
    throw error;
  } finally {
    state.isBusy = false;
    render();
  }
}

function normalizeItemUrl(value: unknown): string {
  return core().normalizeBoothItemUrl(value) || core().safeString(value).trim();
}

function core() {
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
