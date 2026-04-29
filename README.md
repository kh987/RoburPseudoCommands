# RoburPseudoCommands

MVP plugin for Topomatic Robur pseudo commands.

Flow:

1. Edit aliases with `pseudo_edit_aliases` or by editing `aliases.json`.
2. Save existing alias target changes and run `pseudo_reload_aliases`, or restart Robur after adding, deleting, or renaming aliases.
3. Run a short alias such as `L` or `PL` from the Robur command line.
4. The plugin resolves the alias through `aliases.json` and calls the Robur command/action.

No default hotkeys are assigned because Robur Genplan already uses common function keys.

On startup the plugin dynamically registers aliases from `aliases.json` as native Robur commands.
Duplicate commands with a trailing Backspace control character are registered for each alias
to tolerate the first command-line input quirk observed after Robur startup.
Aliases may specify an optional `action` such as `core.id_pline`; when present and no args are used,
the plugin invokes that Robur action instead of directly executing the command function. If `action`
is empty, the plugin scans installed `.plugin` files and tries to resolve an action by the command name.

On first run, the plugin copies bundled `aliases.json` to:

`%AppData%\Topomatic\RoburPseudoCommands\aliases.json`

Edit that user file and run `pseudo_reload_aliases` to reload it.
The editor shows whether an alias is already active or requires a Robur restart.
This is expected: Robur command names are registered during plugin initialization.
The editor hides `Action` and `Args` by default; enable `Расширенно` to edit those fields.

Diagnostic log:

`%AppData%\Topomatic\RoburPseudoCommands\RoburPseudoCommands.log`

Run `pseudo_show_log` to show the path and latest log lines.
On plugin initialization the log includes the plugin version, loaded DLL path, and active aliases file.
The dynamic registration log also includes command names and Unicode code points.
Adding a new alias name requires restarting Robur so the dynamic command type can be rebuilt.

Build with the default Robur install path:

```powershell
dotnet build
```

Or override the Robur installation directory:

```powershell
dotnet build -p:RoburInstallDir="C:\Program Files\Topomatic Robur Road 16.0"
```
