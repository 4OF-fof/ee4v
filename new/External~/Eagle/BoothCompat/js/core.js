(function () {
  "use strict";

  const fs = require("fs/promises");
  const path = require("path");
  const https = require("https");

  const BOOTH_META_TAG = "BoothMeta";
  const BOOTH_META_TAGS = [BOOTH_META_TAG, "VRCMeta"];

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
    lastUpdatedAtUtc: "",
    downloads: []
  };

  async function ensureBoothMetaForUrl(itemUrl) {
    const boothRef = parseBoothItemReference(itemUrl);
    if (!boothRef) {
      throw new Error("有効な Booth item URL を入力してください。");
    }

    return ensureBoothMetaForProduct({
      boothItemId: boothRef.itemId,
      itemUrl: boothRef.normalizedUrl
    });
  }

  async function ensureBoothMetaForProduct(product, snapshotOverride) {
    const existing = findExistingBoothMeta(await loadBoothMetaItemsByTag(), product);
    if (existing) {
      return {
        ...existing,
        rootFolder: null,
        created: false
      };
    }

    const rootFolder = await requireVrcAssetRootFolder();
    const snapshot = await resolveBoothSnapshot(product, snapshotOverride);
    const targetFolderName = resolveBoothFolderName(snapshot.name || product.name, snapshot.boothItemId || product.boothItemId);
    let targetFolder = await findDirectChildFolder(rootFolder.id, targetFolderName);
    if (!targetFolder) {
      targetFolder = await eagle.folder.createSubfolder(rootFolder.id, {
        name: targetFolderName
      });
    }

    const itemUrl = normalizeBoothItemUrl(snapshot.itemUrl || product.itemUrl);
    const now = new Date().toISOString();
    const meta = normalizeMeta({
      ...DEFAULT_META,
      ...snapshot,
      boothItemId: snapshot.boothItemId || product.boothItemId,
      itemUrl,
      name: snapshot.name || product.name || targetFolderName,
      description: snapshot.description || product.description,
      thumbnailUrl: snapshot.thumbnailUrl || product.thumbnailUrl,
      shopName: snapshot.shopName || product.shopName,
      shopUrl: snapshot.shopUrl || product.shopUrl,
      shopThumbnailUrl: snapshot.shopThumbnailUrl || product.shopThumbnailUrl,
      tags: snapshot.tags || product.tags,
      attachedAt: now,
      lastUpdatedAtUtc: snapshot.lastUpdatedAtUtc || now
    });

    const tempDir = await Promise.resolve(eagle.app.getPath("temp"));
    const filePath = path.join(tempDir, `ee4v-boothmeta-${Date.now()}.json`);
    await fs.writeFile(filePath, JSON.stringify(meta, null, 2) + "\n", "utf8");

    const itemId = await eagle.item.addFromPath(filePath, {
      folders: [targetFolder.id],
      name: meta.name || targetFolderName,
      tags: [BOOTH_META_TAG]
    });

    const item = await eagle.item.getById(itemId);
    item.name = meta.name || targetFolderName;
    item.url = itemUrl;
    item.annotation = meta.description || "";
    item.tags = ensureBoothMetaTag(item.tags);
    await item.save();
    await applyThumbnailToItem(item, meta.thumbnailUrl, tempDir);

    return {
      item,
      meta,
      folder: targetFolder,
      rootFolder,
      created: true
    };
  }

  async function resolveBoothSnapshot(product, snapshotOverride) {
    if (snapshotOverride) {
      return normalizeSnapshot(snapshotOverride);
    }

    const boothRef = parseProductBoothReference(product);
    if (!boothRef) {
      return normalizeSnapshot(product || {});
    }

    let fetched = null;
    try {
      fetched = await fetchBoothSnapshot(boothRef);
    } catch (error) {
      console.warn(`Failed to fetch Booth snapshot: ${error.message}`);
      fetched = {};
    }

    return normalizeSnapshot({
      ...product,
      ...fetched,
      boothItemId: fetched.boothItemId || boothRef.itemId || product.boothItemId,
      itemUrl: fetched.itemUrl || boothRef.normalizedUrl || product.itemUrl
    });
  }

  function parseProductBoothReference(product) {
    const itemRef = parseBoothItemReference(product && product.itemUrl);
    const shopUrl = normalizeBoothShopUrl(product && product.shopUrl);
    if (!itemRef || !shopUrl) {
      return itemRef;
    }

    try {
      const shopHost = new URL(shopUrl).hostname.toLowerCase();
      if (shopHost && shopHost !== "booth.pm") {
        return {
          ...itemRef,
          fetchUrl: `${shopUrl}/items/${itemRef.itemId}`
        };
      }
    } catch (error) {
      return itemRef;
    }

    return itemRef;
  }

  async function loadBoothMetaItems(rootFolder) {
    const items = await getAllItems();
    const folders = await eagle.folder.getAll();
    const descendantFolderIds = new Set(findDescendantFolderIds(folders, rootFolder.id));
    const records = [];

    for (let index = 0; index < items.length; index += 1) {
      const item = items[index];
      if (!isJsonLikeItem(item)) {
        continue;
      }

      const itemFolderIds = getItemFolderIds(item);
      const folderId = itemFolderIds.find(id => descendantFolderIds.has(id));
      if (!folderId) {
        continue;
      }

      const meta = await loadMetaFromItem(item);
      if (!isBoothMetaItem(item) && !isBoothMetaMeta(meta)) {
        continue;
      }

      records.push({
        item,
        folder: folders.find(folder => folder.id === folderId) || null,
        meta
      });
    }

    return records;
  }

  async function loadBoothMetaItemsByTag() {
    const items = await getAllItems();
    const folders = await eagle.folder.getAll();
    const records = [];

    for (let index = 0; index < items.length; index += 1) {
      const item = items[index];
      if (!isBoothMetaItem(item)) {
        continue;
      }

      const meta = await loadMetaFromItem(item);
      if (!isBoothMetaMeta(meta)) {
        continue;
      }

      const folderId = getItemFolderIds(item)[0] || "";
      records.push({
        item,
        folder: folders.find(folder => folder.id === folderId) || (folderId ? { id: folderId } : null),
        meta
      });
    }

    return records;
  }

  function findExistingBoothMeta(records, product) {
    const boothItemId = toPositiveInteger(product && product.boothItemId);
    if (boothItemId > 0) {
      const matchById = records.find(record => toPositiveInteger(record.meta && record.meta.boothItemId) === boothItemId);
      if (matchById) {
        return matchById;
      }
    }

    return records.find(record => isSameProduct(record.meta, product)) || null;
  }

  async function getAllItems() {
    if (eagle.item && typeof eagle.item.getAll === "function") {
      return await eagle.item.getAll();
    }

    if (eagle.item && typeof eagle.item.getItems === "function") {
      return await eagle.item.getItems();
    }

    return [];
  }

  async function requireVrcAssetRootFolder() {
    const rootFolder = await findVrcAssetRootFolder();
    if (!rootFolder) {
      throw new Error("library root VRCAsset folder was not found.");
    }

    return rootFolder;
  }

  async function findVrcAssetRootFolder() {
    const folders = await eagle.folder.getAll();
    const matches = folders.filter(folder => folder.name === "VRCAsset" && !folder.parent);
    if (matches.length !== 1) {
      return null;
    }

    return matches[0];
  }

  async function findDirectChildFolder(parentId, name) {
    const folders = await eagle.folder.getAll();
    return folders.find(folder => folder.parent === parentId && folder.name === name) || null;
  }

  async function loadMetaFromItem(item) {
    try {
      const raw = await fs.readFile(item.filePath, "utf8");
      return normalizeMeta(JSON.parse(raw));
    } catch (error) {
      return { ...DEFAULT_META };
    }
  }

  async function saveMetaToItem(item, meta) {
    const tempPath = path.join(await Promise.resolve(eagle.app.getPath("temp")), `${item.id}-boothcompat.json`);
    await fs.writeFile(tempPath, JSON.stringify(normalizeMeta(meta), null, 2) + "\n", "utf8");
    await item.replaceFile(tempPath);
  }

  async function applyThumbnailToItem(item, thumbnailUrl, tempDir) {
    const normalizedThumbnailUrl = normalizeUrl(thumbnailUrl);
    if (!normalizedThumbnailUrl) {
      return;
    }

    try {
      const extension = path.extname(new URL(normalizedThumbnailUrl).pathname || "").toLowerCase() || ".jpg";
      const thumbnailPath = path.join(tempDir, `boothmeta-thumb-${item.id}${extension.length <= 5 ? extension : ".jpg"}`);
      await downloadFile(normalizedThumbnailUrl, thumbnailPath);
      await item.setCustomThumbnail(thumbnailPath);
    } catch (error) {
      console.warn(`Failed to apply custom thumbnail: ${error.message}`);
    }
  }

  async function fetchBoothSnapshot(boothRef) {
    const payload = await requestJson(`${boothRef.fetchUrl}.json`);
    const boothItemId = toPositiveInteger(payload.id) || boothRef.itemId;
    const itemUrlFromPayload = normalizeCanonicalBoothItemUrl(payload.url) || normalizeCanonicalBoothItemUrl(boothRef.normalizedUrl) || boothRef.normalizedUrl;
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

  function findDescendantFolderIds(folders, rootId) {
    const result = [rootId];
    const queue = [rootId];
    while (queue.length > 0) {
      const parentId = queue.shift();
      folders
        .filter(folder => folder.parent === parentId)
        .forEach(folder => {
          result.push(folder.id);
          queue.push(folder.id);
        });
    }

    return result;
  }

  function parseBoothItemReference(value) {
    const url = tryCreateUrl(value);
    if (!url || !/(?:^|\.)booth\.pm$/i.test(url.hostname)) {
      return null;
    }

    const match = url.pathname.match(/^\/(?:(?:[a-z]{2,8}(?:[-_][a-z]{2,8})*)\/)?items\/(\d+)(?:\/)?$/i);
    if (!match) {
      return null;
    }

    const itemId = parseInt(match[1], 10);
    const host = url.hostname.toLowerCase();
    if (host === "booth.pm") {
      const localeMatch = url.pathname.match(/^\/([a-z]{2,8}(?:[-_][a-z]{2,8})*)\/items\/\d+(?:\/)?$/i);
      const localePath = localeMatch ? `/${localeMatch[1].toLowerCase()}` : "";
      return {
        itemId,
        fetchUrl: `https://booth.pm${localePath}/items/${itemId}`,
        normalizedUrl: `https://booth.pm${localePath}/items/${itemId}`
      };
    }

    return {
      itemId,
      fetchUrl: `https://${host}/items/${itemId}`,
      normalizedUrl: `https://${host}/items/${itemId}`
    };
  }

  function normalizeMeta(meta) {
    return {
      schemaVersion: 1,
      boothItemId: toPositiveInteger(meta && meta.boothItemId),
      itemUrl: normalizeBoothItemUrl(meta && meta.itemUrl) || safeString(meta && meta.itemUrl).trim(),
      name: safeString(meta && meta.name),
      description: safeString(meta && meta.description),
      thumbnailUrl: normalizeUrl(meta && meta.thumbnailUrl),
      shopName: safeString(meta && meta.shopName),
      shopUrl: normalizeBoothShopUrl(meta && meta.shopUrl) || normalizeUrl(meta && meta.shopUrl),
      shopThumbnailUrl: normalizeUrl(meta && meta.shopThumbnailUrl),
      tags: normalizeTags(meta && meta.tags),
      attachedAt: normalizeTimestamp(meta && meta.attachedAt),
      lastUpdatedAtUtc: normalizeTimestamp(meta && meta.lastUpdatedAtUtc),
      downloads: normalizeDownloads(meta && meta.downloads)
    };
  }

  function normalizeDownloads(downloads) {
    if (!Array.isArray(downloads)) {
      return [];
    }

    return downloads.map(download => ({
      downloadUrl: normalizeDownloadUrl(download.downloadUrl),
      downloadId: toPositiveInteger(download.downloadId) || extractDownloadId(download.downloadUrl),
      filename: safeString(download.filename),
      requestedAt: normalizeTimestamp(download.requestedAt),
      importedAt: normalizeTimestamp(download.importedAt),
      importedItemIds: Array.isArray(download.importedItemIds)
        ? download.importedItemIds.map(value => safeString(value)).filter(Boolean)
        : []
    }));
  }

  function normalizeSnapshot(value) {
    return {
      boothItemId: toPositiveInteger(value.boothItemId),
      itemUrl: normalizeBoothItemUrl(value.itemUrl),
      name: safeString(value.name),
      description: safeString(value.description),
      thumbnailUrl: normalizeUrl(value.thumbnailUrl),
      shopName: safeString(value.shopName),
      shopUrl: normalizeBoothShopUrl(value.shopUrl) || normalizeUrl(value.shopUrl),
      shopThumbnailUrl: normalizeUrl(value.shopThumbnailUrl),
      tags: normalizeTags(value.tags),
      lastUpdatedAtUtc: normalizeTimestamp(value.lastUpdatedAtUtc) || new Date().toISOString()
    };
  }

  function isSameProduct(meta, product) {
    const left = normalizeMeta(meta || DEFAULT_META);
    const rightItemId = toPositiveInteger(product.boothItemId);
    const rightUrl = normalizeBoothItemUrl(product.itemUrl);
    return (left.boothItemId > 0 && rightItemId > 0 && left.boothItemId === rightItemId)
      || (left.itemUrl && rightUrl && left.itemUrl === rightUrl);
  }

  function isBoothMetaItem(item) {
    return Boolean(item)
      && !item.isDeleted
      && isJsonLikeItem(item)
      && hasBoothMetaTag(item.tags);
  }

  function isJsonLikeItem(item) {
    if (!item) {
      return false;
    }

    if (safeString(item.ext).replace(/^\./, "").toLowerCase() === "json") {
      return true;
    }

    return [item.name, item.filePath]
      .some(value => safeString(value).toLowerCase().endsWith(".json"));
  }

  function isBoothMetaMeta(meta) {
    const normalized = normalizeMeta(meta || DEFAULT_META);
    return normalized.schemaVersion === 1
      && normalized.boothItemId > 0
      && Boolean(normalized.itemUrl);
  }

  function hasBoothMetaTag(tags) {
    return Array.isArray(tags)
      && tags.some(tag => BOOTH_META_TAGS.includes(safeString(typeof tag === "string" ? tag : tag && tag.name).trim()));
  }

  function ensureBoothMetaTag(tags) {
    const normalized = Array.isArray(tags)
      ? tags.map(tag => safeString(typeof tag === "string" ? tag : tag && tag.name).trim()).filter(Boolean)
      : [];

    if (!normalized.includes(BOOTH_META_TAG)) {
      normalized.push(BOOTH_META_TAG);
    }

    return Array.from(new Set(normalized));
  }

  function getItemFolderIds(item) {
    if (Array.isArray(item.folders)) {
      return item.folders;
    }

    if (Array.isArray(item.folderIds)) {
      return item.folderIds;
    }

    if (item.folderId) {
      return [item.folderId];
    }

    return [];
  }

  function buildDownloadKey(download) {
    const downloadId = toPositiveInteger(download.downloadId) || extractDownloadId(download.downloadUrl);
    if (downloadId > 0) {
      return `download:${downloadId}`;
    }

    const downloadUrl = normalizeDownloadUrl(download.downloadUrl);
    if (downloadUrl) {
      return `url:${downloadUrl}`;
    }

    return `filename:${normalizeFilename(download.filename)}`;
  }

  function extractDownloadId(downloadUrl) {
    const normalized = normalizeDownloadUrl(downloadUrl);
    const match = normalized.match(/\/downloadables\/(\d+)$/);
    return match ? parseInt(match[1], 10) : 0;
  }

  function normalizeBoothItemUrl(value) {
    const parsed = parseBoothItemReference(value);
    return parsed ? parsed.normalizedUrl : "";
  }

  function normalizeCanonicalBoothItemUrl(value) {
    const parsed = parseBoothItemReference(value);
    if (!parsed) {
      return "";
    }

    const url = new URL(parsed.normalizedUrl);
    const host = url.hostname.toLowerCase();
    return host === "booth.pm"
      ? `https://booth.pm/items/${parsed.itemId}`
      : `https://${host}/items/${parsed.itemId}`;
  }

  function normalizeDownloadUrl(value) {
    const url = tryCreateUrl(value);
    if (!url || url.hostname.toLowerCase() !== "booth.pm") {
      return "";
    }

    const match = url.pathname.match(/^\/downloadables\/(\d+)(?:\/)?$/i);
    return match ? `https://booth.pm/downloadables/${match[1]}` : "";
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

  function normalizeTags(tags) {
    if (!Array.isArray(tags)) {
      return [];
    }

    return Array.from(new Set(tags
      .map(tag => safeString(typeof tag === "string" ? tag : tag && tag.name).trim())
      .filter(Boolean)))
      .sort((left, right) => left.localeCompare(right, "ja"));
  }

  function normalizeTimestamp(value) {
    const trimmed = safeString(value).trim();
    if (!trimmed) {
      return "";
    }

    const date = new Date(trimmed);
    return Number.isNaN(date.getTime()) ? "" : date.toISOString();
  }

  function resolveBoothFolderName(name, fallbackItemId) {
    const trimmed = safeString(name).trim();
    const fallback = toPositiveInteger(fallbackItemId) > 0 ? String(fallbackItemId) : "Booth Item";
    const sanitized = (trimmed || fallback)
      .replace(/[\\/:*?"<>|]/g, " ")
      .replace(/\s+/g, " ")
      .replace(/[. ]+$/g, "")
      .trim();

    return sanitized || fallback;
  }

  function normalizeFilename(value) {
    return safeString(value).trim().toLowerCase();
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

  function isMetaEquivalent(left, right) {
    return JSON.stringify(normalizeMeta(left || DEFAULT_META)) === JSON.stringify(normalizeMeta(right || DEFAULT_META));
  }

  async function requestJson(url, redirectDepth = 0) {
    if (redirectDepth > 4) {
      throw new Error("Booth item JSON のリダイレクト回数が上限を超えました。");
    }

    return new Promise((resolve, reject) => {
      const request = https.get(url, {
        headers: {
          Accept: "application/json",
          "User-Agent": "ee4v-eagle-boothcompat/0.1.0"
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

  async function downloadFile(url, destinationPath, redirectDepth = 0) {
    if (redirectDepth > 4) {
      throw new Error("thumbnail image のリダイレクト回数が上限を超えました。");
    }

    return new Promise((resolve, reject) => {
      const request = https.get(url, {
        headers: {
          "User-Agent": "ee4v-eagle-boothcompat/0.1.0"
        }
      }, response => {
        const statusCode = response.statusCode || 0;
        const location = response.headers.location;

        if (statusCode >= 300 && statusCode < 400 && location) {
          response.resume();
          downloadFile(new URL(location, url).toString(), destinationPath, redirectDepth + 1).then(resolve, reject);
          return;
        }

        if (statusCode < 200 || statusCode >= 300) {
          response.resume();
          reject(new Error(`thumbnail image の取得に失敗しました。HTTP ${statusCode}`));
          return;
        }

        const chunks = [];
        response.on("data", chunk => chunks.push(chunk));
        response.on("end", async () => {
          try {
            await fs.writeFile(destinationPath, Buffer.concat(chunks));
            resolve();
          } catch (error) {
            reject(error);
          }
        });
      });

      request.on("error", error => {
        reject(new Error(`thumbnail image の取得に失敗しました: ${error.message}`));
      });
      request.setTimeout(15000, () => {
        request.destroy(new Error("timeout"));
      });
    });
  }

  window.BoothCompatCore = {
    BOOTH_META_TAG,
    DEFAULT_META,
    ensureBoothMetaForUrl,
    ensureBoothMetaForProduct,
    resolveBoothSnapshot,
    loadBoothMetaItems,
    getAllItems,
    requireVrcAssetRootFolder,
    findVrcAssetRootFolder,
    findDirectChildFolder,
    loadMetaFromItem,
    saveMetaToItem,
    applyThumbnailToItem,
    fetchBoothSnapshot,
    normalizeMeta,
    normalizeDownloads,
    isSameProduct,
    isBoothMetaItem,
    ensureBoothMetaTag,
    getItemFolderIds,
    buildDownloadKey,
    extractDownloadId,
    parseBoothItemReference,
    normalizeBoothItemUrl,
    normalizeCanonicalBoothItemUrl,
    normalizeDownloadUrl,
    normalizeBoothShopUrl,
    normalizeUrl,
    normalizeFilename,
    normalizeTags,
    normalizeTimestamp,
    resolveBoothFolderName,
    safeString,
    toPositiveInteger,
    firstNonEmpty,
    isMetaEquivalent
  };
})();
