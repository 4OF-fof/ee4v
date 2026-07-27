using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Domain
{
    internal enum CatalogRuleError
    {
        RequiredValue,
        SelfDependency,
        UnsupportedDependencyTarget,
        SmartConditionRequired,
        SmartConditionQueryRequired
    }

    internal sealed class CatalogRuleException : Exception
    {
        internal CatalogRuleException(CatalogRuleError error, string field = null)
        {
            Error = error;
            Field = field ?? string.Empty;
        }

        internal CatalogRuleError Error { get; }
        internal string Field { get; }
    }

    internal static class CatalogCommandPolicy
    {
        internal static void Require(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new CatalogRuleException(CatalogRuleError.RequiredValue, field);
            }
        }

        internal static void EnsureNoSelfDependency(
            string sourceId,
            IReadOnlyList<string> targetIds)
        {
            if (targetIds == null)
            {
                return;
            }

            for (var i = 0; i < targetIds.Count; i++)
            {
                if (string.Equals(sourceId, targetIds[i], StringComparison.Ordinal))
                {
                    throw new CatalogRuleException(CatalogRuleError.SelfDependency);
                }
            }
        }

        internal static void EnsureNoSelfDependency(
            int sourceType,
            string sourceId,
            IReadOnlyList<CatalogDependencyTarget> targets)
        {
            if (targets == null)
            {
                return;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null)
                {
                    throw new CatalogRuleException(
                        CatalogRuleError.RequiredValue,
                        "dependency target");
                }

                if (targets[i].Type == 2)
                {
                    throw new CatalogRuleException(
                        CatalogRuleError.UnsupportedDependencyTarget);
                }

                Require(targets[i].Id, "dependency target id");
                if (sourceType == targets[i].Type &&
                    string.Equals(sourceId, targets[i].Id, StringComparison.Ordinal))
                {
                    throw new CatalogRuleException(CatalogRuleError.SelfDependency);
                }
            }
        }

        internal static void EnsureSmartConditions(
            IReadOnlyList<string> conditionQueries,
            bool hasNullCondition)
        {
            if (hasNullCondition ||
                conditionQueries == null ||
                conditionQueries.Count == 0)
            {
                throw new CatalogRuleException(CatalogRuleError.SmartConditionRequired);
            }

            for (var i = 0; i < conditionQueries.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(conditionQueries[i]))
                {
                    throw new CatalogRuleException(
                        CatalogRuleError.SmartConditionQueryRequired);
                }
            }
        }
    }

    internal sealed class CatalogDependencyTarget
    {
        internal CatalogDependencyTarget(int type, string id)
        {
            Type = type;
            Id = id ?? string.Empty;
        }

        internal int Type { get; }
        internal string Id { get; }
    }
}
