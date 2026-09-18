using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RoburPseudoCommands
{
    internal sealed class EmergencyCommandForm : Form
    {
        private readonly TextBox _search;
        private readonly ListBox _commands;
        private readonly Label _status;
        private readonly List<Item> _allItems;

        public EmergencyCommandForm()
        {
            Text = "Аварийный запуск команд Robur";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(620, 500);
            MinimumSize = new Size(480, 340);
            Font = SystemFonts.MessageBoxFont;
            KeyPreview = true;

            _search = new TextBox { Dock = DockStyle.Top };
            _commands = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _status = new Label { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(4), AutoEllipsis = true };

            Controls.Add(_commands);
            Controls.Add(_search);
            Controls.Add(_status);

            _allItems = BuildItems();
            _search.TextChanged += delegate { RefreshItems(); };
            _commands.DoubleClick += delegate { AcceptSelected(); };
            _commands.KeyDown += OnCommandsKeyDown;
            Shown += delegate { _search.Focus(); RefreshItems(); };
            KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Escape) Close();
            };
        }

        public event Action<string> CommandAccepted;

        private static List<Item> BuildItems()
        {
            var actions = ActionResolver.GetActions();
            var actionByCommand = actions
                .Where(x => !string.IsNullOrWhiteSpace(x.Command))
                .GroupBy(x => x.Command, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            return EmergencyCommandRegistry.GetCommandNames()
                .Select(command =>
                {
                    RoburActionInfo action;
                    actionByCommand.TryGetValue(command, out action);
                    return new Item(
                        command,
                        action == null ? string.Empty : action.Title,
                        action == null ? string.Empty : action.Description);
                })
                .OrderBy(x => x.Command, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void RefreshItems()
        {
            var query = (_search.Text ?? string.Empty).Trim();
            var filtered = _allItems
                .Where(x => query.Length == 0 || x.SearchText.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .Take(500)
                .ToArray();

            _commands.BeginUpdate();
            try
            {
                _commands.Items.Clear();
                _commands.Items.AddRange(filtered);
                if (_commands.Items.Count > 0) _commands.SelectedIndex = 0;
            }
            finally
            {
                _commands.EndUpdate();
            }

            _status.Text = "Снимок: " + EmergencyCommandRegistry.Count +
                " команд; показано: " + filtered.Length +
                ". Enter/двойной щелчок — выполнить напрямую.";
        }

        private void OnCommandsKeyDown(object sender, KeyEventArgs args)
        {
            if (args.KeyCode != Keys.Enter) return;
            args.Handled = true;
            args.SuppressKeyPress = true;
            AcceptSelected();
        }

        private void AcceptSelected()
        {
            var item = _commands.SelectedItem as Item;
            if (item == null) return;

            var handler = CommandAccepted;
            Close();
            if (handler != null) handler(item.Command);
        }

        private sealed class Item
        {
            public Item(string command, string title, string description)
            {
                Command = command;
                Title = title;
                Description = description;
                SearchText = command + " " + title + " " + description;
            }

            public string Command { get; private set; }
            public string Title { get; private set; }
            public string Description { get; private set; }
            public string SearchText { get; private set; }

            public override string ToString()
            {
                return string.IsNullOrWhiteSpace(Title) ? Command : Command + " — " + Title;
            }
        }
    }
}