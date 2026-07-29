using Ee4v.Testing.Contracts;

[assembly: FeatureTestSuite(
    "AssetManager Infrastructure",
    "AssetManager",
    "Ee4v.AssetManager.Infrastructure.Tests.Editor",
    "AssetManager Infrastructure の DB schema、file adapter、datasource sync を確認します。",
    order: 300)]
