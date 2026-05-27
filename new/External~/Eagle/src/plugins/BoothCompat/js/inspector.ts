(function () {
  "use strict";

interface InspectorState {
  item: EagleItem | null;
  meta: BoothMeta | null;
  isBusy: boolean;
  isPluginReady: boolean;
}

interface InspectorElements {
  unsupportedCard: HTMLElement;
  editorCard: HTMLElement;
  shopNameValue: HTMLElement;
  shopUrlValue: HTMLAnchorElement;
  tagsValue: HTMLElement;
  lastUpdatedValue: HTMLElement;
}

const state: InspectorState = {
  item: null,
  meta: null,
  isBusy: false,
  isPluginReady: false
};

const elements = {} as InspectorElements;

window.addEventListener("DOMContentLoaded", () => {
  cacheElements();
  renderUnsupported();
});

eagle.onPluginCreate(async () => {
  state.isPluginReady = true;
  applyTheme(await Promise.resolve(eagle.app.theme));
  eagle.onThemeChanged(theme => applyTheme(theme));
  await reloadState();
});

eagle.onPluginShow(() => {
  if (state.isPluginReady) {
    reloadState();
  }
});

eagle.onPluginRun(() => {
  if (state.isPluginReady) {
    reloadState();
  }
});

function cacheElements() {
  elements.unsupportedCard = requireElement("unsupported-card", HTMLElement);
  elements.editorCard = requireElement("editor-card", HTMLElement);
  elements.shopNameValue = requireElement("shop-name-value", HTMLElement);
  elements.shopUrlValue = requireElement("shop-url-value", HTMLAnchorElement);
  elements.tagsValue = requireElement("tags-value", HTMLElement);
  elements.lastUpdatedValue = requireElement("last-updated-value", HTMLElement);
}

function applyTheme(theme: EagleTheme) {
  document.body.setAttribute("theme", theme || "LIGHT");
}

async function reloadState() {
  if (!state.isPluginReady) {
    return;
  }

  return runBusy(async () => {
    const selectedItems = await eagle.item.getSelected();
    const selectedItem = selectedItems[0] || null;
    const item = selectedItem ? await eagle.item.getById(selectedItem.id) : null;

    if (!core().isBoothMetaItem(item)) {
      state.item = null;
      state.meta = null;
      renderUnsupported();
      return;
    }

    state.item = item;
    state.meta = await loadAndSyncMeta(item);
    renderEditor();
  });
}

async function loadAndSyncMeta(item: EagleItem): Promise<BoothMeta> {
  const storedMeta = await core().loadMetaFromItem(item);
  const itemUrl = normalizeItemUrl(item.url);
  const syncBase = core().normalizeMeta({
    ...storedMeta,
    itemUrl,
    name: core().safeString(item.name).trim(),
    description: core().safeString(item.annotation)
  });

  let nextMeta = syncBase;
  let shouldSaveMeta = !core().isMetaEquivalent(storedMeta, syncBase);
  let shouldSaveItem = false;

  if (itemUrl && (normalizeItemUrl(itemUrl) !== normalizeItemUrl(storedMeta.itemUrl) || !storedMeta.boothItemId)) {
    try {
      const snapshot = await fetchBoothSnapshot(itemUrl, syncBase, syncBase.name);
      const nextItemName = core().safeString(snapshot.name).trim() || syncBase.name;
      const nextItemUrl = snapshot.itemUrl || itemUrl;
      const nextItemDescription = core().safeString(snapshot.description);
      nextMeta = core().normalizeMeta({
        ...syncBase,
        ...snapshot,
        itemUrl: nextItemUrl,
        name: nextItemName,
        description: nextItemDescription
      });
      shouldSaveMeta = true;

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
    } catch (error) {
      console.error(error);
    }
  }

  const originalTags = Array.isArray(item.tags) ? item.tags : [];
  const normalizedTags = core().ensureBoothMetaTag(originalTags);
  if (JSON.stringify(originalTags) !== JSON.stringify(normalizedTags)) {
    item.tags = normalizedTags;
    shouldSaveItem = true;
  }

  if (shouldSaveItem) {
    await item.save();
  }

  if (shouldSaveMeta) {
    await core().saveMetaToItem(item, nextMeta);
  }

  renderEditorValues(nextMeta);
  return nextMeta;
}

async function fetchBoothSnapshot(itemUrl: string, meta: BoothMeta, fallbackName: string): Promise<Partial<BoothMeta>> {
  const resolvedItemUrl = normalizeItemUrl(itemUrl) || normalizeItemUrl(meta.itemUrl);
  if (!resolvedItemUrl) {
    return {};
  }

  const boothRef = core().parseBoothItemReference(resolvedItemUrl);
  if (!boothRef) {
    return {};
  }
  const snapshot = await core().fetchBoothSnapshot(boothRef);
  const boothItemId = snapshot.boothItemId || meta.boothItemId;
  if (boothItemId <= 0) {
    return {};
  }

  return {
    ...core().normalizeMeta(snapshot),
    boothItemId,
    itemUrl: snapshot.itemUrl || resolvedItemUrl,
    name: fallbackName || snapshot.name,
    attachedAt: meta.attachedAt,
    lastUpdatedAtUtc: snapshot.lastUpdatedAtUtc
  };
}

function renderUnsupported() {
  elements.editorCard.classList.add("hidden");
  elements.unsupportedCard.classList.remove("hidden");
}

function renderEditor() {
  elements.unsupportedCard.classList.add("hidden");
  elements.editorCard.classList.remove("hidden");
  renderEditorValues(state.meta || core().DEFAULT_META);
}

function renderEditorValues(meta: BoothMeta): void {
  elements.shopNameValue.textContent = meta.shopName || "-";
  refreshSelectedItemReference().then(() => {
    renderTagsValue(elements.tagsValue, meta.tags);
  }).catch(error => {
    console.error(error);
    renderTagsValue(elements.tagsValue, meta.tags);
  });
  renderLinkValue(elements.shopUrlValue, meta.shopUrl);
  elements.lastUpdatedValue.textContent = formatTokyoTimestamp(meta.lastUpdatedAtUtc);
}

function renderTagsValue(element: HTMLElement, tags: string[]): void {
  element.replaceChildren();
  const values = Array.isArray(tags) ? tags.filter(Boolean) : [];
  element.classList.toggle("value-empty", values.length === 0);

  if (values.length === 0) {
    element.textContent = "-";
    return;
  }

  values.forEach(tag => {
    const chip = document.createElement("button");
    chip.type = "button";
    chip.className = "tag-chip";
    chip.textContent = tag;
    const promoted = hasEagleTag(state.item, tag);
    chip.title = promoted ? "Eagle tag から削除" : "Eagle tag に追加";
    chip.classList.toggle("is-promoted", promoted);
    chip.addEventListener("click", () => {
      toggleTagToEagle(tag).catch(error => {
        console.error(error);
      });
    });
    element.appendChild(chip);
  });
}

async function toggleTagToEagle(tag: string): Promise<void> {
  const normalizedTag = core().safeString(tag).trim();
  if (!state.item || !normalizedTag) {
    return;
  }

  await refreshSelectedItemReference();
  const item = state.item;
  item.tags = hasEagleTag(item, normalizedTag)
    ? removeTag(item.tags, normalizedTag)
    : ensureTags(item.tags, normalizedTag);
  await item.save();
  state.item = await eagle.item.getById(item.id);
  renderEditorValues(state.meta || core().DEFAULT_META);
}

async function refreshSelectedItemReference() {
  if (!state.item) {
    return;
  }

  const item = await eagle.item.getById(state.item.id);
  if (item) {
    state.item = item;
  }
}

function hasEagleTag(item: EagleItem | null, tag: string): boolean {
  if (!item || !Array.isArray(item.tags)) {
    return false;
  }

  const normalizedTag = core().safeString(tag).trim();
  return item.tags.some(value => tagName(value) === normalizedTag);
}

function ensureTags(tags: unknown, tag: string): string[] {
  const normalized = Array.isArray(tags)
    ? tags.map(tagName).filter(Boolean)
    : [];

  if (!normalized.includes(tag)) {
    normalized.push(tag);
  }

  return Array.from(new Set(normalized));
}

function removeTag(tags: unknown, tag: string): string[] {
  const normalizedTag = core().safeString(tag).trim();
  return Array.isArray(tags)
    ? tags
      .map(tagName)
      .filter(value => value && value !== normalizedTag)
    : [];
}

function renderLinkValue(element: HTMLAnchorElement, url: string): void {
  const normalizedUrl = core().normalizeUrl(url);
  if (!normalizedUrl) {
    element.textContent = "-";
    element.href = "#";
    element.classList.add("is-empty");
    return;
  }

  element.textContent = normalizedUrl;
  element.href = normalizedUrl;
  element.classList.remove("is-empty");
}

function formatTokyoTimestamp(value: unknown): string {
  const normalized = core().normalizeTimestamp(value);
  if (!normalized) {
    return "-";
  }

  const formatter = new Intl.DateTimeFormat("ja-JP", {
    timeZone: "Asia/Tokyo",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false
  });
  const parts = formatter.formatToParts(new Date(normalized));
  const values = Object.fromEntries(parts.map(part => [part.type, part.value]));
  return `${values.year}/${values.month}/${values.day} ${values.hour}:${values.minute}`;
}

function normalizeItemUrl(value: unknown): string {
  return core().normalizeCanonicalBoothItemUrl(value) || core().safeString(value).trim();
}

function core() {
  return window.BoothCompatCore;
}

function tagName(tag: unknown): string {
  return core().safeString(typeof tag === "string" ? tag : tag && typeof tag === "object" ? (tag as EagleTag).name : "").trim();
}

async function runBusy(action: () => Promise<void>): Promise<void> {
  if (state.isBusy) {
    return;
  }

  state.isBusy = true;
  try {
    await action();
  } catch (error) {
    console.error(error);
  } finally {
    state.isBusy = false;
  }
}

function requireElement<T extends HTMLElement>(id: string, constructor: { new(): T }): T {
  const element = document.getElementById(id);
  if (!(element instanceof constructor)) {
    throw new Error(`Element #${id} was not found.`);
  }
  return element;
}
})();
