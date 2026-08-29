using System;
using System.IO;
using System.Text;

namespace RoburPseudoCommands
{
    internal static class AtomicFileWriter
    {
        public static void WriteAllText(string path, string contents, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Output path is empty.", "path");
            if (encoding == null)
                throw new ArgumentNullException("encoding");

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var tempPath = Path.Combine(
                directory,
                "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                File.WriteAllText(tempPath, contents ?? string.Empty, encoding);

                if (File.Exists(fullPath))
                    File.Replace(tempPath, fullPath, fullPath + ".bak", true);
                else
                    File.Move(tempPath, fullPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
