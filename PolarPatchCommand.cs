using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Topomatic.ApplicationPlatform.Plugins;

namespace RoburPseudoCommands
{
    public partial class Module
    {
        private static bool _polarStartupAttempted;
        private static void InitializePolarPatch()
        {
            if (_polarStartupAttempted) return;
            _polarStartupAttempted = true;
            if (!PluginSettings.IsNativePolarPatchEnabled()) return;
            try { EnableNativePolarPatch(); }
            catch (Exception ex) { Logger.Error("native polar patch startup refused; original Robur behavior retained", ex); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void EnableNativePolarPatch()
        {
            NativePolarPatch.SetRotationEnabled(PluginSettings.IsPolarViewRotationEnabled());
            NativePolarPatch.Enable();
        }

        // Compatibility entry points: no standalone settings dialogs or duplicate apply logic.
        [cmd("pseudo_polar_rotation")]
        public void PolarRotationCommand() { OpenAliasEditor(true); }

        [cmd("pseudo_polar_patch")]
        public void PolarPatchCommand() { OpenAliasEditor(true); }

        [cmd("pseudo_polar_diagnostics")]
        public void PolarDiagnosticsCommand()
        {
            MessageBox.Show("Временная полярная трассировка удалена из рабочей сборки.\nНастройки находятся в «Сервис → Псевдокоманды…».",
                "Полярное отслеживание", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
