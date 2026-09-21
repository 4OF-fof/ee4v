using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ee4v.PhysBoneCollider;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ee4v.Mcp
{
    internal static class PhysBoneColliderMcpTools
    {
        internal static void Register()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_plan_physbone_colliders",
                "Builds a non-mutating ee4v PhysBone Collider proposal, loads existing ee4v-owned values, discovers assignable PhysBones under selected direct Armatures, and checks prerequisites.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["preset"] = McpSchemas.Enum("Lightweight", "Standard", "Full"),
                    ["minimumBoneLength"] = McpSchemas.Number(),
                    ["searchRootRefs"] = McpSchemas.Array(McpSchemas.String(
                        "Avatar or outfit roots whose direct Armature children should be searched for PhysBones."))
                }, "avatarRef"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    PhysBoneColliderApi.Plan(
                        Avatar(arguments),
                        (string)arguments["preset"] ?? "Standard",
                        (float?)arguments["minimumBoneLength"] ?? 0.03f,
                        SearchRoots(arguments))))),
                readOnly: true));

            var numberArray = McpSchemas.Array(McpSchemas.Number());
            var overrideSchema = McpSchemas.Object(new JObject
            {
                ["id"] = McpSchemas.String(
                    "Collider id returned by ee4v_plan_physbone_colliders."),
                ["enabled"] = McpSchemas.Boolean(),
                ["bonePath"] = McpSchemas.String(),
                ["position"] = numberArray,
                ["rotation"] = numberArray,
                ["radius"] = McpSchemas.Number(),
                ["height"] = McpSchemas.Number(),
                ["assignedPhysBonePaths"] = McpSchemas.Array(McpSchemas.String())
            }, "id");
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_apply_physbone_colliders",
                "Creates or replaces only the ee4v-owned collider prefab and assigns the selected colliders to selected PhysBones. Run ee4v_plan_physbone_colliders immediately before this tool.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["preset"] = McpSchemas.Enum("Lightweight", "Standard", "Full"),
                    ["minimumBoneLength"] = McpSchemas.Number(),
                    ["searchRootRefs"] = McpSchemas.Array(McpSchemas.String()),
                    ["colliderOverrides"] = McpSchemas.Array(overrideSchema),
                    ["assignAllTargets"] = McpSchemas.Boolean(
                        "When true, every enabled collider is assigned to every discovered PhysBone target.")
                }, "avatarRef"),
                arguments => Task.FromResult(McpToolResult.Success(McpJson.From(
                    PhysBoneColliderApi.Apply(
                        Avatar(arguments),
                        (string)arguments["preset"] ?? "Standard",
                        (float?)arguments["minimumBoneLength"] ?? 0.03f,
                        SearchRoots(arguments),
                        McpJson.To<List<PhysBoneColliderOverrideData>>(
                            arguments["colliderOverrides"]),
                        (bool?)arguments["assignAllTargets"] ?? false)))),
                readOnly: false,
                destructive: false,
                idempotent: true));
        }

        private static GameObject Avatar(JObject arguments)
        {
            return UnityObjectReference.ResolveGameObject(
                (string)arguments["avatarRef"]);
        }

        private static IReadOnlyList<GameObject> SearchRoots(JObject arguments)
        {
            return (McpJson.To<List<string>>(arguments["searchRootRefs"]) ??
                    new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(UnityObjectReference.ResolveGameObject)
                .ToArray();
        }
    }
}
