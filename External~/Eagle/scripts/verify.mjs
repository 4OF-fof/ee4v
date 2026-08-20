import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const plugins = [
  {
    name: "BoothCompat",
    entries: ["index.html", "inspector.html", "js/core.js", "js/plugin.js", "js/inspector.js"],
    localizedSources: ["src/shared/core.ts", "src/plugins/BoothCompat/js/plugin.ts", "src/plugins/BoothCompat/js/inspector.ts"]
  },
  {
    name: "BoothCompatService",
    entries: ["index.html", "css/service.css", "js/core.js", "js/bridge.js"],
    localizedSources: ["src/shared/core.ts", "src/plugins/BoothCompatService/js/bridge.ts"]
  }
];
const locales = ["en", "ja_JP"];

for (const plugin of plugins) {
  const distPath = path.join(root, "dist", plugin.name);
  const manifest = JSON.parse(await readFile(path.join(distPath, "manifest.json"), "utf8"));
  assert.deepEqual(manifest.languages, locales, `${plugin.name} manifest languages must match packaged locales`);
  assert.match(manifest.name, /^{{.+}}$/, `${plugin.name} manifest name must use localization`);

  const dictionaries = await Promise.all(locales.map(async locale => {
    const dictionary = JSON.parse(await readFile(path.join(distPath, "_locales", `${locale}.json`), "utf8"));
    return flattenKeys(dictionary);
  }));
  assert.deepEqual(dictionaries[1], dictionaries[0], `${plugin.name} locale keys must match`);

  const localizedKeys = new Set(dictionaries[0]);
  for (const source of plugin.localizedSources) {
    const text = await readFile(path.join(root, source), "utf8");
    for (const match of text.matchAll(/\bt\("([^"]+)"/g)) {
      assert(localizedKeys.has(match[1]), `${plugin.name} is missing locale key ${match[1]}`);
    }
  }

  for (const entry of plugin.entries) {
    await access(path.join(distPath, entry));
  }
}

const inspectorHtml = await readFile(path.join(root, "dist/BoothCompat/inspector.html"), "utf8");
assert(!inspectorHtml.includes("sync-button"), "Inspector sync button must remain removed");

const serviceManifest = JSON.parse(await readFile(path.join(root, "dist/BoothCompatService/manifest.json"), "utf8"));
assert.equal(serviceManifest.main.serviceMode, true, "Service plugin must run in service mode");
assert.equal(serviceManifest.main.frame, false, "Service status window must remain frameless");
assert.equal(serviceManifest.main.width, 480, "Service status window width must remain usable");
assert.equal(serviceManifest.main.height, 320, "Service status window height must remain usable");

console.log("Eagle plugin build and locale structure verified.");

function flattenKeys(value, prefix = "") {
  return Object.entries(value)
    .flatMap(([key, child]) => {
      const next = prefix ? `${prefix}.${key}` : key;
      return child && typeof child === "object" && !Array.isArray(child)
        ? flattenKeys(child, next)
        : [next];
    })
    .sort();
}
