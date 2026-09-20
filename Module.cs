using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
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
        private static int _autoloadBroadcastObserved;

        public override void Initialize(PluginFactory factory)
        {
            base.Initialize(factory);
            _activeCadViewProvider = () => CadView;
            LogLoaded();
            try
            {
                AnnotationBackgroundScalePatch.Enable();
            }
            catch (Exception ex)
            {
                Logger.Error("annotation background scale patch startup refused; original Robur behavior retained", ex);
            }
            try
            {
                EmergencyCommandRegistry.ScheduleCapture();
            }
            catch (Exception ex)
            {
                Logger.Error("failed to schedule emergency command snapshot", ex);
            }

            try
            {
                KeyInterceptor.Attach(() => CadView);
            }
            catch (Exception ex)
            {
                Logger.Error("failed to attach keyboard message filter; plugin remains available without keyboard interception", ex);
            }
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
            ExecuteAlias(alias, false, 0, "command");
        }

        [cmd("pseudo_reload_aliases")]
        public void ReloadAliases()
        {
            try
            {
                var count = AliasStore.Reload();
                KeyInterceptor.ClearPopupRepeatHistory("aliases-reloaded");
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
            OpenAliasEditor();
        }

        private void OpenAliasEditor()
        {
            try
            {
                using (var form = new AliasEditorForm(() => CadView))
                {
                    form.ShowDialog();

                    if (form.Saved)
                    {
                        var count = AliasStore.Reload();
                        KeyInterceptor.ClearPopupRepeatHistory("aliases-reloaded-after-editor-save");
                        Logger.Info("aliases reloaded after editor save count=" + count + " path='" + AliasStore.ActivePath + "'");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("failed to open or reload aliases after editor save", ex);
                MessageDlg.Show("Failed to edit aliases:" + Environment.NewLine + ex.Message);
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

            try
            {
                sb.AppendLine(Logger.LogPath);
                sb.AppendLine("Log status:");
                sb.AppendLine(PluginSettings.IsLogEnabled() ? "Enabled" : "Disabled");
                sb.AppendLine();

                var lines = Logger.ReadTailLines(40);
                if (lines.Count == 0)
                {
                    sb.AppendLine("Log file does not exist yet.");
                }
                else
                {
                    foreach (var line in lines)
                        sb.AppendLine(line);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("(unavailable)");
                sb.AppendLine();
                sb.AppendLine("Failed to read log:");
                sb.AppendLine(ex.Message);
            }

            MessageDlg.Show(sb.ToString());
        }


        [cmd("pseudo_autoload")]
        public void Autoload()
        {
            if (System.Threading.Interlocked.Exchange(ref _autoloadBroadcastObserved, 1) != 0)
                return;

            Logger.Info("autoload assembly_loaded broadcast received");
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

        internal static bool ExecuteRegisteredAlias(string alias, bool forceExecute)
        {
            return ExecuteRegisteredAlias(alias, forceExecute, 0, "command");
        }

        internal static bool ExecuteRegisteredAlias(
            string alias,
            bool forceExecute,
            long dispatchId,
            string source)
        {
            if (IsAnnotationBackgroundScaleAlias(alias))
                return ExecuteAnnotationBackgroundScaleAlias(dispatchId, source);

            if (!EnsureAliasesLoaded())
                return false;

            return ExecuteAlias(alias, forceExecute, dispatchId, source);
        }

        internal static bool IsKnownAlias(string alias)
        {
            if (IsAnnotationBackgroundScaleAlias(alias))
                return true;

            if (!EnsureAliasesLoaded())
                return false;

            alias = (alias ?? string.Empty).Trim();
            return alias.Length > 0 && AliasStore.Aliases.ContainsKey(alias);
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

        private static bool ExecuteAlias(string alias, bool forceExecute, long dispatchId, string source)
        {
            if (IsAnnotationBackgroundScaleAlias(alias))
                return ExecuteAnnotationBackgroundScaleAlias(dispatchId, source);

            AliasEntry entry;
            if (!AliasStore.Aliases.TryGetValue(alias, out entry))
            {
                Logger.Info("alias not found dispatchId=" + dispatchId + " alias='" + alias + "' forceExecute=" + forceExecute);
                MessageBox.Show(string.Format("Alias '{0}' not found.", alias), "RoburPseudoCommands",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            var action = GetAction(entry);
            var args = entry.Args == null ? new object[0] : entry.Args.Cast<object>().ToArray();

            if (ProtectedCommandArguments.RequiresPackedHandler(entry.Command))
            {
                string protectedError;
                Logger.Info("alias execute diagnostic dispatchId=" + dispatchId + " source=" +
                    source + " route=safe-signature-direct alias='" + alias +
                    "' command='" + entry.Command + "' args=" + args.Length);
                var protectedResult = EmergencyCommandRegistry.TryExecute(entry.Command, args, out protectedError);
                if (protectedResult == EmergencyExecutionResult.Cancelled ||
                    protectedResult == EmergencyExecutionResult.Completed)
                    return true;

                MessageBox.Show(
                    "Не удалось безопасно выполнить псевдокоманду '" + entry.Alias + "':" +
                    Environment.NewLine + protectedError,
                    "RoburPseudoCommands", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                Logger.Info("alias execute diagnostic dispatchId=" + dispatchId + " source=" +
                    source + " route=command alias='" + alias +
                    "' command='" + entry.Command + "' action='" + action + "' args=" +
                    args.Length + " forceExecute=" + forceExecute);
                ApplicationHost.Current.Plugins.Execute(entry.Command, args);
                return true;
            }
            catch (Exception ex)
            {
                if (EmergencyCommandRegistry.IsCancellation(ex))
                {
                    Logger.Info("alias dispatch cancelled dispatchId=" + dispatchId + " alias='" +
                        entry.Alias + "' command='" + entry.Command + "'");
                    return true;
                }

                Logger.Error(
                    "failed dispatchId=" + dispatchId + " alias='" + entry.Alias + "' target='" +
                    (string.IsNullOrEmpty(action) ? entry.Command : action) +
                    "' forceExecute=" + forceExecute, ex);

                MessageBox.Show(
                    string.Format("Failed to execute alias '{0}' -> '{1}':{2}{3}",
                        entry.Alias,
                        string.IsNullOrEmpty(action) ? entry.Command : action,
                        Environment.NewLine,
                        ex.Message),
                    "RoburPseudoCommands", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
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
