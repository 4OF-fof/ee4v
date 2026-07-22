namespace Ee4v.AssetManager
{
    internal sealed class FileTreeDetailState
    {
        public FileTreeDetailState(string id, string name, string parentName = null)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            ParentName = parentName ?? string.Empty;
        }

        public string Id { get; }

        public string Name { get; }

        public string ParentName { get; }
    }
}
