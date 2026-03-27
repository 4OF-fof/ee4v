const fs = require("fs/promises");
const path = require("path");
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
  selectedFolders: [],
  activeFolder: null,
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
  await reloadSelection("選択 folder を読み込みました。");
});

eagle.onPluginRun(() => {
  if (state.isPluginReady) {
    reloadSelection("選択 folder を再読込しました。");
  }
});

eagle.onPluginShow(() => {
  if (state.isPluginReady) {
    reloadSelection("選択 folder を再読込しました。");
  }
});

function cacheElements() {
  elements.selectionCard = document.getElementById("selection-card");
  elements.reloadButton = document.getElementById("reload-button");
  elements.createButton = document.getElementById("create-button");
  elements.openFolderButton = document.getElementById("open-folder-button");
  elements.statusBanner = document.getElementById("status-banner");
}

function bindEvents() {
  elements.reloadButton.addEventListener("click", () => reloadSelection("選択 folder を再読込しました。"));
  elements.createButton.addEventListener("click", handleCreate);
  elements.openFolderButton.addEventListener("click", handleOpenFolder);
}

function applyTheme(theme) {
  document.body.setAttribute("theme", theme || "LIGHT");
}

async function reloadSelection(message) {
  if (!state.isPluginReady) {
    setStatus("Eagle plugin の初期化を待っています。", "success");
    return;
  }

  return runBusy(async () => {
    state.selectedFolders = await eagle.folder.getSelected();
    state.activeFolder = state.selectedFolders.length === 1 ? state.selectedFolders[0] : null;
    render();
    setStatus(message, "success");
  });
}

async function handleCreate() {
  return runBusy(async () => {
    const folder = requireActiveFolder();
    const validation = await validateTargetFolder(folder.id);
    if (!validation.isValid) {
      throw new Error(validation.message);
    }

    const tempDir = await Promise.resolve(eagle.app.getPath("temp"));
    const filePath = path.join(tempDir, `_boothmeta-${folder.id}.json`);
    const content = {
      ...DEFAULT_META,
      attachedAt: new Date().toISOString()
    };

    await fs.writeFile(filePath, JSON.stringify(content, null, 2) + "\n", "utf8");
    const itemId = await eagle.item.addFromPath(filePath, {
      folders: [folder.id],
      name: folder.name,
      tags: [BOOTH_META_TAG]
    });

    const item = await eagle.item.getById(itemId);
    item.tags = ensureBoothMetaTag(item.tags);
    await item.save();
    await eagle.item.select([itemId]);
    setStatus(`"${folder.name}" に BoothMeta タグ付き JSON を作成しました。`, "success");
  });
}

async function handleOpenFolder() {
  const folder = requireActiveFolder(false);
  if (!folder) {
    setStatus("folder を 1 つ選択してください。", "error");
    return;
  }

  await folder.open();
}

async function validateTargetFolder(folderId) {
  const folderChain = await getFolderChain(folderId);
  const rootFolder = folderChain[folderChain.length - 1];
  const targetFolder = folderChain[0];

  if (!rootFolder || rootFolder.name !== "VRCAsset") {
    return {
      isValid: false,
      message: "選択 folder は library 直下の VRCAsset 配下である必要があります。"
    };
  }

  if (targetFolder.id === rootFolder.id) {
    return {
      isValid: false,
      message: "VRCAsset 自体ではなく、その配下の folder を選択してください。"
    };
  }

  const existing = await findBoothMetaItem(folderId);
  if (existing) {
    return {
      isValid: false,
      message: "この folder には既に BoothMeta タグ付き JSON があります。"
    };
  }

  const ancestorIds = folderChain.slice(1, folderChain.length - 1).map(folder => folder.id);
  for (let index = 0; index < ancestorIds.length; index += 1) {
    const ancestorItem = await findBoothMetaItem(ancestorIds[index]);
    if (ancestorItem) {
      return {
        isValid: false,
        message: "祖先 folder に BoothMeta タグ付き JSON があるため、nested root は作成できません。"
      };
    }
  }

  const descendantRoot = await findDescendantBoothMetaItem(folderId);
  if (descendantRoot) {
    return {
      isValid: false,
      message: "子孫 folder に BoothMeta タグ付き JSON があるため、nested root は作成できません。"
    };
  }

  return {
    isValid: true,
    message: ""
  };
}

async function getFolderChain(folderId) {
  const chain = [];
  let current = await eagle.folder.getById(folderId);
  while (current) {
    chain.push(current);
    if (!current.parent) {
      break;
    }
    current = await eagle.folder.getById(current.parent);
  }
  return chain;
}

async function findBoothMetaItem(folderId) {
  const items = await eagle.item.get({
    folders: [folderId],
    ext: "json",
    fields: ["id", "name", "ext", "folders", "tags"]
  });

  return items.find(item => isBoothMetaItem(item) && Array.isArray(item.folders) && item.folders.includes(folderId)) || null;
}

async function findDescendantBoothMetaItem(folderId) {
  const allFolders = await eagle.folder.getAll();
  const descendantIds = allFolders
    .filter(folder => isDescendantFolder(folder, folderId, allFolders))
    .map(folder => folder.id);

  for (let index = 0; index < descendantIds.length; index += 1) {
    const descendantItem = await findBoothMetaItem(descendantIds[index]);
    if (descendantItem) {
      return descendantItem;
    }
  }

  return null;
}

function isDescendantFolder(folder, ancestorId, allFolders) {
  let currentParent = folder.parent;
  while (currentParent) {
    if (currentParent === ancestorId) {
      return true;
    }

    const nextFolder = allFolders.find(candidate => candidate.id === currentParent);
    currentParent = nextFolder ? nextFolder.parent : null;
  }

  return false;
}

function isBoothMetaItem(item) {
  if (!item) {
    return false;
  }

  return item.ext === "json" && hasBoothMetaTag(item.tags);
}

function hasBoothMetaTag(tags) {
  return Array.isArray(tags) && tags.some(tag => String(typeof tag === "string" ? tag : tag && tag.name || "").trim() === BOOTH_META_TAG);
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

function requireActiveFolder(shouldThrow = true) {
  if (state.activeFolder) {
    return state.activeFolder;
  }
  if (shouldThrow) {
    throw new Error("folder を 1 つ選択してください。");
  }
  return null;
}

function render() {
  renderSelection();
  renderButtons();
}

function renderSelection() {
  if (!state.activeFolder) {
    elements.selectionCard.className = "panel is-empty";
    elements.selectionCard.innerHTML = state.selectedFolders.length === 0
      ? "folder を 1 つ選択してください。"
      : "複数 folder が選択されています。1 つに絞ってください。";
    return;
  }

  elements.selectionCard.className = "panel";
  elements.selectionCard.innerHTML = [
    `<strong>${escapeHtml(state.activeFolder.name)}</strong>`,
    `<div class="muted">ID: ${escapeHtml(state.activeFolder.id)}</div>`,
    `<div class="muted">この folder 直下に BoothMeta タグ付き JSON を作成します。</div>`
  ].join("");
}

function renderButtons() {
  const isInteractive = state.isPluginReady && !state.isBusy;
  const hasActiveFolder = Boolean(state.activeFolder);
  elements.reloadButton.disabled = !isInteractive;
  elements.createButton.disabled = !isInteractive || !hasActiveFolder;
  elements.openFolderButton.disabled = !isInteractive || !hasActiveFolder;
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

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}
