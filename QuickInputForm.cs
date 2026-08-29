using System;
using System.Drawing;
using System.Windows.Forms;

namespace RoburPseudoCommands
{
    internal sealed class QuickInputForm : Form
    {
        private readonly Func<string, bool> _isKnownAlias;
        private readonly TextBox _input;
        private readonly Label _status;

        public QuickInputForm(string initialText, Func<string, bool> isKnownAlias)
        {
            if (isKnownAlias == null)
                throw new ArgumentNullException("isKnownAlias");

            _isKnownAlias = isKnownAlias;
            Text = "Псевдокоманда";
            ClientSize = new Size(300, 58);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Font = SystemFonts.MessageBoxFont;

            _input = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12f),
                Text = initialText ?? string.Empty
            };
            _input.KeyDown += InputKeyDown;
            _input.TextChanged += delegate { ClearStatus(); };

            _status = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(2, 5, 2, 0),
                Text = "Enter или Space — выполнить; Esc — отменить"
            };

            Controls.Add(_status);
            Controls.Add(_input);
            Deactivate += delegate { Close(); };
            Shown += delegate
            {
                _input.Focus();
                _input.SelectionStart = _input.TextLength;
            };
        }

        public string AcceptedAlias { get; private set; }

        private void InputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                Close();
                return;
            }

            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;

            var alias = (_input.Text ?? string.Empty).Trim();
            if (alias.Length == 0 || !_isKnownAlias(alias))
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = alias.Length == 0
                    ? "Введите псевдокоманду."
                    : "Псевдокоманда не найдена — исправьте ввод.";
                return;
            }

            AcceptedAlias = alias;
            Close();
        }

        private void ClearStatus()
        {
            _status.ForeColor = SystemColors.GrayText;
            _status.Text = "Enter или Space — выполнить; Esc — отменить";
        }
    }
}
