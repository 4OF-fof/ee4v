(function () {
  "use strict";

  const http = require("http");
  const fs = require("fs/promises");
  const path = require("path");

  const HOST = "127.0.0.1";
  const PORT = 41596;
  const POLL_INTERVAL_MS = 1500;
  const STABLE_AGE_MS = 2000;
  const JOB_TIMEOUT_MS = 30 * 60 * 1000;
  const IMPORT_RETRY_DELAY_MS = 5000;
  const MIN_IMPORT_FILE_SIZE_BYTES = 1;
  const PARTIAL_EXTENSIONS = [".crdownload", ".download", ".part", ".tmp"];

  let server = null;
  let isPluginReady = false;
  let pollTimer = 0;

  const importJobs = new Map();
  const fileSnapshots = new Map();

  eagle.onPluginCreate(async () => {
    isPluginReady = true;
    await startBridge();
    startDownloadWatcher();
  });

  async function startBridge() {
    if (server) {
      return;
    }

    server = http.createServer((request, response) => {
      handleRequest(request, response).catch(error => {
        console.error(error);
        sendJson(response, 500, {
          ok: false,
          error: error.message || "Internal bridge error"
        });
      });
    });

    server.on("error", error => {
      console.error(`EE4V Booth bridge failed: ${error.message}`);
      server = null;
    });

    server.listen(PORT, HOST, () => {
      console.log(`EE4V Booth bridge listening at http://${HOST}:${PORT}`);
    });
  }

  function startDownloadWatcher() {
    if (pollTimer) {
      return;
    }

    pollTimer = window.setInterval(() => {
      processImportJobs().catch(console.error);
    }, POLL_INTERVAL_MS);
  }

  async function handleRequest(request, response) {
    if (request.method === "OPTIONS") {
      sendJson(response, 204, {});
      return;
    }

    const url = new URL(request.url, `http://${HOST}:${PORT}`);
    if (request.method === "GET" && url.pathname === "/health") {
      await handleHealth(response);
      return;
    }

    if (request.method === "POST" && url.pathname === "/v1/status") {
      await handleStatus(request, response);
      return;
    }

    if (request.method === "POST" && url.pathname === "/v1/import") {
      await handleImport(request, response);
      return;
    }

    sendJson(response, 404, {
      ok: false,
      error: "Not found"
    });
  }

  async function handleHealth(response) {
    const rootFolder = isPluginReady ? await core().findVrcAssetRootFolder().catch(() => null) : null;
    sendJson(response, 200, {
      ok: isPluginReady,
      rootFolderAvailable: Boolean(rootFolder)
    });
  }

  async function handleStatus(request, response) {
    const payload = await readJson(request);
    const items = Array.isArray(payload.items) ? payload.items : [];
    const rootFolder = await core().requireVrcAssetRootFolder();
    const boothMetaItems = await core().loadBoothMetaItems(rootFolder);
    const allItems = await core().getAllItems();

    sendJson(response, 200, {
      ok: true,
      items: items.map(item => buildProductStatus(item, boothMetaItems, allItems))
    });
  }

  async function handleImport(request, response) {
    const payload = await readJson(request);
    const product = payload.product || {};
    const download = payload.download || {};
    const boothMeta = await core().ensureBoothMetaForProduct(product);
    const nextMeta = appendDownloadRequest(boothMeta.meta, download);
    const existing = findImportedDownload(nextMeta, download, await core().getAllItems());
    const jobId = core().buildDownloadKey(download);

    if (!existing && !core().isMetaEquivalent(boothMeta.meta, nextMeta)) {
      await core().saveMetaToItem(boothMeta.item, nextMeta);
    }

    if (!existing) {
      importJobs.set(jobId, {
        jobId,
        product,
        download: normalizeDownloadRequest(download),
        boothMetaItemId: boothMeta.item.id,
        boothMetaFolderId: boothMeta.folder.id,
        startedAt: Date.now(),
        nextAttemptAt: 0,
        status: "watching",
        importedItemId: ""
      });
    }

    sendJson(response, 200, {
      ok: true,
      jobId,
      alreadyImported: Boolean(existing),
      createdBoothMeta: Boolean(boothMeta.created),
      downloadUrl: core().normalizeDownloadUrl(download.downloadUrl)
    });
  }

  function buildProductStatus(product, boothMetaItems, allItems) {
    const boothMeta = boothMetaItems.find(record => core().isSameProduct(record.meta, product));
    const downloads = Array.isArray(product.downloads) ? product.downloads : [];
    return {
      boothItemId: core().toPositiveInteger(product.boothItemId),
      itemUrl: core().normalizeBoothItemUrl(product.itemUrl),
      hasBoothMeta: Boolean(boothMeta),
      downloads: downloads.map(download => {
        const match = findImportedDownload(boothMeta && boothMeta.meta, download, allItems);
        const job = importJobs.get(core().buildDownloadKey(download));
        return {
          downloadUrl: core().normalizeDownloadUrl(download.downloadUrl),
          downloadId: core().extractDownloadId(download.downloadUrl),
          filename: core().safeString(download.filename),
          imported: Boolean(match),
          pending: Boolean(job && job.status !== "imported" && job.status !== "failed"),
          matchedBy: match ? match.matchedBy : ""
        };
      })
    };
  }

  async function processImportJobs() {
    if (importJobs.size === 0) {
      return;
    }

    const downloadsDir = await Promise.resolve(eagle.app.getPath("downloads"));
    const entries = await fs.readdir(downloadsDir).catch(() => []);
    const now = Date.now();

    for (const [jobId, job] of Array.from(importJobs.entries())) {
      if (now - job.startedAt > JOB_TIMEOUT_MS) {
        job.status = "failed";
        importJobs.delete(jobId);
        continue;
      }

      if (job.nextAttemptAt && now < job.nextAttemptAt) {
        continue;
      }

      const filePath = await findStableDownloadedFile(downloadsDir, entries, job.download.filename, now);
      if (!filePath) {
        continue;
      }

      try {
        await importDownloadedFile(job, filePath);
        importJobs.delete(jobId);
      } catch (error) {
        job.status = "watching";
        job.nextAttemptAt = now + IMPORT_RETRY_DELAY_MS;
        console.warn(`Booth import is waiting for file readiness: ${error.message}`);
      }
    }
  }

  async function findStableDownloadedFile(downloadsDir, entries, filename, now) {
    const expectedName = path.basename(core().safeString(filename));
    if (!expectedName) {
      return null;
    }

    if (entries.some(entry => isPartialDownloadForExpectedName(entry, expectedName))) {
      return null;
    }

    const candidate = entries.find(entry => entry === expectedName);
    if (!candidate || isPartialDownloadName(candidate)) {
      return null;
    }

    const filePath = path.join(downloadsDir, candidate);
    const stat = await fs.stat(filePath).catch(() => null);
    if (!stat || !stat.isFile()) {
      return null;
    }

    if (stat.size < MIN_IMPORT_FILE_SIZE_BYTES) {
      fileSnapshots.delete(filePath);
      return null;
    }

    if (now - stat.mtimeMs < STABLE_AGE_MS) {
      return null;
    }

    const previous = fileSnapshots.get(filePath);
    fileSnapshots.set(filePath, {
      size: stat.size,
      mtimeMs: stat.mtimeMs
    });

    if (!previous) {
      return null;
    }

    return previous.size === stat.size && previous.mtimeMs === stat.mtimeMs
      ? filePath
      : null;
  }

  async function importDownloadedFile(job, filePath) {
    job.status = "importing";
    const itemName = getItemNameFromFilePath(filePath);
    const itemId = await eagle.item.addFromPath(filePath, {
      folders: [job.boothMetaFolderId],
      name: itemName
    });

    const item = await eagle.item.getById(itemId);
    item.name = itemName;
    item.url = job.download.downloadUrl;
    item.annotation = [
      `Booth item: ${job.product.itemUrl || ""}`,
      `Download: ${job.download.downloadUrl || ""}`,
      `Filename: ${job.download.filename || ""}`
    ].filter(Boolean).join("\n");
    await item.save();

    const boothMetaItem = await eagle.item.getById(job.boothMetaItemId);
    const meta = await core().loadMetaFromItem(boothMetaItem);
    const nextMeta = markDownloadImported(meta, job.download, itemId);
    await core().saveMetaToItem(boothMetaItem, nextMeta);

    job.status = "imported";
    job.importedItemId = itemId;
    await eagle.notification.show({
      title: "Booth Compat",
      body: `${path.basename(filePath)} を Eagle に取り込みました。`,
      mute: true,
      duration: 3000
    });
  }

  function appendDownloadRequest(meta, download) {
    const normalized = core().normalizeMeta(meta);
    const normalizedDownload = normalizeDownloadRequest(download);
    const key = core().buildDownloadKey(normalizedDownload);
    const existing = normalized.downloads.find(item => core().buildDownloadKey(item) === key);
    if (existing) {
      return normalized;
    }

    normalized.downloads.push({
      ...normalizedDownload,
      requestedAt: new Date().toISOString(),
      importedAt: "",
      importedItemIds: []
    });
    return normalized;
  }

  function markDownloadImported(meta, download, importedItemId) {
    const normalized = core().normalizeMeta(meta);
    const key = core().buildDownloadKey(download);
    let target = normalized.downloads.find(item => core().buildDownloadKey(item) === key);
    if (!target) {
      target = {
        ...normalizeDownloadRequest(download),
        requestedAt: new Date().toISOString(),
        importedAt: "",
        importedItemIds: []
      };
      normalized.downloads.push(target);
    }

    target.importedAt = new Date().toISOString();
    target.importedItemIds = Array.from(new Set([...(target.importedItemIds || []), importedItemId]));
    return normalized;
  }

  function findImportedDownload(meta, download, allItems) {
    if (isDownloadImported(meta, download)) {
      return {
        matchedBy: "boothmeta"
      };
    }

    const downloadId = core().extractDownloadId(download.downloadUrl);
    const filename = core().normalizeFilename(download.filename);
    const item = allItems.find(candidate => {
      const annotation = core().safeString(candidate.annotation);
      const url = core().safeString(candidate.url);
      const name = core().normalizeFilename(candidate.name);
      return (downloadId > 0 && (annotation.includes(String(downloadId)) || url.includes(`/downloadables/${downloadId}`)))
        || (filename && name === filename);
    });

    return item
      ? { matchedBy: downloadId > 0 ? "downloadId" : "filename" }
      : null;
  }

  function isDownloadImported(meta, download) {
    const normalized = core().normalizeMeta(meta || core().DEFAULT_META);
    const key = core().buildDownloadKey(download);
    return normalized.downloads.some(item => {
      if (core().buildDownloadKey(item) !== key) {
        return false;
      }

      return Boolean(item.importedAt)
        || (Array.isArray(item.importedItemIds) && item.importedItemIds.length > 0);
    });
  }

  function normalizeDownloadRequest(download) {
    return {
      downloadUrl: core().normalizeDownloadUrl(download.downloadUrl),
      downloadId: core().extractDownloadId(download.downloadUrl),
      filename: core().safeString(download.filename)
    };
  }

  function isPartialDownloadName(filename) {
    const lower = core().safeString(filename).toLowerCase();
    return PARTIAL_EXTENSIONS.some(extension => lower.endsWith(extension));
  }

  function isPartialDownloadForExpectedName(entry, expectedName) {
    const lowerEntry = core().safeString(entry).toLowerCase();
    const lowerExpected = core().safeString(expectedName).toLowerCase();
    return PARTIAL_EXTENSIONS.some(extension => lowerEntry === `${lowerExpected}${extension}`);
  }

  function getItemNameFromFilePath(filePath) {
    const filename = path.basename(filePath);
    const extension = path.extname(filename);
    return extension ? filename.slice(0, -extension.length) : filename;
  }

  function core() {
    return window.BoothCompatCore;
  }

  function readJson(request) {
    return new Promise((resolve, reject) => {
      const chunks = [];
      request.on("data", chunk => chunks.push(chunk));
      request.on("end", () => {
        if (chunks.length === 0) {
          resolve({});
          return;
        }

        try {
          resolve(JSON.parse(Buffer.concat(chunks).toString("utf8")));
        } catch (error) {
          reject(new Error("Invalid JSON request body."));
        }
      });
      request.on("error", reject);
    });
  }

  function sendJson(response, statusCode, payload) {
    const body = statusCode === 204 ? "" : JSON.stringify(payload || {});
    response.writeHead(statusCode, {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type",
      "Content-Type": "application/json; charset=utf-8",
      "Content-Length": Buffer.byteLength(body)
    });
    response.end(body);
  }
})();
