using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ee4v.Cli;

internal static class Program
{
    internal const int ApiVersion = 1;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private static readonly HashSet<string> ClientOptions = new(StringComparer.Ordinal)
        { "project", "state-dir", "timeout", "output", "no-wait", "args-file", "raw", "agent", "destination" };

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        try { return await Run(Arguments.Parse(args), cancellation.Token); }
        catch (CliException e) { Print(new JsonObject { ["ok"] = false, ["code"] = e.Code, ["message"] = e.Message, ["details"] = e.Details }); return e.ExitCode; }
        catch (OperationCanceledException) { Print(new JsonObject { ["ok"] = false, ["code"] = "client_cancelled", ["message"] = "Waiting stopped. A started Editor operation may still be running; inspect its job instead of repeating it." }); return 4; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or HttpRequestException or FormatException or InvalidOperationException)
        { Print(new JsonObject { ["ok"] = false, ["code"] = "client_error", ["message"] = e.Message }); return 1; }
    }

    private static async Task<int> Run(Arguments args, CancellationToken token)
    {
        var command = args.Positionals.FirstOrDefault() ?? "help";
        if (command is "help" or "--help" or "-h") { Console.WriteLine(Help); return 0; }
        if (command is "version" or "--version") { Print(new JsonObject { ["version"] = "0.2.0", ["apiVersion"] = ApiVersion }); return 0; }
        if (command is "setup" or "doctor" or "uninstall") return Installation.Run(command, args);
        var editors = await Discover(args, token);
        if (command == "projects") { Print(new JsonArray(editors.Select(e => (JsonNode)e.PublicValue()).ToArray())); return 0; }
        var editor = SelectEditor(editors, args.Value("project"));
        using var connection = new Connection(editor);
        if (command == "health") { Print(await connection.Send(HttpMethod.Get, "/api/health", null, token)); return 0; }
        if (command == "jobs") { Print(await connection.Send(HttpMethod.Get, "/api/jobs", null, token)); return 0; }
        if (command is "wait" or "job" or "cancel" or "release")
        {
            if (args.Positionals.Count != 2) throw Invalid("Supply exactly one job id.");
            var id = args.Positionals[1];
            var path = "/api/jobs/" + Uri.EscapeDataString(id);
            if (command == "cancel") { Print(await connection.Send(HttpMethod.Post, path + "/cancel", new JsonObject(), token)); return 0; }
            if (command == "release") { Print(await connection.Send(HttpMethod.Delete, path, null, token)); return 0; }
            var job = command == "wait" ? await Wait(connection, id, args.Seconds, token) : await connection.Send(HttpMethod.Get, path, null, token);
            return await PrintJob(job, args, token);
        }
        var catalog = (await connection.Send(HttpMethod.Get, "/api/operations", null, token))["operations"]!.AsArray();
        if (command == "operations")
        {
            var words = string.Join(" ", args.Positionals.Skip(1)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Print(new JsonArray(catalog.Where(n => words.All(w => ((string)n!["name"]! + " " + (string)n["description"]!).Contains(w, StringComparison.OrdinalIgnoreCase)))
                .Select(n => (JsonNode)new JsonObject { ["command"] = CommandName((string)n!["name"]!), ["description"] = n["description"]!.DeepClone(), ["annotations"] = n["annotations"]!.DeepClone() }).ToArray()));
            return 0;
        }
        var requested = command == "describe" ? args.Positionals.ElementAtOrDefault(1) : command;
        var definition = catalog.FirstOrDefault(n => (string)n!["name"]! == requested || CommandName((string)n["name"]!) == requested);
        if (definition == null) throw Invalid("Unknown operation. Use 'ee4v-cli operations <words>' to search.");
        if (command == "describe" || args.Positionals.Count == 2 && (args.Positionals[1] is "--help" or "-h"))
        { Print(definition.DeepClone()); return 0; }
        if (args.Positionals.Count != 1) throw Invalid("Operation inputs must use named options or --args-file.");
        var input = args.BuildInput(definition["inputSchema"]!.AsObject());
        var waitSeconds = args.Seconds; // Reject invalid local options before submitting a write.
        args.ValidateFlags();
        JsonObject jobResult;
        try
        {
            jobResult = await connection.Send(HttpMethod.Post, "/api/invoke", new JsonObject { ["operation"] = definition["name"]!.DeepClone(), ["arguments"] = input }, token);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            throw new CliException(3, "submission_outcome_unknown", "The submission response was lost. The Editor may have executed the operation. Inspect 'jobs' before considering another write.", new JsonObject { ["cause"] = e.Message });
        }
        var idValue = (string)jobResult["id"]!;
        Console.Error.WriteLine("job " + idValue);
        if (args.Flag("no-wait")) { Print(jobResult); return 0; }
        try { jobResult = await Wait(connection, idValue, waitSeconds, token); }
        catch (OperationCanceledException)
        {
            Print(new JsonObject { ["ok"] = false, ["code"] = "wait_stopped", ["jobId"] = idValue, ["message"] = "Use wait or cancel with this job id; do not resubmit the operation." });
            return 4;
        }
        return await PrintJob(jobResult, args, token);
    }

    internal static string CommandName(string name) => name.StartsWith("ee4v_", StringComparison.Ordinal) ? name[5..].Replace('_', '-') : name;
    internal static void Print(JsonNode? value) => Console.WriteLine(value?.ToJsonString(Json) ?? "null");
    internal static CliException Invalid(string message) => new(2, "invalid_arguments", message);

    private static Editor SelectEditor(List<Editor> editors, string? selector)
    {
        var candidates = editors;
        if (selector != null)
        {
            var exact = editors.Where(e => string.Equals(e.ProjectPath.TrimEnd('\\', '/'), selector.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.ProjectName, selector, StringComparison.OrdinalIgnoreCase)).ToList();
            candidates = exact.Count > 0 ? exact : editors.Where(e => e.ProjectPath.Contains(selector, StringComparison.OrdinalIgnoreCase) || e.ProjectName.Contains(selector, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else
        {
            var cwd = Path.GetFullPath(Environment.CurrentDirectory).TrimEnd(Path.DirectorySeparatorChar);
            var local = editors.Where(e => cwd.Equals(e.ProjectPath, StringComparison.OrdinalIgnoreCase) || cwd.StartsWith(e.ProjectPath.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)).ToList();
            if (local.Count == 1) candidates = local;
        }
        if (candidates.Count != 1) throw new CliException(3, candidates.Count == 0 ? "editor_not_found" : "ambiguous_project",
            candidates.Count == 0 ? "Open the target project in Unity with ee4v installed." : "Several Editors match; supply --project with an absolute project path.",
            new JsonArray(candidates.Select(e => (JsonNode)e.PublicValue()).ToArray()));
        return candidates[0];
    }

    private static async Task<List<Editor>> Discover(Arguments args, CancellationToken token)
    {
        var root = args.Value("state-dir") ?? Environment.GetEnvironmentVariable("EE4V_CLI_STATE_DIR") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ee4v", "cli");
        var directory = Path.Combine(root, "instances");
        var found = new List<Editor>();
        if (!Directory.Exists(directory)) return found;
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var descriptor = JsonNode.Parse(await File.ReadAllTextAsync(file, token))!.AsObject();
                if ((int?)descriptor["apiVersion"] != ApiVersion) continue;
                var editor = new Editor(descriptor);
                using var process = Process.GetProcessById(editor.Pid);
                if (process.HasExited) continue;
                using var connection = new Connection(editor);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(2));
                var health = await connection.Send(HttpMethod.Get, "/api/health", null, deadline.Token);
                if ((string?)health["session"] == editor.Session && (string?)health["projectPath"] == editor.ProjectPath) found.Add(editor);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or HttpRequestException or OperationCanceledException or CliException) { }
        }
        return found;
    }

    private static async Task<JsonObject> Wait(Connection connection, string id, double seconds, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
        try
        {
            while (true)
            {
                var job = await connection.Send(HttpMethod.Get, "/api/jobs/" + Uri.EscapeDataString(id), null, deadline.Token);
                if (job["result"] != null) return job;
                await Task.Delay(200, deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new CliException(4, "wait_timeout", "The job continues in the Editor. Use wait with this job id, rather than resubmitting.", new JsonObject { ["jobId"] = id });
        }
        catch (CliException e)
        {
            throw new CliException(e.ExitCode, e.Code, e.Message, new JsonObject { ["jobId"] = id, ["error"] = e.Details?.DeepClone() });
        }
        catch (HttpRequestException e)
        {
            throw new CliException(3, "job_connection_lost", "The job outcome is unknown. Inspect this job after reconnecting; do not repeat the write.",
                new JsonObject { ["jobId"] = id, ["cause"] = e.Message });
        }
    }

    private static async Task<int> PrintJob(JsonObject job, Arguments args, CancellationToken token)
    {
        if (job["result"] is not JsonObject result) { Print(job); return 0; }
        if (args.Flag("raw")) { Print(job); return (bool?)result["isError"] == true ? 1 : 0; }
        var images = result["content"]!.AsArray().Where(n => (string?)n?["type"] == "image").ToArray();
        var saved = new JsonArray();
        try
        {
        for (var index = 0; index < images.Length; index++)
        {
            var mime = (string)images[index]!["mimeType"]!;
            var extension = mime switch { "image/png" => ".png", "image/jpeg" => ".jpg", "image/gif" => ".gif", "image/webp" => ".webp", _ => throw Invalid("Unsupported image content: " + mime) };
            var output = args.Value("output");
            var path = output == null ? Path.Combine(Path.GetTempPath(), "ee4v-cli", "images", Guid.NewGuid().ToString("N") + extension) : Path.GetFullPath(output);
            if (images.Length > 1) path = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-" + (index + 1) + extension);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bytes = Convert.FromBase64String((string)images[index]!["data"]!);
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await stream.WriteAsync(bytes, token);
            saved.Add(new JsonObject { ["path"] = Path.GetFullPath(path), ["mimeType"] = mime, ["bytes"] = bytes.Length });
        }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            throw new CliException(1, "image_save_failed", e.Message, new JsonObject { ["jobId"] = job["id"]!.DeepClone(), ["imagesWritten"] = saved });
        }
        Print(new JsonObject { ["jobId"] = job["id"]!.DeepClone(), ["state"] = job["state"]!.DeepClone(),
            ["isError"] = result["isError"]!.DeepClone(), ["result"] = result["structuredContent"]!.DeepClone(), ["images"] = saved });
        return (bool?)result["isError"] == true ? 1 : 0;
    }

    private sealed class Editor
    {
        private readonly JsonObject _value;
        internal Editor(JsonObject value)
        {
            _value = value;
            var uri = new Uri(Endpoint);
            if (uri.Scheme != "http" || !IPAddress.TryParse(uri.Host, out var ip) || !IPAddress.IsLoopback(ip) || uri.AbsolutePath != "/" || uri.UserInfo.Length > 0)
                throw Invalid("Editor descriptors must use a loopback HTTP endpoint.");
        }
        internal string Endpoint => (string)_value["endpoint"]!;
        internal string ProjectPath => (string)_value["projectPath"]!;
        internal string ProjectName => (string)_value["projectName"]!;
        internal string Session => (string)_value["session"]!;
        internal string Token => (string)_value["token"]!;
        internal int Pid => (int)_value["pid"]!;
        internal JsonObject PublicValue() { var value = (JsonObject)_value.DeepClone(); value.Remove("token"); return value; }
    }

    private sealed class Connection : IDisposable
    {
        private readonly HttpClient _http;
        internal Connection(Editor editor)
        {
            _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = new Uri(editor.Endpoint), Timeout = TimeSpan.FromSeconds(15) };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", editor.Token);
        }
        internal async Task<JsonObject> Send(HttpMethod method, string path, JsonObject? body, CancellationToken token)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body != null) request.Content = new StringContent(body.ToJsonString(Json), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, token);
            var value = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!.AsObject();
            if (!response.IsSuccessStatusCode) throw new CliException(response.StatusCode is HttpStatusCode.BadRequest ? 2 : 3,
                (string?)value["code"] ?? "connection_error", (string?)value["message"] ?? "Editor rejected the request.", value);
            return value;
        }
        public void Dispose() => _http.Dispose();
    }

    internal sealed class CliException(int exitCode, string code, string message, JsonNode? details = null) : Exception(message)
    { internal int ExitCode { get; } = exitCode; internal string Code { get; } = code; internal JsonNode? Details { get; } = details; }

    internal sealed class Arguments
    {
        internal List<string> Positionals { get; } = [];
        internal Dictionary<string, List<string>> Options { get; } = new(StringComparer.Ordinal);
        internal bool Has(string key) => Options.ContainsKey(key);
        internal string? Value(string key) => Options.TryGetValue(key, out var values) ? values.Last() : null;
        internal bool Flag(string key) => Value(key) is string value ? bool.TryParse(value, out var boolean) ? boolean : throw Invalid("--" + key + " requires true or false.") : false;
        internal void ValidateFlags() { Flag("raw"); Flag("no-wait"); }
        internal double Seconds => double.TryParse(Value("timeout") ?? "120", NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 && seconds <= 86400 ? seconds : throw Invalid("--timeout requires 0 < seconds <= 86400.");
        internal static Arguments Parse(string[] values)
        {
            var args = new Arguments();
            for (var index = 0; index < values.Length; index++)
            {
                if (!values[index].StartsWith("--", StringComparison.Ordinal) || values[index] is "--help" or "--version") { args.Positionals.Add(values[index]); continue; }
                var key = values[index][2..];
                var equals = key.IndexOf('=');
                string value;
                if (equals >= 0) { value = key[(equals + 1)..]; key = key[..equals]; }
                else value = index + 1 < values.Length && !values[index + 1].StartsWith("--", StringComparison.Ordinal) ? values[++index] : "true";
                if (!args.Options.TryGetValue(key, out var entries)) args.Options[key] = entries = [];
                entries.Add(value);
            }
            return args;
        }
        internal JsonObject BuildInput(JsonObject schema)
        {
            var input = Value("args-file") is string file ? JsonNode.Parse(file == "-" ? Console.In.ReadToEnd() : File.ReadAllText(file)) as JsonObject ?? throw Invalid("--args-file must contain a JSON object.") : new JsonObject();
            foreach (var option in Options.Where(p => !ClientOptions.Contains(p.Key)))
            {
                var segments = option.Key.Split('.');
                var target = input;
                var currentSchema = schema;
                for (var index = 0; index < segments.Length; index++)
                {
                    var properties = currentSchema["properties"] as JsonObject ?? throw Invalid("Use --args-file for complex inputs.");
                    static string Normalize(string name) => name.Replace("-", "").Replace("_", "").ToLowerInvariant();
                    var matching = properties.Where(p => Normalize(p.Key) == Normalize(segments[index])).ToArray();
                    if (matching.Length != 1) throw Invalid("Unknown or ambiguous argument: --" + option.Key);
                    var name = matching[0].Key;
                    var propertySchema = matching[0].Value!.AsObject();
                    if (index < segments.Length - 1)
                    {
                        if ((string?)propertySchema["type"] != "object") throw Invalid("Nested options require an object property: " + name);
                        target[name] ??= new JsonObject();
                        target = target[name] as JsonObject ?? throw Invalid("Conflicting inputs for " + name);
                        currentSchema = propertySchema;
                        continue;
                    }
                    if ((string?)propertySchema["type"] == "array")
                        target[name] = new JsonArray(option.Value.Select(value => ParseValue(value, propertySchema["items"]!.AsObject())).ToArray());
                    else
                    {
                        if (option.Value.Count != 1) throw Invalid("Only array arguments can be repeated: " + name);
                        target[name] = ParseValue(option.Value[0], propertySchema);
                    }
                }
            }
            return input;
        }
        private static JsonNode? ParseValue(string value, JsonObject schema)
        {
            var type = (string?)schema["type"];
            if (type == "string") return JsonValue.Create(value);
            if (type == "boolean" && bool.TryParse(value, out var boolean)) return JsonValue.Create(boolean);
            if (type == "integer" && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return JsonValue.Create(integer);
            if (type == "number" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)) return JsonValue.Create(number);
            throw Invalid("Invalid " + (type ?? "complex") + " value; use --args-file for objects and nested arrays.");
        }
    }

    private const string Help = """
ee4v-cli — ee4v domain operations in a running Unity Editor

ee4v-cli projects | health | version
ee4v-cli operations [search words]
ee4v-cli describe <operation>
ee4v-cli <operation> --argument value [--args-file input.json]
ee4v-cli jobs | job <id> | wait <id> | cancel <id> | release <id>
ee4v-cli setup|doctor|uninstall [--agent codex|claude-code|both] [--destination <skills directory>]

Operations use kebab-case without ee4v_ (inspect-avatar, asset-search, ...).
MCP operation names are also accepted. Arguments accept camelCase or kebab-case.
Repeat options for arrays; use --settings.field value for object fields.
Options override --args-file fields. Use --args-file - to read JSON from stdin.

--project <absolute path or unique name>  Select the running Editor
--timeout <seconds>                       Wait deadline (120 by default)
--no-wait                                 Return a job id immediately
--output <image path>                     Save images; multiple images get numbered
--raw                                    Include the full result and base64 images
--state-dir <directory>                   Override EE4V_CLI_STATE_DIR

stdout: JSON results. stderr: job ids/progress. No write is automatically retried.
Exit: 0 success/submitted, 1 operation or local failure, 2 invalid arguments,
      3 connection/selection/unknown outcome, 4 waiting stopped or timed out.
Open Unity first. Generic Unity operations belong to your Unity CLI.
""";
}
