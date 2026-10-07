import http from 'node:http';
import crypto from 'node:crypto';
import path from 'node:path';
import fs from 'node:fs/promises';

// No defaults for data locations: an operator must explicitly select both directories.
const args = process.argv.slice(2);
function option(name) { const index = args.indexOf(name); return index < 0 ? '' : args[index + 1]; }
if (!option('--library') || !option('--downloads')) {
  console.error('Usage: node server.mjs --library <folderlibrary> --downloads <download-directory> [--port 48197]');
  process.exit(1);
}
const library = path.resolve(option('--library'));
const downloads = path.resolve(option('--downloads'));
const port = Number(option('--port') || 48197);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Invalid port');
await fs.access(downloads);
await fs.mkdir(library, { recursive: true });
const itemsRoot = path.join(library, 'Items');
await fs.mkdir(itemsRoot, { recursive: true });
const token = crypto.randomBytes(32).toString('hex');
const jobs = new Map();
let processing = false;
await rejectLink(library);
await rejectLink(itemsRoot);
await rejectLink(downloads);

async function rejectLink(file) {
  if ((await fs.lstat(file)).isSymbolicLink()) throw new Error('Symbolic links are not supported');
}
function productId(product) {
  const id = String(product?.boothItemId || '');
  if (!/^[1-9]\d{0,15}$/.test(id)) throw new Error('Invalid BOOTH item id');
  return id;
}
function normalizeDownload(download) {
  const url = new URL(download?.downloadUrl);
  if (url.protocol !== 'https:' || !(url.hostname === 'booth.pm' || url.hostname.endsWith('.booth.pm')) ||
      !/\/downloadables\/\d+/.test(url.pathname)) throw new Error('Invalid BOOTH download URL');
  const filename = String(download?.filename || '').trim();
  if (!filename || filename !== path.basename(filename) || /[\\/:\x00-\x1f]/.test(filename) ||
      filename === '.' || filename === '..') throw new Error('Invalid download filename');
  return { downloadUrl: url.href, filename,
    id: crypto.createHash('sha256').update(url.pathname + '\n' + filename).digest('hex') };
}
function key(id, download) { return id + '/' + download.id; }
async function readMetadata(id) {
  const directory = path.join(itemsRoot, id);
  try {
    await rejectLink(directory);
    await rejectLink(path.join(directory, 'metadata.json'));
    const metadata = JSON.parse(await fs.readFile(path.join(directory, 'metadata.json'), 'utf8'));
    if (metadata.schemaVersion !== 1 || metadata.id !== id || !Array.isArray(metadata.files))
      throw new Error('Invalid library metadata');
    return metadata;
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
    return { schemaVersion: 1, id, files: [] };
  }
}
async function imported(metadata, download) {
  const entry = metadata.files.find(file => file.id === download.id);
  if (!entry) return false;
  if (entry.fileName !== path.basename(entry.fileName) || /[\\/:]/.test(entry.fileName))
    throw new Error('Invalid stored file path');
  try {
    const file = path.join(itemsRoot, metadata.id, entry.fileName);
    await rejectLink(file);
    return (await fs.stat(file)).isFile();
  } catch (error) { if (error.code === 'ENOENT') return false; throw error; }
}
function send(response, status, payload) {
  response.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  response.end(JSON.stringify(payload));
}
async function readBody(request) {
  let size = 0;
  const chunks = [];
  for await (const chunk of request) {
    size += chunk.length;
    if (size > 1024 * 1024) throw new Error('Request body exceeds 1 MiB');
    chunks.push(chunk);
  }
  return JSON.parse(Buffer.concat(chunks).toString('utf8'));
}
const server = http.createServer(async (request, response) => {
  try {
    if (!['127.0.0.1:' + port, 'localhost:' + port].includes(request.headers.host))
      return send(response, 403, { ok: false, error: 'Invalid host' });
    const origin = request.headers.origin;
    if (origin && !/^https:\/\/(?:[a-z0-9-]+\.)?booth\.pm$/i.test(origin))
      return send(response, 403, { ok: false, error: 'Invalid origin' });
    if (request.method === 'GET' && request.url === '/health')
      return send(response, 200, { ok: true, rootFolderAvailable: true, token });
    if (request.method !== 'POST' || request.headers['x-ee4v-bridge-token'] !== token)
      return send(response, 401, { ok: false, error: 'Invalid session token' });
    const payload = await readBody(request);
    if (request.url === '/v1/status') {
      const items = [];
      for (const product of payload.items || []) {
        const id = productId(product);
        const metadata = await readMetadata(id);
        const results = [];
        for (const input of product.downloads || []) {
          const download = normalizeDownload(input);
          const job = jobs.get(key(id, download));
          results.push({ ...download, imported: await imported(metadata, download),
            pending: Boolean(job && job.status === 'watching'), error: job?.error || '' });
        }
        items.push({ boothItemId: Number(id), itemUrl: product.itemUrl,
          hasBoothMeta: metadata.files.length > 0, downloads: results });
      }
      return send(response, 200, { ok: true, items });
    }
    if (request.url === '/v1/import') {
      const id = productId(payload.product);
      const download = normalizeDownload(payload.download);
      const jobId = key(id, download);
      const metadata = await readMetadata(id);
      if (await imported(metadata, download))
        return send(response, 200, { ok: true, jobId, alreadyImported: true });
      const previous = jobs.get(jobId);
      if (previous?.status === 'watching')
        return send(response, 200, { ok: true, jobId, alreadyPending: true });
      if ([...jobs.values()].some(job => job.status === 'watching' && job.download.filename === download.filename))
        throw new Error('Another pending import uses this filename; finish it first');
      jobs.set(jobId, { id, download, product: payload.product, started: Date.now(), status: 'watching' });
      return send(response, 200, { ok: true, jobId });
    }
    send(response, 404, { ok: false, error: 'Not found' });
  } catch (error) { send(response, 400, { ok: false, error: error.message }); }
});
server.requestTimeout = 15000;
server.headersTimeout = 15000;

