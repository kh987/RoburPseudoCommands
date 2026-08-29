using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace RoburPseudoCommands
{
    internal sealed class AliasStore
    {
        private Dictionary<string, AliasEntry> _aliases;
        private bool _loaded;

        public AliasStore()
        {
            _aliases = new Dictionary<string, AliasEntry>(StringComparer.OrdinalIgnoreCase);
        }

        public string ActivePath { get; private set; }

        public IDictionary<string, AliasEntry> Aliases
        {
            get { return _aliases; }
        }

        public void EnsureLoaded()
        {
            if (_loaded)
                return;

            Reload();
        }

        public int Reload()
        {
            var aliases = new Dictionary<string, AliasEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in LoadEntries())
            {
                if (entry == null)
                    continue;

                entry.Normalize();
                if (string.IsNullOrEmpty(entry.Alias) || string.IsNullOrEmpty(entry.Command))
                    continue;

                aliases[entry.Alias] = entry;
            }

            _aliases = aliases;
            _loaded = true;
            ActivePath = GetConfigPath();
            return _aliases.Count;
        }

        public static string GetConfigPath()
        {
            return EnsureConfigFile();
        }

        public static List<AliasEntry> LoadEntries()
        {
            var path = EnsureConfigFile();
            return LoadEntriesFromFile(path);
        }

        public static List<AliasEntry> LoadEntriesFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Import path is empty.", "path");

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(AliasConfig));

                AliasConfig config;
                using (var stream = File.OpenRead(path))
                {
                    config = (AliasConfig)serializer.ReadObject(stream);
                }

                if (config == null || config.Aliases == null)
                    return new List<AliasEntry>();

                foreach (var entry in config.Aliases)
                {
                    if (entry != null)
                        entry.Normalize();
                }

                return config.Aliases;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    "Не удалось прочитать файл псевдокоманд: " + path + Environment.NewLine +
                    "Файл оставлен без изменений. Резервная копия, если она создавалась: " + path + ".bak",
                    ex);
            }
        }

        public static void SaveEntries(IEnumerable<AliasEntry> entries)
        {
            var path = EnsureConfigFile();
            WriteEntries(path, entries);
        }

        public static void ExportEntries(string path, IEnumerable<AliasEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Export path is empty.", "path");

            WriteEntries(path, entries);
        }

        private static void WriteEntries(string path, IEnumerable<AliasEntry> entries)
        {
            var config = new AliasConfig
            {
                Aliases = new List<AliasEntry>()
            };

            foreach (var entry in entries)
            {
                if (entry == null)
                    continue;

                entry.Normalize();
                config.Aliases.Add(entry);
            }

            AtomicFileWriter.WriteAllText(path, FormatConfig(config), new UTF8Encoding(false));
        }

        private static string FormatConfig(AliasConfig config)
        {
            var text = new StringBuilder();
            var aliases = config == null || config.Aliases == null
                ? new List<AliasEntry>()
                : config.Aliases;

            text.AppendLine("{");
            text.AppendLine("  \"aliases\": [");

            for (var i = 0; i < aliases.Count; i++)
            {
                var entry = aliases[i] ?? new AliasEntry();
                entry.Normalize();

                text.AppendLine("    {");
                AppendJsonProperty(text, "alias", entry.Alias, true);
                AppendJsonProperty(text, "action", entry.Action, true);
                AppendJsonProperty(text, "command", entry.Command, true);
                AppendJsonArrayProperty(text, "args", entry.Args, true);
                AppendJsonProperty(text, "description", entry.Description, false);
                text.Append("    }");

                if (i + 1 < aliases.Count)
                    text.Append(",");

                text.AppendLine();
            }

            text.AppendLine("  ]");
            text.AppendLine("}");
            return text.ToString();
        }

        private static void AppendJsonProperty(StringBuilder text, string name, string value, bool comma)
        {
            text
                .Append("      \"")
                .Append(EscapeJsonString(name))
                .Append("\": \"")
                .Append(EscapeJsonString(value ?? string.Empty))
                .Append("\"");

            if (comma)
                text.Append(",");

            text.AppendLine();
        }

        private static void AppendJsonArrayProperty(StringBuilder text, string name, IList<string> values, bool comma)
        {
            values = values ?? new List<string>();

            text
                .Append("      \"")
                .Append(EscapeJsonString(name))
                .Append("\": ");

            if (values.Count == 0)
            {
                text.Append("[]");
            }
            else
            {
                text.AppendLine("[");
                for (var i = 0; i < values.Count; i++)
                {
                    text
                        .Append("        \"")
                        .Append(EscapeJsonString(values[i] ?? string.Empty))
                        .Append("\"");

                    if (i + 1 < values.Count)
                        text.Append(",");

                    text.AppendLine();
                }

                text.Append("      ]");
            }

            if (comma)
                text.Append(",");

            text.AppendLine();
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var text = new StringBuilder();
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\':
                        text.Append("\\\\");
                        break;
                    case '"':
                        text.Append("\\\"");
                        break;
                    case '\b':
                        text.Append("\\b");
                        break;
                    case '\f':
                        text.Append("\\f");
                        break;
                    case '\n':
                        text.Append("\\n");
                        break;
                    case '\r':
                        text.Append("\\r");
                        break;
                    case '\t':
                        text.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(ch))
                            text.Append("\\u").Append(((int)ch).ToString("x4"));
                        else
                            text.Append(ch);
                        break;
                }
            }

            return text.ToString();
        }

        private static string EnsureConfigFile()
        {
            var path = GetUserConfigPath();
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(path))
            {
                var bundledPath = Path.Combine(GetAssemblyDirectory(), "aliases.json");
                if (File.Exists(bundledPath))
                    AtomicFileWriter.WriteAllText(
                        path,
                        File.ReadAllText(bundledPath, Encoding.UTF8),
                        new UTF8Encoding(false));
                else
                    AtomicFileWriter.WriteAllText(path, DefaultAliasesJson, new UTF8Encoding(false));
            }

            return path;
        }

        private static string GetUserConfigPath()
        {
            return Path.Combine(UserDataPaths.GetPluginDirectory(), "aliases.json");
        }

        private static string GetAssemblyDirectory()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(assemblyPath);
        }

        private const string DefaultAliasesJson =
