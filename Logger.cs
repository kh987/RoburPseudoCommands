using System;
using System.IO;
using System.Text;

namespace RoburPseudoCommands
{
    internal static class Logger
    {
        private static readonly object SyncRoot = new object();

        public static string LogPath
        {
            get
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(appData))
                    appData = AppDomain.CurrentDomain.BaseDirectory;

                return Path.Combine(appData, "Topomatic", "RoburPseudoCommands", "RoburPseudoCommands.log");
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

                    File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
                }
            }
            catch
            {
            }
        }
    }
}
