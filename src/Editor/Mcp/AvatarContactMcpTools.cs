using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Mcp
{
    internal static class AvatarContactMcpTools
    {
        private const string SenderTypeName =
            "VRC.SDK3.Dynamics.Contact.Components.VRCContactSender";
        private const string ReceiverTypeName =
            "VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver";
        private const string ObjectPrefix = "ee4v Contact - ";

        internal static void Register()
        {
            RegisterList();
            RegisterUpsert();
        }

        private static void RegisterList()
        {
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_list_contacts",
                "Lists VRChat Contact Senders and Receivers with shapes, tags, and Receiver parameters on an avatar.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String()
                }, "avatarRef"),
                arguments => Task.FromResult(McpToolResult.Success(
                    ListContacts(Avatar(arguments)))),
                readOnly: true));
        }

        private static void RegisterUpsert()
        {
            var vector = McpSchemas.Object(new JObject
            {
                ["x"] = McpSchemas.Number(),
                ["y"] = McpSchemas.Number(),
                ["z"] = McpSchemas.Number()
            }, "x", "y", "z");
            McpToolRegistry.Register(new McpToolDefinition(
                "ee4v_upsert_contact",
                "Creates or updates an ee4v-owned VRChat Contact child below an avatar bone. Supports Sender and Receiver sphere/capsule shapes and validates the containing avatar.",
                McpSchemas.Object(new JObject
                {
                    ["avatarRef"] = McpSchemas.String(),
                    ["parentRef"] = McpSchemas.String(
                        "Avatar root or descendant that should own the Contact child."),
                    ["contactName"] = McpSchemas.String(
                        "Stable unique name below parentRef."),
                    ["kind"] = McpSchemas.Enum("sender", "receiver"),
                    ["shape"] = McpSchemas.Enum("sphere", "capsule"),
                    ["radius"] = McpSchemas.Number(),
                    ["height"] = McpSchemas.Number(
                        "Capsule height. Ignored for a sphere."),
                    ["position"] = vector,
                    ["rotationEuler"] = vector,
                    ["collisionTags"] = McpSchemas.Array(McpSchemas.String()),
                    ["allowSelf"] = McpSchemas.Boolean(),
                    ["allowOthers"] = McpSchemas.Boolean(),
                    ["parameter"] = McpSchemas.String(
                        "Required for a Receiver."),
                    ["receiverType"] = McpSchemas.Enum(
                        "constant", "onEnter", "proximity"),
                    ["localOnly"] = McpSchemas.Boolean(),
                    ["minVelocity"] = McpSchemas.Number(),
                    ["dryRun"] = McpSchemas.Boolean()
                }, "avatarRef", "parentRef", "contactName", "kind", "shape",
                    "radius", "position", "rotationEuler", "collisionTags"),
                arguments => Task.FromResult(McpToolResult.Success(Upsert(
                    Avatar(arguments),
                    UnityObjectReference.ResolveGameObject(
                        (string)arguments["parentRef"]),
                    RequiredName(arguments),
                    RequiredEnum(arguments, "kind", "sender", "receiver"),
                    RequiredEnum(arguments, "shape", "sphere", "capsule"),
                    (float)arguments["radius"],
                    (float?)arguments["height"] ?? 0f,
                    Vector(arguments["position"] as JObject),
                    Vector(arguments["rotationEuler"] as JObject),
                    StringArray(arguments, "collisionTags"),
                    (bool?)arguments["allowSelf"] ?? true,
                    (bool?)arguments["allowOthers"] ?? true,
                    (string)arguments["parameter"],
                    (string)arguments["receiverType"] ?? "constant",
                    (bool?)arguments["localOnly"] ?? false,
                    (float?)arguments["minVelocity"],
                    (bool?)arguments["dryRun"] ?? false))),
                readOnly: false,
                destructive: false,
                idempotent: true));
        }

        private static JObject ListContacts(GameObject avatar)
        {
            var senderType = FindComponentType(SenderTypeName);
            var receiverType = FindComponentType(ReceiverTypeName);
            if (senderType == null || receiverType == null)
            {
                return new JObject
                {
                    ["ok"] = true,
                    ["vrchatSdkAvailable"] = false,
                    ["contacts"] = new JArray(),
                    ["warnings"] = new JArray("VRChat Contact types were not found.")
                };
            }

            var contacts = avatar.GetComponentsInChildren<Component>(true)
                .Where(component => component != null &&
                                    (senderType.IsInstanceOfType(component) ||
                                     receiverType.IsInstanceOfType(component)))
                .OrderBy(component => UnityObjectReference.HierarchyPath(
                    component.transform), StringComparer.Ordinal)
                .Select(component => ContactValue(
                    avatar,
                    component,
                    receiverType.IsInstanceOfType(component)));
            return new JObject
            {
                ["ok"] = true,
                ["vrchatSdkAvailable"] = true,
                ["contacts"] = new JArray(contacts)
            };
        }

        private static JObject Upsert(
            GameObject avatar,
            GameObject parent,
            string contactName,
            string kind,
            string shape,
            float radius,
            float height,
            Vector3 position,
            Vector3 rotationEuler,
            IReadOnlyList<string> collisionTags,
            bool allowSelf,
            bool allowOthers,
            string parameter,
            string receiverType,
            bool localOnly,
            float? minVelocity,
            bool dryRun)
        {
            EnsureEditableChild(avatar, parent);
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
            {
                throw new McpToolException(
                    "invalid_contact_radius",
                    "radius must be a finite value greater than zero.");
            }

            if (shape == "capsule" &&
                (height <= 0f || float.IsNaN(height) || float.IsInfinity(height)))
            {
                throw new McpToolException(
                    "invalid_contact_height",
                    "height must be a finite value greater than zero for a capsule.");
            }

            if (collisionTags.Count == 0)
            {
                throw new McpToolException(
                    "contact_tags_required",
                    "collisionTags must contain at least one tag.");
            }

            var isReceiver = kind == "receiver";
            if (isReceiver && string.IsNullOrWhiteSpace(parameter))
            {
                throw new McpToolException(
                    "contact_parameter_required",
                    "parameter is required for a Contact Receiver.");
            }

            var componentType = RequireComponentType(
                isReceiver ? ReceiverTypeName : SenderTypeName);
            var otherType = RequireComponentType(
                isReceiver ? SenderTypeName : ReceiverTypeName);
            var objectName = ObjectPrefix + contactName;
            var existing = DirectChild(parent.transform, objectName)?.gameObject;
            if (existing != null && existing.GetComponent(otherType) != null)
            {
                throw new McpToolException(
                    "contact_type_conflict",
                    "contactName is already used by the other Contact kind below parentRef.");
            }

            var plan = new JObject
            {
                ["ok"] = true,
                ["dryRun"] = dryRun,
                ["kind"] = kind,
                ["contactName"] = contactName,
                ["parentRef"] = UnityObjectReference.Create(parent),
                ["shape"] = shape,
                ["radius"] = radius,
                ["height"] = shape == "capsule" ? height : 0f,
                ["position"] = VectorValue(position),
                ["rotationEuler"] = VectorValue(rotationEuler),
                ["collisionTags"] = new JArray(collisionTags),
                ["allowSelf"] = allowSelf,
                ["allowOthers"] = allowOthers
            };
            if (isReceiver)
            {
                plan["parameter"] = parameter.Trim();
                plan["receiverType"] = receiverType;
                plan["localOnly"] = localOnly;
                if (minVelocity.HasValue)
                {
                    plan["minVelocity"] = minVelocity.Value;
                }
            }

            if (dryRun)
            {
                return plan;
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Upsert ee4v Contact");
            try
            {
                var contactObject = existing;
                if (contactObject == null)
                {
                    contactObject = new GameObject(objectName);
                    Undo.RegisterCreatedObjectUndo(contactObject, "Create VRChat Contact");
                    contactObject.transform.SetParent(parent.transform, false);
                }

                var contact = contactObject.GetComponent(componentType) ??
                              Undo.AddComponent(contactObject, componentType);
                Undo.RecordObject(contact, "Configure VRChat Contact");
                SetRequired(contact, "rootTransform", contactObject.transform);
                SetEnum(contact, "shapeType", shape);
                SetRequired(contact, "radius", radius);
                SetRequired(contact, "height", shape == "capsule" ? height : 0f);
                SetRequired(contact, "position", position);
                SetRequired(contact, "rotation", Quaternion.Euler(rotationEuler));
                SetStringList(contact, "collisionTags", collisionTags);
                SetOptional(contact, "allowSelf", allowSelf);
                SetOptional(contact, "allowOthers", allowOthers);
                if (isReceiver)
                {
                    SetRequired(contact, "parameter", parameter.Trim());
                    SetEnum(contact, "receiverType", receiverType);
                    SetOptional(contact, "localOnly", localOnly);
                    if (minVelocity.HasValue)
                    {
                        SetOptional(contact, "minVelocity", minVelocity.Value);
                    }
                }

                EditorUtility.SetDirty(contact);
                PrefabUtility.RecordPrefabInstancePropertyModifications(contact);
                plan["dryRun"] = false;
                plan["contactRef"] = UnityObjectReference.Create(contact);
                plan["objectRef"] = UnityObjectReference.Create(contactObject);
                plan["path"] = AnimationUtility.CalculateTransformPath(
                    contactObject.transform,
                    avatar.transform);
                Undo.CollapseUndoOperations(group);
                return plan;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static JObject ContactValue(
            GameObject avatar,
            Component component,
            bool isReceiver)
        {
            var value = new JObject
            {
                ["contactRef"] = UnityObjectReference.Create(component),
                ["objectRef"] = UnityObjectReference.Create(component.gameObject),
                ["path"] = AnimationUtility.CalculateTransformPath(
                    component.transform,
                    avatar.transform),
                ["kind"] = isReceiver ? "receiver" : "sender",
                ["shape"] = Get(component, "shapeType")?.ToString() ?? string.Empty,
                ["radius"] = Number(Get(component, "radius")),
                ["height"] = Number(Get(component, "height")),
                ["position"] = VectorValue(Get(component, "position") is Vector3 position
                    ? position
                    : Vector3.zero),
                ["collisionTags"] = new JArray(StringValues(
                    Get(component, "collisionTags")))
            };
            if (isReceiver)
            {
                value["parameter"] = (string)Get(component, "parameter") ?? string.Empty;
                value["receiverType"] = Get(component, "receiverType")?.ToString() ??
                                        string.Empty;
                value["localOnly"] = Boolean(Get(component, "localOnly"));
                value["allowSelf"] = Boolean(Get(component, "allowSelf"));
                value["allowOthers"] = Boolean(Get(component, "allowOthers"));
                value["minVelocity"] = Number(Get(component, "minVelocity"));
            }

            return value;
        }

        private static GameObject Avatar(JObject arguments)
        {
            return UnityObjectReference.ResolveGameObject(
                (string)arguments["avatarRef"]);
        }

        private static void EnsureEditableChild(GameObject avatar, GameObject child)
        {
            if (EditorUtility.IsPersistent(avatar))
            {
                throw new McpToolException(
                    "prefab_asset_not_editable",
                    "Open the avatar prefab in Prefab Mode or place it in a scene before applying changes.");
            }

            if (child != avatar && !child.transform.IsChildOf(avatar.transform))
            {
                throw new McpToolException(
                    "object_outside_avatar",
                    "parentRef must resolve to the avatar root or one of its descendants.");
            }
        }

        private static string RequiredName(JObject arguments)
        {
            var value = ((string)arguments["contactName"])?.Trim();
            if (string.IsNullOrWhiteSpace(value) || value.Contains("/") || value.Contains("\\"))
            {
                throw new McpToolException(
                    "invalid_contact_name",
                    "contactName must be non-empty and must not contain a path separator.");
            }

            return value;
        }

        private static string RequiredEnum(
            JObject arguments,
            string property,
            params string[] values)
        {
            var value = ((string)arguments[property])?.Trim();
            var match = values.FirstOrDefault(candidate => string.Equals(
                candidate,
                value,
                StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                throw new McpToolException(
                    "invalid_" + property,
                    property + " must be one of: " + string.Join(", ", values) + ".");
            }

            return match;
        }

        private static IReadOnlyList<string> StringArray(
            JObject arguments,
            string property)
        {
            return (arguments[property] as JArray)?.Values<string>()
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
        }

        private static Vector3 Vector(JObject value)
        {
            if (value == null)
            {
                throw new McpToolException(
                    "vector_required",
                    "position and rotationEuler must contain x, y, and z.");
            }

            return new Vector3(
                (float?)value["x"] ?? 0f,
                (float?)value["y"] ?? 0f,
                (float?)value["z"] ?? 0f);
        }

        private static JObject VectorValue(Vector3 value)
        {
            return new JObject
            {
                ["x"] = value.x,
                ["y"] = value.y,
                ["z"] = value.z
            };
        }

        private static Type RequireComponentType(string fullName)
        {
            var type = FindComponentType(fullName);
            if (type == null)
            {
                throw new McpToolException(
                    "vrchat_sdk_unavailable",
                    "Required VRChat SDK component was not found: " + fullName);
            }

            return type;
        }

        private static Type FindComponentType(string fullName)
        {
            return TypeCache.GetTypesDerivedFrom<Component>()
                .FirstOrDefault(type => string.Equals(
                    type.FullName,
                    fullName,
                    StringComparison.Ordinal));
        }

        private static Transform DirectChild(Transform root, string name)
        {
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static object Get(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            var member = Member(target.GetType(), name);
            return member is FieldInfo field
                ? field.GetValue(target)
                : (member as PropertyInfo)?.GetValue(target, null);
        }

        private static void SetRequired(object target, string name, object value)
        {
            if (!TrySet(target, name, value))
            {
                throw ApiMismatch(target, name);
            }
        }

        private static void SetOptional(object target, string name, object value)
        {
            TrySet(target, name, value);
        }

        private static bool TrySet(object target, string name, object value)
        {
            var member = Member(target.GetType(), name);
            if (member is FieldInfo field)
            {
                field.SetValue(target, value);
                return true;
            }

            if (member is PropertyInfo property && property.CanWrite)
            {
                property.SetValue(target, value, null);
                return true;
            }

            return false;
        }

        private static void SetEnum(object target, string name, string value)
        {
            var member = Member(target.GetType(), name);
            var type = member is FieldInfo field
                ? field.FieldType
                : (member as PropertyInfo)?.PropertyType;
            if (type == null || !type.IsEnum)
            {
                throw ApiMismatch(target, name);
            }

            try
            {
                SetRequired(target, name, Enum.Parse(type, value, true));
            }
            catch (ArgumentException)
            {
                throw new McpToolException(
                    "vrchat_sdk_api_mismatch",
                    target.GetType().FullName + "." + name +
                    " does not support value " + value + ".");
            }
        }

        private static void SetStringList(
            object target,
            string name,
            IReadOnlyList<string> values)
        {
            var member = Member(target.GetType(), name);
            var type = member is FieldInfo field
                ? field.FieldType
                : (member as PropertyInfo)?.PropertyType;
            if (type == null || !typeof(IList).IsAssignableFrom(type))
            {
                throw ApiMismatch(target, name);
            }

            var list = (IList)Activator.CreateInstance(type);
            foreach (var value in values)
            {
                list.Add(value);
            }

            SetRequired(target, name, list);
        }

        private static MemberInfo Member(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance |
                                       BindingFlags.Public |
                                       BindingFlags.NonPublic;
            return type.GetField(name, flags) as MemberInfo ??
                   type.GetProperty(name, flags);
        }

        private static IEnumerable<string> StringValues(object value)
        {
            return value is IEnumerable values
                ? values.Cast<object>()
                    .Select(entry => entry?.ToString())
                    .Where(entry => !string.IsNullOrEmpty(entry))
                : Enumerable.Empty<string>();
        }

        private static JToken Number(object value)
        {
            return value == null
                ? JValue.CreateNull()
                : JToken.FromObject(Convert.ToSingle(value));
        }

        private static JToken Boolean(object value)
        {
            return value == null
                ? JValue.CreateNull()
                : JToken.FromObject(Convert.ToBoolean(value));
        }

        private static McpToolException ApiMismatch(object target, string name)
        {
            return new McpToolException(
                "vrchat_sdk_api_mismatch",
                target.GetType().FullName +
                " does not expose the expected member " + name + ".");
        }
    }
}
