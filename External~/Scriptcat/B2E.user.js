// ==UserScript==
// @name         Booth to Ealge
// @namespace    https://4of.dev
// @version      0.1.3
// @description  Add Eagle import badges and actions to the BOOTH library and item pages.
// @match        https://accounts.booth.pm/library*
// @match        https://accounts.booth.pm/library/gifts*
// @match        https://booth.pm/items/*
// @match        https://booth.pm/*/items/*
// @match        https://*.booth.pm/items/*
// @match        https://*.booth.pm/*/items/*
// @grant        GM_xmlhttpRequest
// @grant        GM_openInTab
// @connect      127.0.0.1
// @connect      localhost
// @run-at       document-idle
// ==/UserScript==

(function () {
  "use strict";

  // Eagle plugin bridge
  const BRIDGE_BASE = "http://127.0.0.1:48196";
  // bridge の生存確認間隔
  const HEALTH_INTERVAL_MS = 10000;
  // BOOTH の DOM 更新が連続した時に status API 呼び出しをまとめる待ち時間。
  const STATUS_DEBOUNCE_MS = 500;
  // import 要求後、Eagle 側の取込完了を反映するために追加で status 更新するタイミング。
  const FOLLOW_UP_STATUS_DELAYS_MS = [2000, 5000, 10000];
  // BOOTH の download tab を開いた後、自動で閉じるまでの待ち時間。
  const DOWNLOAD_TAB_CLOSE_DELAY_MS = 1500;
  const OWNED_SELECTORS = {
    actionHost: ".ee4v-booth-inline-action-host",
    productBadgeHost: ".ee4v-booth-product-badge-host",
    downloadBadgeHost: ".ee4v-booth-download-badge-host",
  };

  const BOOTH_LIBRARY_SELECTORS = {
    // 商品カードの起点。サムネイルから親カードへ辿る。
    thumbnail: ".l-library-item-thumbnail",
    cardFromThumbnail: ".bg-white",
    // 商品情報。商品 URL から item id を抽出し、名前やショップ情報を補完する。
    productLink: 'a[href*="/items/"]',
    productName: ".text-text-default.font-bold.text-16",
    shopName: ".text-14.text-text-gray600",
    shopLink: 'a[href*=".booth.pm"]:not([href*="accounts.booth.pm"])',
    header: ".flex.gap-8.desktop\\:gap-16.border-b",
    // ダウンロード行。通常の BOOTH download button と同じ行に import action を差し込む。
    downloadTrigger: '.js-download-button[data-test="downloadable"]',
    downloadRow:
      "div.mt-16.desktop\\:flex.desktop\\:justify-between.desktop\\:items-center",
    downloadFilename: ".text-14",
    downloadActions: ".shrink-0.flex.items-center.gap-8",
    otherDownloads: '[data-test="other-downloads-button"]',
  };
  const BOOTH_ITEM_SELECTORS = {
    root: ".summary",
    productName: ".summary header h2, .market-item-detail h2",
    shopLink:
      '.summary header a[href*=".booth.pm/"]:not([href*="accounts.booth.pm"])',
    variations: "#variations",
    variationRow: ".variation-item",
    variationName: ".variation-name",
    downloadTrigger:
      'a[href*="/downloadables/"]:not([href*="/deeplink"])',
    otherDownloads:
      '[data-test="other-downloads-button"], .js-download-free-button',
  };
  const pageAdapter = createCompositePageAdapter([
    createBoothLibraryPageAdapter(BOOTH_LIBRARY_SELECTORS),
    createBoothItemPageAdapter(BOOTH_ITEM_SELECTORS),
  ]);

  const state = {
    bridgeConnected: false,
    bridgeToken: "",
    rootFolderAvailable: false,
    healthTimerId: 0,
    statusTimerId: 0,
    productStates: new Map(),
    downloadStates: new Map(),
    pendingDownloads: new Map(),
  };

  init();

  function init() {
    injectStyles();
    observeDomChanges();
    refreshBridgeHealth();
    state.healthTimerId = window.setInterval(
      refreshBridgeHealth,
      HEALTH_INTERVAL_MS,
    );
    scheduleStatusRefresh();
  }

  function injectStyles() {
    const style = document.createElement("style");
    style.textContent = `
      .ee4v-booth-inline-action-host {
        display: inline-flex !important;
        align-items: center !important;
        margin-right: 4px !important;
      }

      .ee4v-booth-inline-action-host--block {
        display: flex !important;
        align-items: stretch !important;
        width: 100% !important;
        margin: 8px 0 0 !important;
      }

      .ee4v-booth-inline-action {
        display: inline-flex !important;
        align-items: center !important;
        min-height: 24px !important;
        padding: 4px 10px !important;
        border: 1px solid #93c5fd !important;
        border-radius: 9999px !important;
        background: #eff6ff !important;
        color: #1d4ed8 !important;
        font-size: 12px !important;
        line-height: 1 !important;
        font-weight: 700 !important;
        white-space: nowrap !important;
        cursor: pointer !important;
      }

      .ee4v-booth-inline-action-host--block .ee4v-booth-inline-action {
        justify-content: center !important;
        width: 100% !important;
        min-height: 32px !important;
        border-radius: 8px !important;
      }

      .ee4v-booth-inline-action[aria-disabled="true"] {
        cursor: default !important;
        opacity: 0.7 !important;
      }

      .ee4v-booth-inline-action[data-state="imported"] {
        border-color: #86efac !important;
        background: #ecfdf3 !important;
        color: #166534 !important;
      }

      .ee4v-booth-inline-action[data-state="pending"] {
        border-color: #fde68a !important;
        background: #fffbeb !important;
        color: #92400e !important;
      }
    `;
    document.head.appendChild(style);
  }

  function observeDomChanges() {
    const observer = new MutationObserver(() => {
      scheduleStatusRefresh();
    });

    observer.observe(document.body, {
      childList: true,
      subtree: true,
    });
  }

  async function refreshBridgeHealth() {
    try {
      const result = await bridgeRequest("GET", "/health");
      state.bridgeConnected = Boolean(result && result.ok);
      state.rootFolderAvailable = Boolean(result && result.rootFolderAvailable);
      state.bridgeToken = String((result && result.token) || "");
    } catch (error) {
      state.bridgeConnected = false;
      state.rootFolderAvailable = false;
      state.bridgeToken = "";
    }

    scheduleStatusRefresh();
  }

  function scheduleStatusRefresh() {
    window.clearTimeout(state.statusTimerId);
    state.statusTimerId = window.setTimeout(() => {
      refreshStatuses().catch((error) => {
        console.error(error);
      });
    }, STATUS_DEBOUNCE_MS);
  }

  async function refreshStatuses() {
    const contexts = collectCardContexts();

    if (
      !state.bridgeConnected ||
      !state.rootFolderAvailable ||
      contexts.length === 0
    ) {
      clearAddedElements();
      return;
    }

    const result = await bridgeRequest(
      "POST",
      "/v1/status",
      buildStatusPayload(contexts),
    );
    if (!result || !Array.isArray(result.items)) {
      return;
    }

    state.productStates.clear();
    state.downloadStates.clear();

    result.items.forEach((productState) => {
      const productKey = buildProductKey({
        boothItemId: productState.boothItemId,
        itemUrl: productState.itemUrl,
      });
      state.productStates.set(productKey, productState);

      (productState.downloads || []).forEach((downloadState) => {
        const downloadKey = buildDownloadKey({
          product: {
            boothItemId: productState.boothItemId,
            itemUrl: productState.itemUrl,
          },
          download: downloadState,
        });
        state.downloadStates.set(downloadKey, downloadState);
      });
    });

    for (const [downloadKey, pending] of Array.from(
      state.pendingDownloads.entries(),
    )) {
      const downloadState = state.downloadStates.get(downloadKey);
      if (downloadState && downloadState.imported) {
        state.pendingDownloads.delete(downloadKey);
      } else if (Date.now() - pending.startedAt > 20 * 60 * 1000) {
        state.pendingDownloads.delete(downloadKey);
      }
    }

    renderContexts(collectCardContexts());
  }

  function collectCardContexts() {
    return pageAdapter.collectContexts(document);
  }

  function renderContexts(contexts) {
    if (!state.bridgeConnected || !state.rootFolderAvailable) {
      clearAddedElements();
      return;
    }

    contexts.forEach((context) => {
      context.downloads.forEach((download) => {
        ensureInlineImportAction(context, download);
      });
    });
  }

  function ensureInlineImportAction(context, download) {
    let host = download.actions.querySelector(OWNED_SELECTORS.actionHost);
    if (!host) {
      host = document.createElement("div");
      host.className = "ee4v-booth-inline-action-host";
      if (download.actionLayout === "block") {
        host.classList.add("ee4v-booth-inline-action-host--block");
        download.actions.appendChild(host);
      } else {
        download.actions.insertBefore(host, download.actions.firstChild);
      }
    }

    let item = host.querySelector(
      '.ee4v-booth-inline-action[data-download-key="' +
        buildDownloadKey(download) +
        '"]',
    );
    if (!item) {
      item = document.createElement("button");
      item.type = "button";
      item.className = "ee4v-booth-inline-action";
      item.dataset.downloadKey = buildDownloadKey(download);
      item.textContent = "Import Eagle";
      item.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();
        handleImportClick(context, download).catch((error) => {
          console.error(error);
        });
      });
      host.appendChild(item);
    }

    const downloadKey = buildDownloadKey(download);
    const downloadState = state.downloadStates.get(downloadKey);
    const imported = Boolean(downloadState && downloadState.imported);
    const pending =
      state.pendingDownloads.has(downloadKey) ||
      Boolean(downloadState && downloadState.pending);
    const enabled =
      state.bridgeConnected &&
      state.rootFolderAvailable &&
      !pending &&
      !imported;
    item.setAttribute("aria-disabled", enabled ? "false" : "true");
    item.disabled = !enabled;
    item.dataset.state = imported ? "imported" : pending ? "pending" : "ready";
    item.textContent = imported
      ? "取込済み"
      : pending
        ? "取込待ち"
        : "Import Eagle";
  }

  function clearAddedElements() {
    document
      .querySelectorAll(Object.values(OWNED_SELECTORS).join(", "))
      .forEach((element) => {
        element.remove();
      });
  }

  async function handleImportClick(context, download) {
    if (!state.bridgeConnected) {
      throw new Error("Eagle plugin に接続できません。");
    }
    if (!state.rootFolderAvailable) {
      throw new Error("Eagle library 直下に VRCAsset folder がありません。");
    }

    const downloadKey = buildDownloadKey(download);
    markDownloadPending(downloadKey, "");
    openDownloadInNewTab(download.download.downloadUrl);

    let result = null;
    try {
      result = await bridgeRequest(
        "POST",
        "/v1/import",
        buildImportPayload(context, download),
      );
    } catch (error) {
      state.pendingDownloads.delete(downloadKey);
      renderContexts(collectCardContexts());
      throw error;
    }

    if (result && result.alreadyImported) {
      state.pendingDownloads.delete(downloadKey);
      renderContexts(collectCardContexts());
      scheduleStatusRefresh();
      return;
    }

    if (result && result.alreadyPending) {
      markDownloadPending(downloadKey, result.jobId || "");
      scheduleStatusRefresh();
      return;
    }

    markDownloadPending(
      downloadKey,
      result && result.jobId ? result.jobId : "",
    );
    renderContexts(collectCardContexts());
    scheduleStatusRefresh();
    FOLLOW_UP_STATUS_DELAYS_MS.forEach((delay) => {
      window.setTimeout(() => {
        scheduleStatusRefresh();
      }, delay);
    });
  }

  function markDownloadPending(downloadKey, jobId) {
    const existing = state.pendingDownloads.get(downloadKey);
    state.pendingDownloads.set(downloadKey, {
      jobId,
      startedAt: existing ? existing.startedAt : Date.now(),
    });
    renderContexts(collectCardContexts());
  }

  function openDownloadInNewTab(downloadUrl) {
    const normalized = normalizeDownloadUrl(downloadUrl);
    if (!normalized) {
      return;
    }

    if (typeof GM_openInTab === "function") {
      const tab = GM_openInTab(normalized, {
        active: false,
        insert: true,
      });
      window.setTimeout(() => {
        if (tab && typeof tab.close === "function") {
          tab.close();
        }
      }, DOWNLOAD_TAB_CLOSE_DELAY_MS);
      return;
    }

    const tab = window.open(normalized, "_blank", "noopener");
    window.setTimeout(() => {
      if (tab && !tab.closed) {
        tab.close();
      }
    }, DOWNLOAD_TAB_CLOSE_DELAY_MS);
  }

  function createBoothLibraryPageAdapter(selectors) {
    return {
      collectContexts(root) {
        // 商品カードの探索方法は BOOTH の markup に依存するため adapter 内に閉じ込める。
        const contexts = [];
        root.querySelectorAll(selectors.thumbnail).forEach((thumbnail) => {
          const context = this.buildProductContext(thumbnail);
          if (context) {
            contexts.push(context);
          }
        });
        return contexts;
      },

      buildProductContext(thumbnail) {
        const card = this.findCard(thumbnail);
        if (!card) {
          return null;
        }

        const productLink = card.querySelector(selectors.productLink);
        const product = this.readProduct(card, thumbnail, productLink);
        if (!product) {
          return null;
        }

        return {
          card,
          header:
            card.querySelector(selectors.header) || card.firstElementChild,
          product,
          downloads: this.readDownloads(card, product),
        };
      },

      findCard(thumbnail) {
        // 色や余白の class は BOOTH のデザイン変更で変わりやすいため、
        // 商品 URL と download action を一緒に含む最小の祖先をカードとみなす。
        let current = thumbnail.parentElement;
        while (current && current !== document.body) {
          if (
            current.querySelector(selectors.productLink) &&
            current.querySelector(selectors.downloadTrigger)
          ) {
            return current;
          }
          current = current.parentElement;
        }

        return thumbnail.closest(selectors.cardFromThumbnail);
      },

      readProduct(card, thumbnail, productLink) {
        if (!productLink) {
          return null;
        }

        const itemUrl = normalizeItemUrl(productLink.href);
        const boothItemId = extractItemId(itemUrl);
        if (!itemUrl || !boothItemId) {
          return null;
        }

        return {
          boothItemId,
          itemUrl,
          name:
            readText(card.querySelector(selectors.productName)) ||
            readText(productLink),
          thumbnailUrl: thumbnail.getAttribute("src") || "",
          shopName: readText(card.querySelector(selectors.shopName)),
          shopUrl: normalizeShopUrl(
            card.querySelector(selectors.shopLink)?.href || "",
          ),
        };
      },

      readDownloads(card, product) {
        const downloads = [];
        card.querySelectorAll(selectors.downloadTrigger).forEach((trigger) => {
          const download = this.readDownload(trigger, product);
          if (download) {
            downloads.push(download);
          }
        });
        return downloads;
      },

      readDownload(trigger, product) {
        const downloadUrl = normalizeDownloadUrl(
          trigger.getAttribute("data-href") ||
            trigger.getAttribute("href") ||
            trigger.href,
        );
        const actions = this.findDownloadActions(trigger);
        const row = this.findDownloadRow(trigger, actions);
        if (!row || !actions || !downloadUrl) {
          return null;
        }

        const filenameElement = this.findDownloadFilename(row, actions);
        const filename = readText(filenameElement);
        if (!filename) {
          return null;
        }

        return {
          row,
          actions,
          download: {
            boothItemId: product.boothItemId,
            itemUrl: product.itemUrl,
            downloadUrl,
            downloadId: extractDownloadId(downloadUrl),
            filename,
          },
        };
      },

      findDownloadActions(trigger) {
        // 新しい library DOM では通常 download と「その他のDL方法」が
        // wrapper を挟んだ兄弟になっている。両方を含む最小の祖先を使う。
        let current = trigger.parentElement;
        while (current && current !== document.body) {
          if (current.querySelector(selectors.otherDownloads)) {
            return current;
          }
          current = current.parentElement;
        }

        return (
          trigger.closest(selectors.downloadActions) ||
          trigger.parentElement
        );
      },

      findDownloadFilename(row, actions) {
        // 新しい DOM では filename block と actions が兄弟になっている。
        // actions 内の dropdown label を filename と誤認しないよう外側だけを見る。
        let current = actions;
        while (current && current !== row) {
          let sibling = current.previousElementSibling;
          while (sibling) {
            if (readText(sibling)) {
              return sibling;
            }
            sibling = sibling.previousElementSibling;
          }
          current = current.parentElement;
        }

        return Array.from(
          row.querySelectorAll(selectors.downloadFilename),
        ).find((element) => !actions.contains(element));
      },

      findDownloadRow(trigger, actions) {
        // filename block と actions が兄弟になっている最小の祖先を探す。
        let current = actions;
        while (current && current !== document.body) {
          let sibling = current.previousElementSibling;
          while (sibling) {
            if (readText(sibling)) {
              return current.parentElement;
            }
            sibling = sibling.previousElementSibling;
          }
          current = current.parentElement;
        }

        return trigger.closest(selectors.downloadRow);
      },
    };
  }

  function createBoothItemPageAdapter(selectors) {
    return {
      collectContexts(root) {
        const variations = root.querySelector(selectors.variations);
        if (!variations) {
          return [];
        }

        const product = this.readProduct(root);
        if (!product) {
          return [];
        }

        const context = {
          card: root.querySelector(selectors.root) || root.body || root,
          header: root.querySelector(selectors.root) || root.body || root,
          product,
          downloads: this.readDownloads(variations, product),
        };

        return context.downloads.length > 0 ? [context] : [];
      },

      readProduct(root) {
        const itemUrl =
          normalizeItemUrl(readLinkHref(root.querySelector('link[rel="canonical"]'))) ||
          normalizeItemUrl(readMetaContent(root, 'meta[property="og:url"]')) ||
          normalizeItemUrl(window.location.href);
        const boothItemId = extractItemId(itemUrl);
        if (!itemUrl || !boothItemId) {
          return null;
        }

        const shopLink = root.querySelector(selectors.shopLink);
        const shopName =
          readText(shopLink && shopLink.querySelector("span")) ||
          (shopLink && shopLink.querySelector("img")
            ? shopLink.querySelector("img").getAttribute("alt") || ""
            : "");

        return {
          boothItemId,
          itemUrl,
          name:
            readText(root.querySelector(selectors.productName)) ||
            stripBoothTitleSuffix(readMetaContent(root, 'meta[property="og:title"]')) ||
            stripBoothTitleSuffix(document.title),
          thumbnailUrl:
            readMetaContent(root, 'meta[property="og:image"]') ||
            readMetaContent(root, 'meta[name="twitter:image"]'),
          shopName,
          shopUrl: normalizeShopUrl(shopLink ? shopLink.href : ""),
        };
      },

      readDownloads(variations, product) {
        const downloads = [];
        variations
          .querySelectorAll(selectors.downloadTrigger)
          .forEach((trigger) => {
            const download = this.readDownload(trigger, product);
            if (download) {
              downloads.push(download);
            }
          });
        return downloads;
      },

      readDownload(trigger, product) {
        const downloadUrl = normalizeDownloadUrl(
          trigger.getAttribute("href") || trigger.href,
        );
        const row = trigger.closest(selectors.variationRow);
        if (!row || !downloadUrl) {
          return null;
        }

        const otherDownloads = row.querySelector(selectors.otherDownloads);
        const actions = otherDownloads ? otherDownloads.parentElement : trigger.parentElement;
        if (!actions) {
          return null;
        }

        const filename =
          trigger.getAttribute("title") ||
          readText(otherDownloads && otherDownloads.previousElementSibling) ||
          readText(row.querySelector(selectors.variationName)) ||
          `download-${extractDownloadId(downloadUrl)}`;

        return {
          row,
          actions,
          actionLayout: "block",
          download: {
            boothItemId: product.boothItemId,
            itemUrl: product.itemUrl,
            downloadUrl,
            downloadId: extractDownloadId(downloadUrl),
            filename,
          },
        };
      },
    };
  }

  function createCompositePageAdapter(adapters) {
    return {
      collectContexts(root) {
        return adapters.flatMap((adapter) => adapter.collectContexts(root));
      },
    };
  }

  function buildStatusPayload(contexts) {
    return {
      items: contexts.map((context) => ({
        ...buildProductPayload(context),
        downloads: context.downloads.map(buildDownloadPayload),
      })),
    };
  }

  function buildImportPayload(context, download) {
    return {
      product: buildProductPayload(context),
      download: buildDownloadPayload(download),
    };
  }

  function buildProductPayload(context) {
    return {
      boothItemId: context.product.boothItemId,
      itemUrl: context.product.itemUrl,
      name: context.product.name,
      thumbnailUrl: context.product.thumbnailUrl,
      shopName: context.product.shopName,
      shopUrl: context.product.shopUrl,
    };
  }

  function buildDownloadPayload(download) {
    return {
      downloadUrl: download.download.downloadUrl,
      filename: download.download.filename,
    };
  }

  function buildProductKey(product) {
    return (
      String(product.boothItemId || extractItemId(product.itemUrl) || "") ||
      normalizeItemUrl(product.itemUrl)
    );
  }

  function buildDownloadKey(downloadContext) {
    const download = downloadContext.download || downloadContext;
    if (download.downloadId) {
      return `download:${download.downloadId}`;
    }
    const normalizedDownloadUrl = normalizeDownloadUrl(download.downloadUrl);
    if (normalizedDownloadUrl) {
      return `url:${normalizedDownloadUrl}`;
    }
    return `fallback:${buildProductKey(downloadContext.product || downloadContext)}:${normalizeText(download.filename)}`;
  }

  function extractItemId(itemUrl) {
    const normalized = normalizeItemUrl(itemUrl);
    const match = normalized.match(/\/items\/(\d+)$/);
    return match ? parseInt(match[1], 10) : 0;
  }

  function extractDownloadId(downloadUrl) {
    const normalized = normalizeDownloadUrl(downloadUrl);
    const match = normalized.match(/\/downloadables\/(\d+)$/);
    return match ? parseInt(match[1], 10) : 0;
  }

  function normalizeItemUrl(value) {
    const url = tryCreateUrl(value);
    if (!url || !/(?:^|\.)booth\.pm$/i.test(url.hostname)) {
      return "";
    }

    const match = url.pathname.match(
      /^\/(?:(?:[a-z]{2,8}(?:[-_][a-z]{2,8})*)\/)?items\/(\d+)(?:\/)?$/i,
    );
    if (!match) {
      return "";
    }

    return url.hostname.toLowerCase() === "booth.pm"
      ? `https://booth.pm/items/${match[1]}`
      : `https://${url.hostname.toLowerCase()}/items/${match[1]}`;
  }

  function normalizeDownloadUrl(value) {
    const url = tryCreateUrl(value);
    if (!url || url.hostname.toLowerCase() !== "booth.pm") {
      return "";
    }

    const match = url.pathname.match(/^\/downloadables\/(\d+)(?:\/)?$/i);
    return match ? `https://booth.pm/downloadables/${match[1]}` : "";
  }

  function normalizeShopUrl(value) {
    const url = tryCreateUrl(value);
    return url && /\.booth\.pm$/i.test(url.hostname)
      ? `https://${url.hostname.toLowerCase()}`
      : "";
  }

  function tryCreateUrl(value) {
    try {
      return new URL(value);
    } catch (error) {
      return null;
    }
  }

  function normalizeText(value) {
    return String(value || "")
      .trim()
      .toLowerCase();
  }

  function readText(element) {
    return element ? String(element.textContent || "").trim() : "";
  }

  function readLinkHref(element) {
    return element ? String(element.getAttribute("href") || "").trim() : "";
  }

  function readMetaContent(root, selector) {
    const element = root.querySelector(selector);
    return element ? String(element.getAttribute("content") || "").trim() : "";
  }

  function stripBoothTitleSuffix(value) {
    return String(value || "")
      .replace(/\s+-\s+[^-]+?\s+-\s+BOOTH$/i, "")
      .replace(/\s+-\s+BOOTH$/i, "")
      .trim();
  }

  function bridgeRequest(method, path, payload) {
    return new Promise((resolve, reject) => {
      GM_xmlhttpRequest({
        method,
        url: `${BRIDGE_BASE}${path}`,
        data: payload ? JSON.stringify(payload) : undefined,
        headers: payload
          ? {
              "Content-Type": "application/json",
              "X-EE4V-Bridge-Token": state.bridgeToken,
            }
          : undefined,
        onload: (response) => {
          if (response.status < 200 || response.status >= 300) {
            reject(new Error(`HTTP ${response.status}`));
            return;
          }

          try {
            resolve(
              response.responseText ? JSON.parse(response.responseText) : {},
            );
          } catch (error) {
            reject(new Error("Bridge response の解析に失敗しました。"));
          }
        },
        onerror: () => {
          reject(new Error("Eagle bridge に接続できません。"));
        },
      });
    });
  }
})();
