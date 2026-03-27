const fs = require("fs/promises");
const path = require("path");
const https = require("https");

const BOOTH_META_TAG = "BoothMeta";

const DEFAULT_META = {
  schemaVersion: 1,
  boothItemId: 0,
  itemUrl: "",
  name: "",
  description: "",
  thumbnailUrl: "",
  shopName: "",
  shopUrl: "",
  shopThumbnailUrl: "",
  tags: [],
  attachedAt: "",
  lastUpdatedAtUtc: ""
};

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

function cacheElements() {
  elements.unsupportedCard = document.getElementById("unsupported-card");
  elements.editorCard = document.getElementById("editor-card");
  elements.boothItemIdValue = document.getElementById("booth-item-id-value");
  elements.thumbnailUrlValue = document.getElementById("thumbnail-url-value");
  elements.shopNameValue = document.getElementById("shop-name-value");
  elements.shopUrlValue = document.getElementById("shop-url-value");
  elements.shopThumbnailUrlValue = document.getElementById("shop-thumbnail-url-value");
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

    if (!isBoothMetaItem(item)) {
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

async function loadMetaFromItem(item) {
  try {
    const raw = await fs.readFile(item.filePath, "utf8");
    return normalizeMeta(JSON.parse(raw));
  } catch (error) {
    return { ...DEFAULT_META };
  }
}

async function loadAndSyncMeta(item) {
  const storedMeta = await loadMetaFromItem(item);
  const itemUrl = normalizeItemUrl(item.url);
  const syncBase = normalizeMeta({
    ...storedMeta,
    itemUrl,
    name: safeString(item.name).trim(),
    description: safeString(item.annotation)
  });

  let nextMeta = syncBase;
  let shouldSaveMeta = !isMetaEquivalent(storedMeta, syncBase);
  let shouldSaveItem = false;

  if (itemUrl && (normalizeBoothItemUrl(itemUrl) !== normalizeBoothItemUrl(storedMeta.itemUrl) || !storedMeta.boothItemId)) {
    try {
      const snapshot = await fetchBoothSnapshot(itemUrl, syncBase, syncBase.name);
      const nextItemName = safeString(snapshot.name).trim() || syncBase.name;
      const nextItemUrl = snapshot.itemUrl || itemUrl;
      const nextItemDescription = safeString(snapshot.description);
      nextMeta = normalizeMeta({
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
  const normalizedTags = ensureBoothMetaTag(originalTags);
  if (JSON.stringify(originalTags) !== JSON.stringify(normalizedTags)) {
    item.tags = normalizedTags;
    shouldSaveItem = true;
  }

  if (shouldSaveItem) {
    await item.save();
  }

  if (shouldSaveMeta) {
    await saveMetaToItem(item, nextMeta);
  }

  return nextMeta;
}

async function saveMetaToItem(item, meta) {
  const normalized = normalizeMeta(meta);
  const tempPath = path.join(await Promise.resolve(eagle.app.getPath("temp")), `${item.id}-boothmeta.json`);
  await fs.writeFile(tempPath, JSON.stringify(normalized, null, 2) + "\n", "utf8");
  await item.replaceFile(tempPath);
}

async function fetchBoothSnapshot(itemUrl, meta, fallbackName) {
  const resolvedItemUrl = normalizeBoothItemUrl(itemUrl) || normalizeBoothItemUrl(meta.itemUrl);
  if (!resolvedItemUrl) {
    return {};
  }

  const payload = await requestJson(`${resolvedItemUrl}.json`);
  const boothItemId = toPositiveInteger(payload.id) || meta.boothItemId || extractBoothItemId(resolvedItemUrl);
  if (boothItemId <= 0) {
    return {};
  }

  const itemUrlFromPayload = normalizeBoothItemUrl(payload.url) || resolvedItemUrl;
  const shopUrl = normalizeBoothShopUrl(firstNonEmpty([
    payload.shop && payload.shop.url,
    payload.shopUrl,
    `${new URL(itemUrlFromPayload).origin}`
  ]));

  return {
    boothItemId,
    itemUrl: itemUrlFromPayload,
    name: fallbackName || safeString(payload.name),
    description: safeString(payload.description),
    thumbnailUrl: normalizeUrl(firstNonEmpty([
      payload.thumbnailUrl,
      payload.thumbnail_url,
      payload.imageUrl,
      payload.image_url,
      payload.images && payload.images[0] && payload.images[0].original,
      payload.images && payload.images[0] && payload.images[0].url
    ])),
    shopName: safeString(firstNonEmpty([
      payload.shop && payload.shop.name,
      payload.shopName
    ])),
    shopUrl,
    shopThumbnailUrl: normalizeUrl(firstNonEmpty([
      payload.shop && payload.shop.thumbnailUrl,
      payload.shop && payload.shop.thumbnail_url,
      payload.shopThumbnailUrl
    ])),
    tags: normalizeTags(payload.tags),
    attachedAt: meta.attachedAt,
    lastUpdatedAtUtc: new Date().toISOString()
  };
}

async function requestJson(url, redirectDepth = 0) {
  if (redirectDepth > 4) {
    throw new Error("Booth item JSON のリダイレクト回数が上限を超えました。");
  }

  return new Promise((resolve, reject) => {
    const request = https.get(url, {
      headers: {
        Accept: "application/json",
        "User-Agent": "ee4v-eagle-boothmeta-inspector/0.1.0"
      }
    }, response => {
      const statusCode = response.statusCode || 0;
      const location = response.headers.location;

      if (statusCode >= 300 && statusCode < 400 && location) {
        response.resume();
        requestJson(new URL(location, url).toString(), redirectDepth + 1).then(resolve, reject);
        return;
      }

      if (statusCode < 200 || statusCode >= 300) {
        response.resume();
        reject(new Error(`Booth item JSON の取得に失敗しました。HTTP ${statusCode}`));
        return;
      }

      const chunks = [];
      response.setEncoding("utf8");
      response.on("data", chunk => chunks.push(chunk));
      response.on("end", () => {
        try {
          resolve(JSON.parse(chunks.join("")));
        } catch (error) {
          reject(new Error("Booth item JSON の解析に失敗しました。"));
        }
      });
    });

    request.on("error", error => {
      reject(new Error(`Booth item JSON の取得に失敗しました: ${error.message}`));
    });
    request.setTimeout(15000, () => {
      request.destroy(new Error("timeout"));
    });
  });
}

function renderUnsupported() {
  elements.editorCard.classList.add("hidden");
  elements.unsupportedCard.classList.remove("hidden");
}

function renderEditor() {
  const meta = state.meta || DEFAULT_META;
  elements.unsupportedCard.classList.add("hidden");
  elements.editorCard.classList.remove("hidden");

  elements.boothItemIdValue.textContent = meta.boothItemId > 0 ? String(meta.boothItemId) : "-";
  elements.thumbnailUrlValue.textContent = meta.thumbnailUrl || "-";
  elements.shopNameValue.textContent = meta.shopName || "-";
  elements.shopUrlValue.textContent = meta.shopUrl || "-";
  elements.shopThumbnailUrlValue.textContent = meta.shopThumbnailUrl || "-";
  elements.tagsValue.textContent = meta.tags.length > 0 ? meta.tags.join(", ") : "-";
  elements.lastUpdatedValue.textContent = meta.lastUpdatedAtUtc || "-";
}

function requireMetaItem() {
  if (!state.item) {
    throw new Error("BoothMeta タグ付き JSON item を選択してください。");
  }
  return state.item;
}

function normalizeMeta(meta) {
  return {
    schemaVersion: 1,
    boothItemId: toPositiveInteger(meta.boothItemId),
    itemUrl: normalizeItemUrl(meta.itemUrl),
    name: safeString(meta.name),
    description: safeString(meta.description),
    thumbnailUrl: normalizeUrl(meta.thumbnailUrl),
    shopName: safeString(meta.shopName),
    shopUrl: normalizeBoothShopUrl(meta.shopUrl) || normalizeUrl(meta.shopUrl),
    shopThumbnailUrl: normalizeUrl(meta.shopThumbnailUrl),
    tags: normalizeTags(meta.tags),
    attachedAt: normalizeTimestamp(meta.attachedAt),
    lastUpdatedAtUtc: normalizeTimestamp(meta.lastUpdatedAtUtc)
  };
}

function isBoothMetaItem(item) {
  return Boolean(item) && item.ext === "json" && hasBoothMetaTag(item.tags);
}

function hasBoothMetaTag(tags) {
  return Array.isArray(tags) && tags.some(tag => safeString(typeof tag === "string" ? tag : tag && tag.name).trim() === BOOTH_META_TAG);
}

function ensureBoothMetaTag(tags) {
  const normalized = Array.isArray(tags)
    ? tags
      .map(tag => safeString(typeof tag === "string" ? tag : tag && tag.name).trim())
      .filter(Boolean)
    : [];

  if (!normalized.includes(BOOTH_META_TAG)) {
    normalized.push(BOOTH_META_TAG);
  }

  return Array.from(new Set(normalized));
}

function isMetaEquivalent(left, right) {
  return JSON.stringify(normalizeMeta(left || DEFAULT_META)) === JSON.stringify(normalizeMeta(right || DEFAULT_META));
}

function normalizeTags(tags) {
  if (!Array.isArray(tags)) {
    return [];
  }

  const values = tags
    .map(tag => safeString(typeof tag === "string" ? tag : tag && tag.name))
    .map(tag => tag.trim())
    .filter(Boolean);

  return Array.from(new Set(values)).sort((left, right) => left.localeCompare(right, "ja"));
}

function normalizeTimestamp(value) {
  const trimmed = safeString(value).trim();
  if (!trimmed) {
    return "";
  }

  const date = new Date(trimmed);
  return Number.isNaN(date.getTime()) ? "" : date.toISOString();
}

function normalizeBoothItemUrl(value) {
  const url = tryCreateUrl(value);
  if (!url || !/(?:^|\.)booth\.pm$/i.test(url.hostname)) {
    return "";
  }

  const match = url.pathname.match(/^\/(?:(?:[a-z]{2,8}(?:[-_][a-z]{2,8})*)\/)?items\/(\d+)(?:\/)?$/i);
  if (!match) {
    return "";
  }

  return `https://${url.hostname.toLowerCase()}/items/${match[1]}`;
}

function normalizeBoothShopUrl(value) {
  const url = tryCreateUrl(value);
  if (!url || !/\.booth\.pm$/i.test(url.hostname)) {
    return "";
  }

  return `https://${url.hostname.toLowerCase()}`;
}

function normalizeUrl(value) {
  const url = tryCreateUrl(value);
  return url ? url.toString() : "";
}

function normalizeItemUrl(value) {
  return normalizeBoothItemUrl(value) || safeString(value).trim();
}

function tryCreateUrl(value) {
  const trimmed = safeString(value).trim();
  if (!trimmed) {
    return null;
  }

  try {
    return new URL(trimmed);
  } catch (error) {
    return null;
  }
}

function extractBoothItemId(itemUrl) {
  const normalized = normalizeBoothItemUrl(itemUrl);
  if (!normalized) {
    return 0;
  }

  const match = normalized.match(/\/items\/(\d+)$/);
  return match ? parseInt(match[1], 10) : 0;
}

function toPositiveInteger(value) {
  const parsed = typeof value === "number" ? value : parseInt(String(value), 10);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : 0;
}

function safeString(value) {
  return typeof value === "string" ? value : "";
}

function firstNonEmpty(values) {
  for (let index = 0; index < values.length; index += 1) {
    const value = safeString(values[index]).trim();
    if (value) {
      return value;
    }
  }
  return "";
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
