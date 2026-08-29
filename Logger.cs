using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RoburPseudoCommands
{
    internal static class Logger
    {
        private static readonly object SyncRoot = new object();
        private const long MaxLogBytes = 5L * 1024L * 1024L;
        private const int TailReadBytes = 64 * 1024;

        public static string LogPath
        {
            get
            {
                return Path.Combine(UserDataPaths.GetPluginDirectory(), "RoburPseudoCommands.log");
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        public static IList<string> ReadTailLines(int maxLines)
        {
            if (maxLines <= 0)
                throw new ArgumentOutOfRangeException("maxLines");

            lock (SyncRoot)
            {
                var path = LogPath;
                if (!File.Exists(path))
                    return new string[0];

                var lines = new Queue<string>(maxLines);
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var start = Math.Max(0L, stream.Length - TailReadBytes);
                    stream.Seek(start, SeekOrigin.Begin);

                    using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        if (start > 0)
                            reader.ReadLine();

                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (lines.Count == maxLines)
                                lines.Dequeue();

                            lines.Enqueue(line);
                        }
                    }
                }

                return lines.ToArray();
            }
        }

        private static void Write(string level, string message, Exception exception)
        {
            if (!PluginSettings.IsLogEnabled())
                return;

            try
            {
                lock (SyncRoot)
                {
                    var path = LogPath;
                    var directory = Path.GetDirectoryName(path);
                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    var sb = new StringBuilder();
                    sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    sb.Append(" [");
                    sb.Append(level);
                    sb.Append("] ");
                    sb.AppendLine(message ?? string.Empty);

                    if (exception != null)
                        sb.AppendLine(exception.ToString());

                    var text = sb.ToString();
                    RotateIfNeeded(path, Encoding.UTF8.GetByteCount(text));
                    File.AppendAllText(path, text, new UTF8Encoding(false));
                }
            }
            catch
            {
            }
        }

        private static void RotateIfNeeded(string path, int pendingBytes)
        {
            if (!File.Exists(path))
                return;

            var length = new FileInfo(path).Length;
            if (length == 0 || length + pendingBytes <= MaxLogBytes)
                return;

            var backupPath = path + ".1";
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            File.Move(path, backupPath);
        }
    }
}