@"{
  ""aliases"": [
    {
      ""alias"": ""L"",
      ""action"": ""core.id_line"",
      ""command"": ""line"",
      ""args"": [],
      ""description"": ""Example: replace 'line' with the Robur command name you need""
    },
    {
      ""alias"": ""PL"",
      ""action"": ""core.id_pline"",
      ""command"": ""polyline"",
      ""args"": [],
      ""description"": ""Example alias""
    }
  ]
}";
    }

    [DataContract]
    internal sealed class AliasConfig
    {
        [DataMember(Name = "aliases")]
        public List<AliasEntry> Aliases { get; set; }
    }

    [DataContract]
    internal sealed class AliasEntry
    {
        [DataMember(Name = "alias")]
        public string Alias { get; set; }

        [DataMember(Name = "action")]
        public string Action { get; set; }

        [DataMember(Name = "command")]
        public string Command { get; set; }

        [DataMember(Name = "args")]
        public List<string> Args { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        public void Normalize()
        {
            Alias = (Alias ?? string.Empty).Trim();
            Action = (Action ?? string.Empty).Trim();
            Command = (Command ?? string.Empty).Trim();
            Description = (Description ?? string.Empty).Trim();

            if (Args == null)
                Args = new List<string>();

            for (var i = Args.Count - 1; i >= 0; i--)
            {
                if (Args[i] == null)
                    Args.RemoveAt(i);
                else
                    Args[i] = Args[i].Trim();
            }
        }
    }
}
