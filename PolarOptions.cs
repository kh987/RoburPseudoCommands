using System;
using Topomatic.Cad.View;

namespace RoburPseudoCommands
{
    internal static class PolarOptions
    {
        internal static string Status()
        {
            try
            {
                var active = NativePolarPatch.Enabled;
                var text = "Фактически: независимость — " + (active ? "вкл." : "выкл.") +
                    "; поправка поворота — " + (active && NativePolarPatch.RotationEnabled ? "вкл." : "не действует");
                if (PluginSettings.IsNativePolarPatchEnabled() != active ||
                    (active && PluginSettings.IsPolarViewRotationEnabled() != NativePolarPatch.RotationEnabled))
                    text += " • Сохранённые настройки не применены";
                return text;
            }
            catch { return "Фактический статус недоступен"; }
        }
        internal static void Apply(CadView view, bool enabled, bool rotation)
        {
            if (view != null && (view.IsDisposed || view.IsGettingValue || view.IsModalEdit))
                throw new InvalidOperationException("Сначала завершите текущую команду и ввод точки.");
            var oldEnabled = NativePolarPatch.Enabled;
            var oldRotation = NativePolarPatch.RotationEnabled;
            try
            {
                SetRuntime(view, enabled, rotation);
                // One atomic settings write for both preferences, only after successful runtime application.
                PluginSettings.SetPolarOptions(enabled, rotation);
            }
            catch (Exception applyError)
            {
                try { SetRuntime(view, oldEnabled, oldRotation); }
                catch (Exception rollbackError)
                {
                    throw new AggregateException("Применение и восстановление состояния завершились ошибкой. Перезапустите Robur.",
                        applyError, rollbackError);
                }
                throw;
            }
        }
        private static void SetRuntime(CadView view, bool enabled, bool rotation)
        {
            NativePolarPatch.SetRotationEnabled(rotation);
            if (enabled) NativePolarPatch.Enable(); else NativePolarPatch.Disable();
            if (view != null) { NativePolarPatch.RefreshRotationView(view); view.Invalidate(); }
            if (NativePolarPatch.Enabled != enabled || NativePolarPatch.RotationEnabled != rotation)
                throw new InvalidOperationException("Robur не подтвердил применение полярных настроек. Проверьте фактический статус.");
        }
    }
}
