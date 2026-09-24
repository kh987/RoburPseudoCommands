using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace RoburPseudoCommands
{
    internal sealed partial class AliasEditorForm : Form
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
        private readonly CheckBox _logEnabledCheckBox;
        private readonly CheckBox _quickInputEnabledCheckBox;
        private readonly CheckBox _spaceActsAsEnterCheckBox;
        private readonly CheckBox _advancedCheckBox;
        private readonly Label _summaryLabel;
        private readonly ToolTip _toolTip;

        public AliasEditorForm(Func<Topomatic.Cad.View.CadView> viewProvider = null)
        {
            Text = "Псевдокоманды";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(900, 520);
            Size = new Size(1120, 680);
            Font = SystemFonts.MessageBoxFont;

            _table = CreateTable();
            _bindingSource = new BindingSource();
            _grid = new DataGridView();
            _filterTextBox = new TextBox();
            _logEnabledCheckBox = new CheckBox();
            _quickInputEnabledCheckBox = new CheckBox();
            _spaceActsAsEnterCheckBox = new CheckBox();
            _advancedCheckBox = new CheckBox();
            _summaryLabel = new Label();
            _toolTip = new ToolTip();

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
                RowCount = 5,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 2
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
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

            _logEnabledCheckBox.Text = "Вести лог";
            _logEnabledCheckBox.AutoSize = true;
            _logEnabledCheckBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _logEnabledCheckBox.Margin = new Padding(12, 3, 0, 0);
            _logEnabledCheckBox.Checked = PluginSettings.IsLogEnabled();
            _logEnabledCheckBox.CheckedChanged += delegate { SaveLogEnabledSetting(); };
            _toolTip.SetToolTip(_logEnabledCheckBox, "Включить запись диагностического лога в AppData. По умолчанию лог отключен.");
            top.Controls.Add(_logEnabledCheckBox, 2, 0);
            top.SetRowSpan(_logEnabledCheckBox, 2);

            _advancedCheckBox.Text = "Расширенно";
            _advancedCheckBox.AutoSize = true;
            _advancedCheckBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _advancedCheckBox.Margin = new Padding(12, 3, 0, 0);
            _advancedCheckBox.CheckedChanged += delegate { UpdateAdvancedMode(); };
            top.Controls.Add(_advancedCheckBox, 3, 0);
            top.SetRowSpan(_advancedCheckBox, 2);

            top.Controls.Add(new Label
            {
                Text = "Файл",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 4, 8, 0)
            }, 0, 1);

            var configPathLabel = new Label
            {
                Text = AliasStore.GetConfigPath(),
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 4, 0, 0)
            };
            _toolTip.SetToolTip(configPathLabel, AliasStore.GetConfigPath());
            top.Controls.Add(configPathLabel, 1, 1);

            root.Controls.Add(top, 0, 0);

            var inputOptions = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 6, 0, 2),
                WrapContents = true
            };

            _quickInputEnabledCheckBox.Text = "QuickInput для псевдокоманд";
            _quickInputEnabledCheckBox.AutoSize = true;
            _quickInputEnabledCheckBox.Checked = PluginSettings.IsQuickInputEnabled();
            _quickInputEnabledCheckBox.CheckedChanged += delegate { SaveKeyboardInputSettings(); };
            _toolTip.SetToolTip(
                _quickInputEnabledCheckBox,
                "Открывать собственный ввод псевдокоманд у курсора.");
            inputOptions.Controls.Add(_quickInputEnabledCheckBox);

            _spaceActsAsEnterCheckBox.Text = "Space действует как Enter";
            _spaceActsAsEnterCheckBox.AutoSize = true;
            _spaceActsAsEnterCheckBox.Margin = new Padding(18, 3, 0, 3);
            _spaceActsAsEnterCheckBox.Checked = PluginSettings.IsSpaceActsAsEnterEnabled();
            _spaceActsAsEnterCheckBox.CheckedChanged += delegate { SaveKeyboardInputSettings(); };
            _toolTip.SetToolTip(
                _spaceActsAsEnterCheckBox,
                "Передавать Space в Robur как штатный Enter: повторять последнюю команду и подтверждать дополнительные запросы.");
            inputOptions.Controls.Add(_spaceActsAsEnterCheckBox);

            root.Controls.Add(inputOptions, 0, 1);

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
            root.Controls.Add(_grid, 0, 2);

            _summaryLabel.AutoSize = true;
            _summaryLabel.Dock = DockStyle.Fill;
            _summaryLabel.Padding = new Padding(0, 6, 0, 6);
            root.Controls.Add(_summaryLabel, 0, 3);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                Padding = new Padding(0, 0, 4, 0)
            };

            buttons.Controls.Add(CreateButton("Закрыть", Close, "Закрыть окно редактора."));
            buttons.Controls.Add(CreateButton("Сохранить", SaveRows, "Записать таблицу в active aliases.json."));
            buttons.Controls.Add(CreateButton("О плагине", ShowAbout, "Показать версию, стадию и диагностические пути плагина."));
            buttons.Controls.Add(CreateButton("Отменить правки", ReloadRows, "Отменить несохраненные изменения и заново загрузить active aliases.json."));
            buttons.Controls.Add(CreateButton("Экспорт", ExportRows, "Сохранить текущую таблицу aliases в отдельный JSON-файл."));
            buttons.Controls.Add(CreateButton("Импорт", ImportRows, "Загрузить aliases из JSON в таблицу без сохранения active config."));
            buttons.Controls.Add(CreateButton("Удалить", DeleteSelectedRows, "Удалить выбранные строки из таблицы."));
            buttons.Controls.Add(CreateButton("Добавить", AddRow, "Добавить новую строку псевдокоманды."));
            buttons.Controls.Add(new Label
            {
                Text = "Версия: " + GetPluginVersion(),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 8, 12, 0),
                Margin = new Padding(4)
            });
            root.Controls.Add(buttons, 0, 4);
        }

        private Button CreateButton(string text, Action action, string toolTipText)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(88, 28),
                Margin = new Padding(4)
            };

            button.Click += delegate { action(); };
            _toolTip.SetToolTip(button, toolTipText);
            return button;
        }

        private void SaveLogEnabledSetting()
        {
            try
            {
                PluginSettings.SetLogEnabled(_logEnabledCheckBox.Checked);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Не удалось сохранить настройку лога",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SaveKeyboardInputSettings()
        {
            try
            {
                PluginSettings.SetKeyboardInputOptions(
                    _quickInputEnabledCheckBox.Checked,
                    _spaceActsAsEnterCheckBox.Checked);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Не удалось сохранить настройки ввода",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
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
            _logEnabledCheckBox.Visible = _advancedCheckBox.Checked;
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
            LoadRows(AliasStore.LoadEntries());
        }

        private void LoadRows(IEnumerable<AliasEntry> entries)
        {
            _table.Clear();

            foreach (var entry in entries ?? Enumerable.Empty<AliasEntry>())
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
            if (!ConfirmDiscardChanges(
                    "Отменить правки",
                    "Отменить несохраненные изменения и перечитать active aliases.json?"))
                return;

            LoadRows();
            RefreshStatuses();
            _table.AcceptChanges();
            Logger.Info("alias editor reloaded rows from '" + AliasStore.GetConfigPath() + "'");
        }

        private bool ConfirmDiscardChanges()
        {
            return ConfirmDiscardChanges(
                "Псевдокоманды",
                "Несохраненные изменения будут потеряны. Продолжить?");
        }

        private bool ConfirmDiscardChanges(string title, string message)
        {
            var changes = _table.GetChanges();
            if (changes != null && changes.Rows.Count > 0)
            {
                var result = MessageBox.Show(
                    this,
                    message,
                    title,
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
            if (!string.IsNullOrEmpty(action.Description))
                return action.Description;

            return action.Title;
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

            AliasStore.SaveEntries(entries);
            Saved = true;
            Logger.Info("alias editor saved popup-only aliases count=" + entries.Count + " path='" + AliasStore.GetConfigPath() + "'");
            MessageBox.Show(
                this,
                "Сохранено. Псевдокоманды доступны в QuickInput сразу, без регистрации в command layer и без перезапуска Robur.",
                "Псевдокоманды",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            RefreshStatuses();
            _table.AcceptChanges();
        }

        private void ExportRows()
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

            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Экспорт псевдокоманд";
                dialog.Filter = "JSON aliases (*.json)|*.json|Все файлы (*.*)|*.*";
                dialog.FileName = "aliases.json";
                dialog.DefaultExt = "json";
                dialog.AddExtension = true;
                dialog.OverwritePrompt = true;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    AliasStore.ExportEntries(dialog.FileName, entries);
                    Logger.Info("alias editor exported aliases count=" + entries.Count + " path='" + dialog.FileName + "'");
                    MessageBox.Show(
                        this,
                        "Псевдокоманды экспортированы.",
                        "Псевдокоманды",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Logger.Error("failed to export aliases to '" + dialog.FileName + "'", ex);
                    MessageBox.Show(
                        this,
                        ex.Message,
                        "Не удалось экспортировать",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void ImportRows()
        {
            if (!ConfirmDiscardChanges())
                return;

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Импорт псевдокоманд";
                dialog.Filter = "JSON aliases (*.json)|*.json|Все файлы (*.*)|*.*";
                dialog.DefaultExt = "json";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                List<AliasEntry> entries;
                try
                {
                    entries = AliasStore.LoadEntriesFromFile(dialog.FileName);
                }
                catch (Exception ex)
                {
                    Logger.Error("failed to import aliases from '" + dialog.FileName + "'", ex);
                    MessageBox.Show(
                        this,
                        ex.Message,
                        "Не удалось импортировать",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                _bindingSource.Filter = string.Empty;
                _filterTextBox.Text = string.Empty;
                LoadRows(entries);
                RefreshStatuses();
                Logger.Info("alias editor imported aliases count=" + entries.Count + " path='" + dialog.FileName + "'");
                MessageBox.Show(
                    this,
                    "Псевдокоманды импортированы в таблицу. Нажмите \"Сохранить\", чтобы записать их в active config.",
                    "Псевдокоманды",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void ShowAbout()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var text = new StringBuilder();
            text.AppendLine("RoburPseudoCommands");
            text.AppendLine();
            text.AppendLine("Версия: " + GetPluginVersion());
            text.AppendLine("Стадия: Stable / 0.8.0 (MapsLeader background restore)");
            text.AppendLine();
            text.AppendLine("DLL:");
            text.AppendLine(assembly.Location);
            text.AppendLine();
            text.AppendLine("Active aliases:");
            text.AppendLine(AliasStore.GetConfigPath());
            text.AppendLine();
            text.AppendLine("Log:");
            text.AppendLine(PluginSettings.IsLogEnabled() ? "Включен" : "Отключен");
            text.AppendLine(Logger.LogPath);
            text.AppendLine();
            text.AppendLine("Settings:");
            text.AppendLine(PluginSettings.SettingsPath);
            text.AppendLine("QuickInput: " + (PluginSettings.IsQuickInputEnabled() ? "Включен" : "Отключен"));
            text.AppendLine("Space как Enter: " + (PluginSettings.IsSpaceActsAsEnterEnabled() ? "Включен" : "Отключен"));
            text.AppendLine("Aliases: только QuickInput, без динамической регистрации команд");
            text.AppendLine("Message filter: " + (KeyInterceptor.IsAttached ? "Подключен" : "Отключен"));
            text.AppendLine();
            text.AppendLine("Aliases в таблице: " + CountVisibleRows());

            MessageBox.Show(this, text.ToString(), "О плагине", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private int CountVisibleRows()
        {
            return _table.Rows
                .Cast<DataRow>()
                .Count(x => x.RowState != DataRowState.Deleted);
        }

        private static string GetPluginVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informational = assembly
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .OfType<AssemblyInformationalVersionAttribute>()
                .FirstOrDefault();

            if (informational != null && !string.IsNullOrWhiteSpace(informational.InformationalVersion))
                return StripSourceRevision(informational.InformationalVersion);

            var version = assembly.GetName().Version;
            return version == null ? "unknown" : version.ToString();
        }

        private static string StripSourceRevision(string version)
        {
            var index = (version ?? string.Empty).IndexOf('+');
            return index < 0 ? version : version.Substring(0, index);
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
            var active = rows.Count(x => GetCell(x, ColStatus) == "Доступна в QuickInput");
            var invalid = rows.Count(x => IsInvalidStatus(GetCell(x, ColStatus)));
            _summaryLabel.Text = string.Format(
                "Всего: {0}; доступны в QuickInput: {1}; ошибок: {2}",
                rows.Count,
                active,
                invalid);
        }

        private string GetStatus(DataRow row, IDictionary<string, int> aliases)
        {
            var alias = GetCell(row, ColAlias);
            var action = GetCell(row, ColAction);
            var command = GetCell(row, ColCommand);

            if (string.IsNullOrEmpty(alias))
                return "Пустое имя";

            if (alias.StartsWith("pseudo_", StringComparison.OrdinalIgnoreCase))
                return "Служебное имя";

            if (!DynamicAliasCommandFactory.IsSupportedAliasName(alias))
                return "Некорректное имя";

            if (aliases.ContainsKey(alias) && aliases[alias] > 1)
                return "Дубликат";

            if (string.IsNullOrEmpty(command))
                return "Нет команды";

            return "Доступна в QuickInput";
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
            return status != "Доступна в QuickInput";
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
