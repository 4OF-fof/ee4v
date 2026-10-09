import { appendFile, copyFile, mkdir, readFile, writeFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import path from 'node:path';

const repository = process.env.GITHUB_REPOSITORY;
if (!/^[\w.-]+\/[\w.-]+$/.test(repository ?? '')) {
  throw new Error('GITHUB_REPOSITORY must be owner/repository.');
}
const output = path.resolve(process.env.VPM_OUTPUT ?? 'Temp~/VpmBuild');
const manifest = JSON.parse(await readFile('src/package.json', 'utf8'));
if (!/^[a-z0-9]+(?:[.-][a-z0-9]+)+$/.test(manifest.name)
    || !/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(manifest.version)) {
  throw new Error('A valid package name and stable SemVer version are required.');
}
const tag = `v${manifest.version}`;
const releaseSha = process.env.VPM_RELEASE_SHA ?? process.env.GITHUB_SHA;
const zipName = `${manifest.name}-${manifest.version}.zip`;
const zipPath = path.join(output, zipName);
const packagePath = path.join(output, 'package');
const [owner, repoName] = repository.split('/');
const baseUrl = (process.env.PAGES_BASE_URL
  ?? `https://${owner.toLowerCase()}.github.io/${repoName}`).replace(/\/$/, '');
const listingUrl = `${baseUrl}/index.json`;
const apiRoot = `https://api.github.com/repos/${repository}`;

async function jsonFile(file, value) {
  await writeFile(file, `${JSON.stringify(value, null, 2)}\n`);
}

async function api(endpoint, options = {}) {
  const url = endpoint.startsWith('https:') ? endpoint : `${apiRoot}${endpoint}`;
  if (!['api.github.com', 'uploads.github.com'].includes(new URL(url).hostname)) {
    throw new Error('Unexpected GitHub API host.');
  }
  if (!process.env.GITHUB_TOKEN) throw new Error('GITHUB_TOKEN is required.');
  const response = await fetch(url, {
    ...options,
    headers: {
      Accept: 'application/vnd.github+json',
      Authorization: `Bearer ${process.env.GITHUB_TOKEN}`,
      'X-GitHub-Api-Version': '2022-11-28',
      ...options.headers,
    },
    signal: AbortSignal.timeout(120_000),
  });
  if (!response.ok) throw new Error(`GitHub API: ${response.status} ${endpoint}`);
  return response.status === 204 ? null : response.json();
}

async function* allReleases() {
  for (let page = 1; ; page++) {
    const batch = await api(`/releases?per_page=100&page=${page}`);
    yield* batch;
    if (batch.length < 100) return;
  }
}

async function prepare() {
  // Only tracked package files are distributed; local Generated assets are excluded.
  await mkdir(packagePath, { recursive: true });
  const files = execFileSync('git', ['ls-files', '-z', '--', 'src'], { encoding: 'utf8' })
    .split('\0').filter(Boolean);
  for (const file of files) {
    const destination = path.join(packagePath, file.slice('src/'.length));
    await mkdir(path.dirname(destination), { recursive: true });
    await copyFile(file, destination);
  }
  await copyFile('LICENSE.md', path.join(packagePath, 'LICENSE.md'));
  const noticesPath = path.join(packagePath, 'ThirdParty~');
  await mkdir(path.join(noticesPath, 'FluentUiSystemIcons'), { recursive: true });
  const notices = await readFile('ThirdParty~/THIRD_PARTY_NOTICES.md', 'utf8');
  await writeFile(path.join(noticesPath, 'THIRD_PARTY_NOTICES.md'),
    notices.replaceAll('../src/', '../'));
  for (const name of ['LICENSE.txt', 'NOTICE.txt']) {
    await copyFile(`ThirdParty~/FluentUiSystemIcons/${name}`,
      path.join(noticesPath, 'FluentUiSystemIcons', name));
  }
  const packagedManifest = {
    ...manifest,
    url: `https://github.com/${repository}/releases/download/${tag}/${zipName}`,
    repo: listingUrl,
  };
  await jsonFile(path.join(packagePath, 'package.json'), packagedManifest);
  if (process.env.GITHUB_OUTPUT) {
    await appendFile(process.env.GITHUB_OUTPUT, `zip_path=${zipPath}\n`);
  }
  console.log(`Prepared ${manifest.name} ${manifest.version}: ${packagePath}`);
}

async function publish() {
  // Listing releases also finds drafts, which the published-by-tag endpoint may omit.
  let release;
  for await (const candidate of allReleases()) {
    if (candidate.tag_name === tag) {
      release = candidate;
      break;
    }
  }
  if (release && !release.draft) {
    if (release.prerelease || !release.assets.some(asset => asset.name === zipName)
        || !release.assets.some(asset => asset.name === 'package.json')) {
      throw new Error(`Published ${tag} is not a compatible release. Use a new version.`);
    }
    console.log(`${tag} is already published; its assets are preserved.`);
    return;
  }
  // Draft releases can be retried after an interrupted upload.
  if (!release) {
    release = await api('/releases', {
      method: 'POST',
      body: JSON.stringify({
        tag_name: tag,
        target_commitish: releaseSha,
        name: `${manifest.displayName ?? manifest.name} ${manifest.version}`,
        draft: true,
        prerelease: false,
        body: 'Install this release through the ee4v VPM repository.',
      }),
    });
  }
  const zip = await readFile(zipPath);
  const releasedManifest = JSON.parse(await readFile(path.join(packagePath, 'package.json'), 'utf8'));
  releasedManifest.zipSHA256 = createHash('sha256').update(zip).digest('hex');
  await jsonFile(path.join(output, 'package.json'), releasedManifest);
  for (const [name, content, contentType] of [
    [zipName, zip, 'application/zip'],
    ['package.json', await readFile(path.join(output, 'package.json')), 'application/json'],
  ]) {
    const previous = release.assets.find(asset => asset.name === name);
    if (previous) await api(`/releases/assets/${previous.id}`, { method: 'DELETE' });
    const uploadUrl = `${release.upload_url.split('{')[0]}?name=${encodeURIComponent(name)}`;
    await api(uploadUrl, {
      method: 'POST', body: content, headers: { 'Content-Type': contentType },
    });
  }
  await api(`/releases/${release.id}`, {
    method: 'PATCH', body: JSON.stringify({
      draft: false, prerelease: false, target_commitish: releaseSha,
    }),
  });
  console.log(`Published ${tag}.`);
}

async function listing() {
  const versions = {};
  for await (const release of allReleases()) {
    if (release.draft || release.prerelease) continue;
    const asset = release.assets.find(item => item.name === 'package.json');
    if (!asset) continue;
    // Public asset downloads do not receive the API token.
    const response = await fetch(asset.browser_download_url, {
      signal: AbortSignal.timeout(60_000),
    });
    if (!response.ok) throw new Error(`Cannot download manifest for ${release.tag_name}.`);
    const entry = await response.json();
    if (entry.name !== manifest.name) continue;
    if (!/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(entry.version)) continue;
    const zip = release.assets.find(item => item.name === `${entry.name}-${entry.version}.zip`);
    if (!zip || entry.url !== zip.browser_download_url) {
      throw new Error(`Missing or mismatched ZIP for ${release.tag_name}.`);
    }
    if (entry.zipSHA256 && (!/^[a-f0-9]{64}$/.test(entry.zipSHA256)
        || (zip.digest && zip.digest !== `sha256:${entry.zipSHA256}`))) {
      throw new Error(`Invalid ZIP checksum for ${release.tag_name}.`);
    }
    if (versions[entry.version]) throw new Error(`Duplicate version ${entry.version}.`);
    versions[entry.version] = entry;
  }
  if (!versions[manifest.version]) throw new Error('Current version is missing from the listing.');
  const sitePath = path.join(output, 'site');
  await mkdir(sitePath, { recursive: true });
  await jsonFile(path.join(sitePath, 'index.json'), {
    name: 'ee4v Packages',
    id: `${manifest.name}.repository`,
    author: owner,
    url: listingUrl,
    packages: { [manifest.name]: { versions } },
  });
  const html = await readFile('.github/vpm/index.html', 'utf8');
  await writeFile(path.join(sitePath, 'index.html'),
    html.replaceAll('__LISTING_URL__', listingUrl)
      .replaceAll('__ADD_REPO_URL__', `vcc://vpm/addRepo?url=${encodeURIComponent(listingUrl)}`)
      .replaceAll('__REPOSITORY__', repository));
  console.log(`Built ${listingUrl} with ${Object.keys(versions).length} versions.`);
}

const commands = { prepare, publish, listing };
const command = commands[process.argv[2]];
if (!command) throw new Error('Usage: node .github/scripts/vpm.mjs prepare|publish|listing');
await command();
