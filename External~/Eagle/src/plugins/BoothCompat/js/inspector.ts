(function () {
  "use strict";

  interface InspectorState {
    item: EagleItem | null;
    meta: BoothMeta | null;
    isDomReady: boolean;
    isPluginReady: boolean;
    isReloading: boolean;
    isTagBusy: boolean;
    reloadRevision: number;
  }

  interface InspectorElements {
    loadingCard: HTMLElement;
    loadingMessage: HTMLElement;
    unsupportedCard: HTMLElement;
    unsupportedMessage: HTMLElement;
    editorCard: HTMLElement;
    shopNameLabel: HTMLElement;
    shopNameValue: HTMLElement;
    shopUrlLabel: HTMLElement;
    shopUrlValue: HTMLAnchorElement;
    tagsLabel: HTMLElement;
    tagsValue: HTMLElement;
    lastUpdatedLabel: HTMLElement;
    lastUpdatedValue: HTMLElement;
  }

  const state: InspectorState = {
    item: null,
    meta: null,
    isDomReady: false,
    isPluginReady: false,
    isReloading: false,
    isTagBusy: false,
    reloadRevision: 0
  };

  const elements = {} as InspectorElements;

  window.addEventListener("DOMContentLoaded", () => {
    cacheElements();
    state.isDomReady = true;
    localizeDocument();
    renderLoading();
  });

  eagle.onPluginCreate(async () => {
    state.isPluginReady = true;
    localizeDocument();
    await applyTheme(await Promise.resolve(eagle.app.theme));
    eagle.onThemeChanged(theme => {
      applyTheme(theme).catch(console.error);
    });
    eagle.onLibraryChanged(() => {
      reloadState().catch(console.error);
    });
    await reloadState();
  });

  eagle.onPluginShow(() => {
    if (state.isPluginReady) {
      reloadState().catch(console.error);
    }
  });

  eagle.onPluginRun(() => {
    if (state.isPluginReady) {
      reloadState().catch(console.error);
    }
  });

  function cacheElements(): void {
    elements.loadingCard = requireElement("loading-card", HTMLElement);
    elements.loadingMessage = requireElement("loading-message", HTMLElement);
    elements.unsupportedCard = requireElement("unsupported-card", HTMLElement);
    elements.unsupportedMessage = requireElement("unsupported-message", HTMLElement);
    elements.editorCard = requireElement("editor-card", HTMLElement);
    elements.shopNameLabel = requireElement("shop-name-label", HTMLElement);
    elements.shopNameValue = requireElement("shop-name-value", HTMLElement);
    elements.shopUrlLabel = requireElement("shop-url-label", HTMLElement);
    elements.shopUrlValue = requireElement("shop-url-value", HTMLAnchorElement);
    elements.tagsLabel = requireElement("tags-label", HTMLElement);
    elements.tagsValue = requireElement("tags-value", HTMLElement);
    elements.lastUpdatedLabel = requireElement("last-updated-label", HTMLElement);
    elements.lastUpdatedValue = requireElement("last-updated-value", HTMLElement);
  }

  function localizeDocument(): void {
    if (!state.isDomReady) {
      return;
    }

    document.documentElement.lang = normalizeLocale(eagle.app.locale);
    document.title = t("inspector.title", "BOOTH info");
    elements.loadingMessage.textContent = t("inspector.loading", "Loading BOOTH metadata…");
    elements.unsupportedMessage.textContent = t("inspector.unsupported", "Select a JSON item tagged BoothMeta.");
    elements.shopNameLabel.textContent = t("inspector.shopName", "Shop name");
    elements.tagsLabel.textContent = t("inspector.tags", "Tags");
    elements.shopUrlLabel.textContent = t("inspector.shopUrl", "Shop URL");
    elements.lastUpdatedLabel.textContent = t("inspector.lastUpdated", "Last updated");
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

  async function reloadState(): Promise<void> {
    if (!state.isPluginReady) {
      return;
    }

    const revision = ++state.reloadRevision;
    state.isReloading = true;
    renderLoading();

    try {
      const selectedItems = await eagle.item.getSelected();
      if (revision !== state.reloadRevision) {
        return;
      }

      if (selectedItems.length !== 1) {
        state.item = null;
        state.meta = null;
        renderUnsupported();
        return;
      }

      const item = await eagle.item.getById(selectedItems[0].id);
      if (revision !== state.reloadRevision) {
        return;
      }

      if (!core().isBoothMetaItem(item)) {
        state.item = null;
        state.meta = null;
        renderUnsupported();
        return;
      }

      const meta = await loadAndSyncMeta(item);
      if (revision !== state.reloadRevision) {
        return;
      }

      state.item = item;
      state.meta = meta;
      renderEditor();
    } catch (error) {
      if (revision === state.reloadRevision) {
        console.error(error);
        state.item = null;
        state.meta = null;
        renderUnsupported();
      }
    } finally {
      if (revision === state.reloadRevision) {
        state.isReloading = false;
      }
    }
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

  function renderLoading(): void {
    if (!state.isDomReady) {
      return;
    }
    elements.loadingCard.classList.remove("hidden");
    elements.unsupportedCard.classList.add("hidden");
    elements.editorCard.classList.add("hidden");
  }

  function renderUnsupported(): void {
    if (!state.isDomReady) {
      return;
    }
    elements.loadingCard.classList.add("hidden");
    elements.editorCard.classList.add("hidden");
    elements.unsupportedCard.classList.remove("hidden");
  }

  function renderEditor(): void {
    if (!state.isDomReady) {
      return;
    }
    elements.loadingCard.classList.add("hidden");
    elements.unsupportedCard.classList.add("hidden");
    elements.editorCard.classList.remove("hidden");
    renderEditorValues(state.meta || core().DEFAULT_META);
  }

  function renderEditorValues(meta: BoothMeta): void {
    elements.shopNameValue.textContent = meta.shopName || "-";
    renderTagsValue(elements.tagsValue, meta.tags);
    renderLinkValue(elements.shopUrlValue, meta.shopUrl);
    elements.lastUpdatedValue.textContent = formatTimestamp(meta.lastUpdatedAtUtc);
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
      const promoted = hasEagleTag(state.item, tag);
      chip.type = "button";
      chip.className = "tag-chip";
      chip.textContent = tag;
      chip.title = promoted
        ? t("inspector.removeTag", "Remove {{tag}} from Eagle tags", { tag })
        : t("inspector.addTag", "Add {{tag}} to Eagle tags", { tag });
      chip.setAttribute("aria-pressed", String(promoted));
      chip.disabled = state.isTagBusy;
      chip.classList.toggle("is-promoted", promoted);
      chip.addEventListener("click", () => {
        toggleTagToEagle(tag).catch(console.error);
      });
      element.appendChild(chip);
    });
  }

  async function toggleTagToEagle(tag: string): Promise<void> {
    const normalizedTag = core().safeString(tag).trim();
    const currentItem = state.item;
    if (!currentItem || !normalizedTag || state.isTagBusy) {
      return;
    }

    state.isTagBusy = true;
    renderTagsValue(elements.tagsValue, state.meta ? state.meta.tags : []);
    try {
      const item = await eagle.item.getById(currentItem.id);
      if (!item) {
        return;
      }

      item.tags = hasEagleTag(item, normalizedTag)
        ? removeTag(item.tags, normalizedTag)
        : ensureTags(item.tags, normalizedTag);
      await item.save();

      if (state.item && state.item.id === item.id) {
        state.item = await eagle.item.getById(item.id);
      }
    } finally {
      state.isTagBusy = false;
      if (state.meta) {
        renderTagsValue(elements.tagsValue, state.meta.tags);
      }
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
      element.removeAttribute("aria-label");
      return;
    }

    element.textContent = normalizedUrl;
    element.href = normalizedUrl;
    element.setAttribute("aria-label", normalizedUrl);
    element.classList.remove("is-empty");
  }

  function formatTimestamp(value: unknown): string {
    const normalized = core().normalizeTimestamp(value);
    if (!normalized) {
      return "-";
    }

    return new Intl.DateTimeFormat(normalizeLocale(eagle.app.locale), {
      timeZone: "Asia/Tokyo",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false
    }).format(new Date(normalized));
  }

  function normalizeLocale(locale: unknown): string {
    return core().safeString(locale).replace("_", "-") || "en";
  }

  function normalizeItemUrl(value: unknown): string {
    return core().normalizeCanonicalBoothItemUrl(value) || core().safeString(value).trim();
  }

  function tagName(tag: unknown): string {
    return core().safeString(typeof tag === "string" ? tag : tag && typeof tag === "object" ? (tag as EagleTag).name : "").trim();
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
