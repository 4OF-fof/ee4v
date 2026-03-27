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
  rootFolder: null,
  isBusy: false,
  isPluginReady: false
};

const elements = {};

window.addEventListener("DOMContentLoaded", () => {
  cacheElements();
  bindEvents();
  render();
  setStatus("Eagle plugin の初期化を待っています。", "success");
});

eagle.onPluginCreate(async () => {
  state.isPluginReady = true;
  applyTheme(await Promise.resolve(eagle.app.theme));
  eagle.onThemeChanged(theme => applyTheme(theme));
  await reloadState("VRCAsset folder を読み込みました。");
});

eagle.onPluginRun(() => {
  if (state.isPluginReady) {
    reloadState("VRCAsset folder を再読込しました。");
  }
});

eagle.onPluginShow(() => {
  if (state.isPluginReady) {
    reloadState("VRCAsset folder を再読込しました。");
  }
});

function cacheElements() {
  elements.itemUrlInput = document.getElementById("item-url-input");
  elements.selectionCard = document.getElementById("selection-card");
  elements.reloadButton = document.getElementById("reload-button");
  elements.createButton = document.getElementById("create-button");
  elements.openFolderButton = document.getElementById("open-folder-button");
  elements.statusBanner = document.getElementById("status-banner");
}

function bindEvents() {
  elements.reloadButton.addEventListener("click", () => reloadState("VRCAsset folder を再読込しました。"));
  elements.createButton.addEventListener("click", handleCreate);
  elements.openFolderButton.addEventListener("click", handleOpenFolder);
  elements.itemUrlInput.addEventListener("input", () => renderButtons());
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
    state.rootFolder = await findVrcAssetRootFolder();
    render();
    setStatus(message, "success");
  });
}

async function handleCreate() {
  return runBusy(async () => {
    const rootFolder = requireRootFolder();
    const itemUrl = normalizeBoothItemUrl(elements.itemUrlInput.value);
    const boothItemId = extractBoothItemId(itemUrl);
    if (!itemUrl || boothItemId <= 0) {
      throw new Error("有効な Booth item URL を入力してください。");
    }

    const targetFolderName = String(boothItemId);
    const existingFolder = await findDirectChildFolder(rootFolder.id, targetFolderName);
    if (existingFolder) {
      throw new Error(`"${targetFolderName}" folder は既に存在します。`);
    }

    const snapshot = await fetchBoothSnapshot(itemUrl);
    const targetFolder = await eagle.folder.createSubfolder(rootFolder.id, {
      name: targetFolderName
    });

    const itemId = await createBoothMetaItem(targetFolder, itemUrl, snapshot);
    await targetFolder.open();
    await eagle.item.select([itemId]);
    setStatus(`"${targetFolderName}" folder と BoothMeta を作成しました。`, "success");
  });
}

async function handleOpenFolder() {
  const folder = state.rootFolder;
  if (!folder) {
    setStatus("VRCAsset folder が見つかりません。", "error");
    return;
  }

  await folder.open();
}

async function findVrcAssetRootFolder() {
  const folders = await eagle.folder.getAll();
  const matches = folders.filter(folder => folder.name === "VRCAsset" && !folder.parent);
  if (matches.length === 0) {
    throw new Error("library 直下の VRCAsset folder が見つかりません。");
  }
  if (matches.length > 1) {
    throw new Error("library 直下の VRCAsset folder が複数あります。");
  }
  return matches[0];
}

async function findDirectChildFolder(parentId, name) {
  const folders = await eagle.folder.getAll();
  return folders.find(folder => folder.parent === parentId && folder.name === name) || null;
}

async function createBoothMetaItem(folder, itemUrl, snapshot) {
  const tempDir = await Promise.resolve(eagle.app.getPath("temp"));
  const filePath = path.join(tempDir, `boothmeta-${folder.id}.json`);
  const content = normalizeMeta({
    ...DEFAULT_META,
    ...snapshot,
    boothItemId: snapshot.boothItemId || extractBoothItemId(itemUrl),
    itemUrl,
    name: snapshot.name || folder.name,
    attachedAt: new Date().toISOString(),
    lastUpdatedAtUtc: snapshot.lastUpdatedAtUtc || new Date().toISOString()
  });

  await fs.writeFile(filePath, JSON.stringify(content, null, 2) + "\n", "utf8");
  const itemId = await eagle.item.addFromPath(filePath, {
    folders: [folder.id],
    name: snapshot.name || folder.name,
    tags: [BOOTH_META_TAG]
  });

  const item = await eagle.item.getById(itemId);
  item.name = snapshot.name || folder.name;
  item.url = itemUrl;
  item.annotation = snapshot.description || "";
  item.tags = ensureBoothMetaTag(item.tags);
  await item.save();
  return itemId;
}

async function fetchBoothSnapshot(itemUrl) {
  const payload = await requestJson(`${itemUrl}.json`);
  const boothItemId = toPositiveInteger(payload.id) || extractBoothItemId(itemUrl);
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
        "User-Agent": "ee4v-eagle-boothmeta-create/0.1.0"
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

function requireRootFolder() {
  if (state.rootFolder) {
    return state.rootFolder;
  }
  throw new Error("VRCAsset folder が見つかりません。");
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

function ensureBoothMetaTag(tags) {
  const normalized = Array.isArray(tags)
    ? tags
      .map(tag => String(typeof tag === "string" ? tag : tag && tag.name || "").trim())
      .filter(Boolean)
    : [];

  if (!normalized.includes(BOOTH_META_TAG)) {
    normalized.push(BOOTH_META_TAG);
  }

  return Array.from(new Set(normalized));
}

function render() {
  renderSelection();
  renderButtons();
}

function renderSelection() {
  if (!state.rootFolder) {
    elements.selectionCard.className = "panel is-empty";
    elements.selectionCard.textContent = "VRCAsset folder が見つかりません。";
    return;
  }

  const itemUrl = normalizeBoothItemUrl(elements.itemUrlInput.value);
  const boothItemId = extractBoothItemId(itemUrl);
  elements.selectionCard.className = "panel";
  elements.selectionCard.innerHTML = [
    `<strong>${escapeHtml(state.rootFolder.name)}</strong>`,
    `<div class="muted">ID: ${escapeHtml(state.rootFolder.id)}</div>`,
    boothItemId > 0
      ? `<div class="muted">作成先: VRCAsset/${escapeHtml(String(boothItemId))}</div>`
      : `<div class="muted">URL 末尾の itemId を folder 名に使います。</div>`
  ].join("");
}

function renderButtons() {
  renderSelection();
  const isInteractive = state.isPluginReady && !state.isBusy;
  const hasRootFolder = Boolean(state.rootFolder);
  const hasValidUrl = extractBoothItemId(normalizeBoothItemUrl(elements.itemUrlInput.value)) > 0;
  elements.reloadButton.disabled = !isInteractive;
  elements.createButton.disabled = !isInteractive || !hasRootFolder || !hasValidUrl;
  elements.openFolderButton.disabled = !isInteractive || !hasRootFolder;
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

function normalizeTimestamp(value) {
  const trimmed = safeString(value).trim();
  if (!trimmed) {
    return "";
  }

  const date = new Date(trimmed);
  return Number.isNaN(date.getTime()) ? "" : date.toISOString();
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

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}
