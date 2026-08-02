using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Ee4v.SaveAndBackup.Application;
using Newtonsoft.Json;
using UnityEditor;

namespace Ee4v.SaveAndBackup.Infrastructure.Git
{
    internal sealed class GitSaveAndBackupGateway : ISaveAndBackupGateway
    {
        private const string SnapshotStateFile = ".save-and-backup-snapshot.json";
        private readonly string _projectRoot;
        private readonly Func<string> _getBackupRoot;
        private readonly string _projectId;
        private readonly string _snapshotRoot;

        internal GitSaveAndBackupGateway(
            string projectRoot,
            string backupRoot,
            string projectId)
            : this(projectRoot, () => backupRoot, projectId)
        {
        }

        internal GitSaveAndBackupGateway(
            string projectRoot,
            Func<string> getBackupRoot,
            string projectId)
        {
            _projectRoot = NormalizeFullPath(projectRoot);
            _getBackupRoot = getBackupRoot ??
                throw new ArgumentNullException(nameof(getBackupRoot));
            _projectId = SafeSegment(projectId);
            _snapshotRoot = Path.Combine(
                _projectRoot,
                "Library",
                "ee4v",
                "save-and-backup-snapshots");
        }

        public BackupOperationResult CreateSnapshot(BackupSnapshotRequest request)
        {
            if (request?.Record == null)
            {
                return Failure("Save target is required.");
            }

            var snapshotId = Guid.NewGuid().ToString("N");
            var snapshotPath = Path.Combine(_snapshotRoot, snapshotId);
            try
            {
                Directory.CreateDirectory(snapshotPath);
                CopyTargetDependencies(request.Record, snapshotPath);
                CopyProjectFile("Packages/manifest.json", snapshotPath);
                CopyProjectFile("Packages/packages-lock.json", snapshotPath);
                CopyProjectFile("ProjectSettings/ProjectVersion.txt", snapshotPath);

                var state = new SnapshotState
                {
                    SnapshotId = snapshotId,
                    RepositoryPath = GetRepositoryPath(request.Record.Id),
                    Record = request.Record,
                    BuildOutputPath = request.BuildOutputPath ?? string.Empty,
                    Platform = string.IsNullOrWhiteSpace(request.Platform)
                        ? EditorUserBuildSettings.activeBuildTarget.ToString()
                        : request.Platform,
                    CreatedAtUtc = request.CreatedAtUtc
                };
                File.WriteAllText(
                    Path.Combine(snapshotPath, SnapshotStateFile),
                    JsonConvert.SerializeObject(state, Formatting.Indented));
                return new BackupOperationResult
                {
                    Succeeded = true,
                    SnapshotId = snapshotId
                };
            }
            catch (Exception exception)
            {
                return new BackupOperationResult
                {
                    Succeeded = false,
                    SnapshotId = snapshotId,
                    Error = exception.Message
                };
            }
        }

        public BackupOperationResult Commit(string snapshotId, string message)
        {
            var snapshotPath = GetSnapshotPath(snapshotId);
            if (string.IsNullOrWhiteSpace(snapshotPath) || !Directory.Exists(snapshotPath))
            {
                return Failure("Snapshot was not found.", snapshotId);
            }

            try
            {
                var state = JsonConvert.DeserializeObject<SnapshotState>(
                    File.ReadAllText(Path.Combine(snapshotPath, SnapshotStateFile)));
                if (state == null ||
                    !IsDescendant(state.RepositoryPath, BackupRoot))
                {
                    return Failure("Snapshot repository is invalid.", snapshotId);
                }

                Directory.CreateDirectory(state.RepositoryPath);
                EnsureRepository(state.RepositoryPath);
                ClearWorkTree(state.RepositoryPath);
                CopyDirectory(snapshotPath, state.RepositoryPath);

                RunGit(state.RepositoryPath, "add", "--all");
                var diff = RunGit(
                    state.RepositoryPath,
                    "diff",
                    "--cached",
                    "--quiet",
                    "--",
                    ".",
                    ":(exclude)" + SnapshotStateFile);
                if (diff.ExitCode == 0)
                {
                    EnsureGitSuccess(RunGit(
                        state.RepositoryPath,
                        "reset",
                        "--hard",
                        "HEAD"));
                    Discard(snapshotId);
                    return new BackupOperationResult
                    {
                        Succeeded = true,
                        Skipped = true,
                        SnapshotId = snapshotId
                    };
                }

                if (diff.ExitCode != 1)
                {
                    throw new InvalidOperationException(diff.Error);
                }

                EnsureGitSuccess(RunGit(
                    state.RepositoryPath,
                    "commit",
                    "-m",
                    message ?? "SaveAndBackup"));
                var revision = RunGit(
                    state.RepositoryPath,
                    "rev-parse",
                    "HEAD");
                EnsureGitSuccess(revision);
                Discard(snapshotId);
                return new BackupOperationResult
                {
                    Succeeded = true,
                    SnapshotId = snapshotId,
                    CommitId = revision.Output.Trim()
                };
            }
            catch (Exception exception)
            {
                return Failure(exception.Message, snapshotId);
            }
        }

