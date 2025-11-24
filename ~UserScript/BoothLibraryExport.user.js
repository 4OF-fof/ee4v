// ==UserScript==
// @name         EE4V BOOTH Library Sync
// @namespace    https://4of.dev
// @version      0.1.0
// @description  Send your BOOTH library info to EE4V (Unity).
// @match        https://accounts.booth.pm/library*
// @match        https://accounts.booth.pm/library/gifts*
// @grant        GM_xmlhttpRequest
// @connect      localhost
// ==/UserScript==

(function () {
    'use strict';

    // Hide Booth UI
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

    // Overlay UI
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


    // extraction logic
    const storePattern = /^https:\/\/[a-zA-Z0-9-]+\.booth\.pm\/$/;
    const itemPattern = /^https:\/\/booth\.pm\/[^/]+\/items\/\d+/;
    const dlPattern = /^https:\/\/booth\.pm\/downloadables\/\d+/;
    const pagePattern = /page=(\d+)/;

    async function fetchPage(url) {
        const res = await fetch(url, { credentials: "include" });
        return new DOMParser().parseFromString(await res.text(), "text/html");
    }

    function extractLinks(dom) {
        const list = [];
        dom.querySelectorAll("a").forEach(a => {
            const href = a.href;
            if (!href) return;
            if (href.startsWith("https://accounts.booth.pm")) return;

            if (itemPattern.test(href)) list.push({ type: "item", url: href });
            else if (storePattern.test(href)) list.push({ type: "store", url: href });
            else if (dlPattern.test(href)) {
                let filename = null;
                const flex = a.closest(".desktop\\:flex") || a.closest(".mt-16");
                if (flex) {
                    const nameDiv = flex.querySelector(".min-w-0 .text-14") || flex.querySelector(".text-14");
                    if (nameDiv) filename = nameDiv.textContent.trim();
                }
                list.push({ type: "download", url: href, filename });
            }
        });
        return list;
    }

    function groupByItem(arr) {
        const res = [];
        let tmp = [];
        arr.forEach(x => {
            if (x.type === "item") {
                if (tmp.length) res.push(tmp);
                tmp = [x];
            } else tmp.push(x);
        });
        if (tmp.length) res.push(tmp);
        return res;
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

    function merge(allGroups, infoMap) {
        const result = [];
        allGroups.forEach(g => {
            const item = g.find(x => x.type === "item");
            const store = g.find(x => x.type === "store");
            const downloads = g.filter(x => x.type === "download");
            if (!item || !store || downloads.length === 0) return;

            const storeURL = store.url;
            const itemURL = item.url;

            let entry = result.find(r => r.shopURL === storeURL);
            if (!entry) {
                entry = { shopURL: storeURL, shopName: infoMap[itemURL]?.shopName ?? null, items: [] };
                result.push(entry);
            }

            entry.items.push({
                itemURL,
                name: infoMap[itemURL]?.name ?? null,
                description: infoMap[itemURL]?.description ?? null,
                imageURL: infoMap[itemURL]?.imageURL ?? null,
                files: downloads.map(f => ({ url: f.url, filename: f.filename ?? null }))
            });
        });

        return result;
    }

    async function runExtract() {
        statusText.textContent = "抽出中...";
        const targets = [
            "https://accounts.booth.pm/library",
            "https://accounts.booth.pm/library/gifts"
        ];

        let groups = [];
        let items = [];

        for (const base of targets) {
            const first = await fetchPage(base);
            const maxPage = Math.max(...[...first.querySelectorAll("a")]
                .map(a => (a.href.match(pagePattern) || [0,1])[1]).map(Number));

            const infoFirst = extractLinks(first);
            const gFirst = groupByItem(infoFirst);
            groups.push(...gFirst);
            gFirst.forEach(g => items.push(g.find(x=>x.type==="item")?.url));

            for (let p=2; p<=maxPage; p++) {
                const dom = await fetchPage(`${base}?page=${p}`);
                const info = extractLinks(dom);
                const g = groupByItem(info);
                groups.push(...g);
                g.forEach(group => items.push(group.find(x=>x.type==="item")?.url));
            }
        }

        items = [...new Set(items)].filter(Boolean);
        const map = {};

        for (const item of items) {
            statusText.textContent = `詳細取得中... (${items.indexOf(item)+1}/${items.length})`;
            map[item] = await fetchItemInfo(item);
        }

        return merge(groups, map);
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
                サーバーを確認してもう一度試してください。
            `;
            sendBtn.disabled = false;
        }
    };


    // Initial check for Unity server
    GM_xmlhttpRequest({
        method: "GET",
        url: "http://localhost:58080/",
        onload: (res) => {
            if (res.status === 200) {
                document.body.classList.add("booth-hidden");
                overlay.style.display = "flex";
            }
        }
    });

})();
