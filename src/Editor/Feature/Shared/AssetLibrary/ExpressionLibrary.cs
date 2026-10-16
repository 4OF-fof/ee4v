using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ee4v.AssetLibrary
{
    public sealed class ExpressionTarget
    {
        public string FbxGuid { get; set; }
        public string FbxName { get; set; }
        public long MeshLocalId { get; set; }
        public string RendererPath { get; set; }
        public string ShapeName { get; set; }
    }

    public sealed class ExpressionAsset
    {
        public string Guid { get; set; }
        public string Path { get; set; }
        public string Hash { get; set; }
        public string MetaHash { get; set; }
    }

    public sealed class ExpressionDependency
    {
        public string DependentGuid { get; set; }
        public string AssetGuid { get; set; }
        public string SourceType { get; set; }
        public string SourceId { get; set; }
        public string[] TargetPaths { get; set; } = Array.Empty<string>();
    }

    public sealed class ExpressionLibraryEntry
    {
        public int SchemaVersion { get; set; } = 1;
        public string Id { get; set; }
        public string Name { get; set; }
        public string Folder { get; set; } = string.Empty;
        public string RootGuid { get; set; }
        public string PrimaryFbxGuid { get; set; }
        public string Revision { get; set; }
        public string ContentDirectory { get; set; }
        public string PreviewHash { get; set; }
        public ExpressionTarget[] Targets { get; set; } = Array.Empty<ExpressionTarget>();
        public ExpressionAsset[] Assets { get; set; } = Array.Empty<ExpressionAsset>();
        public ExpressionDependency[] Dependencies { get; set; } = Array.Empty<ExpressionDependency>();
    }

    public sealed class ExpressionPublishRequest
    {
        public string AssetPath { get; set; }
        public string Name { get; set; }
        public string Folder { get; set; }
        public string PrimaryFbxGuid { get; set; }
        public ExpressionTarget[] Targets { get; set; }
        public byte[] Preview { get; set; }
        public string ExpectedRevision { get; set; }
    }

    public interface IExpressionLibrary
    {
        event Action Changed;
        void Synchronize();
        IReadOnlyList<ExpressionLibraryEntry> List(string primaryFbxGuid = null);
        ExpressionLibraryEntry FindByRootGuid(string guid);
        string GetWorkingRevision(string guid);
        ExpressionLibraryEntry Publish(ExpressionPublishRequest request);
        Task Delete(string id, string expectedRevision, CancellationToken cancellationToken = default);
        Task<string> EnsureImported(string id, string expectedRevision, string destinationFolder,
            CancellationToken cancellationToken = default);
        string GetPreviewPath(string id);
    }

    public static class ExpressionLibrary
    {
        private static Func<IExpressionLibrary> _factory;
        public static void Register(Func<IExpressionLibrary> factory) =>
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        public static IExpressionLibrary Open() => _factory?.Invoke() ??
            throw new InvalidOperationException("The AssetManager expression library service is unavailable.");
    }
}
