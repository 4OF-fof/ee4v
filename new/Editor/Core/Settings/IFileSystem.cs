namespace Ee4v.Core.Settings
{
    internal interface IFileSystem
    {
        bool FileExists(string path);

        string ReadAllText(string path);

        void WriteAllText(string path, string content);

        bool DirectoryExists(string path);

        void CreateDirectory(string path);
    }
}
