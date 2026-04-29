using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RoburPseudoCommands
{
    internal static class ActionResolver
    {
        private static readonly object SyncRoot = new object();
        private static Dictionary<string, string> _actionsByCommand;
        private static List<RoburActionInfo> _actions;

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

        public static RoburActionInfo[] GetActions()
        {
            EnsureLoaded();
            return _actions.ToArray();
        }

        public static string[] GetScanDirectories()
        {
            return GetPluginDirectories().ToArray();
        }

        private static void EnsureLoaded()
        {
            if (_actionsByCommand != null)
                return;

            lock (SyncRoot)
            {
                if (_actionsByCommand != null)
                    return;

                var actions = LoadActions();
                _actionsByCommand = BuildActionsByCommand(actions);
                _actions = actions
                    .OrderBy(x => string.IsNullOrEmpty(x.Title) ? x.Command : x.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(x => x.Command, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Action, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Logger.Info("action resolver loaded commandActionsCount=" + _actionsByCommand.Count + "; actionsCount=" + _actions.Count);
            }
        }

        private static Dictionary<string, string> BuildActionsByCommand(IEnumerable<RoburActionInfo> actions)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var action in actions)
            {
                AddAction(result, action.Command, action.Action);
                AddAction(result, action.CommandLine, action.Action);
            }

            return result;
        }

        private static void AddAction(IDictionary<string, string> result, string command, string action)
        {
            command = (command ?? string.Empty).Trim();
            action = (action ?? string.Empty).Trim();

            if (command.Length == 0 || action.Length == 0 || result.ContainsKey(command))
                return;

            result.Add(command, action);
        }

        private static List<RoburActionInfo> LoadActions()
        {
            var result = new List<RoburActionInfo>();

            foreach (var directory in GetPluginDirectories())
            {
                string[] pluginPaths;
                try
                {
                    pluginPaths = Directory.GetFiles(directory, "*.plugin");
                }
                catch (Exception ex)
                {
                    Logger.Error("failed to enumerate plugin files in '" + directory + "'", ex);
                    continue;
                }

                foreach (var pluginPath in pluginPaths.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
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
            }

            return result;
        }

        private static IEnumerable<string> GetPluginDirectories()
        {
            var result = new List<string>();

            var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            AddDirectory(result, assemblyDirectory);
            AddDirectory(result, AppDomain.CurrentDomain.BaseDirectory);

            if (!string.IsNullOrEmpty(assemblyDirectory))
            {
                var parent = Directory.GetParent(assemblyDirectory);
                if (parent != null)
                {
                    AddDirectory(result, parent.FullName);
                    AddDirectory(result, Path.Combine(parent.FullName, "plugins"));
                }

                AddDirectory(result, Path.Combine(assemblyDirectory, "plugins"));
            }

            return result;
        }

        private static void AddDirectory(ICollection<string> result, string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return;

            try
            {
                directory = Path.GetFullPath(directory);
            }
            catch
            {
                return;
            }

            if (!Directory.Exists(directory))
                return;

            if (result.Any(x => string.Equals(x, directory, StringComparison.OrdinalIgnoreCase)))
                return;

            result.Add(directory);
        }

        private static void LoadPluginActions(string pluginPath, IList<RoburActionInfo> result)
        {
            var prefix = Path.GetFileNameWithoutExtension(pluginPath);
            var text = File.ReadAllText(pluginPath);
            int actionsStart;
            int actionsEnd;

            if (!TryFindObjectProperty(text, "actions", out actionsStart, out actionsEnd))
                return;

            var actionsText = text.Substring(actionsStart, actionsEnd - actionsStart + 1);
            foreach (var property in EnumerateObjectProperties(actionsText))
            {
                var commandLine = GetJsonStringProperty(property.Value, "cmd").Trim();
                if (commandLine.Length == 0)
                    continue;

                var title = GetJsonStringProperty(property.Value, "title").Trim();
                var description = GetJsonStringProperty(property.Value, "description").Trim();
                var action = property.Name.IndexOf('.') >= 0 ? property.Name : prefix + "." + property.Name;
                result.Add(new RoburActionInfo(
                    action,
                    ExtractCommandName(commandLine),
                    commandLine,
                    title,
                    description,
                    prefix,
                    pluginPath));
            }
        }

        private static bool TryFindObjectProperty(string text, string propertyName, out int objectStart, out int objectEnd)
        {
            objectStart = -1;
            objectEnd = -1;

            var match = Regex.Match(
                text,
                "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\\{",
                RegexOptions.Singleline);

            if (!match.Success)
                return false;

            objectStart = text.IndexOf('{', match.Index + match.Length - 1);
            if (objectStart < 0)
                return false;

            objectEnd = FindMatchingBrace(text, objectStart);
            return objectEnd >= 0;
        }

        private static IEnumerable<JsonObjectProperty> EnumerateObjectProperties(string objectText)
        {
            var index = 1;
            var limit = objectText.Length - 1;

            while (index < limit)
            {
                index = SkipSeparators(objectText, index);
                if (index >= limit)
                    yield break;

                if (objectText[index] != '"')
                {
                    index++;
                    continue;
                }

                string name;
                if (!TryReadJsonString(objectText, ref index, out name))
                    yield break;

                index = SkipWhitespace(objectText, index);
                if (index >= limit || objectText[index] != ':')
                    yield break;

                index = SkipWhitespace(objectText, index + 1);
                if (index >= limit)
                    yield break;

                if (objectText[index] != '{')
                {
                    index = SkipJsonValue(objectText, index);
                    continue;
                }

                var valueStart = index;
                var valueEnd = FindMatchingBrace(objectText, valueStart);
                if (valueEnd < 0)
                    yield break;

                yield return new JsonObjectProperty(name, objectText.Substring(valueStart, valueEnd - valueStart + 1));
                index = valueEnd + 1;
            }
        }

        private static int FindMatchingBrace(string text, int start)
        {
            var depth = 0;
            var inString = false;
            var escaped = false;

            for (var i = start; i < text.Length; i++)
            {
                var ch = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (ch == '"')
                        inString = false;

                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == '{')
                {
                    depth++;
                    continue;
                }

                if (ch != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return i;
            }

            return -1;
        }

        private static bool TryReadJsonString(string text, ref int index, out string value)
        {
            value = string.Empty;
            if (index >= text.Length || text[index] != '"')
                return false;

            var start = index + 1;
            var escaped = false;

            for (var i = start; i < text.Length; i++)
            {
                var ch = text[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch != '"')
                    continue;

                value = UnescapeJsonString(text.Substring(start, i - start));
                index = i + 1;
                return true;
            }

            return false;
        }

        private static int SkipSeparators(string text, int index)
        {
            while (index < text.Length)
            {
                var ch = text[index];
                if (!char.IsWhiteSpace(ch) && ch != ',')
                    break;

                index++;
            }

            return index;
        }

        private static int SkipWhitespace(string text, int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;

            return index;
        }

        private static int SkipJsonValue(string text, int index)
        {
            var inString = false;
            var escaped = false;

            while (index < text.Length)
            {
                var ch = text[index];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (ch == '\\')
                    {
                        escaped = true;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }

                    index++;
                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    index++;
                    continue;
                }

                if (ch == ',' || ch == '}')
                    return index;

                index++;
            }

            return index;
        }

        private static string GetJsonStringProperty(string objectText, string propertyName)
        {
            var match = Regex.Match(
                objectText,
                "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"",
                RegexOptions.Singleline);

            return match.Success
                ? UnescapeJsonString(match.Groups["value"].Value)
                : string.Empty;
        }

        private static string ExtractCommandName(string commandLine)
        {
            commandLine = (commandLine ?? string.Empty).Trim();
            if (commandLine.Length == 0)
                return string.Empty;

            var inQuote = false;
            for (var i = 0; i < commandLine.Length; i++)
            {
                var ch = commandLine[i];
                if (ch == '"')
                    inQuote = !inQuote;

                if (!inQuote && char.IsWhiteSpace(ch))
                    return commandLine.Substring(0, i).Trim();
            }

            return commandLine;
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

        private sealed class JsonObjectProperty
        {
            public JsonObjectProperty(string name, string value)
            {
                Name = name ?? string.Empty;
                Value = value ?? string.Empty;
            }

            public string Name { get; private set; }

            public string Value { get; private set; }
        }
    }

    internal sealed class RoburActionInfo
    {
        public RoburActionInfo(
            string action,
            string command,
            string commandLine,
            string title,
            string description,
            string pluginName,
            string pluginPath)
        {
            Action = (action ?? string.Empty).Trim();
            Command = (command ?? string.Empty).Trim();
            CommandLine = (commandLine ?? string.Empty).Trim();
            Title = (title ?? string.Empty).Trim();
            Description = (description ?? string.Empty).Trim();
            PluginName = (pluginName ?? string.Empty).Trim();
            PluginPath = (pluginPath ?? string.Empty).Trim();
        }

        public string Action { get; private set; }

        public string Command { get; private set; }

        public string CommandLine { get; private set; }

        public string Title { get; private set; }

        public string Description { get; private set; }

        public string PluginName { get; private set; }

        public string PluginPath { get; private set; }
    }
}
