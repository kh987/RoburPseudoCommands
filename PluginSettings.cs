using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace RoburPseudoCommands
{
    internal static class PluginSettings
    {
        private static readonly object SyncRoot = new object();

        public static string SettingsPath
        {
            get
            {
                return Path.Combine(GetSettingsDirectory(), "settings.json");
            }
        }

        public static bool IsLogEnabled()
        {
            return Load().LogEnabled;
        }

        public static void SetLogEnabled(bool enabled)
        {
            var settings = Load();
            settings.LogEnabled = enabled;
            Save(settings);
        }

        private static PluginSettingsData Load()
        {
            lock (SyncRoot)
            {
                try
                {
                    var path = SettingsPath;
                    if (!File.Exists(path))
                        return new PluginSettingsData();

                    var serializer = new DataContractJsonSerializer(typeof(PluginSettingsData));
                    using (var stream = File.OpenRead(path))
                    {
                        var settings = (PluginSettingsData)serializer.ReadObject(stream);
                        return settings ?? new PluginSettingsData();
                    }
                }
                catch
                {
                    return new PluginSettingsData();
                }
            }
        }

        private static void Save(PluginSettingsData settings)
        {
            lock (SyncRoot)
            {
                var directory = GetSettingsDirectory();
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                settings = settings ?? new PluginSettingsData();
                var text = new StringBuilder();
                text.AppendLine("{");
                text.Append("  \"logEnabled\": ");
                text.Append(settings.LogEnabled ? "true" : "false");
                text.AppendLine();
                text.AppendLine("}");
                File.WriteAllText(SettingsPath, text.ToString(), new UTF8Encoding(false));
            }
        }

        private static string GetSettingsDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
                return AppDomain.CurrentDomain.BaseDirectory;

            return Path.Combine(appData, "Topomatic", "RoburPseudoCommands");
        }
    }

    [DataContract]
    internal sealed class PluginSettingsData
    {
        [DataMember(Name = "logEnabled")]
        public bool LogEnabled { get; set; }
    }
}
