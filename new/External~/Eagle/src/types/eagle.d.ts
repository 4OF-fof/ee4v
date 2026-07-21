type EagleTheme = "LIGHT" | "DARK" | string;
type JsonRecord = Record<string, unknown>;

interface EagleBounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

interface EagleDisplay {
  bounds: EagleBounds;
  workArea?: EagleBounds;
}

interface EagleFolder {
  id: string;
  name?: string;
  parent?: string;
  children?: EagleFolder[];
  open?: () => Promise<void>;
}

interface EagleTag {
  name?: string;
}

type EagleTagValue = string | EagleTag;

interface EagleItem {
  id: string;
  name?: string;
  ext?: string;
  url?: string;
  annotation?: string;
  filePath?: string;
  folders?: string[];
  folderIds?: string[];
  folderId?: string;
  tags?: EagleTagValue[];
  size?: number;
  isDeleted?: boolean;
  save(): Promise<void>;
  replaceFile(path: string): Promise<void>;
  setCustomThumbnail(path: string): Promise<void>;
}

interface EagleApi {
  app: {
    theme: EagleTheme | Promise<EagleTheme>;
    locale: string;
    isDarkColors(): boolean | Promise<boolean>;
    getPath(name: "downloads" | "temp" | string): string | Promise<string>;
  };
  folder: {
    getAll(): Promise<EagleFolder[]>;
    createSubfolder(parentId: string, options: { name: string }): Promise<EagleFolder>;
  };
  item: {
    getAll?: () => Promise<EagleItem[]>;
    getItems?: () => Promise<EagleItem[]>;
    getSelected(): Promise<Array<Pick<EagleItem, "id">>>;
    getById(id: string): Promise<EagleItem | null>;
    get?(options?: unknown): Promise<EagleItem[]>;
    addFromPath(path: string, options: { folders: string[]; name: string; tags: string[] }): Promise<string>;
    select(ids: string[]): Promise<void>;
  };
  notification: {
    show(options: { title: string; body: string; mute?: boolean; duration?: number }): Promise<void>;
  };
  screen: {
    getCursorScreenPoint(): Promise<{ x: number; y: number }>;
    getDisplayNearestPoint(point: { x: number; y: number }): Promise<EagleDisplay>;
  };
  window: {
    setAlwaysOnTop(value: boolean): Promise<void>;
    setResizable(value: boolean): Promise<void>;
    setBounds(bounds: EagleBounds): Promise<void>;
    show(): Promise<void>;
    hide(): Promise<void>;
  };
  onPluginCreate(callback: () => void | Promise<void>): void;
  onPluginRun(callback: () => void | Promise<void>): void;
  onPluginShow(callback: () => void | Promise<void>): void;
  onPluginHide(callback: () => void | Promise<void>): void;
  onThemeChanged(callback: (theme: EagleTheme) => void): void;
  onLibraryChanged(callback: (libraryPath: string) => void | Promise<void>): void;
}

interface I18nextApi {
  t(key: string, options?: Record<string, unknown>): string;
}

interface BoothDownloadMeta {
  downloadUrl: string;
  downloadId: number;
  filename: string;
  requestedAt?: string;
  importedAt?: string;
  importedItemIds?: string[];
}

interface BoothDownloadInput {
  downloadUrl?: string;
  downloadId?: number;
  filename?: string;
  requestedAt?: string;
  importedAt?: string;
  importedItemIds?: unknown[];
}

interface BoothMeta {
  schemaVersion: number;
  boothItemId: number;
  itemUrl: string;
  name: string;
  description: string;
  thumbnailUrl: string;
  shopName: string;
  shopUrl: string;
  shopThumbnailUrl: string;
  tags: string[];
  attachedAt: string;
  lastUpdatedAtUtc: string;
  downloads: BoothDownloadMeta[];
}

interface BoothProductInput {
  boothItemId?: number;
  itemUrl?: string;
  name?: string;
  description?: string;
  thumbnailUrl?: string;
  shopName?: string;
  shopUrl?: string;
  shopThumbnailUrl?: string;
  tags?: unknown[];
  downloads?: BoothDownloadInput[];
  lastUpdatedAtUtc?: string;
}

interface BoothSnapshot extends BoothProductInput {
  boothItemId: number;
  itemUrl: string;
  name: string;
  description: string;
  thumbnailUrl: string;
  shopName: string;
  shopUrl: string;
  shopThumbnailUrl: string;
  tags: string[];
  lastUpdatedAtUtc: string;
}

interface BoothItemReference {
  itemId: number;
  fetchUrl: string;
  normalizedUrl: string;
}

interface BoothMetaRecord {
  item: EagleItem;
  folder: EagleFolder | null;
  meta: BoothMeta;
}

