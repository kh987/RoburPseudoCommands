# RoburPseudoCommands

Плагин псевдокоманд для Topomatic Robur: пользователь вводит короткий alias в командной строке Robur, а плагин запускает связанную команду или action Robur.

## Совместимость

- Robur: проверено в Robur 16.0.
- Target framework: .NET Framework `net48`.
- Зависимости Robur: `Topomatic.ApplicationPlatform`, `Topomatic.Cad.View`, `Topomatic.Controls`.

## Установка

1. Скачайте `RoburPseudoCommands-0.5.1.tpm` из GitHub Releases.
2. Установите пакет через менеджер пакетов Robur.
3. Перезапустите Robur.
4. Откройте `Сервис -> Псевдокоманды...`.

TPM содержит:

```text
package.json
bin/RoburPseudoCommands.dll
bin/aliases.json
plugins/RoburPseudoCommands.plugin
icons/
icons/ic_robur_pseudo_commands_*.png
```

При первом запуске плагин создаёт active config:

```text
%AppData%\Topomatic\RoburPseudoCommands\aliases.json
```

Bundled preset содержит 34 aliases. Если active config уже существует, обновление плагина не заменяет его автоматически.

## Команды

| Команда | Где находится | Назначение |
|---|---|---|
| `pseudo_edit_aliases` | `Сервис -> Псевдокоманды...`, командная строка Robur | Открыть редактор aliases. |
| `pseudo_reload_aliases` | Командная строка Robur | Перечитать active `aliases.json`. |
| `pseudo_command` | Командная строка Robur | Ручной ввод alias через prompt. |
| `pseudo_show_log` | Командная строка Robur | Показать путь к диагностическому логу и последние строки. |
| Dynamic aliases из `aliases.json` | Командная строка Robur | Запустить связанную команду или action Robur. |

`pseudo_alias_bootstrap` используется внутренне для прогрева command layer. `pseudo_show_aliases` оставлен скрыто для обратной совместимости, но удалён из публичного `.plugin`, README-сценариев и bundled aliases.

## Редактор

1. Откройте `Сервис -> Псевдокоманды...` или выполните `pseudo_edit_aliases`.
2. Добавьте alias вручную или выберите Robur action двойным кликом по ячейке `Команда`/`Action`.
3. Используйте `Импорт`, чтобы загрузить aliases из JSON в таблицу без немедленной записи active config.
4. Нажмите `Сохранить`, чтобы записать таблицу в active `aliases.json`.
5. Используйте `Отменить правки`, чтобы отменить несохранённые изменения и заново загрузить active `aliases.json`.
6. Используйте `Экспорт`, чтобы сохранить текущую таблицу aliases в отдельный readable JSON-файл.

## Проверка после установки

- В меню Robur есть пункт `Сервис -> Псевдокоманды...`.
- Окно редактора открывается.
- В окне `О плагине` указана версия `0.5.1` и стадия `Stable / 0.5.1`.
- В таблице aliases отображается 34 записи при первом active config.
- Несколько aliases запускают связанные команды Robur после перезапуска.

## Ограничения

- Новые, удалённые или переименованные alias-имена требуют перезапуска Robur.
- Изменение target существующего alias можно применять через `pseudo_reload_aliases`, но при старом кэше Robur может понадобиться перезапуск.
- Bundled `aliases.json` используется только как стартовый preset и не перезаписывает существующий active config.
- Space-as-Enter и Enter для однобуквенного alias штатными command variants не подтверждены.
- Горячие клавиши по умолчанию не назначаются, чтобы не конфликтовать со штатными назначениями Robur.
- Плагин не создаёт геометрию и не работает напрямую с `DwgEntity` или `DrawingLayer`.

## Сборка из исходников

Требуется установленный Robur 16.0 с DLL SDK. Если Robur установлен не в стандартную папку, передайте путь через `-RoburInstallDir`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-tpm.ps1 -Configuration Release -RoburInstallDir "C:\Program Files\Topomatic Robur Road 16.0"
```

Готовый пакет будет создан в `dist\RoburPseudoCommands-0.5.1.tpm`.

## Версия

- Версия плагина: `v0.5.1`.
- Стадия: `Stable`.
- Проверенный host/runtime: Robur 16.0, .NET Framework `net48`.

## Robur Docs / API-основание

- `Создание первого модуля` / https://help.topomatic.ru/v9/doku.php?id=developers:tutorial:module
- `Команды и меню` / https://help.topomatic.ru/v9/doku.php?id=developers:tutorial:cmdattribute
- `Ключ "actions"` / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:actions
- `Ключ "menubars"` / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:menubars
- `Работа с иконками меню и элементов` / https://help.topomatic.ru/v9/doku.php?id=developers:references:icons
- `Ключ "hotkeys"` / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:hotkeys
