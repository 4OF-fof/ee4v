const state = {
  item: null,
  meta: null,
  isBusy: false,
  isPluginReady: false
};

const elements = {};

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
  elements.unsupportedCard = document.getElementById("unsupported-card");
  elements.editorCard = document.getElementById("editor-card");
  elements.shopNameValue = document.getElementById("shop-name-value");
  elements.shopUrlValue = document.getElementById("shop-url-value");
  elements.tagsValue = document.getElementById("tags-value");
  elements.lastUpdatedValue = document.getElementById("last-updated-value");
}

function applyTheme(theme) {
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

async function loadAndSyncMeta(item) {
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

async function fetchBoothSnapshot(itemUrl, meta, fallbackName) {
  const resolvedItemUrl = normalizeItemUrl(itemUrl) || normalizeItemUrl(meta.itemUrl);
  if (!resolvedItemUrl) {
    return {};
  }

  const boothRef = core().parseBoothItemReference(resolvedItemUrl);
  const snapshot = await core().fetchBoothSnapshot(boothRef);
  const boothItemId = snapshot.boothItemId || meta.boothItemId;
  if (boothItemId <= 0) {
    return {};
  }

  return {
    ...snapshot,
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

function renderEditorValues(meta) {
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

function renderTagsValue(element, tags) {
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

async function toggleTagToEagle(tag) {
  const normalizedTag = core().safeString(tag).trim();
  if (!state.item || !normalizedTag) {
    return;
  }

  await refreshSelectedItemReference();
  state.item.tags = hasEagleTag(state.item, normalizedTag)
    ? removeTag(state.item.tags, normalizedTag)
    : ensureTags(state.item.tags, normalizedTag);
  await state.item.save();
  state.item = await eagle.item.getById(state.item.id);
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

function hasEagleTag(item, tag) {
  if (!item || !Array.isArray(item.tags)) {
    return false;
  }

  const normalizedTag = core().safeString(tag).trim();
  return item.tags.some(value => core().safeString(typeof value === "string" ? value : value && value.name).trim() === normalizedTag);
}

function ensureTags(tags, tag) {
  const normalized = Array.isArray(tags)
    ? tags.map(value => core().safeString(typeof value === "string" ? value : value && value.name).trim()).filter(Boolean)
    : [];

  if (!normalized.includes(tag)) {
    normalized.push(tag);
  }

  return Array.from(new Set(normalized));
}

function removeTag(tags, tag) {
  const normalizedTag = core().safeString(tag).trim();
  return Array.isArray(tags)
    ? tags
      .map(value => core().safeString(typeof value === "string" ? value : value && value.name).trim())
      .filter(value => value && value !== normalizedTag)
    : [];
}

function renderLinkValue(element, url) {
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

function formatTokyoTimestamp(value) {
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

function normalizeItemUrl(value) {
  return core().normalizeCanonicalBoothItemUrl(value) || core().safeString(value).trim();
}

function core() {
  return window.BoothCompatCore;
}

async function runBusy(action) {
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
