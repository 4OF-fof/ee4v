(function () {
  "use strict";

  const fs = require("fs/promises");
  const path = require("path");
  const https = require("https");

  const BOOTH_META_TAG = "BoothMeta";
  const BOOTH_META_TAGS = [BOOTH_META_TAG, "VRCMeta"];

  const DEFAULT_META: BoothMeta = {
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

  function asRecord(value: unknown): JsonRecord {
    return value && typeof value === "object" ? value as JsonRecord : {};
  }

  function errorMessage(error: unknown): string {
    return error instanceof Error ? error.message : safeString(error);
  }

  function tagName(tag: unknown): string {
    return safeString(typeof tag === "string" ? tag : asRecord(tag).name).trim();
  }

  async function ensureBoothMetaForUrl(itemUrl: string) {
    const boothRef = parseBoothItemReference(itemUrl);
    if (!boothRef) {
      throw new Error("有効な Booth item URL を入力してください。");
    }

    return ensureBoothMetaForProduct({
      boothItemId: boothRef.itemId,
      itemUrl: boothRef.normalizedUrl
    });
  }

  async function ensureBoothMetaForProduct(product: BoothProductInput, snapshotOverride?: Partial<BoothProductInput>) {
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

    const itemName = resolveEagleItemName(meta.name || targetFolderName, targetFolderName);
    const itemId = await eagle.item.addFromPath(filePath, {
      folders: [targetFolder.id],
      name: itemName,
      tags: [BOOTH_META_TAG]
    });

    const item = await eagle.item.getById(itemId);
    if (!item) {
      throw new Error("BoothMeta item could not be created.");
    }
    item.name = itemName;
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

  async function resolveBoothSnapshot(product: BoothProductInput, snapshotOverride?: Partial<BoothProductInput>): Promise<BoothSnapshot> {
    if (snapshotOverride) {
      return normalizeSnapshot(snapshotOverride);
    }

    const boothRef = parseProductBoothReference(product);
    if (!boothRef) {
      return normalizeSnapshot(product || {});
    }

    let fetched: BoothSnapshot | null = null;
    try {
      fetched = await fetchBoothSnapshot(boothRef);
    } catch (error) {
      console.warn(`Failed to fetch Booth snapshot: ${errorMessage(error)}`);
      fetched = null;
    }

    return normalizeSnapshot({
      ...product,
      ...(fetched || {}),
      boothItemId: (fetched && fetched.boothItemId) || boothRef.itemId || product.boothItemId,
      itemUrl: (fetched && fetched.itemUrl) || boothRef.normalizedUrl || product.itemUrl
    });
  }

  function parseProductBoothReference(product: BoothProductInput): BoothItemReference | null {
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

  async function loadBoothMetaItems(rootFolder: EagleFolder): Promise<BoothMetaRecord[]> {
    const items = await getAllItems();
    const folders = await eagle.folder.getAll();
    const descendantFolderIds = new Set(findDescendantFolderIds(folders, rootFolder.id));
    const records: BoothMetaRecord[] = [];

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

  async function loadBoothMetaItemsByTag(): Promise<BoothMetaRecord[]> {
    const items = await getAllItems();
    const folders = await eagle.folder.getAll();
    const records: BoothMetaRecord[] = [];

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

  function findExistingBoothMeta(records: BoothMetaRecord[], product: BoothProductInput): BoothMetaRecord | null {
    const boothItemId = toPositiveInteger(product && product.boothItemId);
    if (boothItemId > 0) {
      const matchById = records.find(record => toPositiveInteger(record.meta && record.meta.boothItemId) === boothItemId);
      if (matchById) {
        return matchById;
      }
    }

    return records.find(record => isSameProduct(record.meta, product)) || null;
  }

  async function getAllItems(): Promise<EagleItem[]> {
    if (eagle.item && typeof eagle.item.getAll === "function") {
      return await eagle.item.getAll();
    }

    if (eagle.item && typeof eagle.item.getItems === "function") {
      return await eagle.item.getItems();
    }

    return [];
  }

  async function requireVrcAssetRootFolder(): Promise<EagleFolder> {
    const rootFolder = await findVrcAssetRootFolder();
    if (!rootFolder) {
      throw new Error("library root VRCAsset folder was not found.");
    }

    return rootFolder;
  }

  async function findVrcAssetRootFolder(): Promise<EagleFolder | null> {
    const folders = await eagle.folder.getAll();
    const matches = folders.filter(folder => folder.name === "VRCAsset" && !folder.parent);
    if (matches.length !== 1) {
      return null;
    }

    return matches[0];
  }

  async function findDirectChildFolder(parentId: string, name: string): Promise<EagleFolder | null> {
    const folders = await eagle.folder.getAll();
    return folders.find(folder => folder.parent === parentId && folder.name === name) || null;
  }

  async function loadMetaFromItem(item: EagleItem): Promise<BoothMeta> {
    try {
      const raw = await fs.readFile(item.filePath || "", "utf8");
      return normalizeMeta(JSON.parse(raw));
    } catch (error) {
      return { ...DEFAULT_META };
    }
  }

  async function saveMetaToItem(item: EagleItem, meta: Partial<BoothMeta>): Promise<void> {
    const tempPath = path.join(await Promise.resolve(eagle.app.getPath("temp")), `${item.id}-boothcompat.json`);
    await fs.writeFile(tempPath, JSON.stringify(normalizeMeta(meta), null, 2) + "\n", "utf8");
    await item.replaceFile(tempPath);
  }

  async function applyThumbnailToItem(item: EagleItem, thumbnailUrl: string, tempDir: string): Promise<void> {
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
      console.warn(`Failed to apply custom thumbnail: ${errorMessage(error)}`);
    }
  }

  async function fetchBoothSnapshot(boothRef: BoothItemReference): Promise<BoothSnapshot> {
    const payload = await requestJson(`${boothRef.fetchUrl}.json`);
    const shop = asRecord(payload.shop);
    const firstImage = Array.isArray(payload.images) ? asRecord(payload.images[0]) : {};
    const boothItemId = toPositiveInteger(payload.id) || boothRef.itemId;
    const itemUrlFromPayload = normalizeCanonicalBoothItemUrl(payload.url) || normalizeCanonicalBoothItemUrl(boothRef.normalizedUrl) || boothRef.normalizedUrl;
    const shopUrl = normalizeBoothShopUrl(firstNonEmpty([
      shop.url,
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
        firstImage.original,
        firstImage.url
      ])),
      shopName: safeString(firstNonEmpty([
        shop.name,
        payload.shopName
      ])),
      shopUrl,
      shopThumbnailUrl: normalizeUrl(firstNonEmpty([
        shop.thumbnailUrl,
        shop.thumbnail_url,
        payload.shopThumbnailUrl
      ])),
      tags: normalizeTags(payload.tags),
      lastUpdatedAtUtc: new Date().toISOString()
    };
  }

  function findDescendantFolderIds(folders: EagleFolder[], rootId: string): string[] {
    const result: string[] = [rootId];
    const queue: string[] = [rootId];
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

  function parseBoothItemReference(value: unknown): BoothItemReference | null {
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

  function normalizeMeta(meta: unknown): BoothMeta {
    const source = asRecord(meta);
    return {
      schemaVersion: 1,
      boothItemId: toPositiveInteger(source.boothItemId),
      itemUrl: normalizeBoothItemUrl(source.itemUrl) || safeString(source.itemUrl).trim(),
      name: safeString(source.name),
      description: safeString(source.description),
      thumbnailUrl: normalizeUrl(source.thumbnailUrl),
      shopName: safeString(source.shopName),
      shopUrl: normalizeBoothShopUrl(source.shopUrl) || normalizeUrl(source.shopUrl),
      shopThumbnailUrl: normalizeUrl(source.shopThumbnailUrl),
      tags: normalizeTags(source.tags),
      attachedAt: normalizeTimestamp(source.attachedAt),
      lastUpdatedAtUtc: normalizeTimestamp(source.lastUpdatedAtUtc),
      downloads: normalizeDownloads(source.downloads)
    };
  }

  function normalizeDownloads(downloads: unknown): BoothDownloadMeta[] {
    if (!Array.isArray(downloads)) {
      return [];
    }

    return downloads.map(rawDownload => {
      const download = asRecord(rawDownload);
      return {
        downloadUrl: normalizeDownloadUrl(download.downloadUrl),
        downloadId: toPositiveInteger(download.downloadId) || extractDownloadId(download.downloadUrl),
        filename: safeString(download.filename),
        requestedAt: normalizeTimestamp(download.requestedAt),
        importedAt: normalizeTimestamp(download.importedAt),
        importedItemIds: Array.isArray(download.importedItemIds)
        ? download.importedItemIds.map((value: unknown) => safeString(value)).filter(Boolean)
        : []
      };
    });
  }

  function normalizeSnapshot(value: unknown): BoothSnapshot {
    const source = asRecord(value);
    return {
      boothItemId: toPositiveInteger(source.boothItemId),
      itemUrl: normalizeBoothItemUrl(source.itemUrl),
      name: safeString(source.name),
      description: safeString(source.description),
      thumbnailUrl: normalizeUrl(source.thumbnailUrl),
      shopName: safeString(source.shopName),
      shopUrl: normalizeBoothShopUrl(source.shopUrl) || normalizeUrl(source.shopUrl),
      shopThumbnailUrl: normalizeUrl(source.shopThumbnailUrl),
      tags: normalizeTags(source.tags),
      lastUpdatedAtUtc: normalizeTimestamp(source.lastUpdatedAtUtc) || new Date().toISOString()
    };
  }

  function isSameProduct(meta: Partial<BoothMeta>, product: BoothProductInput): boolean {
    const left = normalizeMeta(meta || DEFAULT_META);
    const rightItemId = toPositiveInteger(product.boothItemId);
    const rightUrl = normalizeBoothItemUrl(product.itemUrl);
    return (left.boothItemId > 0 && rightItemId > 0 && left.boothItemId === rightItemId)
      || Boolean(left.itemUrl && rightUrl && left.itemUrl === rightUrl);
  }

  function isBoothMetaItem(item: EagleItem | null): item is EagleItem {
    return item !== null
      && !item.isDeleted
      && isJsonLikeItem(item)
      && hasBoothMetaTag(item.tags);
  }

  function isJsonLikeItem(item: EagleItem | null): boolean {
    if (!item) {
      return false;
    }

    if (safeString(item.ext).replace(/^\./, "").toLowerCase() === "json") {
      return true;
    }

    return [item.name, item.filePath]
      .some(value => safeString(value).toLowerCase().endsWith(".json"));
  }

  function isBoothMetaMeta(meta: Partial<BoothMeta>): boolean {
    const normalized = normalizeMeta(meta || DEFAULT_META);
    return normalized.schemaVersion === 1
      && normalized.boothItemId > 0
      && Boolean(normalized.itemUrl);
  }

  function hasBoothMetaTag(tags: unknown): boolean {
    return Array.isArray(tags)
      && tags.some(tag => BOOTH_META_TAGS.includes(tagName(tag)));
  }

  function ensureBoothMetaTag(tags: unknown): string[] {
    const normalized = Array.isArray(tags)
      ? tags.map(tagName).filter(Boolean)
      : [];

    if (!normalized.includes(BOOTH_META_TAG)) {
      normalized.push(BOOTH_META_TAG);
    }

    return Array.from(new Set(normalized));
  }

  function getItemFolderIds(item: Partial<EagleItem>): string[] {
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

  function buildDownloadKey(download: Partial<BoothDownloadInput>): string {
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

  function extractDownloadId(downloadUrl: unknown): number {
    const normalized = normalizeDownloadUrl(downloadUrl);
    const match = normalized.match(/\/downloadables\/(\d+)$/);
    return match ? parseInt(match[1], 10) : 0;
  }

  function normalizeBoothItemUrl(value: unknown): string {
    const parsed = parseBoothItemReference(value);
    return parsed ? parsed.normalizedUrl : "";
  }

  function normalizeCanonicalBoothItemUrl(value: unknown): string {
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

  function normalizeDownloadUrl(value: unknown): string {
    const url = tryCreateUrl(value);
    if (!url || url.hostname.toLowerCase() !== "booth.pm") {
      return "";
    }

    const match = url.pathname.match(/^\/downloadables\/(\d+)(?:\/)?$/i);
    return match ? `https://booth.pm/downloadables/${match[1]}` : "";
  }

  function normalizeBoothShopUrl(value: unknown): string {
    const url = tryCreateUrl(value);
    if (!url || !/\.booth\.pm$/i.test(url.hostname)) {
      return "";
    }

    return `https://${url.hostname.toLowerCase()}`;
  }

  function normalizeUrl(value: unknown): string {
    const url = tryCreateUrl(value);
    return url ? url.toString() : "";
  }

  function tryCreateUrl(value: unknown): URL | null {
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

  function normalizeTags(tags: unknown): string[] {
    if (!Array.isArray(tags)) {
      return [];
    }

    return Array.from(new Set(tags
      .map(tagName)
      .filter(Boolean)))
      .sort((left, right) => left.localeCompare(right, "ja"));
  }

  function normalizeTimestamp(value: unknown): string {
    const trimmed = safeString(value).trim();
    if (!trimmed) {
      return "";
    }

    const date = new Date(trimmed);
    return Number.isNaN(date.getTime()) ? "" : date.toISOString();
  }

  function resolveBoothFolderName(name: unknown, fallbackItemId: unknown): string {
    const trimmed = safeString(name).trim();
    const fallback = toPositiveInteger(fallbackItemId) > 0 ? String(fallbackItemId) : "Booth Item";
    const sanitized = (trimmed || fallback)
      .replace(/[\\/:*?"<>|]/g, " ")
      .replace(/\s+/g, " ")
      .replace(/[. ]+$/g, "")
      .trim();

    return sanitized || fallback;
  }

  function resolveEagleItemName(name: unknown, fallbackName: unknown): string {
    const fallback = safeString(fallbackName).trim() || "Booth Item";
    const sanitized = safeString(name)
      .replace(/[\\/:*?"<>|]/g, " ")
      .replace(/\s+/g, " ")
      .replace(/[. ]+$/g, "")
      .trim();

    return sanitized || fallback;
  }

  function normalizeFilename(value: unknown): string {
    return safeString(value).trim().toLowerCase();
  }

  function toPositiveInteger(value: unknown): number {
    const parsed = typeof value === "number" ? value : parseInt(String(value), 10);
    return Number.isInteger(parsed) && parsed > 0 ? parsed : 0;
  }

  function safeString(value: unknown): string {
    return typeof value === "string" ? value : "";
  }

  function firstNonEmpty(values: unknown[]): string {
    for (let index = 0; index < values.length; index += 1) {
      const value = safeString(values[index]).trim();
      if (value) {
        return value;
      }
    }
    return "";
  }

  function isMetaEquivalent(left: Partial<BoothMeta>, right: Partial<BoothMeta>): boolean {
    return JSON.stringify(normalizeMeta(left || DEFAULT_META)) === JSON.stringify(normalizeMeta(right || DEFAULT_META));
  }

  async function requestJson(url: string, redirectDepth = 0): Promise<JsonRecord> {
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

        const chunks: string[] = [];
        response.setEncoding("utf8");
        response.on("data", chunk => chunks.push(String(chunk)));
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

  async function downloadFile(url: string, destinationPath: string, redirectDepth = 0): Promise<void> {
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

        const chunks: Uint8Array[] = [];
        response.on("data", chunk => chunks.push(typeof chunk === "string" ? new TextEncoder().encode(chunk) : chunk));
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
    resolveEagleItemName,
    safeString,
    toPositiveInteger,
    firstNonEmpty,
    isMetaEquivalent
  };
})();
