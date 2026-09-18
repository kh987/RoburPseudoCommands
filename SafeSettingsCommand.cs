using System;
using System.Windows.Forms;
using Topomatic.ApplicationPlatform.Plugins;

namespace RoburPseudoCommands
{
    public partial class Module
    {
        internal const string SafeSettingsAlias = "нс";

        [cmd("pseudo_safe_application_settings")]
        public void SafeApplicationSettingsCommand()
        {
            ExecuteSafeSettings(0, "command");
        }

        private static bool IsSafeSettingsAlias(string alias)
        {
            return string.Equals((alias ?? string.Empty).Trim(), SafeSettingsAlias,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool ExecuteSafeSettings(long dispatchId, string source)
        {
            string error;
            Logger.Info("built-in alias execute dispatchId=" + dispatchId + " source=" + source +
                " alias='" + SafeSettingsAlias + "' route=safe-options");
            var result = EmergencyCommandRegistry.TryExecute("options", new object[0], out error);
            if (result == EmergencyExecutionResult.Completed || result == EmergencyExecutionResult.Cancelled)
                return true;

            MessageBox.Show("Не удалось открыть настройки Robur:" + Environment.NewLine + error,
                "RoburPseudoCommands", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    internal static class ProtectedCommandArguments
    {
        internal static bool RequiresPackedHandler(string command)
        {
            switch ((command ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "dsettings":
                case "smdx_manager":
                case "options":
                    return true;
                default:
                    return false;
            }
        }

        internal static object[] Build(string command, object[] commandArguments)
        {
            var args = commandArguments ?? new object[0];
            return RequiresPackedHandler(command) ? new object[] { args } : args;
        }
    }
}
