using System;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.Ndmf;
using UnityEditor.Animations;
using UnityEngine;

namespace Ee4v.ExpressionMenu
{
    internal static class ExpressionMenuParameterCatalog
    {
        internal sealed class Entry
        {
            internal string Name;
            internal AnimatorControllerParameterType Type;
            internal bool Expression;
            internal AnimatorControllerParameter Declaration;
        }

        internal static bool IsBuiltIn(string name) => NdmfIntegration.IsBuiltInParameter(name);

        internal static Entry Find(AvatarEditingContext context, AnimatorController ignore, string name) =>
            Entries(context, ignore).FirstOrDefault(entry => entry.Name == name);

        internal static Entry[] Entries(AvatarEditingContext context, AnimatorController ignore) =>
            context.Root == null ? Array.Empty<Entry>() :
                NdmfIntegration.GetParameters(context.Root, ignore)
                    .Select(entry => new Entry
                    {
                        Name = entry.Name, Type = entry.Type, Expression = entry.Expression,
                        Declaration = entry.Declaration
                    }).ToArray();
    }
}
