using Ee4v.Testing.Contracts;

[assembly: FeatureTestSuite(
    "AssetManager Composition",
    "AssetManager",
    "Ee4v.AssetManager.Composition.Tests.Editor",
    "AssetManager の Unity adapter と main thread 境界を確認します。",
    order: 390)]
