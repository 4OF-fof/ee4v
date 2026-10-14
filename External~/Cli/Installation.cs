using System.Text.Json.Nodes;

namespace Ee4v.Cli;

internal static class Installation
{
    private const string ManagedSkill = "ee4v-cli";

    internal static int Run(string command, Program.Arguments args)
    {
        var agent = args.Value("agent") ?? "both";
        if (agent is not ("both" or "codex" or "claude-code")) throw Program.Invalid("--agent must be codex, claude-code or both.");
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
        if (!File.Exists(Path.Combine(source, "SKILL.md"))) throw Program.Invalid("Run the bundled bin/win-x64/ee4v-cli.exe inside the ee4v-cli skill directory.");
        var destinations = args.Value("destination") is string custom ? new[] { Path.GetFullPath(custom) } :
            new[] { agent is "both" or "codex" ? Path.Combine(Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"), "skills") : null,
                agent is "both" or "claude-code" ? Path.Combine(Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"), "skills") : null }.Where(path => path != null).Cast<string>().ToArray();
        bool IsSource(string target) => string.Equals(Path.TrimEndingDirectorySeparator(target), Path.TrimEndingDirectorySeparator(source), StringComparison.OrdinalIgnoreCase);
        if (command == "uninstall" && destinations.Any(directory => IsSource(Path.GetFullPath(Path.Combine(directory, ManagedSkill)))))
            throw Program.Invalid("The running skill cannot uninstall itself on Windows. Run uninstall from another copy of the skill bundle.");
        var results = new JsonArray();
        var failed = false;
        foreach (var directory in destinations.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var target = Path.GetFullPath(Path.Combine(directory, ManagedSkill));
            if (Directory.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                throw Program.Invalid("The managed skill directory must not be a symlink or junction: " + target);
            var marker = Path.Combine(target, ".ee4v-cli-managed");
            var owned = File.Exists(marker) && File.ReadAllText(marker).Trim() == ManagedSkill;
            var exists = Directory.Exists(target);
            var isSource = IsSource(target);
            if (command == "doctor")
            {
                var current = (owned || isSource) && Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                    .Where(file => Path.GetFileName(file) != ".ee4v-cli-managed").All(file =>
                    File.Exists(Path.Combine(target, Path.GetRelativePath(source, file))) &&
                    File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(Path.Combine(target, Path.GetRelativePath(source, file)))));
                failed |= !current;
                results.Add(new JsonObject { ["path"] = target, ["managed"] = owned, ["current"] = current });
                continue;
            }
            if (exists && !owned && !isSource) throw Program.Invalid("An unmanaged skill exists at " + target + "; choose --destination or retain it.");
            if (command == "uninstall")
            {
                // A fixed child of the explicit skills directory, checked before recursive removal.
                if (!string.Equals(Path.GetDirectoryName(target), Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)), StringComparison.OrdinalIgnoreCase))
                    throw Program.Invalid("The cleanup target is outside the explicit skills directory.");
                if (owned) Directory.Delete(target, true);
                results.Add(new JsonObject { ["path"] = target, ["removed"] = owned });
                continue;
            }
            Directory.CreateDirectory(target);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Where(file => Path.GetFileName(file) != ".ee4v-cli-managed"))
            {
                if (isSource) continue;
                var destination = Path.Combine(target, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, true);
            }
            File.WriteAllText(marker, ManagedSkill);
            results.Add(new JsonObject { ["path"] = target, ["installed"] = true });
        }
        Program.Print(new JsonObject { ["ok"] = !failed, ["skills"] = results });
        return failed ? 1 : 0;
    }
}
