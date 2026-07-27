(function () {
  "use strict";

  const http = require("http");
  const crypto = require("crypto");
  const fs = require("fs/promises");
  const os = require("os");
  const path = require("path");
  const timers = require("timers");

  const HOST = "127.0.0.1";
  const PORT = 41596;
  const EAGLE_API_HOST = "127.0.0.1";
  const EAGLE_API_PORT = 41595;
  const POLL_INTERVAL_MS = 1500;
  const STABLE_AGE_MS = 2000;
  const JOB_TIMEOUT_MS = 30 * 60 * 1000;
  const EAGLE_API_TIMEOUT_MS = JOB_TIMEOUT_MS;
  const IMPORT_RETRY_DELAY_MS = 5000;
  const COPY_READY_RETRY_DELAY_MS = 1500;
  const MIN_IMPORT_FILE_SIZE_BYTES = 1;
  const PARTIAL_EXTENSIONS = [".crdownload", ".download", ".part", ".tmp"];
  const MAX_REQUEST_BODY_BYTES = 1024 * 1024;
  const BRIDGE_TOKEN = crypto.randomBytes(32).toString("hex");

  type BridgeProduct = BoothProductInput;
  type BridgeDownload = BoothDownloadInput;
  type EagleWebItem = Omit<EagleItem, "save" | "replaceFile" | "setCustomThumbnail"> & {
    folders?: string[];
    isDeleted?: boolean;
    save?: () => Promise<void>;
    replaceFile?: (path: string) => Promise<void>;
    setCustomThumbnail?: (path: string) => Promise<void>;
  };
  type EagleApiResult = JsonRecord & { data?: unknown; status?: string; message?: string; error?: string };
  interface BridgeMetaRecord {
    item: EagleWebItem;
    folder: EagleFolder;
    meta: BoothMeta;
  }
  interface ImportedDownloadMatch {
    matchedBy: string;
  }
  interface ImportJob {
    jobId: string;
    product: BridgeProduct;
    download: BoothDownloadMeta;
    boothMetaItemId: string;
    boothMetaItemFilePath: string;
    boothMetaFolderId: string;
    boothMetaInitialMeta: BoothMeta;
    shouldCreateBoothMetaAfterImport: boolean;
    startedAt: number;
    nextAttemptAt: number;
    status: "watching" | "importing" | "finalizing" | "imported" | "failed";
    importedItemId: string;
    importedItemName?: string;
    sourceFilePath?: string;
    sourceFileSize?: number;
    sourceDeleted: boolean;
    boothMetaTempFilePath?: string;
    boothMetaItemName?: string;
  }
  interface FileSnapshot {
    size: number;
    mtimeMs: number;
  }
  interface ListItemsParams {
    limit?: number;
    offset?: number;
    orderBy?: string;
    folders?: string;
    tags?: string;
    keyword?: string;
  }
  interface ServiceElements {
    title: HTMLElement;
    subtitle: HTMLElement;
    runningChip: HTMLElement;
    bridgeLabel: HTMLElement;
    bridgeValue: HTMLElement;
    libraryLabel: HTMLElement;
    libraryValue: HTMLElement;
    pendingLabel: HTMLElement;
    pendingValue: HTMLElement;
    checkedLabel: HTMLElement;
    checkedValue: HTMLElement;
    message: HTMLElement;
    closeButton: HTMLButtonElement;
    refreshButton: HTMLButtonElement;
  }

  let server: NodeServer | null = null;
  let isPluginReady = false;
  let pollTimer: NodeTimerHandle | null = null;
  let isProcessingImportJobs = false;
  let eagleLibraryPath = "";
  let libraryRevision = 0;
  let isServiceDomReady = false;
  let isBridgeListening = false;
  let isStatusRefreshing = false;
  let isStatusWindowRequested = false;
  let rootFolderAvailable = false;
  let lastStatusCheck = "";
  let serviceStatusError = "";

  const importJobs = new Map<string, ImportJob>();
  const fileSnapshots = new Map<string, FileSnapshot>();
  const boothMetaLocks = new Map<string, Promise<void>>();
  const responseOrigins = new WeakMap<NodeServerResponse, string>();
  const serviceElements = {} as ServiceElements;

  if (document.readyState === "loading") {
    window.addEventListener("DOMContentLoaded", initializeServiceUi);
  } else {
    initializeServiceUi();
  }

  function errorMessage(error: unknown): string {
    return error instanceof Error ? error.message : core().safeString(error);
  }

  function hasErrorCode(error: unknown, code: string): boolean {
    return Boolean(error && typeof error === "object" && "code" in error && (error as { code?: unknown }).code === code);
  }

  eagle.onPluginCreate(async () => {
    isPluginReady = true;
    await eagle.window.hide();
    document.documentElement.lang = normalizeLocale(eagle.app.locale);
    document.title = core().t("manifest.app.name", "Booth Compat Service");
    localizeServiceUi();
    await applyTheme(await Promise.resolve(eagle.app.theme));
    eagle.onThemeChanged(theme => {
      applyTheme(theme).catch(console.error);
    });
    eagle.onLibraryChanged(() => {
      libraryRevision += 1;
      eagleLibraryPath = "";
      importJobs.clear();
      fileSnapshots.clear();
      boothMetaLocks.clear();
      refreshServiceStatus().catch(console.error);
    });
    await startBridge();
    startDownloadWatcher();
    await refreshServiceStatus();
    timers.setTimeout(() => {
      hideUnrequestedStatusWindow();
    }, 0);
  });

  eagle.onPluginRun(async () => {
    if (!isPluginReady) {
      return;
    }
    isStatusWindowRequested = true;
    await refreshServiceStatus();
    await eagle.window.show();
  });

  eagle.onPluginShow(() => {
    if (hideUnrequestedStatusWindow()) {
      return;
    }
    if (!isPluginReady) {
      return;
    }
    refreshServiceStatus().catch(console.error);
  });

  eagle.onPluginHide(() => {
    isStatusWindowRequested = false;
  });

  function hideUnrequestedStatusWindow(): boolean {
    if (isStatusWindowRequested) {
      return false;
    }

    eagle.window.hide().catch(console.error);
    return true;
  }

  function initializeServiceUi(): void {
    if (isServiceDomReady) {
      return;
    }

    serviceElements.title = requireElement("service-title", HTMLElement);
    serviceElements.subtitle = requireElement("service-subtitle", HTMLElement);
    serviceElements.runningChip = requireElement("running-chip", HTMLElement);
    serviceElements.bridgeLabel = requireElement("bridge-label", HTMLElement);
    serviceElements.bridgeValue = requireElement("bridge-value", HTMLElement);
    serviceElements.libraryLabel = requireElement("library-label", HTMLElement);
    serviceElements.libraryValue = requireElement("library-value", HTMLElement);
    serviceElements.pendingLabel = requireElement("pending-label", HTMLElement);
    serviceElements.pendingValue = requireElement("pending-value", HTMLElement);
    serviceElements.checkedLabel = requireElement("checked-label", HTMLElement);
    serviceElements.checkedValue = requireElement("checked-value", HTMLElement);
    serviceElements.message = requireElement("service-message", HTMLElement);
    serviceElements.closeButton = requireElement("close-button", HTMLButtonElement);
    serviceElements.refreshButton = requireElement("refresh-button", HTMLButtonElement);
    serviceElements.closeButton.addEventListener("click", () => {
      eagle.window.hide().catch(console.error);
    });
    serviceElements.refreshButton.addEventListener("click", () => {
      refreshServiceStatus().catch(console.error);
    });
    window.addEventListener("keydown", event => {
      if (event.key === "Escape") {
        event.preventDefault();
        eagle.window.hide().catch(console.error);
      }
    });
    isServiceDomReady = true;
    localizeServiceUi();
    renderServiceStatus();
  }

  function localizeServiceUi(): void {
    if (!isServiceDomReady) {
      return;
    }

    document.documentElement.lang = normalizeLocale(eagle.app.locale);
    document.title = core().t("service.title", "Booth Compat Service");
    serviceElements.title.textContent = core().t("service.title", "Booth Compat Service");
    serviceElements.subtitle.textContent = core().t("service.subtitle", "BOOTH download bridge for Eagle");
    serviceElements.bridgeLabel.textContent = core().t("service.bridge", "Bridge endpoint");
    serviceElements.libraryLabel.textContent = core().t("service.library", "VRCAsset folder");
    serviceElements.pendingLabel.textContent = core().t("service.pending", "Pending imports");
    serviceElements.checkedLabel.textContent = core().t("service.lastChecked", "Last checked");
    serviceElements.closeButton.textContent = core().t("service.close", "Close");
    renderServiceStatus();
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

  async function refreshServiceStatus(): Promise<void> {
    if (isStatusRefreshing) {
      return;
    }

    isStatusRefreshing = true;
    serviceStatusError = "";
    renderServiceStatus();
    try {
      rootFolderAvailable = Boolean(await core().findVrcAssetRootFolder());
    } catch (error) {
      console.error(error);
      rootFolderAvailable = false;
      serviceStatusError = errorMessage(error) || core().t("service.checkFailed", "Service status could not be checked.");
    } finally {
      lastStatusCheck = new Date().toISOString();
      isStatusRefreshing = false;
      renderServiceStatus();
    }
  }

  function renderServiceStatus(): void {
    if (!isServiceDomReady) {
      return;
    }

    serviceElements.runningChip.textContent = isBridgeListening
      ? core().t("service.running", "Running")
      : core().t("service.stopped", "Not running");
    serviceElements.runningChip.classList.toggle("is-error", !isBridgeListening);
    serviceElements.bridgeValue.textContent = `http://${HOST}:${PORT}`;
    serviceElements.libraryValue.textContent = rootFolderAvailable
      ? core().t("service.libraryReady", "Ready")
      : core().t("service.libraryMissing", "Not found");
    serviceElements.pendingValue.textContent = String(importJobs.size);
    serviceElements.checkedValue.textContent = lastStatusCheck
      ? formatStatusTimestamp(lastStatusCheck)
      : core().t("service.never", "Not checked yet");
    serviceElements.refreshButton.textContent = isStatusRefreshing
      ? core().t("service.refreshing", "Refreshing…")
      : core().t("service.refresh", "Refresh");
    serviceElements.refreshButton.disabled = isStatusRefreshing;
    serviceElements.refreshButton.setAttribute("aria-busy", String(isStatusRefreshing));

    serviceElements.message.textContent = serviceStatusError
      || (rootFolderAvailable
        ? core().t("service.readyMessage", "The bridge is ready for BOOTH import requests.")
        : core().t("service.missingMessage", "Create one VRCAsset folder at the library root."));
    serviceElements.message.classList.toggle("is-error", Boolean(serviceStatusError) || !rootFolderAvailable);
  }

  function formatStatusTimestamp(value: string): string {
    return new Intl.DateTimeFormat(normalizeLocale(eagle.app.locale), {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit"
    }).format(new Date(value));
  }

  async function startBridge() {
    if (server) {
      return;
    }

    server = http.createServer((request, response) => {
      handleRequest(request, response).catch(error => {
        console.error(error);
        const statusCode = error && typeof error === "object" && "statusCode" in error
          ? Number((error as { statusCode?: unknown }).statusCode) || 500
          : 500;
        sendJson(response, statusCode, {
          ok: false,
          error: errorMessage(error) || "Internal bridge error"
        });
      });
    });

    server.on("error", error => {
      console.error(`EE4V Booth bridge failed: ${error.message}`);
      isBridgeListening = false;
      serviceStatusError = error.message;
      server = null;
      renderServiceStatus();
    });

    server.listen(PORT, HOST, () => {
      isBridgeListening = true;
      serviceStatusError = "";
      console.log(`EE4V Booth bridge listening at http://${HOST}:${PORT}`);
      renderServiceStatus();
    });
  }

  function startDownloadWatcher() {
    if (pollTimer) {
      return;
    }

    pollTimer = timers.setInterval(() => {
      kickImportJobProcessing();
    }, POLL_INTERVAL_MS);
  }

  async function handleRequest(request: NodeIncomingMessage, response: NodeServerResponse): Promise<void> {
    const origin = headerValue(request.headers.origin);
    if (origin && !isAllowedBoothOrigin(origin)) {
      sendJson(response, 403, { ok: false, error: "Origin is not allowed" });
      return;
    }
    if (origin) {
      responseOrigins.set(response, origin);
    }

    if (request.method === "OPTIONS") {
      sendJson(response, 204, {});
      return;
    }

    const url = new URL(request.url || "/", `http://${HOST}:${PORT}`);
    if (request.method === "GET" && url.pathname === "/health") {
      await handleHealth(response);
      return;
    }

    if (request.method === "POST" && headerValue(request.headers["x-ee4v-bridge-token"]) !== BRIDGE_TOKEN) {
      sendJson(response, 401, { ok: false, error: "Bridge token is invalid" });
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

  async function handleHealth(response: NodeServerResponse): Promise<void> {
    const rootFolder = isPluginReady ? await core().findVrcAssetRootFolder().catch(() => null) : null;
    sendJson(response, 200, {
      ok: isPluginReady,
      rootFolderAvailable: Boolean(rootFolder),
      token: BRIDGE_TOKEN
    });
    kickImportJobProcessing();
  }

  async function handleStatus(request: NodeIncomingMessage, response: NodeServerResponse): Promise<void> {
    const payload = await readJson(request);
    const items = Array.isArray(payload.items) ? payload.items as BridgeProduct[] : [];
    const rootFolder = await requireVrcAssetRootFolderForBridge();
    const boothMetaItems = await loadBoothMetaItemsForBridge(rootFolder);
    const allItems = await getAllItemsForBridge();

    sendJson(response, 200, {
      ok: true,
      items: items.map(item => buildProductStatus(item, boothMetaItems, allItems))
    });
    kickImportJobProcessing();
  }

  async function handleImport(request: NodeIncomingMessage, response: NodeServerResponse): Promise<void> {
    const payload = await readJson(request);
    const product = (payload.product || {}) as BridgeProduct;
    const download = (payload.download || {}) as BridgeDownload;
    const result = await withBoothMetaLock(product, async () => {
      const boothMeta = await ensureBoothMetaForImport(product);
      const nextMeta = appendDownloadRequest(boothMeta.meta, download);
      const existing = findImportedDownload(nextMeta, download, await getAllItemsForBridge());
      const jobId = core().buildDownloadKey(download);
      const existingJob = importJobs.get(jobId);

      if (boothMeta.canWriteMeta && !existing && !core().isMetaEquivalent(boothMeta.meta, nextMeta)) {
        await saveMetaForBridge(boothMeta.item, nextMeta);
      }

      if (!existing && !existingJob) {
        importJobs.set(jobId, {
          jobId,
          product,
          download: normalizeDownloadRequest(download),
          boothMetaItemId: boothMeta.item ? boothMeta.item.id : "",
          boothMetaItemFilePath: boothMeta.item && boothMeta.item.filePath ? boothMeta.item.filePath : "",
          boothMetaFolderId: boothMeta.folder.id,
          boothMetaInitialMeta: nextMeta,
          shouldCreateBoothMetaAfterImport: Boolean(boothMeta.shouldCreateAfterImport),
          startedAt: Date.now(),
          nextAttemptAt: 0,
          status: "watching",
          importedItemId: "",
          sourceDeleted: false
        });
      }

      return {
        jobId,
        existing,
        existingJob,
        boothMeta
      };
    });

    sendJson(response, 200, {
      ok: true,
      jobId: result.jobId,
      alreadyImported: Boolean(result.existing),
      alreadyPending: Boolean(result.existingJob),
      createdBoothMeta: Boolean(result.boothMeta.created || result.boothMeta.shouldCreateAfterImport),
      downloadUrl: core().normalizeDownloadUrl(download.downloadUrl)
    });
    renderServiceStatus();
    kickImportJobProcessing();
  }

  function buildProductStatus(product: BridgeProduct, boothMetaItems: BridgeMetaRecord[], allItems: EagleWebItem[]) {
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

  async function withBoothMetaLock<T>(product: BridgeProduct, action: () => Promise<T>): Promise<T> {
    const key = buildBoothMetaLockKey(product);
    const previous = boothMetaLocks.get(key) || Promise.resolve();
    let release: () => void = () => {};
    const gate = new Promise<void>(resolve => {
      release = resolve;
    });
    const next = previous.catch(() => {}).then(() => gate);
    boothMetaLocks.set(key, next);

    await previous.catch(() => {});
    try {
      return await action();
    } finally {
      release();
      if (boothMetaLocks.get(key) === next) {
        boothMetaLocks.delete(key);
      }
    }
  }

  function buildBoothMetaLockKey(product: BridgeProduct): string {
    const boothItemId = core().toPositiveInteger(product && product.boothItemId);
    if (boothItemId > 0) {
      return `item:${boothItemId}`;
    }

    const itemUrl = core().normalizeBoothItemUrl(product && product.itemUrl);
    return itemUrl ? `url:${itemUrl}` : `fallback:${core().safeString(product && product.name)}`;
  }

  async function processImportJobs() {
    if (isProcessingImportJobs || importJobs.size === 0) {
      return;
    }

    isProcessingImportJobs = true;
    const processingLibraryRevision = libraryRevision;
    try {
      const downloadsDir = await Promise.resolve(eagle.app.getPath("downloads"));
      const entries = await fs.readdir(downloadsDir).catch(() => []);
      const now = Date.now();

      for (const [jobId, job] of Array.from(importJobs.entries())) {
        if (processingLibraryRevision !== libraryRevision) {
          break;
        }

        if (now - job.startedAt > JOB_TIMEOUT_MS) {
          job.status = "failed";
          importJobs.delete(jobId);
          continue;
        }

        if (job.status === "importing") {
          continue;
        }

        if (job.nextAttemptAt && now < job.nextAttemptAt) {
          continue;
        }

        if (job.status === "finalizing") {
          if (await finalizeImportedDownloadFile(job, now)) {
            importJobs.delete(jobId);
          }
          continue;
        }

        const filePath = await findStableDownloadedFile(downloadsDir, entries, job.download.filename, now);
        if (!filePath) {
          continue;
        }

        try {
          await importDownloadedFile(job, filePath);
          const status = job.status as ImportJob["status"];
          if (status === "finalizing") {
            if (await finalizeImportedDownloadFile(job, Date.now())) {
              importJobs.delete(jobId);
            }
          } else if (job.status === "imported") {
            importJobs.delete(jobId);
          }
        } catch (error) {
          job.status = "watching";
          job.nextAttemptAt = now + IMPORT_RETRY_DELAY_MS;
          console.warn(`Booth import failed and will retry. job=${jobId}, filename=${job.download.filename}, error=${errorMessage(error)}`);
        }
      }
    } finally {
      isProcessingImportJobs = false;
      renderServiceStatus();
    }
  }

  async function findStableDownloadedFile(downloadsDir: string, entries: string[], filename: unknown, now: number): Promise<string | null> {
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

  async function importDownloadedFile(job: ImportJob, filePath: string): Promise<void> {
    job.status = "importing";
    const sourceStat = await fs.stat(filePath);
    const boothMetaItem = {
      id: job.boothMetaItemId,
      filePath: job.boothMetaItemFilePath
    };
    const meta = job.boothMetaItemFilePath
      ? await loadMetaForBridge(boothMetaItem)
      : core().normalizeMeta(job.boothMetaInitialMeta || core().DEFAULT_META);
    const existing = findImportedDownload(meta, job.download, await getAllItemsForBridge());
    if (existing) {
      job.status = "imported";
      return;
    }

    const itemName = core().resolveEagleItemName(getItemNameFromFilePath(filePath), job.download.filename || job.jobId);
    const annotation = buildImportedDownloadAnnotation(job);
    const itemId = await importDownloadedFileViaWebApi(job, filePath, itemName, annotation, sourceStat);

    const nextMeta = markDownloadImported(meta, job.download, itemId);
    if (job.boothMetaItemFilePath) {
      await saveMetaForBridge(boothMetaItem, nextMeta);
    } else if (job.shouldCreateBoothMetaAfterImport) {
      const boothMetaItem = await createBoothMetaItemViaWebApi(job.boothMetaFolderId, nextMeta);
      job.boothMetaItemId = boothMetaItem.id;
      job.boothMetaItemFilePath = boothMetaItem.filePath || "";
      job.shouldCreateBoothMetaAfterImport = false;
    }

    job.status = "finalizing";
    job.importedItemId = itemId;
    job.importedItemName = itemName;
    job.sourceFilePath = filePath;
    job.sourceFileSize = sourceStat.size;
  }

  async function finalizeImportedDownloadFile(job: ImportJob, now: number): Promise<boolean> {
    if (!job.sourceFilePath) {
      job.status = "imported";
      return true;
    }

    if (!job.sourceDeleted) {
      const sourceStat = await fs.stat(job.sourceFilePath).catch((error: unknown) => {
        if (hasErrorCode(error, "ENOENT")) {
          return null;
        }
        throw error;
      });
      if (!sourceStat) {
        console.warn(`Imported download source is missing before Eagle copy completed. job=${job.jobId}, path=${job.sourceFilePath}`);
        job.status = "failed";
        return true;
      }

      const importedItem = await resolveImportedJobItem(job, sourceStat);
      if (!importedItem) {
        job.nextAttemptAt = now + COPY_READY_RETRY_DELAY_MS;
        return false;
      }

      if (!await isEagleStoredFileReady(importedItem, sourceStat.size)) {
        job.nextAttemptAt = now + COPY_READY_RETRY_DELAY_MS;
        return false;
      }

      if (!await deleteImportedDownloadFile(job.sourceFilePath)) {
        job.nextAttemptAt = now + IMPORT_RETRY_DELAY_MS;
        return false;
      }
      job.sourceDeleted = true;
    }

    if (!await cleanupBoothMetaTempFile(job)) {
      job.nextAttemptAt = now + COPY_READY_RETRY_DELAY_MS;
      return false;
    }

    job.status = "imported";
    showImportCompletedNotification(job.sourceFilePath);
    return true;
  }

  async function resolveImportedJobItem(job: ImportJob, sourceStat: NodeStats): Promise<EagleWebItem | null> {
    if (job.importedItemId) {
      const item = await getItemInfoViaWebApi(job.importedItemId).catch(() => null);
      if (item) {
        return item;
      }
    }

    const item = await findImportedWebApiItem(job, job.importedItemName || "", sourceStat);
    if (item && item.id) {
      job.importedItemId = item.id;
    }
    return item;
  }

  async function importDownloadedFileViaWebApi(job: ImportJob, filePath: string, itemName: string, annotation: string, sourceStat: NodeStats): Promise<string> {
    const result = await requestEagleApi("POST", "/api/item/addFromPath", {
      path: filePath,
      name: itemName,
      website: job.download.downloadUrl,
      annotation,
      folderId: job.boothMetaFolderId
    });

    const resultItemId = extractEagleApiItemId(result);
    if (resultItemId) {
      return resultItemId;
    }

    const importedItem = await findImportedWebApiItem(job, itemName, sourceStat);
    return importedItem ? importedItem.id : "";
  }

  async function findImportedWebApiItem(job: ImportJob, itemName: string, sourceStat: NodeStats): Promise<EagleWebItem | null> {
    const downloadId = core().extractDownloadId(job.download.downloadUrl);
    const keywords = Array.from(new Set([
      downloadId > 0 ? String(downloadId) : "",
      job.download.downloadUrl,
      job.download.filename,
      itemName
    ].filter(Boolean)));

    for (const keyword of keywords) {
      const items = await listItemsViaWebApi({
        limit: 100,
        orderBy: "-CREATEDATE",
        folders: job.boothMetaFolderId,
        keyword
      });
      const match = items.find(item => isImportedWebApiItemMatch(item, job, itemName, sourceStat));
      if (match) {
        return match;
      }
    }

    const recentItems = await listItemsViaWebApi({
      limit: 100,
      orderBy: "-CREATEDATE",
      folders: job.boothMetaFolderId
    });
    return recentItems.find(item => isImportedWebApiItemMatch(item, job, itemName, sourceStat)) || null;
  }

  function isImportedWebApiItemMatch(item: EagleWebItem, job: ImportJob, itemName: string, sourceStat: NodeStats): boolean {
    if (!item || item.isDeleted) {
      return false;
    }

    const folders = Array.isArray(item.folders) ? item.folders : [];
    if (!folders.includes(job.boothMetaFolderId)) {
      return false;
    }

    if (Number.isFinite(sourceStat.size) && item.size && item.size !== sourceStat.size) {
      return false;
    }

    const downloadId = core().extractDownloadId(job.download.downloadUrl);
    const annotation = core().safeString(item.annotation);
    const url = core().safeString(item.url);
    const name = core().safeString(item.name);
    return name === itemName
      || url === job.download.downloadUrl
      || (downloadId > 0 && (annotation.includes(String(downloadId)) || url.includes(`/downloadables/${downloadId}`)))
      || annotation.includes(job.download.downloadUrl)
      || annotation.includes(job.download.filename);
  }

  function buildImportedDownloadAnnotation(job: ImportJob): string {
    return [
      `Booth item: ${job.product.itemUrl || ""}`,
      `Download: ${job.download.downloadUrl || ""}`,
      `Filename: ${job.download.filename || ""}`
    ].filter(Boolean).join("\n");
  }

  async function ensureBoothMetaForImport(product: BridgeProduct) {
    const meta = await buildBoothMetaFromProduct(product);
    const existingItem = await findExistingBoothMetaItemViaWebApi(meta);
    if (existingItem) {
      const folderId = core().getItemFolderIds(existingItem)[0] || "";
      const folder = folderId
        ? { id: folderId }
        : await findOrCreateBoothFolderForImport(meta);
      const storedMeta = await loadMetaForBridge(existingItem);
      const mergedMeta = mergeBoothMeta(storedMeta, meta);
      return {
        item: {
          id: existingItem.id,
          filePath: await findEagleStoredItemFilePath(existingItem.id, ".json")
        },
        meta: mergedMeta,
        folder,
        created: false,
        canWriteMeta: true,
        shouldCreateAfterImport: false
      };
    }

    const folder = await findOrCreateBoothFolderForImport(meta);
    const item = await createBoothMetaItemViaWebApi(folder.id, meta);
    return {
      item,
      meta,
      folder,
      created: true,
      canWriteMeta: true,
      shouldCreateAfterImport: false
    };
  }

  async function findOrCreateBoothFolderForImport(meta: BoothMeta): Promise<EagleFolder> {
    const rootFolder = await requireVrcAssetRootFolderForBridge();
    const folderName = core().resolveBoothFolderName(meta.name, meta.boothItemId);
    const existingFolder = await findDirectChildFolderForBridge(rootFolder.id, folderName);
    if (existingFolder) {
      return existingFolder;
    }

    return await createFolderViaWebApi(rootFolder.id, folderName);
  }

  async function buildBoothMetaFromProduct(product: BridgeProduct): Promise<BoothMeta> {
    const now = new Date().toISOString();
    const snapshot = await core().resolveBoothSnapshot(product || {});
    return core().normalizeMeta({
      ...core().DEFAULT_META,
      ...snapshot,
      boothItemId: snapshot.boothItemId || core().toPositiveInteger(product && product.boothItemId),
      itemUrl: snapshot.itemUrl || core().normalizeBoothItemUrl(product && product.itemUrl),
      name: snapshot.name || core().safeString(product && product.name),
      description: snapshot.description || core().safeString(product && product.description),
      thumbnailUrl: snapshot.thumbnailUrl || core().normalizeUrl(product && product.thumbnailUrl),
      shopName: snapshot.shopName || core().safeString(product && product.shopName),
      shopUrl: snapshot.shopUrl || core().normalizeBoothShopUrl(product && product.shopUrl),
      shopThumbnailUrl: snapshot.shopThumbnailUrl || core().normalizeUrl(product && product.shopThumbnailUrl),
      tags: snapshot.tags && snapshot.tags.length ? snapshot.tags : core().normalizeTags(product && product.tags),
      attachedAt: now,
      lastUpdatedAtUtc: snapshot.lastUpdatedAtUtc || now
    });
  }

  function mergeBoothMeta(storedMeta: Partial<BoothMeta>, snapshotMeta: Partial<BoothMeta>): BoothMeta {
    const stored = core().normalizeMeta(storedMeta || core().DEFAULT_META);
    const snapshot = core().normalizeMeta(snapshotMeta || core().DEFAULT_META);
    return core().normalizeMeta({
      schemaVersion: 1,
      boothItemId: snapshot.boothItemId || stored.boothItemId,
      itemUrl: snapshot.itemUrl || stored.itemUrl,
      name: snapshot.name || stored.name,
      description: snapshot.description || stored.description,
      thumbnailUrl: snapshot.thumbnailUrl || stored.thumbnailUrl,
      shopName: snapshot.shopName || stored.shopName,
      shopUrl: snapshot.shopUrl || stored.shopUrl,
      shopThumbnailUrl: snapshot.shopThumbnailUrl || stored.shopThumbnailUrl,
      tags: snapshot.tags && snapshot.tags.length ? snapshot.tags : stored.tags,
      attachedAt: stored.attachedAt || snapshot.attachedAt,
      lastUpdatedAtUtc: snapshot.lastUpdatedAtUtc || stored.lastUpdatedAtUtc,
      downloads: stored.downloads && stored.downloads.length ? stored.downloads : snapshot.downloads
    });
  }

  async function createBoothMetaItemViaWebApi(folderId: string, meta: BoothMeta): Promise<EagleWebItem> {
    const normalizedMeta = core().normalizeMeta(meta);
    const tempPath = path.join(os.tmpdir(), `ee4v-boothmeta-${Date.now()}-${Math.random().toString(36).slice(2)}.json`);
    const itemName = core().resolveEagleItemName(normalizedMeta.name, normalizedMeta.boothItemId ? String(normalizedMeta.boothItemId) : "Booth Item");
    const body = JSON.stringify(normalizedMeta, null, 2) + "\n";
    await fs.writeFile(tempPath, body, "utf8");

    try {
      const result = await requestEagleApi("POST", "/api/item/addFromPath", {
        path: tempPath,
        name: itemName,
        website: normalizedMeta.itemUrl,
        annotation: normalizedMeta.description,
        tags: [core().BOOTH_META_TAG],
        folderId
      });
      let itemId = extractEagleApiItemId(result);
      let item = itemId ? await getItemInfoViaWebApi(itemId).catch(() => null) : null;
      if (!item) {
        item = await waitForCreatedBoothMetaItem(normalizedMeta, folderId, Buffer.byteLength(body));
        itemId = item && item.id ? item.id : "";
      }

      if (!itemId) {
        throw new Error("Created BoothMeta item could not be resolved.");
      }

      const filePath = await waitForEagleStoredItemFilePath(itemId, ".json", Buffer.byteLength(body));
      if (!filePath) {
        throw new Error("Created BoothMeta JSON file could not be resolved.");
      }

      await applyBoothMetaThumbnailForBridge(itemId, normalizedMeta.thumbnailUrl);

      return {
        ...(item || {}),
        id: itemId,
        name: itemName,
        filePath,
        folders: [folderId]
      };
    } finally {
      fs.unlink(tempPath).catch(() => {});
    }
  }

  async function applyBoothMetaThumbnailForBridge(itemId: string, thumbnailUrl: string): Promise<void> {
    const normalizedThumbnailUrl = core().normalizeUrl(thumbnailUrl);
    if (!itemId || !normalizedThumbnailUrl) {
      return;
    }

    try {
      const item = await getPluginItemById(itemId);
      if (!item) {
        throw new Error("BoothMeta item could not be resolved through Plugin API.");
      }

      const tempDir = await Promise.resolve(eagle.app.getPath("temp"));
      await core().applyThumbnailToItem(item, normalizedThumbnailUrl, tempDir);
    } catch (error) {
      console.warn(`Failed to apply BoothMeta thumbnail: ${errorMessage(error)}`);
    }
  }

  async function getPluginItemById(itemId: string): Promise<EagleItem | null> {
    if (eagle.item && typeof eagle.item.getById === "function") {
      return await eagle.item.getById(itemId);
    }

    if (eagle.item && typeof eagle.item.get === "function") {
      const result = await eagle.item.get({ id: itemId });
      return Array.isArray(result) ? result[0] || null : result || null;
    }

    return null;
  }

  async function waitForCreatedBoothMetaItem(meta: BoothMeta, folderId: string, expectedSize: number): Promise<EagleWebItem | null> {
    const deadline = Date.now() + JOB_TIMEOUT_MS;
    while (Date.now() < deadline) {
      const items = await listItemsViaWebApi({
        limit: 1000,
        tags: core().BOOTH_META_TAG
      });
      for (const item of items) {
        if (!core().getItemFolderIds(item).includes(folderId) || !await isSameBoothMetaWebApiItem(item, meta)) {
          continue;
        }

        const filePath = await findEagleStoredItemFilePath(item.id, ".json");
        if (filePath) {
          const stat = await fs.stat(filePath).catch(() => null);
          if (!expectedSize || (stat && stat.isFile() && stat.size === expectedSize)) {
            return item;
          }
        }
      }

      await delay(COPY_READY_RETRY_DELAY_MS);
    }

    return null;
  }

  async function findExistingBoothMetaItemViaWebApi(meta: BoothMeta): Promise<EagleWebItem | null> {
    const items = await listItemsViaWebApi({
      limit: 1000,
      tags: core().BOOTH_META_TAG
    });

    for (const item of items) {
      if (await isSameBoothMetaWebApiItem(item, meta)) {
        return item;
      }
    }

    return null;
  }

  async function isSameBoothMetaWebApiItem(item: EagleWebItem, meta: BoothMeta): Promise<boolean> {
    if (!item || item.isDeleted) {
      return false;
    }

    const storedMeta = await loadMetaForBridge(item);
    const itemUrl = core().normalizeBoothItemUrl(item.url);
    const leftId = core().toPositiveInteger(meta && meta.boothItemId);
    const rightId = core().toPositiveInteger(itemUrl.match(/\/items\/(\d+)$/)?.[1]);
    return (leftId > 0 && rightId > 0 && leftId === rightId)
      || (meta.itemUrl && itemUrl && meta.itemUrl === itemUrl)
      || core().isSameProduct(storedMeta, meta);
  }

  function appendDownloadRequest(meta: BoothMeta, download: BridgeDownload): BoothMeta {
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

  function markDownloadImported(meta: BoothMeta, download: BridgeDownload, importedItemId: string): BoothMeta {
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
    target.importedItemIds = Array.from(new Set([
      ...(target.importedItemIds || []),
      core().safeString(importedItemId)
    ].filter(Boolean)));
    return normalized;
  }

  function findImportedDownload(meta: Partial<BoothMeta> | null | undefined, download: BridgeDownload, allItems: EagleWebItem[]): ImportedDownloadMatch | null {
    if (isDownloadImported(meta, download)) {
      return {
        matchedBy: "boothmeta"
      };
    }

    const downloadId = core().extractDownloadId(download.downloadUrl || "");
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

  function isDownloadImported(meta: Partial<BoothMeta> | null | undefined, download: BridgeDownload): boolean {
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

  function normalizeDownloadRequest(download: BridgeDownload): BoothDownloadMeta {
    return {
      downloadUrl: core().normalizeDownloadUrl(download.downloadUrl),
      downloadId: core().extractDownloadId(download.downloadUrl || ""),
      filename: core().safeString(download.filename)
    };
  }

  function isPartialDownloadName(filename: unknown): boolean {
    const lower = core().safeString(filename).toLowerCase();
    return PARTIAL_EXTENSIONS.some(extension => lower.endsWith(extension));
  }

  function isPartialDownloadForExpectedName(entry: string, expectedName: string): boolean {
    const lowerEntry = core().safeString(entry).toLowerCase();
    const lowerExpected = core().safeString(expectedName).toLowerCase();
    return PARTIAL_EXTENSIONS.some(extension => lowerEntry === `${lowerExpected}${extension}`);
  }

  function getItemNameFromFilePath(filePath: string): string {
    const filename = path.basename(filePath);
    const extension = path.extname(filename);
    return extension ? filename.slice(0, -extension.length) : filename;
  }

  async function loadMetaForBridge(item: Partial<EagleWebItem>): Promise<BoothMeta> {
    if (item && item.filePath) {
      try {
        const raw = await fs.readFile(item.filePath, "utf8");
        return core().normalizeMeta(JSON.parse(raw));
      } catch (error) {
        console.warn(`BoothMeta file could not be read directly: ${errorMessage(error)}`);
      }
    }

    if (item && item.id) {
      const storedPath = await findEagleStoredItemFilePath(item.id, ".json");
      if (storedPath) {
        try {
          const raw = await fs.readFile(storedPath, "utf8");
          return core().normalizeMeta(JSON.parse(raw));
        } catch (error) {
          console.warn(`BoothMeta stored file could not be read: ${errorMessage(error)}`);
        }
      }
    }

    return { ...core().DEFAULT_META };
  }

  async function saveMetaForBridge(item: Partial<EagleWebItem>, meta: BoothMeta): Promise<void> {
    const normalized = core().normalizeMeta(meta);
    if (item && item.filePath) {
      await fs.writeFile(item.filePath, JSON.stringify(normalized, null, 2) + "\n", "utf8");
      return;
    }

    if (item && item.id) {
      const storedPath = await findEagleStoredItemFilePath(item.id, ".json");
      if (storedPath) {
        await fs.writeFile(storedPath, JSON.stringify(normalized, null, 2) + "\n", "utf8");
        return;
      }
    }

    throw new Error("BoothMeta JSON file path could not be resolved.");
  }

  async function getAllItemsForBridge(): Promise<EagleWebItem[]> {
    try {
      return await listAllItemsViaWebApi();
    } catch (error) {
      console.warn(`Eagle Web API item list failed: ${errorMessage(error)}`);
      return [];
    }
  }

  async function loadBoothMetaItemsForBridge(rootFolder: EagleFolder): Promise<BridgeMetaRecord[]> {
    const folders = await getFoldersForBridge();
    const descendantFolderIds = new Set(findDescendantFolderIds(folders, rootFolder.id));
    const items = await listItemsViaWebApi({
      limit: 1000,
      tags: core().BOOTH_META_TAG
    });

    const records: BridgeMetaRecord[] = [];
    for (const item of items) {
      const folderIds = core().getItemFolderIds(item);
      const folderId = folderIds.find(id => descendantFolderIds.has(id)) || "";
      if (!folderId) {
        continue;
      }

      const fallbackMeta = core().normalizeMeta({
        ...core().DEFAULT_META,
        boothItemId: core().toPositiveInteger(core().normalizeBoothItemUrl(item.url).match(/\/items\/(\d+)$/)?.[1]),
        itemUrl: core().normalizeBoothItemUrl(item.url),
        name: core().safeString(item.name),
        description: core().safeString(item.annotation)
      });
      const meta = mergeBoothMeta(await loadMetaForBridge(item), fallbackMeta);
      if (!meta.itemUrl || meta.boothItemId <= 0) {
        continue;
      }

      records.push({
        item,
        folder: folders.find(folder => folder.id === folderId) || { id: folderId },
        meta
      });
    }

    return records;
  }

  async function requireVrcAssetRootFolderForBridge(): Promise<EagleFolder> {
    const rootFolder = await findVrcAssetRootFolderForBridge();
    if (!rootFolder) {
      throw new Error("library root VRCAsset folder was not found.");
    }

    return rootFolder;
  }

  async function findVrcAssetRootFolderForBridge(): Promise<EagleFolder | null> {
    const folders = await getFoldersForBridge();
    const matches = folders.filter(folder => folder.name === "VRCAsset" && !folder.parent);
    return matches.length === 1 ? matches[0] : null;
  }

  async function findDirectChildFolderForBridge(parentId: string, name: string): Promise<EagleFolder | null> {
    const folders = await getFoldersForBridge();
    return folders.find(folder => folder.parent === parentId && folder.name === name) || null;
  }

  async function createFolderViaWebApi(parentId: string, folderName: string): Promise<EagleFolder> {
    const result = await requestEagleApi("POST", "/api/folder/create", {
      folderName,
      parent: parentId
    });
    const folder = (result.data && typeof result.data === "object" ? result.data : {}) as Partial<EagleFolder>;
    return {
      ...folder,
      id: core().safeString(folder.id),
      name: core().safeString(folder.name) || folderName,
      parent: parentId
    };
  }

  async function getFoldersForBridge(): Promise<EagleFolder[]> {
    const result = await requestEagleApi("GET", "/api/folder/list");
    return flattenFolders(Array.isArray(result.data) ? result.data : []);
  }

  function flattenFolders(folders: EagleFolder[], parentId = ""): EagleFolder[] {
    const result: EagleFolder[] = [];
    folders.forEach((folder: EagleFolder) => {
      result.push({
        ...folder,
        parent: parentId
      });
      result.push(...flattenFolders(Array.isArray(folder.children) ? folder.children : [], folder.id));
    });
    return result;
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

  async function listAllItemsViaWebApi(): Promise<EagleWebItem[]> {
    const limit = 200;
    const items: EagleWebItem[] = [];
    let offset = 0;

    while (true) {
      const page = await listItemsViaWebApi({
        limit,
        offset,
        orderBy: "-CREATEDATE"
      });
      items.push(...page);

      if (page.length < limit) {
        break;
      }

      offset += limit;
    }

    return items;
  }

  async function listItemsViaWebApi(params: ListItemsParams): Promise<EagleWebItem[]> {
    const query = buildQueryString({ ...params });
    const result = await requestEagleApi("GET", `/api/item/list${query ? `?${query}` : ""}`);
    return Array.isArray(result.data) ? result.data as EagleWebItem[] : [];
  }

  async function getItemInfoViaWebApi(itemId: string): Promise<EagleWebItem | null> {
    const query = buildQueryString({ id: itemId });
    const result = await requestEagleApi("GET", `/api/item/info?${query}`);
    return result.data && typeof result.data === "object" ? result.data as EagleWebItem : null;
  }

  async function cleanupBoothMetaTempFile(job: ImportJob): Promise<boolean> {
    if (!job.boothMetaTempFilePath) {
      return true;
    }

    const sourceStat = await fs.stat(job.boothMetaTempFilePath).catch((error: unknown) => {
      if (hasErrorCode(error, "ENOENT")) {
        return null;
      }
      throw error;
    });
    if (!sourceStat) {
      job.boothMetaTempFilePath = "";
      return true;
    }

    const item = await resolveBoothMetaTempItem(job, sourceStat);
    if (!item || !await isEagleStoredFileReady(item, sourceStat.size)) {
      return false;
    }

    await fs.unlink(job.boothMetaTempFilePath);
    job.boothMetaTempFilePath = "";
    return true;
  }

  async function resolveBoothMetaTempItem(job: ImportJob, sourceStat: NodeStats): Promise<EagleWebItem | null> {
    if (job.boothMetaItemId) {
      const item = await getItemInfoViaWebApi(job.boothMetaItemId).catch(() => null);
      if (item) {
        return item;
      }
    }

    const items = await listItemsViaWebApi({
      limit: 100,
      orderBy: "-CREATEDATE",
      folders: job.boothMetaFolderId,
      tags: core().BOOTH_META_TAG,
      keyword: job.boothMetaItemName || ""
    });
    const item = items.find(candidate => {
      if (!candidate || candidate.isDeleted) {
        return false;
      }
      const folders = Array.isArray(candidate.folders) ? candidate.folders : [];
      return folders.includes(job.boothMetaFolderId)
        && core().safeString(candidate.name) === core().safeString(job.boothMetaItemName)
        && (!candidate.size || candidate.size === sourceStat.size);
    }) || null;

    if (item && item.id) {
      job.boothMetaItemId = item.id;
    }
    return item;
  }

  async function isEagleStoredFileReady(item: EagleWebItem | null, expectedSize: number): Promise<boolean> {
    const storedPath = await findEagleStoredItemFilePath(item && item.id);
    if (!storedPath) {
      return false;
    }

    const stat = await fs.stat(storedPath).catch(() => null);
    return Boolean(stat && stat.isFile() && stat.size === expectedSize);
  }

  async function findEagleStoredItemFilePath(itemId: unknown, preferredExtension = ""): Promise<string> {
    const itemDir = await getEagleItemDirectoryPath(itemId);
    if (!itemDir) {
      return "";
    }

    const entries = await fs.readdir(itemDir, { withFileTypes: true }).catch(() => []);
    const files = entries
      .filter(entry => entry.isFile() && entry.name !== "metadata.json")
      .map(entry => path.join(itemDir, entry.name));
    if (preferredExtension) {
      const lowerExtension = preferredExtension.toLowerCase();
      const preferred = files.find(file => path.extname(file).toLowerCase() === lowerExtension);
      if (preferred) {
        return preferred;
      }
    }

    return files[0] || "";
  }

  async function waitForEagleStoredItemFilePath(itemId: string, preferredExtension: string, expectedSize: number): Promise<string> {
    const deadline = Date.now() + JOB_TIMEOUT_MS;
    while (Date.now() < deadline) {
      const filePath = await findEagleStoredItemFilePath(itemId, preferredExtension);
      if (filePath) {
        const stat = await fs.stat(filePath).catch(() => null);
        if (!expectedSize || (stat && stat.isFile() && stat.size === expectedSize)) {
          return filePath;
        }
      }

      await delay(COPY_READY_RETRY_DELAY_MS);
    }

    return "";
  }

  async function getEagleItemDirectoryPath(itemId: unknown): Promise<string> {
    const id = core().safeString(itemId);
    if (!id) {
      return "";
    }

    const libraryPath = await getEagleLibraryPath();
    return libraryPath ? path.join(libraryPath, "images", `${id}.info`) : "";
  }

  function delay(ms: number): Promise<void> {
    return new Promise(resolve => {
      timers.setTimeout(resolve, ms);
    });
  }

  async function getEagleLibraryPath(): Promise<string> {
    if (eagleLibraryPath) {
      return eagleLibraryPath;
    }

    const result = await requestEagleApi("GET", "/api/library/info");
    const data = result.data && typeof result.data === "object" ? result.data as JsonRecord : {};
    const library = data.library && typeof data.library === "object" ? data.library as JsonRecord : {};
    eagleLibraryPath = core().safeString(library.path);
    return eagleLibraryPath;
  }

  function requestEagleApi(method: "GET" | "POST", apiPath: string, payload?: unknown): Promise<EagleApiResult> {
    return new Promise((resolve, reject) => {
      const body = payload ? JSON.stringify(payload) : "";
      const request = http.request({
        hostname: EAGLE_API_HOST,
        port: EAGLE_API_PORT,
        path: apiPath,
        method,
        headers: {
          Accept: "application/json",
          ...(body
            ? {
                "Content-Type": "application/json",
                "Content-Length": Buffer.byteLength(body)
              }
            : {})
        }
      }, response => {
        const chunks: string[] = [];
        response.setEncoding("utf8");
        response.on("data", chunk => chunks.push(String(chunk)));
        response.on("end", () => {
          const text = chunks.join("");
          let result: EagleApiResult = {};
          try {
            result = text ? JSON.parse(text) : {};
          } catch (error) {
            reject(new Error(`Eagle Web API response was not JSON. HTTP ${response.statusCode || 0}`));
            return;
          }

          if ((response.statusCode || 0) < 200 || (response.statusCode || 0) >= 300) {
            reject(new Error(`Eagle Web API HTTP ${response.statusCode || 0}`));
            return;
          }

          if (result.status && result.status !== "success") {
            reject(new Error(result.message || result.error || `Eagle Web API returned ${result.status}`));
            return;
          }

          resolve(result);
        });
      });

      request.on("error", error => {
        reject(new Error(`Eagle Web API request failed: ${error.message}`));
      });
      request.setTimeout(EAGLE_API_TIMEOUT_MS, () => {
        request.destroy(new Error("timeout"));
      });

      if (body) {
        request.write(body);
      }
      request.end();
    });
  }

  function buildQueryString(params: Record<string, unknown>): string {
    const query = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value === undefined || value === null || value === "") {
        return;
      }
      query.set(key, String(value));
    });
    return query.toString();
  }

  function extractEagleApiItemId(result: EagleApiResult): string {
    const data = result.data && typeof result.data === "object" ? result.data as JsonRecord : {};
    const dataArray = Array.isArray(result.data) ? result.data : [];
    const candidates = [
      result.id,
      result.itemId,
      data.id,
      data.itemId,
      dataArray[0] && typeof dataArray[0] === "object" ? (dataArray[0] as JsonRecord).id : ""
    ];

    return candidates
      .map(value => core().safeString(value))
      .find(Boolean) || "";
  }

  async function deleteImportedDownloadFile(filePath: string): Promise<boolean> {
    try {
      await fs.unlink(filePath);
      fileSnapshots.delete(filePath);
      return true;
    } catch (error) {
      if (hasErrorCode(error, "ENOENT")) {
        fileSnapshots.delete(filePath);
        return true;
      }
      console.warn(`Imported download file could not be deleted: ${errorMessage(error)}`);
      return false;
    }
  }

  function showImportCompletedNotification(filePath: string): void {
    Promise.resolve(eagle.notification.show({
      title: core().t("notification.title", "Booth Compat"),
      body: core().t("notification.importCompleted", "Imported {{name}} into Eagle.", {
        name: path.basename(filePath)
      }),
      mute: true,
      duration: 3000
    })).catch(error => {
      console.warn(`Imported notification failed: ${errorMessage(error)}`);
    });
  }

  function kickImportJobProcessing() {
    if (isProcessingImportJobs || importJobs.size === 0) {
      return;
    }

    processImportJobs().catch(error => {
      console.error(error);
    });
  }

  function core(): BoothCompatCore {
    return window.BoothCompatCore;
  }

  function normalizeLocale(locale: unknown): string {
    return core().safeString(locale).replace("_", "-") || "en";
  }

  function requireElement<T extends HTMLElement>(id: string, constructor: { new(): T }): T {
    const element = document.getElementById(id);
    if (!(element instanceof constructor)) {
      throw new Error(`Element #${id} was not found.`);
    }
    return element;
  }

  function readJson(request: NodeIncomingMessage): Promise<JsonRecord> {
    return new Promise((resolve, reject) => {
      const chunks: Uint8Array[] = [];
      let byteLength = 0;
      let tooLarge = false;
      request.on("data", chunk => {
        const bytes = typeof chunk === "string" ? new TextEncoder().encode(chunk) : chunk;
        byteLength += bytes.length;
        if (byteLength > MAX_REQUEST_BODY_BYTES) {
          tooLarge = true;
          return;
        }
        chunks.push(bytes);
      });
      request.on("end", () => {
        if (tooLarge) {
          const error = new Error("Request body is too large.") as Error & { statusCode?: number };
          error.statusCode = 413;
          reject(error);
          return;
        }

        if (chunks.length === 0) {
          resolve({});
          return;
        }

        try {
          const text = new TextDecoder().decode(Buffer.concat(chunks));
          const parsed = JSON.parse(text);
          resolve(parsed && typeof parsed === "object" ? parsed as JsonRecord : {});
        } catch (error) {
          reject(new Error("Invalid JSON request body."));
        }
      });
      request.on("error", reject);
    });
  }

  function sendJson(response: NodeServerResponse, statusCode: number, payload: unknown): void {
    const body = statusCode === 204 ? "" : JSON.stringify(payload || {});
    const headers: Record<string, string | number> = {
      "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type, X-EE4V-Bridge-Token",
      "Content-Type": "application/json; charset=utf-8",
      "Content-Length": Buffer.byteLength(body)
    };
    const origin = responseOrigins.get(response);
    if (origin) {
      headers["Access-Control-Allow-Origin"] = origin;
      headers.Vary = "Origin";
    }
    response.writeHead(statusCode, headers);
    response.end(body);
  }

  function headerValue(value: string | undefined): string {
    return core().safeString(value);
  }

  function isAllowedBoothOrigin(origin: string): boolean {
    try {
      const url = new URL(origin);
      const hostname = url.hostname.toLowerCase();
      return url.protocol === "https:" && (hostname === "booth.pm" || hostname.endsWith(".booth.pm"));
    } catch {
      return false;
    }
  }
})();
