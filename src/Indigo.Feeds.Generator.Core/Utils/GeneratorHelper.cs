using System.IO;

namespace Indigo.Feeds.Generator.Core.Utils
{
    public static class GeneratorHelper
    {
        public static string GetFilePath(string fileName, string path, string fileNameFormat)
        {
            return Path.Combine(path, string.Format(fileNameFormat, fileName));
        }

        public static string GetFilePathFormatted(string path, string fileNameFormat, params string[] fileNameReplacements)
        {
            return Path.Combine(path, string.Format(fileNameFormat, fileNameReplacements));
        }

        public static string GetFilePath(string identifier, int batchNumber, string path, string fileNameFormat)
        {
            return GetFilePath(string.Format("{0}_{1}", identifier, batchNumber), path, fileNameFormat);
        }
    }
}
