# RoburPseudoCommands

Плагин псевдокоманд для Topomatic Robur: пользователь вводит короткий alias в командной строке Robur, а плагин запускает связанную команду или action Robur.

## Требования

- Robur: Robur 16.0; сборка выполнялась против `C:\Program Files\Topomatic Robur Road 16.0`.
- .NET / SDK: .NET Framework `net48`, C# `7.3`, SDK-style project.
- Зависимости: `Topomatic.ApplicationPlatform`, `Topomatic.Cad.View`, `Topomatic.Controls`, `System.Windows.Forms`, `System.Drawing`, `System.Data`, `System.Runtime.Serialization`.

## Установка

- Актуальный TPM: `dist\RoburPseudoCommands-0.5.1.tpm`.
- DLL: `bin/RoburPseudoCommands.dll`.
- `.plugin`: `plugins/RoburPseudoCommands.plugin`.
- Bundled aliases preset: `bin/aliases.json`, 34 aliases.

Структура TPM:

```text
package.json
bin/RoburPseudoCommands.dll
bin/aliases.json
plugins/RoburPseudoCommands.plugin
icons/
icons/ic_robur_pseudo_commands_*.png
```

После установки в меню Robur должен появиться пункт `Сервис -> Псевдокоманды...`.

## Команды

| Команда | Action | Где находится | Назначение |
|---|---|---|---|
| `pseudo_command` | `id_pseudo_command` | Командная строка Robur | Ручной ввод alias через prompt. |
| `pseudo_edit_aliases` | `id_pseudo_edit_aliases` | `Сервис -> Псевдокоманды...`, командная строка Robur | Открыть редактор aliases. |
| `pseudo_reload_aliases` | `id_pseudo_reload_aliases` | Командная строка Robur | Перечитать active `aliases.json`. |
| `pseudo_show_log` | `id_pseudo_show_log` | Командная строка Robur | Показать путь к диагностическому логу и последние строки. |
| `pseudo_alias_bootstrap` | нет публичного action | Внутренняя команда | Прогрев command layer после старта Robur. |
| Dynamic aliases из `aliases.json` | найденный Robur action, если применимо | Командная строка Robur | Запустить связанную команду или action Robur. |

`pseudo_show_aliases` оставлена в коде скрыто для обратной совместимости, но удалена из публичного `.plugin`, README-сценариев и bundled aliases.

## Редактор

1. Откройте `Сервис -> Псевдокоманды...` или выполните `pseudo_edit_aliases`.
2. Добавьте alias вручную или выберите Robur action двойным кликом по ячейке `Команда`/`Action`.
3. При необходимости используйте `Импорт`, чтобы загрузить aliases из JSON в таблицу без немедленной записи active config.
4. Нажмите `Сохранить`, чтобы записать таблицу в active `aliases.json`.
5. Используйте `Отменить правки`, чтобы отменить несохраненные изменения и заново загрузить active `aliases.json`.
6. Используйте `Экспорт`, чтобы сохранить текущую таблицу aliases в отдельный readable JSON-файл.

Active config хранится в `%AppData%\Topomatic\RoburPseudoCommands\aliases.json`. При первом запуске плагин копирует туда bundled `aliases.json`. Для существующих установок обновление bundled preset не заменяет active config автоматически.

## Проверка

- `0.5.1` проверен пользователем в Robur 16.0: TPM устанавливается и работает, новый bundled preset принят, кнопка `Отменить правки` удобна.
- TPM `0.5.1` проверен после сборки: `packageVersion=0.5.1`, `bin/aliases.json` содержит 34 записи, DLL, `.plugin`, `icons/` и PNG-иконки на месте.
- `0.5.0` проверен пользователем: импорт/экспорт aliases, readable JSON, валидация, статусы, `О плагине`, версия в UI.
- `0.4.8` проверен пользователем в Robur 16.0: новые aliases работают после перезапуска без предварительного открытия окна плагина.

Сборка TPM:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-tpm.ps1 -RoburInstallDir "C:\Program Files\Topomatic Robur Road 16.0"
```

## Ограничения

- Новые, удаленные или переименованные alias-имена требуют перезапуска Robur.
- Изменение target существующего alias можно применять через `pseudo_reload_aliases`, но при старом кэше Robur может понадобиться перезапуск.
- `clearcache` документирован Robur, но не считается гарантированной заменой перезапуска для dynamic command types.
- Space-as-Enter и Enter для однобуквенного alias штатными command variants не подтверждены.
- Глобальные keyboard hooks не применять без отдельного P2 и явного согласия.
- Горячие клавиши по умолчанию не назначаются, чтобы не конфликтовать со штатными назначениями Robur.
- Плагин не создаёт геометрию и не работает напрямую с `DwgEntity` или `DrawingLayer`.

## Версия и стадия

- Версия: `v0.5.1` package / `0.5.1` informational version в UI.
- Стадия: `Stable`; stable-check и ручные пункты `RC -> Stable` подтверждены для TPM `0.5.1`.
- Последняя стабильная версия: `v0.5.1`.
- Актуальная отсечка: `dd4b716 Mark 0.5.1 as stabilization`.
- Актуальный TPM: `D:\Codex\RoburPseudoCommands\dist\RoburPseudoCommands-0.5.1.tpm`.

## Robur Docs / API-основание

- `Создание первого модуля` / https://help.topomatic.ru/v9/doku.php?id=developers:tutorial:module
- `Команды и меню` / https://help.topomatic.ru/v9/doku.php?id=developers:tutorial:cmdattribute
- `Ключ "actions"` / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:actions
- `Ключ "menubars"` / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:menubars
- `Работа с иконками меню и элементов` / https://help.topomatic.ru/v9/doku.php?id=developers:references:icons
- `Ключ "hotkeys"` / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:hotkeys
