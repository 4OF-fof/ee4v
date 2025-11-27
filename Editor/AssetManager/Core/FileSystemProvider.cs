using System.Collections.Generic;
using System.IO;

namespace _4OF.ee4v.AssetManager.Core {
    public class FileSystemProvider {
        public static void CreateDirectory(string path) {
            Directory.CreateDirectory(path);
        }

        public static bool DirectoryExists(string path) {
            return Directory.Exists(path);
        }

        public static void DeleteDirectory(string path, bool recursive) {
            Directory.Delete(path, recursive);
        }

        public static IEnumerable<string> GetDirectories(string path) {
            return Directory.GetDirectories(path);
        }

        public IEnumerable<string> GetFiles(string path) {
            return Directory.GetFiles(path);
        }

        public static bool FileExists(string path) {
            return File.Exists(path);
        }

        public static string ReadAllText(string path) {
            return File.ReadAllText(path);
        }

        public static void WriteAllText(string path, string contents) {
            File.WriteAllText(path, contents);
        }

        public static void CopyFile(string sourceFileName, string destFileName, bool overwrite = false) {
            File.Copy(sourceFileName, destFileName, overwrite);
        }

        public static void MoveFile(string sourceFileName, string destFileName) {
            File.Move(sourceFileName, destFileName);
        }

        public static void DeleteFile(string path) {
            File.Delete(path);
        }

        public static byte[] ReadAllBytes(string path) {
            return File.ReadAllBytes(path);
        }

        public static void WriteAllBytes(string path, byte[] bytes) {
            File.WriteAllBytes(path, bytes);
        }
    }
}