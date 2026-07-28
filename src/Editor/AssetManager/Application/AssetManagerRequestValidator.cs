using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Domain;

namespace Ee4v.AssetManager.Application
{
    internal static class AssetManagerRequestValidator
    {
        internal static void Require(string value, string field)
        {
            Execute(() => CatalogCommandPolicy.Require(value, field));
        }

        internal static T RequireRequest<T>(T request, string requestName)
            where T : class
        {
            if (request == null)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.InvalidRequest,
                    requestName + " is required.");
            }

            return request;
        }

        internal static void ValidateFileDependencies(
            string dependentFileId,
            IReadOnlyList<string> dependencyFileIds)
        {
            Execute(() =>
            {
                CatalogCommandPolicy.Require(dependentFileId, "dependent file id");
                CatalogCommandPolicy.EnsureNoSelfDependency(
                    dependentFileId,
                    dependencyFileIds);
            });
        }

        internal static void ValidateDependencies(
            DependencyEndpointRequest source,
            IReadOnlyList<DependencyEndpointRequest> targets)
        {
            RequireRequest(source, "Dependency source");
            Execute(() =>
            {
                CatalogCommandPolicy.Require(source.Id, "dependency source id");
                CatalogCommandPolicy.EnsureNoSelfDependency(
                    (int)source.Type,
                    source.Id,
                    targets == null
                        ? Array.Empty<CatalogDependencyTarget>()
                        : targets
                            .Select(target => target == null
                                ? null
                                : new CatalogDependencyTarget(
                                    (int)target.Type,
                                    target.Id))
                            .ToArray());
            });
        }

        internal static void ValidateSmartCollection(
            CreateSmartCollectionRequest request)
        {
            RequireRequest(request, "Smart Collection request");
            Execute(() =>
            {
                CatalogCommandPolicy.Require(request.Name, "smart collection name");
                CatalogCommandPolicy.EnsureCollectionIcon(
                    (int)request.Icon,
                    (int)AssetCollectionIcon.Search);
                CatalogCommandPolicy.EnsureSmartConditions(
                    request.Conditions == null
                        ? Array.Empty<string>()
                        : request.Conditions
                            .Where(condition => condition != null)
                            .Select(condition =>
                                condition.Operator == SmartCollectionConditionOperator.Exists
                                    ? "exists"
                                    : condition.QueryText)
                            .ToArray(),
                    request.Conditions != null &&
                    request.Conditions.Any(condition => condition == null));
            });
        }

        internal static void ValidateCollection(CreateCollectionRequest request)
        {
            RequireRequest(request, "Create collection request");
            Execute(() =>
            {
                CatalogCommandPolicy.Require(request.Name, "collection name");
            });
        }

        private static void Execute(Action action)
        {
            try
            {
                action();
            }
            catch (CatalogRuleException exception)
            {
                throw new AssetManagerException(
                    GetErrorCode(exception.Error),
                    GetMessage(exception),
                    exception);
            }
        }

        private static AssetManagerErrorCode GetErrorCode(CatalogRuleError error)
        {
            return error == CatalogRuleError.SmartConditionRequired ||
                   error == CatalogRuleError.SmartConditionQueryRequired
                ? AssetManagerErrorCode.InvalidSmartCollectionCondition
                : AssetManagerErrorCode.InvalidRequest;
        }

        private static string GetMessage(CatalogRuleException exception)
        {
            switch (exception.Error)
            {
                case CatalogRuleError.RequiredValue:
                    return exception.Field + " is required.";
                case CatalogRuleError.SelfDependency:
                    return "Self dependency is not allowed.";
                case CatalogRuleError.UnsupportedDependencyTarget:
                    return "Variant group cannot be a dependency target.";
                case CatalogRuleError.UnsupportedCollectionIcon:
                    return "Collection icon is not supported.";
                case CatalogRuleError.SmartConditionRequired:
                    return "Smart Collection condition is required.";
                case CatalogRuleError.SmartConditionQueryRequired:
                    return "Smart Collection condition query text is required.";
                default:
                    return "The request violates an AssetManager domain rule.";
            }
        }
    }
}
