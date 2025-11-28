// ==UserScript==
// @name         EE4V BOOTH Library Sync
// @namespace    https://4of.dev
// @version      0.2.0
// @description  Send your BOOTH library info to EE4V (Unity). Fixed for new BOOTH layout.
// @match        https://accounts.booth.pm/library*
// @match        https://accounts.booth.pm/library/gifts*
// @grant        GM_xmlhttpRequest
// @connect      localhost
// ==/UserScript==

(function () {
    'use strict';

    const hideCSS = document.createElement("style");
    hideCSS.textContent = `
        body.booth-hidden *:not(#booth-overlay):not(#booth-overlay *) {
            visibility: hidden !important;
        }
        body.booth-hidden {
            overflow: hidden !important;
            padding: 0 !important;
            margin: 0 !important;
            background: #f1f1f1 !important;
        }
    `;
    document.head.appendChild(hideCSS);

    const overlay = document.createElement("div");
    overlay.id = "booth-overlay";
    Object.assign(overlay.style, {
        position: "fixed",
        inset: "0",
        background: "#f1f1f1",
        color: "#222",
        display: "none",
        justifyContent: "center",
        alignItems: "center",
        flexDirection: "column",
        zIndex: "999999",
        fontFamily: "sans-serif"
    });
    document.body.appendChild(overlay);

    const card = document.createElement("div");
    Object.assign(card.style, {
        background: "#fff",
        padding: "28px 36px",
        borderRadius: "16px",
        boxShadow: "0px 4px 18px rgba(0,0,0,0.12)",
        textAlign: "center",
        maxWidth: "420px",
        width: "90%"
    });
    overlay.appendChild(card);

    const title = document.createElement("h2");
    title.innerText = "EE4V Library Sync";
    title.style.marginBottom = "14px";
    card.appendChild(title);

    const desc = document.createElement("div");
    desc.innerHTML = `
        BOOTHライブラリ情報をUnityへ送信します。<br>
        <b>処理が終わるまでこのタブとUnityを閉じないでください。</b>
    `;
    desc.style.marginBottom = "22px";
    desc.style.fontSize = "14px";
    card.appendChild(desc);

    const statusText = document.createElement("div");
    statusText.style.margin = "12px 0";
    statusText.style.minHeight = "22px";
    statusText.style.fontSize = "15px";
    statusText.style.color = "#444";
    statusText.textContent = "送信準備完了";
    card.appendChild(statusText);

    const sendBtn = document.createElement("button");
    sendBtn.textContent = "送信を開始する";
    Object.assign(sendBtn.style, {
        width: "100%",
        padding: "12px 18px",
        fontSize: "15px",
        cursor: "pointer",
        background: "#ff6699",
        color: "#fff",
        border: "none",
        borderRadius: "10px",
        marginBottom: "10px"
    });
    card.appendChild(sendBtn);

    const backBtn = document.createElement("button");
    backBtn.textContent = "通常のライブラリ画面へ";
    Object.assign(backBtn.style, {
        width: "100%",
        padding: "10px",
        fontSize: "14px",
        cursor: "pointer",
        background: "#ddd",
        color: "#333",
        border: "none",
        borderRadius: "10px",
        marginTop: "6px"
    });
    card.appendChild(backBtn);

    backBtn.onclick = () => {
        document.body.classList.remove("booth-hidden");
        overlay.style.display = "none";
    };

    const pagePattern = /page=(\d+)/;

    async function fetchPage(url) {
        const res = await fetch(url, { credentials: "include" });
        return new DOMParser().parseFromString(await res.text(), "text/html");
    }

    function extractItemsFromDOM(dom) {
        const extractedItems = [];

        const thumbnails = dom.querySelectorAll('.l-library-item-thumbnail');

        thumbnails.forEach(thumb => {
            const card = thumb.closest('.bg-white');
            if (!card) return;

            const itemLink = card.querySelector('a[href*="/items/"]');
            if (!itemLink) return;
            const itemUrl = itemLink.href;

            const shopLink = card.querySelector('a[href*=".booth.pm"]:not([href*="accounts.booth.pm"])');
            const shopUrl = shopLink ? shopLink.href : null;

            const files = [];
            const dlButtons = card.querySelectorAll('.js-download-button');

            dlButtons.forEach(btn => {
                const dlUrl = btn.getAttribute('data-href');
                if (!dlUrl) return;

                const row = btn.closest('.desktop\\:flex') || btn.closest('.mt-16');
                let filename = null;
                if (row) {
                    const textEl = row.querySelector('.text-14');
                    if (textEl) filename = textEl.textContent.trim();
                }

                files.push({
                    url: dlUrl,
                    filename: filename
                });
            });

            if (files.length > 0) {
                extractedItems.push({
                    itemUrl: itemUrl,
                    shopUrl: shopUrl,
                    files: files
                });
            }
        });

        return extractedItems;
    }

    async function fetchItemInfo(url) {
        try {
            const res = await fetch(url + ".json", { credentials: "include" });
            const d = await res.json();
            return {
                name: d.name,
                description: d.description,
                imageURL: d.images?.[0]?.original || null,
                shopName: d.shop?.name ?? null
            };
        } catch {
            return { name: null, description: null, imageURL: null, shopName: null };
        }
    }

    function restructureData(extractedList, infoMap) {
        const result = [];

        extractedList.forEach(data => {
            if (!data.shopUrl) return;

            let entry = result.find(r => r.shopURL === data.shopUrl);

            const info = infoMap[data.itemUrl] || {};

            if (!entry) {
                entry = {
                    shopURL: data.shopUrl,
                    shopName: info.shopName ?? null,
                    items: []
                };
                result.push(entry);
            }

            const existingItem = entry.items.find(i => i.itemURL === data.itemUrl);
            if (!existingItem) {
                entry.items.push({
                    itemURL: data.itemUrl,
                    name: info.name ?? null,
                    description: info.description ?? null,
                    imageURL: info.imageURL ?? null,
                    files: data.files
                });
            }
        });

        return result;
    }

    function sleep(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    async function runExtract() {
        statusText.textContent = "ページ解析中...";
        const targets = [
            "https://accounts.booth.pm/library",
            "https://accounts.booth.pm/library/gifts"
        ];

        let allExtractedItems = [];

        for (const base of targets) {
            const firstDom = await fetchPage(base);
            const pageLinks = [...firstDom.querySelectorAll("a")].map(a => a.href);
            const pageNums = pageLinks.map(href => {
                const match = href.match(pagePattern);
                return match ? parseInt(match[1]) : 1;
            });
            const maxPage = pageNums.length > 0 ? Math.max(...pageNums) : 1;

            allExtractedItems.push(...extractItemsFromDOM(firstDom));

            for (let p = 2; p <= maxPage; p++) {
                statusText.textContent = `ページ解析中... (${p}/${maxPage})`;
                const dom = await fetchPage(`${base}?page=${p}`);
                allExtractedItems.push(...extractItemsFromDOM(dom));
                await sleep(1000);
            }
        }

        const uniqueItemUrls = [...new Set(allExtractedItems.map(x => x.itemUrl))];
        const infoMap = {};

        let index = 1;
        for (const itemUrl of uniqueItemUrls) {
            statusText.textContent = `詳細取得中... (${index}/${uniqueItemUrls.length})`;
            infoMap[itemUrl] = await fetchItemInfo(itemUrl);
            await sleep(1);
            index++;
        }

        return restructureData(allExtractedItems, infoMap);
    }

    function sendToUnity(json) {
        statusText.textContent = "Unity へ送信中...";
        return new Promise(resolve => {
            GM_xmlhttpRequest({
                method: "POST",
                url: "http://localhost:58080/",
                data: JSON.stringify(json),
                headers: { "Content-Type": "application/json" },
                onload: () => resolve(true),
                onerror: () => resolve(false)
            });
        });
    }

    sendBtn.onclick = async () => {
        sendBtn.disabled = true;
        backBtn.disabled = true;

        try {
            const result = await runExtract();
            const ok = await sendToUnity(result);

            if (ok) {
                statusText.innerHTML = `
                    送信が完了しました。<br><br>
                    <b>Unityに戻り次の操作を続けてください。</b><br>
                    <span style='font-size:12px;color:#666;'>※このタブは閉じても問題ありません。</span>
                `;
            } else {
                statusText.innerHTML = `
                    送信に失敗しました。<br>
                    サーバー(localhost:58080)を確認してもう一度試してください。
                `;
                sendBtn.disabled = false;
                backBtn.disabled = false;
            }
        } catch (e) {
            console.error(e);
            statusText.innerHTML = `エラーが発生しました: <br>${e.message}`;
            sendBtn.disabled = false;
            backBtn.disabled = false;
        }
    };

    GM_xmlhttpRequest({
        method: "GET",
        url: "http://localhost:58080/",
        onload: (res) => {
            if (res.status === 200) {
                document.body.classList.add("booth-hidden");
                overlay.style.display = "flex";
            }
        },
        onerror: () => {
            console.log("Unity server not found.");
        }
    });

})();