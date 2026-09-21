using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Mcp
{
    internal static class McpStatusTools
    {
        internal static void Register()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_server_status",
                "Returns the connected Unity Editor and ee4v MCP server status.",
                McpSchemas.Object(),
                _ => Task.FromResult(McpToolResult.Success(new JObject
                {
                    ["ok"] = true,
                    ["unityVersion"] = Application.unityVersion,
                    ["projectName"] = Application.productName,
                    ["projectPath"] = System.IO.Path.GetFullPath("."),
                    ["isPlaying"] = EditorApplication.isPlaying,
                    ["isCompiling"] = EditorApplication.isCompiling,
                    ["isUpdating"] = EditorApplication.isUpdating
                })),
                readOnly: true));
        }
    }
}
