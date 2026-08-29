# RoburPseudoCommands

Плагин псевдокоманд для Topomatic Robur: пользователь вводит короткий alias в командной строке Robur, а плагин запускает связанную команду или action Robur.

## Совместимость

- Robur: проверено в Robur 16.0.
- Target framework: .NET Framework `net48`.
- Зависимости Robur: `Topomatic.ApplicationPlatform`, `Topomatic.Cad.View`, `Topomatic.Controls`.

## Установка

1. Скачайте актуальный `RoburPseudoCommands-*.tpm` из GitHub Releases.
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
2. Добавьте alias вручную или выберите Robur action двойным кликом по ячейке `Команда`/`Action`. При каждом выборе поля command/action и описание обновляются из выбранной action; если `description` отсутствует, используется `title`.
3. Используйте `Импорт`, чтобы загрузить aliases из JSON в таблицу без немедленной записи active config. У каждой записи обязателен непустой `command`; `action` остаётся дополнительным.
4. Нажмите `Сохранить`, чтобы записать таблицу в active `aliases.json`.
5. Используйте `Отменить правки`, чтобы отменить несохранённые изменения и заново загрузить active `aliases.json`.
6. Используйте `Экспорт`, чтобы сохранить текущую таблицу aliases в отдельный readable JSON-файл.

Active aliases и settings сохраняются атомарно. При замене существующего файла предыдущая версия остаётся рядом с ним с суффиксом `.bak`. Если active `aliases.json` повреждён, плагин сообщает путь и не перезаписывает файл автоматически.

## Быстрый ввод и Space как Enter

В редакторе доступны две независимые настройки, обе по умолчанию включены для новой конфигурации и legacy settings без этих полей:

- `Быстрый ввод псевдокоманд` открывает компактный ввод alias у курсора. Первым символом может быть буква или цифра; Enter и Space запускают выбранный alias, включая односимвольный.
- `Space действует как Enter` передаёт Space в активный `CadView` как штатный Enter. Он повторяет последнюю команду, запускает уже введённую команду, подтверждает текущий шаг и завершает команду там же, где это делает физический Enter.

QuickInput открывается только в свободном командном состоянии. Space-as-Enter работает и во время активной команды. Перехват ограничен `CadView`; Ctrl, Alt и Shift отключают преобразование Space для текущего нажатия.

Явно сохранённые значения настроек не перезаписываются. Если `settings.json` повреждён и не читается, оба клавиатурных механизма остаются выключенными по fail-safe правилу.

## Проверка после установки

- В меню Robur есть пункт `Сервис -> Псевдокоманды...`.
- Окно редактора открывается.
- В окне `О плагине` указана актуальная версия плагина и состояние диагностического лога.
- В `О плагине` для текущей сборки указано `0.7.0`, а состояния QuickInput, Space-as-Enter и message filter соответствуют настройкам.
- При последовательном выборе двух разных Robur actions описание строки соответствует последней выбранной action.
- В таблице aliases отображается 34 записи при первом active config.
- Несколько aliases запускают связанные команды Robur после перезапуска.
- При включённом QuickInput односимвольный и многосимвольный alias запускаются по Enter и Space.
- Alias, начинающийся с цифры, открывается через QuickInput с цифрового ряда и NumPad при включённом NumLock.
- При включённом Space-as-Enter Space повторяет, подтверждает и завершает команды так же, как физический Enter.

## Ограничения

- Новые, удалённые или переименованные alias-имена требуют перезапуска Robur.
- Изменение target существующего alias можно применять через `pseudo_reload_aliases`, но при старом кэше Robur может понадобиться перезапуск.
- Bundled `aliases.json` используется только как стартовый preset и не перезаписывает существующий active config.
- Диагностический лог по умолчанию отключён; включите `Вести лог` в окне редактора, если нужен файл диагностики.
- Активный диагностический лог ограничен 5 МБ; при ротации предыдущий файл сохраняется как `RoburPseudoCommands.log.1`, а команда просмотра читает только хвост лога.
- При включённом Space-as-Enter обычный пробел внутри активного `CadView` недоступен; используйте пробел с модификатором или временно отключите настройку.
- QuickInput и Space-as-Enter реализованы process-local message filter, а не глобальным Windows hook; при трёх последовательных ошибках фильтр отключается с fail-open поведением.
- Горячие клавиши по умолчанию не назначаются, чтобы не конфликтовать со штатными назначениями Robur.
- Плагин не создаёт геометрию и не работает напрямую с `DwgEntity` или `DrawingLayer`.

## Сборка из исходников

Требуется установленный Robur 16.0 с DLL SDK. Если Robur установлен не в стандартную папку, передайте путь через `-RoburInstallDir`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-tpm.ps1 -Configuration Release -RoburInstallDir "C:\Program Files\Topomatic Robur Road 16.0"
```

Готовый пакет будет создан в `dist\RoburPseudoCommands-<version>.tpm`.

## Благодарности и источник идеи

Идея компактного popup-ввода команды и запуска по Enter/Space была почерпнута
из проекта [Y-Abramov/QuickCommands](https://github.com/Y-Abramov/QuickCommands).
Проект использовался как поведенческий референс; динамическая регистрация aliases,
маршрутизация Robur actions, host-owned повтор команды, настройки и защитные
механизмы RoburPseudoCommands реализованы и адаптированы отдельно.

## Лицензия

Исходный код RoburPseudoCommands распространяется по лицензии [MIT](LICENSE).
Topomatic Robur, его SDK, библиотеки и товарные знаки в состав проекта не входят
и регулируются условиями их правообладателей.

## Версия

- Текущая версия: `v0.7.0`.
- Стадия: `Stable`.
- Последняя проверенная стабильная версия: `v0.7.0`.
- Точный TPM `0.7.0`, собранный из commit `d78baa7`, проверен пользователем в Robur Genplan 16.0, .NET Framework `net48`.
- Подтверждены загрузка, aliases, QuickInput, Space-as-Enter, редактор и обновление description после повторного выбора action.
- Последний опубликованный GitHub Release: [`v0.7.0`](https://github.com/kh987/RoburPseudoCommands/releases/tag/v0.7.0).

## Robur Docs / API-основание

- `Создание первого модуля` / https://help.topomatic.ru/v9/doku.php?id=developers:tutorial:module
- `Команды и меню` / https://help.topomatic.ru/v9/doku.php?id=developers:tutorial:cmdattribute
- `Ключ "actions"` / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:actions
- `Ключ "menubars"` / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:menubars
- `Работа с иконками меню и элементов` / https://help.topomatic.ru/v9/doku.php?id=developers:references:icons
- `Ключ "hotkeys"` / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:hotkeys
