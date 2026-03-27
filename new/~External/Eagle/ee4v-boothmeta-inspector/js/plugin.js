const fs = require("fs/promises");
const path = require("path");
const https = require("https");

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
  bindEvents();
  renderUnsupported();
  renderButtons();
  setStatus("Eagle plugin の初期化を待っています。", "success");
});

eagle.onPluginCreate(async () => {
  state.isPluginReady = true;
  applyTheme(await Promise.resolve(eagle.app.theme));
  eagle.onThemeChanged(theme => applyTheme(theme));
  await reloadState("Inspector を読み込みました。");
});

eagle.onPluginShow(() => {
  if (state.isPluginReady) {
    reloadState("状態を再読込しました。");
  }
});

function cacheElements() {
  elements.unsupportedCard = document.getElementById("unsupported-card");
  elements.editorCard = document.getElementById("editor-card");
  elements.reloadButton = document.getElementById("reload-button");
  elements.itemUrlInput = document.getElementById("item-url-input");
  elements.boothItemIdInput = document.getElementById("booth-item-id-input");
  elements.nameInput = document.getElementById("name-input");
  elements.descriptionInput = document.getElementById("description-input");
  elements.thumbnailUrlInput = document.getElementById("thumbnail-url-input");
  elements.shopNameInput = document.getElementById("shop-name-input");
  elements.shopUrlInput = document.getElementById("shop-url-input");
  elements.shopThumbnailUrlInput = document.getElementById("shop-thumbnail-url-input");
  elements.tagsInput = document.getElementById("tags-input");
  elements.saveButton = document.getElementById("save-button");
  elements.refreshButton = document.getElementById("refresh-button");
  elements.openItemButton = document.getElementById("open-item-button");
  elements.metaInfo = document.getElementById("meta-info");
  elements.statusBanner = document.getElementById("status-banner");
}

function bindEvents() {
  elements.reloadButton.addEventListener("click", () => reloadState("状態を再読込しました。"));
  elements.saveButton.addEventListener("click", handleSave);
  elements.refreshButton.addEventListener("click", handleRefreshSnapshot);
  elements.openItemButton.addEventListener("click", async () => {
    if (state.item) {
      await state.item.open();
    }
  });
}

function applyTheme(theme) {
  document.body.setAttribute("theme", theme || "LIGHT");
}

async function reloadState(message) {
  if (!state.isPluginReady) {
    setStatus("Eagle plugin の初期化を待っています。", "success");
    return;
  }

  return runBusy(async () => {
    const selectedItems = await eagle.item.getSelected();
    const item = selectedItems[0] || null;

    if (!isBoothMetaItem(item)) {
      state.item = null;
      state.meta = null;
      renderUnsupported();
      setStatus(message, "success");
      return;
    }

    state.item = item;
    state.meta = await loadMetaFromItem(item);
    renderEditor();
    setStatus(message, "success");
  });
}

async function handleSave() {
  return runBusy(async () => {
    const item = requireMetaItem();
    const nextMeta = collectMetaFromForm();
    if (!nextMeta.attachedAt) {
      nextMeta.attachedAt = state.meta && state.meta.attachedAt ? state.meta.attachedAt : new Date().toISOString();
    }

    await saveMetaToItem(item, nextMeta);
    state.meta = nextMeta;
    renderEditor();
    setStatus("JSON を保存しました。", "success");
  });
}

async function handleRefreshSnapshot() {
  return runBusy(async () => {
    const item = requireMetaItem();
    const meta = collectMetaFromForm();
    const snapshot = await fetchBoothSnapshot(meta);
    const nextMeta = {
      ...meta,
      ...snapshot,
      attachedAt: meta.attachedAt || (state.meta && state.meta.attachedAt) || new Date().toISOString()
    };

    await saveMetaToItem(item, nextMeta);
    state.meta = nextMeta;
    renderEditor();
    setStatus("Booth snapshot を更新しました。", "success");
  });
}

async function loadMetaFromItem(item) {
  try {
    const raw = await fs.readFile(item.filePath, "utf8");
    return normalizeMeta(JSON.parse(raw));
  } catch (error) {
    return {
      ...DEFAULT_META,
      attachedAt: new Date().toISOString()
    };
  }
}

async function saveMetaToItem(item, meta) {
  const normalized = normalizeMeta(meta);
  const tempPath = path.join(await Promise.resolve(eagle.app.getPath("temp")), `${item.id}-boothmeta.json`);
  await fs.writeFile(tempPath, JSON.stringify(normalized, null, 2) + "\n", "utf8");
  await item.replaceFile(tempPath);
}

