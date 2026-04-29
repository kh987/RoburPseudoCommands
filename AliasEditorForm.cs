using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RoburPseudoCommands
{
    internal sealed class AliasEditorForm : Form
    {
        private const string ColAlias = "Alias";
        private const string ColAction = "Action";
        private const string ColCommand = "Command";
        private const string ColArgs = "Args";
        private const string ColDescription = "Description";
        private const string ColStatus = "Status";

        private readonly DataTable _table;
        private readonly BindingSource _bindingSource;
        private readonly DataGridView _grid;
        private readonly TextBox _filterTextBox;
        private readonly CheckBox _advancedCheckBox;
        private readonly Label _summaryLabel;

        public AliasEditorForm()
        {
            Text = "Псевдокоманды";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(820, 480);
            Size = new Size(980, 620);
            Font = SystemFonts.MessageBoxFont;

            _table = CreateTable();
            _bindingSource = new BindingSource();
            _grid = new DataGridView();
            _filterTextBox = new TextBox();
            _advancedCheckBox = new CheckBox();
            _summaryLabel = new Label();

            BuildLayout();
            LoadRows();
            RefreshStatuses();
            _table.AcceptChanges();
        }

        public bool Saved { get; private set; }

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
                ColumnCount = 3,
                RowCount = 2
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            top.Controls.Add(new Label
            {
                Text = "Фильтровать",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 4, 8, 0)
            }, 0, 0);

            _filterTextBox.Dock = DockStyle.Fill;
            _filterTextBox.TextChanged += delegate { ApplyFilter(); };
            top.Controls.Add(_filterTextBox, 1, 0);

            _advancedCheckBox.Text = "Расширенно";
            _advancedCheckBox.AutoSize = true;
            _advancedCheckBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _advancedCheckBox.Margin = new Padding(12, 3, 0, 0);
            _advancedCheckBox.CheckedChanged += delegate { UpdateAdvancedMode(); };
            top.Controls.Add(_advancedCheckBox, 2, 0);
            top.SetRowSpan(_advancedCheckBox, 2);

            top.Controls.Add(new Label
            {
                Text = "Файл",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 4, 8, 0)
            }, 0, 1);

            top.Controls.Add(new Label
            {
                Text = AliasStore.GetConfigPath(),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 4, 0, 0)
            }, 1, 1);

            root.Controls.Add(top, 0, 0);

            _bindingSource.DataSource = _table;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = true;
            _grid.AutoGenerateColumns = false;
            _grid.BackgroundColor = SystemColors.Window;
            _grid.BorderStyle = BorderStyle.Fixed3D;
            _grid.DataSource = _bindingSource;
            _grid.Dock = DockStyle.Fill;
            _grid.MultiSelect = true;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.CellEndEdit += delegate { RefreshStatuses(); };
            _grid.CellDoubleClick += GridCellDoubleClick;
            _grid.UserDeletedRow += delegate { RefreshStatuses(); };
            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            AddColumns();
            UpdateAdvancedMode();
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

            buttons.Controls.Add(CreateButton("Закрыть", Close));
            buttons.Controls.Add(CreateButton("Лог", ShowLog));
            buttons.Controls.Add(CreateButton("Перезагрузить", ReloadRows));
            buttons.Controls.Add(CreateButton("Сохранить", SaveRows));
            buttons.Controls.Add(CreateButton("Удалить", DeleteSelectedRows));
            buttons.Controls.Add(CreateButton("Добавить", AddRow));
            buttons.Controls.Add(CreateButton("\u041a\u043e\u043c\u0430\u043d\u0434\u0430...", SelectCommand));
            root.Controls.Add(buttons, 0, 3);
        }

        private Button CreateButton(string text, Action action)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(88, 28),
                Margin = new Padding(4)
            };

            button.Click += delegate { action(); };
            return button;
        }

        private void AddColumns()
        {
            _grid.Columns.Add(CreateTextColumn(ColAlias, "Псевдокоманда", 110, false));
            _grid.Columns.Add(CreateTextColumn(ColAction, "Action", 180, false));
            _grid.Columns.Add(CreateTextColumn(ColCommand, "Команда", 170, false));
            _grid.Columns.Add(CreateTextColumn(ColArgs, "Аргументы", 170, false));
            _grid.Columns.Add(CreateTextColumn(ColDescription, "Описание", 260, false));
            _grid.Columns.Add(CreateTextColumn(ColStatus, "Статус", 160, true));
        }

        private void UpdateAdvancedMode()
        {
            SetColumnVisible(ColAction, _advancedCheckBox.Checked);
            SetColumnVisible(ColArgs, _advancedCheckBox.Checked);
        }

        private void SetColumnVisible(string columnName, bool visible)
        {
            if (_grid.Columns.Contains(columnName))
                _grid.Columns[columnName].Visible = visible;
        }

        private static DataGridViewTextBoxColumn CreateTextColumn(string name, string header, int width, bool readOnly)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = name,
                HeaderText = header,
                Name = name,
                Width = width,
                ReadOnly = readOnly
            };
        }

        private static DataTable CreateTable()
        {
            var table = new DataTable();
            table.Columns.Add(ColAlias, typeof(string));
            table.Columns.Add(ColAction, typeof(string));
            table.Columns.Add(ColCommand, typeof(string));
            table.Columns.Add(ColArgs, typeof(string));
            table.Columns.Add(ColDescription, typeof(string));
            table.Columns.Add(ColStatus, typeof(string));
            return table;
        }

        private void LoadRows()
        {
            _table.Clear();

            foreach (var entry in AliasStore.LoadEntries())
            {
                if (entry == null)
                    continue;

                _table.Rows.Add(
                    entry.Alias,
                    entry.Action,
                    entry.Command,
                    FormatArgs(entry.Args),
                    entry.Description,
                    string.Empty);
            }
        }

        private void ReloadRows()
        {
            if (!ConfirmDiscardChanges())
                return;

            LoadRows();
            RefreshStatuses();
            _table.AcceptChanges();
            Logger.Info("alias editor reloaded rows from '" + AliasStore.GetConfigPath() + "'");
        }

        private bool ConfirmDiscardChanges()
        {
            var changes = _table.GetChanges();
            if (changes != null && changes.Rows.Count > 0)
            {
                var result = MessageBox.Show(
                    this,
                    "Несохраненные изменения будут потеряны. Продолжить?",
                    "Псевдокоманды",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                return result == DialogResult.Yes;
            }

            return true;
        }

        private void AddRow()
        {
            _bindingSource.Filter = string.Empty;
            _filterTextBox.Text = string.Empty;

            var row = _table.Rows.Add(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            _bindingSource.Position = _bindingSource.Count - 1;
            RefreshStatuses();
        }

        private void DeleteSelectedRows()
        {
            var rows = _grid.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(x => !x.IsNewRow)
                .ToList();

            foreach (var gridRow in rows)
            {
                _grid.Rows.Remove(gridRow);
            }

            RefreshStatuses();
        }

        private void SelectCommand()
        {
            _grid.EndEdit();
            _bindingSource.EndEdit();

            var row = GetCurrentEditableRow();
            if (row == null)
                return;

            var actions = ActionResolver.GetActions();
            if (actions.Length == 0)
            {
                MessageBox.Show(
                    this,
                    "\u041d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d\u043e \u043d\u0438 \u043e\u0434\u043d\u043e\u0439 Robur action \u0432 .plugin \u0444\u0430\u0439\u043b\u0430\u0445."
                        + Environment.NewLine
                        + Environment.NewLine
                        + string.Join(Environment.NewLine, ActionResolver.GetScanDirectories()),
                    "\u0412\u044b\u0431\u043e\u0440 \u043a\u043e\u043c\u0430\u043d\u0434\u044b",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using (var form = new CommandPickerForm(actions, GetCell(row, ColCommand)))
            {
                if (form.ShowDialog(this) != DialogResult.OK || form.SelectedAction == null)
                    return;

                row[ColCommand] = form.SelectedAction.Command;
                row[ColAction] = form.SelectedAction.Action;

                if (string.IsNullOrEmpty(GetCell(row, ColDescription)))
                    row[ColDescription] = GetDefaultDescription(form.SelectedAction);

                Logger.Info("alias editor selected action command='" + form.SelectedAction.Command + "' action='" + form.SelectedAction.Action + "'");
            }

            RefreshStatuses();
        }

        private DataRow GetCurrentEditableRow()
        {
            var view = _bindingSource.Current as DataRowView;
            if (view != null)
                return view.Row;

            AddRow();
            view = _bindingSource.Current as DataRowView;
            return view == null ? null : view.Row;
        }

        private static string GetDefaultDescription(RoburActionInfo action)
        {
            if (!string.IsNullOrEmpty(action.Title))
                return action.Title;

            return action.Description;
        }

        private void GridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var column = _grid.Columns[e.ColumnIndex];
            if (column == null)
                return;

            if (column.Name == ColCommand || column.Name == ColAction)
                SelectCommand();
        }

        private void SaveRows()
        {
            _grid.EndEdit();
            _bindingSource.EndEdit();
            RefreshStatuses();

            var errors = GetValidationErrors();
            if (errors.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, errors.ToArray()),
                    "Проверьте псевдокоманды",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            List<AliasEntry> entries;
            try
            {
                entries = BuildEntries();
            }
            catch (FormatException ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Проверьте аргументы",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var restartRequired = IsRestartRequired(entries);
            AliasStore.SaveEntries(entries);
            Saved = true;

            Logger.Info("alias editor saved aliases count=" + entries.Count + " path='" + AliasStore.GetConfigPath() + "' restartRequired=" + restartRequired);

            var message = restartRequired
                ? "Сохранено. Для регистрации новых, удаленных или переименованных псевдокоманд перезапустите Robur."
                : "Сохранено. Изменения назначений применятся после Reload aliases.";

            MessageBox.Show(this, message, "Псевдокоманды", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshStatuses();
            _table.AcceptChanges();
        }

        private void ShowLog()
        {
            var text = new StringBuilder();
            text.AppendLine("Log file:");
            text.AppendLine(Logger.LogPath);
            text.AppendLine();

            try
            {
                if (!System.IO.File.Exists(Logger.LogPath))
                {
                    text.AppendLine("Log file does not exist yet.");
                }
                else
                {
                    var lines = System.IO.File.ReadAllLines(Logger.LogPath, Encoding.UTF8);
                    var start = Math.Max(0, lines.Length - 40);
                    for (var i = start; i < lines.Length; i++)
                        text.AppendLine(lines[i]);
                }
            }
            catch (Exception ex)
            {
                text.AppendLine("Failed to read log:");
                text.AppendLine(ex.Message);
            }

            MessageBox.Show(this, text.ToString(), "Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RefreshStatuses()
        {
            var aliases = _table.Rows
                .Cast<DataRow>()
                .Where(x => x.RowState != DataRowState.Deleted)
                .Select(x => GetCell(x, ColAlias))
                .Where(x => !string.IsNullOrEmpty(x))
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (DataRow row in _table.Rows)
            {
                if (row.RowState == DataRowState.Deleted)
                    continue;

                row[ColStatus] = GetStatus(row, aliases);
            }

            var rows = _table.Rows
                .Cast<DataRow>()
                .Where(x => x.RowState != DataRowState.Deleted)
                .ToList();
            var active = rows.Count(x => GetCell(x, ColStatus) == "Активна");
            var restart = rows.Count(x => GetCell(x, ColStatus) == "Требуется перезапуск");
            var invalid = rows.Count(x => IsInvalidStatus(GetCell(x, ColStatus)));
            _summaryLabel.Text = string.Format(
                "Всего: {0}; активных: {1}; требуется перезапуск: {2}; ошибок: {3}",
                rows.Count,
                active,
                restart,
                invalid);
        }

        private string GetStatus(DataRow row, IDictionary<string, int> aliases)
        {
            var alias = GetCell(row, ColAlias);
            var command = GetCell(row, ColCommand);

            if (string.IsNullOrEmpty(alias))
                return "Пустое имя";

            if (!DynamicAliasCommandFactory.IsSupportedAliasName(alias))
                return "Некорректное имя";

            if (aliases.ContainsKey(alias) && aliases[alias] > 1)
                return "Дубликат";

            if (string.IsNullOrEmpty(command))
                return "Нет команды";

            return DynamicAliasCommandFactory.IsRegistered(alias)
                ? "Активна"
                : "Требуется перезапуск";
        }

        private List<string> GetValidationErrors()
        {
            var errors = new List<string>();
            foreach (DataRow row in _table.Rows)
            {
                if (row.RowState == DataRowState.Deleted)
                    continue;

                var status = GetCell(row, ColStatus);
                if (!IsInvalidStatus(status))
                    continue;

                errors.Add(string.Format(
                    "{0}: {1}",
                    string.IsNullOrEmpty(GetCell(row, ColAlias)) ? "<пусто>" : GetCell(row, ColAlias),
                    status));
            }

            return errors;
        }

        private static bool IsInvalidStatus(string status)
        {
            return status != "Активна" && status != "Требуется перезапуск";
        }

        private List<AliasEntry> BuildEntries()
        {
            var entries = new List<AliasEntry>();
            foreach (DataRow row in _table.Rows)
            {
                if (row.RowState == DataRowState.Deleted)
                    continue;

                var alias = GetCell(row, ColAlias);
                if (string.IsNullOrEmpty(alias))
                    continue;

                entries.Add(new AliasEntry
                {
                    Alias = alias,
                    Action = GetCell(row, ColAction),
                    Command = GetCell(row, ColCommand),
                    Args = ParseArgs(GetCell(row, ColArgs)),
                    Description = GetCell(row, ColDescription)
                });
            }

            return entries;
        }

        private bool IsRestartRequired(IList<AliasEntry> entries)
        {
            var current = new HashSet<string>(
                entries.Select(x => x.Alias),
                StringComparer.OrdinalIgnoreCase);

            if (current.Any(x => !DynamicAliasCommandFactory.IsRegistered(x)))
                return true;

            return DynamicAliasCommandFactory
                .GetRegisteredAliases()
                .Any(x => !current.Contains(x));
        }

        private void ApplyFilter()
        {
            var value = _filterTextBox.Text.Trim();
            if (string.IsNullOrEmpty(value))
            {
                _bindingSource.Filter = string.Empty;
                return;
            }

            value = EscapeFilterValue(value);
            _bindingSource.Filter = string.Format(
                "{0} LIKE '%{5}%' OR {1} LIKE '%{5}%' OR {2} LIKE '%{5}%' OR {3} LIKE '%{5}%' OR {4} LIKE '%{5}%'",
                ColAlias,
                ColAction,
                ColCommand,
                ColArgs,
                ColDescription,
                value);
        }

        private static string EscapeFilterValue(string value)
        {
            return value
                .Replace("'", "''")
                .Replace("[", "[[]")
                .Replace("%", "[%]")
                .Replace("*", "[*]");
        }

        private static string GetCell(DataRow row, string column)
        {
            return (row[column] == null || row[column] == DBNull.Value)
                ? string.Empty
                : row[column].ToString().Trim();
        }

        private static string FormatArgs(IList<string> args)
        {
            if (args == null || args.Count == 0)
                return string.Empty;

            return string.Join(" ", args.Select(QuoteArg).ToArray());
        }

        private static string QuoteArg(string arg)
        {
            arg = arg ?? string.Empty;
            if (arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
                return arg;

            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }

        private static List<string> ParseArgs(string value)
        {
            var args = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
                return args;

            var current = new StringBuilder();
            var inQuote = false;
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (ch == '\\' && i + 1 < value.Length && value[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    inQuote = !inQuote;
                    continue;
                }

                if (char.IsWhiteSpace(ch) && !inQuote)
                {
                    if (current.Length > 0)
                    {
                        args.Add(current.ToString());
                        current.Length = 0;
                    }

                    continue;
                }

                current.Append(ch);
            }

            if (inQuote)
                throw new FormatException("Не закрыта кавычка в аргументах.");

            if (current.Length > 0)
                args.Add(current.ToString());

            return args;
        }
    }
}
