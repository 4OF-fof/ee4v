using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ee4v.AssetManager.Infrastructure.Ee4v
{
    public sealed partial class Ee4vDatasourceServer
    {
        private JObject ReadMetadata(string id)
        {
            var file = Path.Combine(_itemsRoot, id, "metadata.json");
            RejectLinks(file);
            if (!File.Exists(file)) return new JObject { ["schemaVersion"] = 1, ["id"] = id, ["files"] = new JArray() };
            var metadata = JObject.Parse(File.ReadAllText(file));
            if ((int?)metadata["schemaVersion"] != 1 || (string)metadata["id"] != id || !(metadata["files"] is JArray))
                throw new IOException("Invalid library metadata");
            return metadata;
        }

        private bool Imported(JObject metadata, Download download)
        {
            var entry = ((JArray)metadata["files"]).OfType<JObject>().FirstOrDefault(file => (string)file["id"] == download.Id);
            if (entry == null) return false;
            var filename = (string)entry["fileName"];
            ValidateFilename(filename);
            var path = Path.Combine(_itemsRoot, (string)metadata["id"], filename);
            RejectLinks(path);
            return File.Exists(path);
        }

        private async Task Poll(CancellationToken cancellation)
        {
            ImportJob[] jobs;
            lock (_gate) jobs = _jobs.Values.Where(job => job.Watching).ToArray();
            foreach (var job in jobs)
            {
                cancellation.ThrowIfCancellationRequested();
                if (DateTime.UtcNow - job.Started > TimeSpan.FromMinutes(30))
                {
                    Fail(job, "Download timed out");
                    continue;
                }
                try
                {
                    RejectLinks(_downloads);
                    FileInfo candidate = null;
                    foreach (var path in Directory.EnumerateFiles(_downloads))
                    {
                        if (!MatchesFilename(Path.GetFileName(path), job.Download.Filename)) continue;
                        RejectLinks(path);
                        var file = new FileInfo(path);
                        if (file.Length > 0 && file.LastWriteTimeUtc >= job.Started.AddSeconds(-5) &&
                            DateTime.UtcNow - file.LastWriteTimeUtc >= TimeSpan.FromSeconds(2) &&
                            (candidate == null || file.LastWriteTimeUtc > candidate.LastWriteTimeUtc)) candidate = file;
                    }
                    if (candidate == null)
                    {
                        job.Candidate = null;
                        continue;
                    }
                    if (job.Candidate != candidate.FullName || job.Size != candidate.Length || job.Modified != candidate.LastWriteTimeUtc)
                    {
                        job.Candidate = candidate.FullName;
                        job.Size = candidate.Length;
                        job.Modified = candidate.LastWriteTimeUtc;
                        continue;
                    }
                    await Store(job, cancellation).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exception) { Fail(job, exception.Message); }
            }
        }

        private static bool MatchesFilename(string name, string requested)
        {
            if (Regex.IsMatch(name, @"\.(crdownload|download|part|tmp)\z", RegexOptions.IgnoreCase)) return false;
            if (string.Equals(name, requested, StringComparison.Ordinal)) return true;
            return Path.GetExtension(name) == Path.GetExtension(requested) &&
                   Regex.IsMatch(Path.GetFileNameWithoutExtension(name),
                       @"\A" + Regex.Escape(Path.GetFileNameWithoutExtension(requested)) + @" \([0-9]+\)\z");
        }

        private async Task Store(ImportJob job, CancellationToken cancellation)
        {
            var directory = Path.Combine(_itemsRoot, job.Id);
            RejectLinks(directory);
            var fileName = job.Download.Id + Path.GetExtension(job.Download.Filename).ToLowerInvariant();
            var target = Path.Combine(directory, fileName);
            // Stage outside Items so synchronization cannot see an unfinished new item.
            var staging = Path.Combine(_library, ".import-" + Guid.NewGuid().ToString("N"));
            var temporary = Path.Combine(staging, fileName);
            var metadataPath = Path.Combine(directory, "metadata.json");
            var metadataTemporary = Path.Combine(staging, "metadata.json");
            try
            {
                Directory.CreateDirectory(staging);
                RejectLinks(job.Candidate);
                using (var source = new FileStream(job.Candidate, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                using (var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    await source.CopyToAsync(destination, 81920, cancellation).ConfigureAwait(false);
                var after = new FileInfo(job.Candidate);
                if (after.Length != job.Size || after.LastWriteTimeUtc != job.Modified)
                {
                    job.Candidate = null;
                    return;
                }
                cancellation.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    RejectLinks(directory);
                    RejectLinks(target);
                    var metadata = ReadMetadata(job.Id);
                    metadata["name"] = (string)job.Product["name"] ?? job.Id;
                    metadata["description"] = (string)job.Product["description"] ?? string.Empty;
                    metadata["itemUrl"] = "https://booth.pm/items/" + job.Id;
                    foreach (var field in new[] { "thumbnailUrl", "shopName", "shopUrl" })
                        metadata[field] = (string)job.Product[field] ?? string.Empty;
                    metadata["tags"] = job.Product["tags"] is JArray tags ? tags.DeepClone() : new JArray();
                    var files = (JArray)metadata["files"];
                    foreach (var entry in files.OfType<JObject>().Where(file => (string)file["id"] == job.Download.Id).ToArray()) entry.Remove();
                    files.Add(new JObject
                    {
                        ["id"] = job.Download.Id, ["fileName"] = fileName,
                        ["originalFileName"] = job.Download.Filename, ["downloadUrl"] = job.Download.Url
                    });
                    File.WriteAllText(metadataTemporary, metadata.ToString(Formatting.Indented));
                    RejectLinks(metadataPath);
                    if (!Directory.Exists(directory)) Directory.Move(staging, directory);
                    else
                    {
                        if (!File.Exists(target)) File.Move(temporary, target);
                        if (File.Exists(metadataPath)) File.Replace(metadataTemporary, metadataPath, null);
                        else File.Move(metadataTemporary, metadataPath);
                    }
                    job.Watching = false;
                }
                _messages.Enqueue("ee4v imported " + job.Id + "/" + job.Download.Filename);
            }
            finally
            {
                if (Directory.Exists(staging))
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                    if (File.Exists(metadataTemporary)) File.Delete(metadataTemporary);
                    Directory.Delete(staging);
                }
            }
        }

        private void Fail(ImportJob job, string error)
        {
            lock (_gate)
            {
                job.Watching = false;
                job.Error = error;
            }
            _messages.Enqueue("ee4v import failed (" + job.Id + "/" + job.Download.Filename + "): " + error);
        }
    }
}