async function fetchBoothSnapshot(meta) {
  const itemUrl = normalizeBoothItemUrl(meta.itemUrl);
  if (!itemUrl) {
    throw new Error("Refresh には有効な Booth item URL が必要です。");
  }

  const payload = await requestJson(`${itemUrl}.json`);
  const boothItemId = toPositiveInteger(payload.id) || meta.boothItemId || extractBoothItemId(itemUrl);
  if (boothItemId <= 0) {
    throw new Error("Booth item ID を解決できませんでした。");
  }

  const itemUrlFromPayload = normalizeBoothItemUrl(payload.url) || itemUrl;
  const shopUrl = normalizeBoothShopUrl(firstNonEmpty([
    payload.shop && payload.shop.url,
    payload.shopUrl,
    `${new URL(itemUrlFromPayload).origin}`
  ]));

  return {
    boothItemId,
    itemUrl: itemUrlFromPayload,
    name: safeString(payload.name),
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

function collectMetaFromForm() {
  return normalizeMeta({
    schemaVersion: 1,
    boothItemId: toPositiveInteger(elements.boothItemIdInput.value),
    itemUrl: elements.itemUrlInput.value,
    name: elements.nameInput.value,
    description: elements.descriptionInput.value,
    thumbnailUrl: elements.thumbnailUrlInput.value,
    shopName: elements.shopNameInput.value,
    shopUrl: elements.shopUrlInput.value,
    shopThumbnailUrl: elements.shopThumbnailUrlInput.value,
    tags: splitTags(elements.tagsInput.value),
    attachedAt: state.meta ? state.meta.attachedAt : "",
    lastUpdatedAtUtc: state.meta ? state.meta.lastUpdatedAtUtc : ""
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

  elements.itemUrlInput.value = meta.itemUrl;
  elements.boothItemIdInput.value = meta.boothItemId > 0 ? String(meta.boothItemId) : "";
  elements.nameInput.value = meta.name;
  elements.descriptionInput.value = meta.description;
  elements.thumbnailUrlInput.value = meta.thumbnailUrl;
  elements.shopNameInput.value = meta.shopName;
  elements.shopUrlInput.value = meta.shopUrl;
  elements.shopThumbnailUrlInput.value = meta.shopThumbnailUrl;
  elements.tagsInput.value = meta.tags.join(", ");
  elements.metaInfo.textContent = `attachedAt: ${meta.attachedAt || "-"} / lastUpdatedAtUtc: ${meta.lastUpdatedAtUtc || "-"}`;
  renderButtons();
}

function renderButtons() {
  const disabled = state.isBusy || !state.item || !state.isPluginReady;
  elements.reloadButton.disabled = state.isBusy || !state.isPluginReady;
  elements.saveButton.disabled = disabled;
  elements.refreshButton.disabled = disabled;
  elements.openItemButton.disabled = disabled;
}

function requireMetaItem() {
  if (!state.item) {
    throw new Error("_boothmeta.json を選択してください。");
  }
  return state.item;
}

function normalizeMeta(meta) {
  return {
    schemaVersion: 1,
    boothItemId: toPositiveInteger(meta.boothItemId),
    itemUrl: normalizeBoothItemUrl(meta.itemUrl) || safeString(meta.itemUrl).trim(),
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
  if (!item) {
    return false;
  }

  const fileName = item.filePath ? path.basename(item.filePath).toLowerCase() : "";
  return item.ext === "json" && (item.name === "_boothmeta" || fileName === "_boothmeta.json");
}

function splitTags(value) {
  return String(value)
    .split(/[\r\n,]+/)
    .map(tag => tag.trim())
    .filter(Boolean);
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
  if (!url || !/\.booth\.pm$/i.test(url.hostname)) {
    return "";
  }

  const match = url.pathname.match(/^\/items\/(\d+)/i);
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

function setStatus(message, kind) {
  elements.statusBanner.textContent = message;
  elements.statusBanner.className = "status-banner";
  if (kind) {
    elements.statusBanner.classList.add(`is-${kind}`);
  }
}

async function runBusy(action) {
  if (state.isBusy) {
    return;
  }

  state.isBusy = true;
  renderButtons();
  try {
    await action();
  } catch (error) {
    console.error(error);
    setStatus(error.message || "不明なエラーが発生しました。", "error");
  } finally {
    state.isBusy = false;
    renderButtons();
  }
}
