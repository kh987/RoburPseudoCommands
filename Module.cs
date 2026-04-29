using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Topomatic.ApplicationPlatform;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.Cad.View;
using Topomatic.Cad.View.Hints;
using Topomatic.Controls.Dialogs;

namespace RoburPseudoCommands
{
    public partial class Module : PluginInitializator
    {
        private static readonly AliasStore AliasStore = new AliasStore();

        public override void Initialize(PluginFactory factory)
        {
            base.Initialize(factory);
            LogLoaded();
        }

        [cmd("pseudo_command")]
        public void PseudoCommand()
        {
            if (!EnsureAliasesLoaded())
                return;

            var cadView = CadView;
            if (cadView == null)
            {
                MessageDlg.Show("No active CadView.");
                return;
            }

            var alias = string.Empty;
            var result = CadCursors.GetString(cadView, ref alias, "Command:");
            if (result == GetPointResult.Cancel)
                return;

            alias = (alias ?? string.Empty).Trim();
            if (alias.Length == 0)
                return;

            Logger.Info("pseudo_command alias='" + alias + "'");
            ExecuteAlias(alias, false);
        }

        [cmd("pseudo_reload_aliases")]
        public void ReloadAliases()
        {
            try
            {
                var count = AliasStore.Reload();
                Logger.Info("aliases reloaded count=" + count + " path='" + AliasStore.ActivePath + "'");
                MessageDlg.Show(string.Format(
                    "Loaded {0} aliases from:{1}{2}",
                    count,
                    Environment.NewLine,
                    AliasStore.ActivePath));
            }
            catch (Exception ex)
            {
                Logger.Error("failed to reload aliases", ex);
                MessageDlg.Show("Failed to load aliases:" + Environment.NewLine + ex.Message);
            }
        }

        [cmd("pseudo_edit_aliases")]
        public void EditAliases()
        {
            using (var form = new AliasEditorForm())
            {
                form.ShowDialog();

                if (form.Saved)
                {
                    try
                    {
                        var count = AliasStore.Reload();
                        Logger.Info("aliases reloaded after editor save count=" + count + " path='" + AliasStore.ActivePath + "'");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("failed to reload aliases after editor save", ex);
                    }
                }
            }
        }

        [cmd("pseudo_show_aliases")]
        public void ShowAliases()
        {
            if (!EnsureAliasesLoaded())
                return;

            var aliases = AliasStore.Aliases.Values
                .OrderBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Aliases file:");
            sb.AppendLine(AliasStore.ActivePath);
            sb.AppendLine("Log file:");
            sb.AppendLine(Logger.LogPath);
            sb.AppendLine();

            if (aliases.Count == 0)
            {
                sb.AppendLine("No aliases configured.");
            }
            else
            {
                foreach (var item in aliases)
                {
                    sb.Append(item.Alias);
                    sb.Append(" -> ");
                    sb.Append(item.Command);

                    if (item.Args != null && item.Args.Count > 0)
                    {
                        sb.Append(" ");
                        sb.Append(string.Join(" ", item.Args.ToArray()));
                    }

                    if (!string.IsNullOrEmpty(item.Action))
                    {
                        sb.Append(" [");
                        sb.Append(item.Action);
                        sb.Append("]");
                    }

                    if (!string.IsNullOrEmpty(item.Description))
                    {
                        sb.Append(" (");
                        sb.Append(item.Description);
                        sb.Append(")");
                    }

                    sb.AppendLine();
                }
            }

            MessageDlg.Show(sb.ToString());
        }

        [cmd("pseudo_show_log")]
        public void ShowLog()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Log file:");
            sb.AppendLine(Logger.LogPath);
            sb.AppendLine();

            try
            {
                if (!File.Exists(Logger.LogPath))
                {
                    sb.AppendLine("Log file does not exist yet.");
                }
                else
                {
                    var lines = File.ReadAllLines(Logger.LogPath);
                    var start = Math.Max(0, lines.Length - 40);
                    for (var i = start; i < lines.Length; i++)
                        sb.AppendLine(lines[i]);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("Failed to read log:");
                sb.AppendLine(ex.Message);
            }

            MessageDlg.Show(sb.ToString());
        }

        private static bool EnsureAliasesLoaded()
        {
            try
            {
                AliasStore.EnsureLoaded();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("failed to load aliases", ex);
                MessageDlg.Show("Failed to load aliases:" + Environment.NewLine + ex.Message);
                return false;
            }
        }

        internal static void ExecuteRegisteredAlias(string alias, bool forceExecute)
        {
            if (!EnsureAliasesLoaded())
                return;

            ExecuteAlias(alias, forceExecute);
        }

        private static void LogLoaded()
        {
            var version = GetPluginVersion();
            var assemblyPath = Assembly.GetExecutingAssembly().Location;

            try
            {
                var count = AliasStore.Reload();
                Logger.Info(string.Format(
                    "RoburPseudoCommands {0} loaded; dll='{1}'; aliases='{2}'; aliasesCount={3}",
                    version,
                    assemblyPath,
                    AliasStore.ActivePath,
                    count));
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format(
                    "RoburPseudoCommands {0} loaded; dll='{1}'; failed to load aliases; aliases='{2}'",
                    version,
                    assemblyPath,
                    AliasStore.ActivePath),
                    ex);
            }
        }

        private static string GetPluginVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var attribute = assembly
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .OfType<AssemblyInformationalVersionAttribute>()
                .FirstOrDefault();

            if (attribute != null && !string.IsNullOrEmpty(attribute.InformationalVersion))
                return attribute.InformationalVersion;

            return assembly.GetName().Version.ToString();
        }

        private static void ExecuteAlias(string alias, bool forceExecute)
        {
            AliasEntry entry;
            if (!AliasStore.Aliases.TryGetValue(alias, out entry))
            {
                Logger.Info("alias not found alias='" + alias + "' forceExecute=" + forceExecute);
                MessageDlg.Show(string.Format("Alias '{0}' not found.", alias));
                return;
            }

            var action = GetAction(entry);
            try
            {
                var argsCount = entry.Args == null ? 0 : entry.Args.Count;
                if (!forceExecute && !string.IsNullOrEmpty(action) && argsCount == 0)
                {
                    Logger.Info("alias invokeAction alias='" + alias + "' action='" + action + "' command='" + entry.Command + "'");
                    ApplicationHost.Current.Plugins.InvokeAction(action, string.Empty);
                    return;
                }

                var args = entry.Args == null
                    ? new object[0]
                    : entry.Args.Cast<object>().ToArray();

                Logger.Info("alias execute alias='" + alias + "' command='" + entry.Command + "' args=" + args.Length + " forceExecute=" + forceExecute);
                ApplicationHost.Current.Plugins.Execute(entry.Command, args);
            }
            catch (Exception ex)
            {
                Logger.Error(
                    "failed alias='" + entry.Alias + "' target='" + (string.IsNullOrEmpty(action) ? entry.Command : action) + "' forceExecute=" + forceExecute,
                    ex);
                MessageDlg.Show(string.Format(
                    "Failed to execute alias '{0}' -> '{1}':{2}{3}",
                    entry.Alias,
                    string.IsNullOrEmpty(action) ? entry.Command : action,
                    Environment.NewLine,
                    ex.Message));
            }
        }

        private static string GetAction(AliasEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.Action))
                return entry.Action;

            var action = ActionResolver.Resolve(entry.Command);
            if (!string.IsNullOrEmpty(action))
                Logger.Info("alias resolved action alias='" + entry.Alias + "' command='" + entry.Command + "' action='" + action + "'");

            return action;
        }
    }
}
