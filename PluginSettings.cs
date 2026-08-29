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
        private static PluginSettingsData _settings;

        public static string SettingsPath
        {
            get
            {
                return Path.Combine(GetSettingsDirectory(), "settings.json");
            }
        }

        public static bool IsLogEnabled()
        {
            lock (SyncRoot)
            {
                return GetSettings().LogEnabled;
            }
        }

        public static void SetLogEnabled(bool enabled)
        {
            lock (SyncRoot)
            {
                var settings = GetSettings();
                settings.LogEnabled = enabled;
                Save(settings);
            }
        }

        public static bool IsQuickInputEnabled()
        {
            lock (SyncRoot)
            {
                return GetSettings().QuickInputEnabled;
            }
        }

        public static bool IsSpaceActsAsEnterEnabled()
        {
            lock (SyncRoot)
            {
                return GetSettings().SpaceActsAsEnter;
            }
        }

        public static void SetKeyboardInputOptions(bool quickInputEnabled, bool spaceActsAsEnter)
        {
            lock (SyncRoot)
            {
                var settings = GetSettings();
                settings.QuickInputEnabled = quickInputEnabled;
                settings.SpaceActsAsEnter = spaceActsAsEnter;
                Save(settings);
            }
        }

        private static PluginSettingsData GetSettings()
        {
            if (_settings != null)
                return _settings;

            try
            {
                var path = SettingsPath;
                if (!File.Exists(path))
                {
                    _settings = new PluginSettingsData();
                    return _settings;
                }

                var serializer = new DataContractJsonSerializer(typeof(PluginSettingsData));
                using (var stream = File.OpenRead(path))
                {
                    _settings = (PluginSettingsData)serializer.ReadObject(stream) ?? new PluginSettingsData();
                }
            }
            catch
            {
                _settings = PluginSettingsData.CreateFailSafe();
            }

            return _settings;
        }

        private static void Save(PluginSettingsData settings)
        {
            var directory = GetSettingsDirectory();
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            settings = settings ?? new PluginSettingsData();
            var text = new StringBuilder();
            text.AppendLine("{");
            text.Append("  \"logEnabled\": ");
            text.Append(settings.LogEnabled ? "true" : "false");
            text.AppendLine(",");
            text.Append("  \"quickInputEnabled\": ");
            text.Append(settings.QuickInputEnabled ? "true" : "false");
            text.AppendLine(",");
            text.Append("  \"spaceActsAsEnter\": ");
            text.Append(settings.SpaceActsAsEnter ? "true" : "false");
            text.AppendLine();
            text.AppendLine("}");
            AtomicFileWriter.WriteAllText(SettingsPath, text.ToString(), new UTF8Encoding(false));
            _settings = settings;
        }

        private static string GetSettingsDirectory()
        {
            return UserDataPaths.GetPluginDirectory();
        }
    }

    [DataContract]
    internal sealed class PluginSettingsData
    {
        private bool? _quickInputEnabled;
        private bool? _spaceActsAsEnter;

        [DataMember(Name = "logEnabled")]
        public bool LogEnabled { get; set; }

        [DataMember(Name = "quickInputEnabled", EmitDefaultValue = false)]
        private bool? QuickInputEnabledValue
        {
            get { return _quickInputEnabled; }
            set { _quickInputEnabled = value; }
        }

        [DataMember(Name = "spaceActsAsEnter", EmitDefaultValue = false)]
        private bool? SpaceActsAsEnterValue
        {
            get { return _spaceActsAsEnter; }
            set { _spaceActsAsEnter = value; }
        }

        [IgnoreDataMember]
        public bool QuickInputEnabled
        {
            get { return _quickInputEnabled ?? true; }
            set { _quickInputEnabled = value; }
        }

        [IgnoreDataMember]
        public bool SpaceActsAsEnter
        {
            get { return _spaceActsAsEnter ?? true; }
            set { _spaceActsAsEnter = value; }
        }

        public static PluginSettingsData CreateFailSafe()
        {
            return new PluginSettingsData
            {
                QuickInputEnabled = false,
                SpaceActsAsEnter = false
            };
        }
    }
}