        public void Discard(string snapshotId)
        {
            var path = GetSnapshotPath(snapshotId);
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                DeleteDirectory(path);
            }
        }

        private void CopyTargetDependencies(
            SaveAndBackupRecord record,
            string snapshotPath)
        {
            var prefabPath = AssetDatabase.GUIDToAssetPath(record.TargetPrefabGuid);
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                throw new InvalidOperationException("Save target prefab was not found.");
            }

            var dependencies = AssetDatabase.GetDependencies(prefabPath, true)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var path in dependencies)
            {
                CopyProjectFile(path, snapshotPath);
                CopyProjectFile(path + ".meta", snapshotPath);
            }
        }

        private void CopyProjectFile(string relativePath, string destinationRoot)
        {
            var normalized = (relativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            var source = NormalizeFullPath(Path.Combine(_projectRoot, normalized));
            if (!IsDescendant(source, _projectRoot) || !File.Exists(source))
            {
                return;
            }

            var destination = Path.Combine(destinationRoot, normalized);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
            File.SetAttributes(destination, FileAttributes.Normal);
        }

        private void EnsureRepository(string repositoryPath)
        {
            if (!Directory.Exists(Path.Combine(repositoryPath, ".git")))
            {
                EnsureGitSuccess(RunGit(repositoryPath, "init"));
            }

            EnsureGitSuccess(RunGit(
                repositoryPath,
                "config",
                "--local",
                "user.name",
                "SaveAndBackup"));
            EnsureGitSuccess(RunGit(
                repositoryPath,
                "config",
                "--local",
                "user.email",
                "save-and-backup@localhost"));
        }

        private void ClearWorkTree(string repositoryPath)
        {
            if (!IsDescendant(repositoryPath, BackupRoot))
            {
                throw new InvalidOperationException("Backup repository is outside the configured root.");
            }

            foreach (var file in Directory.GetFiles(repositoryPath))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (var directory in Directory.GetDirectories(repositoryPath))
            {
                if (string.Equals(
                        Path.GetFileName(directory),
                        ".git",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DeleteDirectory(directory);
            }
        }

        private static void CopyDirectory(string sourceRoot, string destinationRoot)
        {
            foreach (var directory in Directory.GetDirectories(
                         sourceRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(
                    destinationRoot,
                    directory.Substring(sourceRoot.Length)
                        .TrimStart(Path.DirectorySeparatorChar)));
            }

            foreach (var file in Directory.GetFiles(
                         sourceRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                var destination = Path.Combine(
                    destinationRoot,
                    file.Substring(sourceRoot.Length)
                        .TrimStart(Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
        }

        private string GetRepositoryPath(string avatarId)
        {
            return Path.Combine(
                BackupRoot,
                _projectId,
                SafeSegment(avatarId));
        }

        private string GetSnapshotPath(string snapshotId)
        {
            if (string.IsNullOrWhiteSpace(snapshotId) ||
                !string.Equals(snapshotId, SafeSegment(snapshotId), StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var path = NormalizeFullPath(Path.Combine(_snapshotRoot, snapshotId));
            return IsDescendant(path, _snapshotRoot) ? path : string.Empty;
        }

        private string BackupRoot => NormalizeFullPath(_getBackupRoot());

        private static void DeleteDirectory(string path)
        {
            foreach (var file in Directory.GetFiles(
                         path,
                         "*",
                         SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, true);
        }

        private static GitResult RunGit(string repositoryPath, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "-C " + Quote(repositoryPath) + " " +
                    string.Join(" ", arguments.Select(Quote)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Git could not be started.");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new GitResult(process.ExitCode, output, error);
            }
        }

        private static void EnsureGitSuccess(GitResult result)
        {
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(result.Error)
                        ? result.Output
                        : result.Error);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string NormalizeFullPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : Path.GetFullPath(path).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
        }

        private static bool IsDescendant(string path, string root)
        {
            var normalizedPath = NormalizeFullPath(path);
            var normalizedRoot = NormalizeFullPath(root);
            return !string.IsNullOrWhiteSpace(normalizedPath) &&
                   !string.IsNullOrWhiteSpace(normalizedRoot) &&
                   normalizedPath.StartsWith(
                       normalizedRoot + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeSegment(string value)
        {
            var result = value ?? string.Empty;
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(result) ? "unknown" : result.Trim();
        }

        private static BackupOperationResult Failure(
            string error,
            string snapshotId = null)
        {
            return new BackupOperationResult
            {
                Succeeded = false,
                SnapshotId = snapshotId ?? string.Empty,
                Error = error ?? string.Empty
            };
        }

        [Serializable]
        private sealed class SnapshotState
        {
            public string SnapshotId { get; set; }
            public string RepositoryPath { get; set; }
            public SaveAndBackupRecord Record { get; set; }
            public string BuildOutputPath { get; set; }
            public string Platform { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }

        private sealed class GitResult
        {
            internal GitResult(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output ?? string.Empty;
                Error = error ?? string.Empty;
            }

            internal int ExitCode { get; }
            internal string Output { get; }
            internal string Error { get; }
        }
    }
}
