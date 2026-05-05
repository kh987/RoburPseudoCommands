# RoburPseudoCommands

Плагин псевдокоманд для Topomatic Robur: пользователь вводит короткий alias в командной строке Robur, а плагин запускает связанную команду или action Robur.

## Требования

- Robur: Robur 16.0; сборка выполнялась против `C:\Program Files\Topomatic Robur Road 16.0`.
- .NET / SDK: .NET Framework `net48`, C# `7.3`, SDK-style project.
- Зависимости: `Topomatic.ApplicationPlatform`, `Topomatic.Cad.View`, `Topomatic.Controls`, `System.Windows.Forms`, `System.Drawing`, `System.Data`, `System.Runtime.Serialization`.

## Установка / подключение

- DLL: `bin/RoburPseudoCommands.dll`.
- `.plugin`: `plugins/RoburPseudoCommands.plugin`.
- Куда положить файлы:
  - при установке TPM используйте пакет `dist\RoburPseudoCommands-0.4.8.tpm`;
  - структура пакета должна содержать:

```text
package.json
bin/RoburPseudoCommands.dll
bin/aliases.json
plugins/RoburPseudoCommands.plugin
icons/
icons/ic_robur_pseudo_commands_*.png
```

- Как проверить, что Robur увидел плагин:
  - в меню Robur должен появиться пункт `Сервис -> Псевдокоманды...`;
  - окно редактора должно показать active aliases в таблице;
  - после перезапуска Robur aliases из active config должны вводиться в командной строке.

## Команды

| Команда | Action | Где находится | Назначение |
|---|---|---|---|
| `pseudo_command` | `id_pseudo_command` | Командная строка Robur | Ручной ввод alias через prompt. |
| `pseudo_edit_aliases` | `id_pseudo_edit_aliases` | `Сервис -> Псевдокоманды...`, командная строка Robur | Открыть редактор aliases. |
| `pseudo_reload_aliases` | `id_pseudo_reload_aliases` | Командная строка Robur | Перечитать active `aliases.json`. |
| `pseudo_show_log` | `id_pseudo_show_log` | Командная строка Robur | Показать путь к диагностическому логу и последние строки. |
| `pseudo_alias_bootstrap` | нет публичного action | Внутренняя команда | Прогрев command layer после старта Robur. |
| Dynamic aliases из `aliases.json` | найденный Robur action, если применимо | Командная строка Robur | Запустить связанную команду или action Robur. |

## Сценарий работы

1. Откройте `Сервис -> Псевдокоманды...` или выполните `pseudo_edit_aliases`.
2. Добавьте alias вручную или выберите Robur action через кнопку `Команда...`.
3. Для изменения назначения существующего alias сохраните настройки и выполните `pseudo_reload_aliases`.
4. После добавления, удаления или переименования alias перезапустите Robur.
5. Введите alias в командной строке Robur.

Active config хранится в `%AppData%\Topomatic\RoburPseudoCommands\aliases.json`. При первом запуске плагин копирует туда bundled `aliases.json`.

## Проверка

- `0.4.8` проверен пользователем в Robur 16.0: новые aliases работают после перезапуска без предварительного открытия окна плагина.
- Пункт `Сервис -> Псевдокоманды...` отображается с иконкой.
- TPM-пакет разворачивается с явной папкой `icons/`.
- `0.4.1` дополнительно проверялся в Robur 16.0: Автомобильные дороги, Генплан, Учебная версия.
- Сборка TPM:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-tpm.ps1 -RoburInstallDir "C:\Program Files\Topomatic Robur Road 16.0"
```

## Известные ограничения

- Новые, удаленные или переименованные alias-имена требуют перезапуска Robur.
- Изменение target существующего alias можно применять через `pseudo_reload_aliases`.
- `clearcache` документирован Robur, но не считается гарантированной заменой перезапуска для dynamic command types.
- Горячие клавиши по умолчанию не назначаются, чтобы не конфликтовать со штатными назначениями Robur.
- Плагин не создает геометрию и не работает напрямую с `DwgEntity` или `DrawingLayer`.

## Версия и стадия

- Версия: `v0.4.8`.
- Стадия: `Stabilization`; проверенная release-точка, но не `Stable`.
- Последняя стабильная: нет формально подтвержденной `Stable`.

## Robur docs / API-основание

- Создание первого модуля / https://help.topomatic.ru/v9/doku.php?id=developers:tutorial:module
- Команды и меню / https://help.topomatic.ru/v9/doku.php?id=developers:tutorial:cmdattribute
- Ключ "actions" / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:actions
- Ключ "menubars" / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:menubars
- Работа с иконками меню и элементов / https://help.topomatic.ru/v9/doku.php?id=developers:references:icons
