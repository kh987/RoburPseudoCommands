using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RoburPseudoCommands
{
    internal static class ActionResolver
    {
        private static readonly object SyncRoot = new object();
        private static Dictionary<string, string> _actionsByCommand;

        public static string Resolve(string command)
        {
            command = (command ?? string.Empty).Trim();
            if (command.Length == 0)
                return string.Empty;

            EnsureLoaded();

            string action;
            if (_actionsByCommand.TryGetValue(command, out action))
                return action;

            return string.Empty;
        }

        private static void EnsureLoaded()
        {
            if (_actionsByCommand != null)
                return;

            lock (SyncRoot)
            {
                if (_actionsByCommand != null)
                    return;

                _actionsByCommand = LoadActionsByCommand();
                Logger.Info("action resolver loaded commandActionsCount=" + _actionsByCommand.Count);
            }
        }

        private static Dictionary<string, string> LoadActionsByCommand()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return result;

            foreach (var pluginPath in Directory.GetFiles(directory, "*.plugin"))
            {
                try
                {
                    LoadPluginActions(pluginPath, result);
                }
                catch (Exception ex)
                {
                    Logger.Error("failed to scan actions in '" + pluginPath + "'", ex);
                }
            }

            return result;
        }

        private static void LoadPluginActions(string pluginPath, IDictionary<string, string> result)
        {
            var prefix = Path.GetFileNameWithoutExtension(pluginPath);
            var text = File.ReadAllText(pluginPath);

            foreach (Match match in Regex.Matches(
                text,
                "\"(?<id>[^\"]+)\"\\s*:\\s*\\{(?<body>.*?)\\}",
                RegexOptions.Singleline))
            {
                var id = match.Groups["id"].Value;
                var body = match.Groups["body"].Value;
                var commandMatch = Regex.Match(
                    body,
                    "\"cmd\"\\s*:\\s*\"(?<cmd>(?:\\\\.|[^\"])*)\"",
                    RegexOptions.Singleline);

                if (!commandMatch.Success)
                    continue;

                var command = UnescapeJsonString(commandMatch.Groups["cmd"].Value).Trim();
                if (command.Length == 0 || result.ContainsKey(command))
                    continue;

                var action = id.IndexOf('.') >= 0 ? id : prefix + "." + id;
                result.Add(command, action);
            }
        }

        private static string UnescapeJsonString(string value)
        {
            if (value.IndexOf('\\') < 0)
                return value;

            return Regex.Replace(value, "\\\\u(?<code>[0-9a-fA-F]{4})|\\\\(?<char>[\"\\\\/bfnrt])", delegate(Match match)
            {
                var code = match.Groups["code"];
                if (code.Success)
                    return ((char)Convert.ToInt32(code.Value, 16)).ToString();

                switch (match.Groups["char"].Value)
                {
                    case "\"":
                        return "\"";
                    case "\\":
                        return "\\";
                    case "/":
                        return "/";
                    case "b":
                        return "\b";
                    case "f":
                        return "\f";
                    case "n":
                        return "\n";
                    case "r":
                        return "\r";
                    case "t":
                        return "\t";
                    default:
                        return match.Value;
                }
            });
        }
    }
}
