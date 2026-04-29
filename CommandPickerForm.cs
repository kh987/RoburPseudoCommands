using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RoburPseudoCommands
{
    internal sealed class CommandPickerForm : Form
    {
        private readonly List<RoburActionInfo> _allActions;
        private readonly BindingSource _bindingSource;
        private readonly TextBox _filterTextBox;
        private readonly DataGridView _grid;
        private readonly Label _summaryLabel;
        private readonly Button _okButton;

        public CommandPickerForm(IEnumerable<RoburActionInfo> actions, string initialFilter)
        {
            Text = "\u0412\u044b\u0431\u043e\u0440 \u043a\u043e\u043c\u0430\u043d\u0434\u044b Robur";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 420);
            Size = new Size(980, 600);
            Font = SystemFonts.MessageBoxFont;

            _allActions = (actions ?? Enumerable.Empty<RoburActionInfo>()).ToList();
            _bindingSource = new BindingSource();
            _filterTextBox = new TextBox();
            _grid = new DataGridView();
            _summaryLabel = new Label();
            _okButton = new Button();

            BuildLayout();
            _filterTextBox.Text = initialFilter ?? string.Empty;
            ApplyFilter();
        }

        public RoburActionInfo SelectedAction { get; private set; }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _filterTextBox.Select();
            _filterTextBox.SelectAll();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            top.Controls.Add(new Label
            {
                Text = "\u0424\u0438\u043b\u044c\u0442\u0440",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 4, 8, 0)
            }, 0, 0);

            _filterTextBox.Dock = DockStyle.Fill;
            _filterTextBox.TextChanged += delegate { ApplyFilter(); };
            _filterTextBox.KeyDown += FilterTextBoxKeyDown;
            top.Controls.Add(_filterTextBox, 1, 0);
            root.Controls.Add(top, 0, 0);

            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.BackgroundColor = SystemColors.Window;
            _grid.BorderStyle = BorderStyle.Fixed3D;
            _grid.DataSource = _bindingSource;
            _grid.Dock = DockStyle.Fill;
            _grid.MultiSelect = false;
            _grid.ReadOnly = true;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.CellDoubleClick += delegate { AcceptSelection(); };
            _grid.KeyDown += GridKeyDown;
            _grid.SelectionChanged += delegate { UpdateOkButton(); };
            AddColumns();
            root.Controls.Add(_grid, 0, 1);

            _summaryLabel.AutoSize = true;
            _summaryLabel.Dock = DockStyle.Fill;
            _summaryLabel.Padding = new Padding(0, 6, 0, 6);
            root.Controls.Add(_summaryLabel, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            var cancelButton = new Button
            {
                Text = "\u041e\u0442\u043c\u0435\u043d\u0430",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                MinimumSize = new Size(88, 28),
                Margin = new Padding(4)
            };

            _okButton.Text = "OK";
            _okButton.AutoSize = true;
            _okButton.MinimumSize = new Size(88, 28);
            _okButton.Margin = new Padding(4);
            _okButton.Click += delegate { AcceptSelection(); };

            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(_okButton);
            root.Controls.Add(buttons, 0, 3);

            AcceptButton = _okButton;
            CancelButton = cancelButton;
        }

        private void AddColumns()
        {
            _grid.Columns.Add(CreateTextColumn("Title", "\u041d\u0430\u0437\u0432\u0430\u043d\u0438\u0435", 220));
            _grid.Columns.Add(CreateTextColumn("Command", "\u041a\u043e\u043c\u0430\u043d\u0434\u0430", 130));
            _grid.Columns.Add(CreateTextColumn("Action", "Action", 220));
            _grid.Columns.Add(CreateTextColumn("Description", "\u041e\u043f\u0438\u0441\u0430\u043d\u0438\u0435", 260));
            _grid.Columns.Add(CreateTextColumn("PluginName", "\u041f\u043b\u0430\u0433\u0438\u043d", 120));
        }

        private static DataGridViewTextBoxColumn CreateTextColumn(string propertyName, string header, int width)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = propertyName,
                HeaderText = header,
                Name = propertyName,
                Width = width
            };
        }

        private void ApplyFilter()
        {
            var terms = (_filterTextBox.Text ?? string.Empty)
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            var filtered = _allActions
                .Where(x => terms.All(term => Contains(x, term)))
                .ToList();

            _bindingSource.DataSource = filtered;
            _summaryLabel.Text = string.Format(
                "\u041d\u0430\u0439\u0434\u0435\u043d\u043e: {0} \u0438\u0437 {1}",
                filtered.Count,
                _allActions.Count);
            UpdateOkButton();
        }

        private static bool Contains(RoburActionInfo action, string term)
        {
            return Contains(action.Title, term)
                || Contains(action.Command, term)
                || Contains(action.CommandLine, term)
                || Contains(action.Action, term)
                || Contains(action.Description, term)
                || Contains(action.PluginName, term);
        }

        private static bool Contains(string value, string term)
        {
            return (value ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AcceptSelection()
        {
            var action = GetCurrentAction();
            if (action == null)
                return;

            SelectedAction = action;
            DialogResult = DialogResult.OK;
            Close();
        }

        private RoburActionInfo GetCurrentAction()
        {
            var rowView = _bindingSource.Current as RoburActionInfo;
            if (rowView != null)
                return rowView;

            if (_grid.CurrentRow == null)
                return null;

            return _grid.CurrentRow.DataBoundItem as RoburActionInfo;
        }

        private void UpdateOkButton()
        {
            _okButton.Enabled = GetCurrentAction() != null;
        }

        private void FilterTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Down || _grid.Rows.Count == 0)
                return;

            _grid.Focus();
            _grid.CurrentCell = _grid.Rows[0].Cells[0];
            e.Handled = true;
        }

        private void GridKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                AcceptSelection();
                e.Handled = true;
            }
        }
    }
}