async function poll() {
  if (processing) return;
  processing = true;
  try {
    for (const job of jobs.values()) {
      if (job.status !== 'watching') continue;
      if (Date.now() - job.started > 30 * 60 * 1000) {
        job.status = 'failed'; job.error = 'Download timed out'; continue;
      }
      try {
        const names = (await fs.readdir(downloads)).filter(name => name === job.download.filename ||
          (path.extname(name) === path.extname(job.download.filename) &&
           path.basename(name, path.extname(name)).startsWith(path.basename(job.download.filename,
             path.extname(job.download.filename)) + ' (') && / \(\d+\)$/.test(path.basename(name, path.extname(name)))))
          .filter(name => !/\.(crdownload|download|part|tmp)$/i.test(name));
        let candidate;
        for (const name of names) {
          const source = path.join(downloads, name);
          await rejectLink(source);
          const stat = await fs.stat(source);
          if (stat.isFile() && stat.size > 0 && stat.mtimeMs >= job.started - 5000 &&
              Date.now() - stat.mtimeMs >= 2000 && (!candidate || stat.mtimeMs > candidate.stat.mtimeMs))
            candidate = { source, stat };
        }
        if (!candidate) continue;
        const signature = candidate.source + ':' + candidate.stat.size + ':' + candidate.stat.mtimeMs;
        if (job.signature !== signature) { job.signature = signature; continue; }
        const directory = path.join(itemsRoot, job.id);
        await fs.mkdir(directory, { recursive: true });
        await rejectLink(directory);
        const fileName = job.download.id + path.extname(job.download.filename).toLowerCase();
        const target = path.join(directory, fileName);
        const temporary = target + '.copy-' + crypto.randomBytes(8).toString('hex');
        try {
          await fs.copyFile(candidate.source, temporary, 1);
          const after = await fs.stat(candidate.source);
          if (after.size !== candidate.stat.size || after.mtimeMs !== candidate.stat.mtimeMs) continue;
          try { await fs.link(temporary, target); }
          catch (error) { if (error.code !== 'EEXIST') throw error; await rejectLink(target); }
        } finally { await fs.rm(temporary, { force: true }); }
        const metadata = await readMetadata(job.id);
        Object.assign(metadata, {
          name: String(job.product.name || job.id), description: String(job.product.description || ''),
          itemUrl: 'https://booth.pm/items/' + job.id,
          thumbnailUrl: String(job.product.thumbnailUrl || ''), shopName: String(job.product.shopName || ''),
          shopUrl: String(job.product.shopUrl || ''), tags: Array.isArray(job.product.tags) ? job.product.tags : []
        });
        metadata.files = metadata.files.filter(file => file.id !== job.download.id);
        metadata.files.push({ id: job.download.id, fileName, originalFileName: job.download.filename,
          downloadUrl: job.download.downloadUrl });
        const metadataTemp = path.join(directory, 'metadata-' + crypto.randomBytes(8).toString('hex') + '.tmp');
        await fs.writeFile(metadataTemp, JSON.stringify(metadata, null, 2), { flag: 'wx' });
        await fs.rename(metadataTemp, path.join(directory, 'metadata.json'));
        job.status = 'imported';
        console.log('Imported ' + job.id + '/' + job.download.filename);
      } catch (error) { job.status = 'failed'; job.error = error.message; console.error('Import failed: ' + error.message); }
    }
  } finally { processing = false; }
}
const timer = setInterval(() => poll().catch(error => console.error(error.message)), 1500);
server.listen(port, '127.0.0.1', () => console.log('ee4v bridge listening on 127.0.0.1:' + port));
server.on('error', error => { console.error(error.message); clearInterval(timer); process.exitCode = 1; });
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => {
  clearInterval(timer); server.close(() => process.exit(0));
});
