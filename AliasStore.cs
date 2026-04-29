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

        public static void SaveEntries(IEnumerable<AliasEntry> entries)
        {
            var path = EnsureConfigFile();
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

            var serializer = new DataContractJsonSerializer(typeof(AliasConfig));
            using (var stream = File.Create(path))
            {
                serializer.WriteObject(stream, config);
            }
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
                    File.Copy(bundledPath, path);
                else
                    File.WriteAllText(path, DefaultAliasesJson, new UTF8Encoding(false));
            }

            return path;
        }

        private static string GetUserConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
                return Path.Combine(GetAssemblyDirectory(), "aliases.json");

            return Path.Combine(appData, "Topomatic", "RoburPseudoCommands", "aliases.json");
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
    },
    {
      ""alias"": ""PGA"",
      ""command"": ""pseudo_show_aliases"",
      ""args"": [],
      ""description"": ""Show aliases from this plugin""
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
