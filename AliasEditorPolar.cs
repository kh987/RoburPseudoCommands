using System;
using System.Drawing;
using System.Windows.Forms;
using Topomatic.Cad.View;

namespace RoburPseudoCommands
{
    internal sealed partial class AliasEditorForm
    {
        private readonly Func<CadView> _viewProvider;
        private CheckBox _polarEnabled, _polarRotation;
        private Label _polarStatus;
        private Timer _polarStatusTimer;
        private bool _savedPolar, _savedRotation;

        private Control BuildPolarOptions()
        {
            _savedPolar = PluginSettings.IsNativePolarPatchEnabled();
            _savedRotation = PluginSettings.IsPolarViewRotationEnabled();
            var group = new GroupBox { Text = "Полярное отслеживание", Dock = DockStyle.Fill,
                AutoSize = true, Padding = new Padding(10), Margin = new Padding(0, 4, 0, 8) };
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 4 };
            _polarEnabled = new CheckBox { Text = "Работать независимо от привязки «Ближайшая»", AutoSize = true,
                Checked = _savedPolar, Name = "polarEnabled" };
            _polarRotation = new CheckBox { Text = "Учитывать поворот пространства", AutoSize = true,
                Checked = _savedRotation, Enabled = _savedPolar, Name = "polarRotation", Margin = new Padding(20, 3, 3, 3) };
            _toolTip.SetToolTip(_polarRotation, "Сохранённый выбор не сбрасывается при отключении первой настройки.");
            _polarEnabled.CheckedChanged += delegate { _polarRotation.Enabled = _polarEnabled.Checked; UpdatePolarStatus(); };
            _polarRotation.CheckedChanged += delegate { UpdatePolarStatus(); };
            rows.Controls.Add(_polarEnabled, 0, 0);
            rows.Controls.Add(_polarRotation, 0, 1);
            var hint = new Label { Text = "Само отслеживание, шаг и дополнительные углы задаются в Robur. Общий режим объектной привязки должен быть включён.",
                AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 4) };
            rows.Controls.Add(hint, 0, 2);
            var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            var apply = CreateButton("Применить", ApplyPolarOptions, "Применить и сохранить только полярные настройки. Таблица псевдокоманд не сохраняется.");
            apply.Name = "applyPolar";
            _polarStatus = new Label { AutoSize = true, Margin = new Padding(12, 10, 3, 3), Name = "polarStatus" };
            footer.Controls.Add(apply); footer.Controls.Add(_polarStatus);
            rows.Controls.Add(footer, 0, 3);
            group.Controls.Add(rows);
            UpdatePolarStatus();
            // UI-only status refresh: no tracing, SDK writes, file writes or event subscriptions.
            _polarStatusTimer = new Timer { Interval = 1000 };
            _polarStatusTimer.Tick += delegate { if (Visible) UpdatePolarStatus(); };
            Shown += delegate { _polarStatusTimer.Start(); };
            return group;
        }

        private bool PolarPending
        {
            get { return _polarEnabled != null && (_polarEnabled.Checked != _savedPolar || _polarRotation.Checked != _savedRotation); }
        }
        private void UpdatePolarStatus()
        {
            if (_polarStatus == null) return;
            _polarStatus.Text = PolarOptions.Status() + (PolarPending ? " • Есть неприменённые изменения" : "");
        }
        private void ApplyPolarOptions()
        {
            try
            {
                PolarOptions.Apply(_viewProvider(), _polarEnabled.Checked, _polarRotation.Checked);
                _savedPolar = _polarEnabled.Checked;
                _savedRotation = _polarRotation.Checked;
            }
            catch (Exception ex)
            {
                Logger.Error("polar settings apply failed", ex);
                MessageBox.Show(this, ex.Message, "Полярные настройки не применены", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { UpdatePolarStatus(); }
        }
        internal void FocusPolarOptions()
        {
            Shown += delegate { _polarEnabled.Focus(); };
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (PolarPending && e.CloseReason == CloseReason.UserClosing &&
                MessageBox.Show(this, "Полярные настройки изменены, но не применены. Закрыть окно без их применения?",
                    "Неприменённые настройки", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) e.Cancel = true;
            base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && _polarStatusTimer != null) { _polarStatusTimer.Dispose(); _polarStatusTimer = null; }
            base.Dispose(disposing);
        }
    }
}
