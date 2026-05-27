import { mkdir, readdir, readFile, rm, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const sourceRoot = path.join(root, "src");
const compiledRoot = path.join(root, "build", "compiled");
const distRoot = path.join(root, "dist");
const sharedCorePath = path.join(compiledRoot, "shared", "core.js");
const plugins = ["BoothCompat", "BoothCompatService"];

await run(process.execPath, [path.join(root, "node_modules", "typescript", "bin", "tsc"), "--project", "tsconfig.json"], root);
await rm(distRoot, { recursive: true, force: true });
await mkdir(distRoot, { recursive: true });

for (const plugin of plugins) {
  const sourcePath = path.join(sourceRoot, "plugins", plugin);
  const compiledPath = path.join(compiledRoot, "plugins", plugin);
  const outputPath = path.join(distRoot, plugin);
  await copyStaticTree(sourcePath, outputPath);
  await copyCompiledTree(compiledPath, outputPath);
  await copyFile(sharedCorePath, path.join(outputPath, "js", "core.js"));
}

async function copyStaticTree(sourcePath, outputPath) {
  const sourceStat = await stat(sourcePath);
  if (sourceStat.isDirectory()) {
    await mkdir(outputPath, { recursive: true });
    const entries = await readdir(sourcePath, { withFileTypes: true });
    for (const entry of entries) {
      const childSource = path.join(sourcePath, entry.name);
      const childOutput = path.join(outputPath, entry.name);
      await copyStaticTree(childSource, childOutput);
    }
    return;
  }

  if (sourcePath.endsWith(".ts")) {
    return;
  }

  await copyFile(sourcePath, outputPath);
}

async function copyCompiledTree(sourcePath, outputPath) {
  const sourceStat = await stat(sourcePath);
  if (sourceStat.isDirectory()) {
    await mkdir(outputPath, { recursive: true });
    const entries = await readdir(sourcePath, { withFileTypes: true });
    for (const entry of entries) {
      const childSource = path.join(sourcePath, entry.name);
      const childOutput = path.join(outputPath, entry.name);
      await copyCompiledTree(childSource, childOutput);
    }
    return;
  }

  await copyFile(sourcePath, outputPath);
}

async function copyFile(sourcePath, outputPath) {
  const bytes = await readFile(sourcePath);
  await mkdir(path.dirname(outputPath), { recursive: true });
  await writeFile(outputPath, bytes);
}

function run(command, args, cwd) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd,
      stdio: "inherit"
    });

    child.on("error", reject);
    child.on("exit", code => {
      if (code === 0) {
        resolve();
        return;
      }

      reject(new Error(`${command} ${args.join(" ")} failed with exit code ${code}`));
    });
  });
}
