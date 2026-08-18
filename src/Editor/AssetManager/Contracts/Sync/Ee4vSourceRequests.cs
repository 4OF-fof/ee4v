using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Contracts
{
    public sealed class Ee4vSyncRequest
    {
        public Ee4vSyncRequest(string libraryPath)
        {
            LibraryPath = libraryPath;
        }

        public string LibraryPath { get; }
    }

    public sealed class ImportEe4vFileRequest
    {
        public ImportEe4vFileRequest(
            string libraryPath,
            string filePath,
            string name = null,
            string description = null,
            IReadOnlyList<string> tags = null)
        {
            LibraryPath = libraryPath;
            FilePath = filePath;
            Name = name;
            Description = description;
            Tags = tags ?? Array.Empty<string>();
        }

        public string LibraryPath { get; }
        public string FilePath { get; }
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<string> Tags { get; }
    }
}
