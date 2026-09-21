using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Ee4v.Mcp
{
    internal sealed class McpToolDefinition
    {
        internal McpToolDefinition(
            string name,
            string description,
            JObject inputSchema,
            Func<JObject, Task<McpToolResult>> invoke,
            bool readOnly,
            bool destructive = false,
            bool idempotent = true,
            bool openWorld = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
            InputSchema = inputSchema ?? McpSchemas.Object();
            Invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
            ReadOnly = readOnly;
            Destructive = destructive;
            Idempotent = idempotent;
            OpenWorld = openWorld;
        }

        internal string Name { get; }
        internal string Description { get; }
        internal JObject InputSchema { get; }
        internal Func<JObject, Task<McpToolResult>> Invoke { get; }
        internal bool ReadOnly { get; }
        internal bool Destructive { get; }
        internal bool Idempotent { get; }
        internal bool OpenWorld { get; }

        internal JObject ToProtocolValue()
        {
            return new JObject
            {
                ["name"] = Name,
                ["description"] = Description,
                ["inputSchema"] = InputSchema,
                ["annotations"] = new JObject
                {
                    ["readOnlyHint"] = ReadOnly,
                    ["destructiveHint"] = Destructive,
                    ["idempotentHint"] = Idempotent,
                    ["openWorldHint"] = OpenWorld
                }
            };
        }
    }

    internal sealed class McpToolResult
    {
        private McpToolResult(
            JToken structuredContent,
            IReadOnlyList<JObject> content,
            bool isError)
        {
            StructuredContent = structuredContent ?? new JObject();
            Content = content ?? Array.Empty<JObject>();
            IsError = isError;
        }

        internal JToken StructuredContent { get; }
        internal IReadOnlyList<JObject> Content { get; }
        internal bool IsError { get; }

        internal static McpToolResult Success(JToken value)
        {
            var structured = value ?? new JObject();
            return new McpToolResult(
                structured,
                new[]
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = structured.ToString(Newtonsoft.Json.Formatting.None)
                    }
                },
                false);
        }

        internal static McpToolResult Image(
            JToken metadata,
            byte[] pngBytes)
        {
            var structured = metadata ?? new JObject();
            var content = new List<JObject>
            {
                new JObject
                {
                    ["type"] = "text",
                    ["text"] = structured.ToString(Newtonsoft.Json.Formatting.None)
                }
            };
            if (pngBytes != null && pngBytes.Length > 0)
            {
                content.Add(new JObject
                {
                    ["type"] = "image",
                    ["data"] = Convert.ToBase64String(pngBytes),
                    ["mimeType"] = "image/png"
                });
            }

            return new McpToolResult(structured, content, false);
        }

        internal static McpToolResult Error(
            string code,
            string message,
            JToken details = null)
        {
            var structured = new JObject
            {
                ["ok"] = false,
                ["code"] = code ?? "tool_error",
                ["message"] = message ?? string.Empty
            };
            if (details != null)
            {
                structured["details"] = details;
            }

            return new McpToolResult(
                structured,
                new[]
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = structured.ToString(Newtonsoft.Json.Formatting.None)
                    }
                },
                true);
        }

        internal JObject ToProtocolValue()
        {
            return new JObject
            {
                ["content"] = new JArray(Content),
                ["structuredContent"] = StructuredContent,
                ["isError"] = IsError
            };
        }
    }

    internal static class McpToolRegistry
    {
        private static readonly Dictionary<string, McpToolDefinition> Tools =
            new Dictionary<string, McpToolDefinition>(StringComparer.Ordinal);

        internal static void Register(McpToolDefinition tool)
        {
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }

            Tools[tool.Name] = tool;
        }

        internal static JArray List()
        {
            return new JArray(Tools.Values
                .OrderBy(tool => tool.Name, StringComparer.Ordinal)
                .Select(tool => tool.ToProtocolValue()));
        }

        internal static async Task<McpToolResult> Invoke(
            string name,
            JObject arguments)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                !Tools.TryGetValue(name, out var tool))
            {
                return McpToolResult.Error(
                    "tool_not_found",
                    "The requested ee4v MCP tool was not found.");
            }

            try
            {
                return await tool.Invoke(arguments ?? new JObject());
            }
            catch (McpToolException exception)
            {
                return McpToolResult.Error(
                    exception.Code,
                    exception.Message,
                    exception.Details);
            }
            catch (Exception exception)
            {
                return McpToolResult.Error(
                    "tool_failed",
                    exception.Message,
                    new JObject
                    {
                        ["exceptionType"] = exception.GetType().FullName
                    });
            }
        }
    }

    internal sealed class McpToolException : Exception
    {
        internal McpToolException(
            string code,
            string message,
            JToken details = null)
            : base(message)
        {
            Code = code ?? "invalid_request";
            Details = details;
        }

        internal string Code { get; }
        internal JToken Details { get; }
    }
}