interface BoothCompatCore {
  BOOTH_META_TAG: string;
  DEFAULT_META: BoothMeta;
  t(key: string, fallback: string, options?: Record<string, unknown>): string;
  ensureBoothMetaForUrl(itemUrl: string): Promise<BoothMetaRecord & { rootFolder: EagleFolder | null; created: boolean }>;
  ensureBoothMetaForProduct(product: BoothProductInput, snapshotOverride?: Partial<BoothProductInput>): Promise<BoothMetaRecord & { rootFolder: EagleFolder | null; created: boolean }>;
  resolveBoothSnapshot(product: BoothProductInput, snapshotOverride?: Partial<BoothProductInput>): Promise<BoothSnapshot>;
  loadBoothMetaItems(rootFolder: EagleFolder): Promise<BoothMetaRecord[]>;
  getAllItems(): Promise<EagleItem[]>;
  requireVrcAssetRootFolder(): Promise<EagleFolder>;
  findVrcAssetRootFolder(): Promise<EagleFolder | null>;
  findDirectChildFolder(parentId: string, name: string): Promise<EagleFolder | null>;
  loadMetaFromItem(item: EagleItem): Promise<BoothMeta>;
  saveMetaToItem(item: EagleItem, meta: Partial<BoothMeta>): Promise<void>;
  applyThumbnailToItem(item: EagleItem, thumbnailUrl: string, tempDir: string): Promise<void>;
  fetchBoothSnapshot(boothRef: BoothItemReference): Promise<BoothSnapshot>;
  normalizeMeta(meta: Partial<BoothMeta> | BoothProductInput | unknown): BoothMeta;
  normalizeDownloads(downloads: unknown): BoothDownloadMeta[];
  isSameProduct(meta: Partial<BoothMeta>, product: BoothProductInput): boolean;
  isBoothMetaItem(item: EagleItem | null): item is EagleItem;
  ensureBoothMetaTag(tags: unknown): string[];
  getItemFolderIds(item: Partial<EagleItem>): string[];
  buildDownloadKey(download: Partial<BoothDownloadInput>): string;
  extractDownloadId(downloadUrl: unknown): number;
  parseBoothItemReference(value: unknown): BoothItemReference | null;
  normalizeBoothItemUrl(value: unknown): string;
  normalizeCanonicalBoothItemUrl(value: unknown): string;
  normalizeDownloadUrl(value: unknown): string;
  normalizeBoothShopUrl(value: unknown): string;
  normalizeUrl(value: unknown): string;
  normalizeFilename(value: unknown): string;
  normalizeTags(tags: unknown): string[];
  normalizeTimestamp(value: unknown): string;
  resolveBoothFolderName(name: unknown, fallbackItemId: unknown): string;
  resolveEagleItemName(name: unknown, fallbackName: unknown): string;
  safeString(value: unknown): string;
  toPositiveInteger(value: unknown): number;
  firstNonEmpty(values: unknown[]): string;
  isMetaEquivalent(left: Partial<BoothMeta>, right: Partial<BoothMeta>): boolean;
}

interface NodeFsPromises {
  readFile(path: string, encoding: "utf8"): Promise<string>;
  writeFile(path: string, data: string | Uint8Array, encoding?: string): Promise<void>;
  readdir(path: string): Promise<string[]>;
  readdir(path: string, options: { withFileTypes: true }): Promise<NodeDirent[]>;
  stat(path: string): Promise<NodeStats>;
  unlink(path: string): Promise<void>;
}

interface NodeDirent {
  name: string;
  isFile(): boolean;
  isDirectory(): boolean;
}

interface NodeStats {
  size: number;
  mtimeMs: number;
  isFile(): boolean;
  isDirectory(): boolean;
}

interface NodePath {
  basename(path: string): string;
  basename(path: string, suffix?: string): string;
  dirname(path: string): string;
  extname(path: string): string;
  join(...paths: string[]): string;
}

interface NodeTimerHandle {}

interface NodeTimers {
  setInterval(callback: () => void, ms: number): NodeTimerHandle;
  setTimeout(callback: () => void, ms: number): NodeTimerHandle;
}

interface NodeOs {
  tmpdir(): string;
}

interface NodeBufferConstructor {
  byteLength(value: string): number;
  concat(values: Uint8Array[]): Uint8Array;
}

interface NodeIncomingMessage {
  method?: string;
  url?: string;
  statusCode?: number;
  headers: { [name: string]: string | undefined; location?: string };
  setEncoding(encoding: string): void;
  on(event: "data", callback: (chunk: string | Uint8Array) => void): void;
  on(event: "end", callback: () => void): void;
  on(event: "error", callback: (error: Error) => void): void;
  resume(): void;
}

interface NodeServerResponse {
  writeHead(statusCode: number, headers: Record<string, string | number>): void;
  end(body?: string): void;
}

interface NodeClientRequest {
  on(event: "error", callback: (error: Error) => void): void;
  setTimeout(ms: number, callback: () => void): void;
  destroy(error?: Error): void;
  write(body: string): void;
  end(): void;
}

interface NodeServer {
  on(event: "error", callback: (error: Error) => void): void;
  listen(port: number, host: string, callback: () => void): void;
}

interface NodeHttp {
  createServer(callback: (request: NodeIncomingMessage, response: NodeServerResponse) => void): NodeServer;
  request(options: {
    hostname: string;
    port: number;
    path: string;
    method: string;
    headers: Record<string, string | number>;
  }, callback: (response: NodeIncomingMessage) => void): NodeClientRequest;
}

interface NodeHttps {
  get(url: string, options: { headers: Record<string, string> }, callback: (response: NodeIncomingMessage) => void): NodeClientRequest;
}

declare function require(name: "fs/promises"): NodeFsPromises;
declare function require(name: "path"): NodePath;
declare function require(name: "timers"): NodeTimers;
declare function require(name: "os"): NodeOs;
declare function require(name: "http"): NodeHttp;
declare function require(name: "https"): NodeHttps;
declare function require(name: "crypto"): { randomBytes(size: number): { toString(encoding: string): string } };
declare function require(name: string): unknown;

declare const eagle: EagleApi;
declare const i18next: I18nextApi;
declare const Buffer: NodeBufferConstructor;

interface Window {
  BoothCompatCore: BoothCompatCore;
}
