using System.IO;
namespace Shion.SDK.Editor
{
    public static class FileSystemUtility
    {
        public static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
        public static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        public static bool Exists(string path)
        {
            return Directory.Exists(path);
        }
    }
}