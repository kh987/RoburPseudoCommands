# RoburPseudoCommands

Плагин псевдокоманд для Topomatic Robur: пользователь запускает связанную команду Robur через компактный QuickInput popup у курсора, набирая короткий alias. В редакторе можно выбрать action для заполнения записи, но при запуске alias исполняется связанная команда. Опционально Space работает как Enter в активном чертёжном виде, повторяя и подтверждая команды. Отдельная команда изменяет коэффициент перекрытия фона маски выбранных мультивыносок Robur так, что он переживает штатное пересоздание текста.

## Совместимость

- Robur: поддерживается Robur 16.0. Проверено: Robur Genplan 16.0 (сборка 16.0.62.12) — полный smoke для `0.8.0-dev.9` и адресные проверки кандидата `0.8.0-debug`; сборка `0.8.0-rc.1` выполнена 2026-09-24, в host ещё не проверялась. Robur Genplan 16.0 для стабильной `v0.7.0`; ранние версии дополнительно проверялись в Robur Road 16.0 и учебной версии.
- Не проверено: Robur 17 (unknown).
- Target framework: .NET Framework `net48`.
- Зависимости Robur: `Topomatic.ApplicationPlatform`, `Topomatic.Cad.View`, `Topomatic.Controls`, `Topomatic.Dwg`, `Topomatic.Dwg.Layer`, `Topomatic.Maps`; `Lib.Harmony` 2.4.2 (поставляется в пакете).

Совместимость по имени пакета не заявляется: поддержка редакции подтверждается только фактической проверкой в ней.

## Установка

1. Скачайте `RoburPseudoCommands-*.tpm` из GitHub Releases.
2. Установите пакет через менеджер пакетов Robur (Topomatic Package Manager).
3. Перезапустите Robur.
4. Откройте `Сервис -> Псевдокоманды...`.

Последний опубликованный релиз — `v0.7.0`. Версия `0.8.0` находится на стадии RC и публично не публиковалась.

TPM содержит:

```text
package.json
bin/RoburPseudoCommands.dll
bin/0Harmony.dll
bin/Harmony-LICENSE.txt
bin/aliases.json
plugins/RoburPseudoCommands.plugin
icons/
icons/ic_robur_pseudo_commands_*.png
```

## Настройки пользователя

При первом запуске плагин создаёт active config:

```text
%AppData%\Topomatic\RoburPseudoCommands\aliases.json
%AppData%\Topomatic\RoburPseudoCommands\settings.json
```

Bundled preset из пакета содержит 34 aliases и используется только как стартовый: уже существующий active config при обновлении плагина не заменяется. `aliases.json` и `settings.json` пишутся атомарно; предыдущая версия остаётся рядом с суффиксом `.bak`. Если файл повреждён и не читается, плагин сообщает путь и действует по fail-safe правилу (клавиатурные механизмы выключены); неизвестные ключи из старых версий настроек игнорируются.

`settings.json`:

- `logEnabled` — диагностический лог, по умолчанию выключен;
- `quickInputEnabled` — QuickInput popup, по умолчанию включён;
- `spaceActsAsEnter` — Space как Enter, по умолчанию включён;
- `annotationBackgroundScale` — последний применённый коэффициент маски, по умолчанию `1.05`.

Диагностический лог: `%AppData%\Topomatic\RoburPseudoCommands\RoburPseudoCommands.log`, ограничен 5 МБ с одной ротацией `.1`.

## Команды

| Команда | Где находится | Назначение |
|---|---|---|
| `pseudo_edit_aliases` | `Сервис -> Псевдокоманды...` | Открыть редактор aliases. |
| `pseudo_command` | Командная строка Robur | Ручной ввод alias через prompt. |
| `pseudo_reload_aliases` | Командная строка Robur | Перечитать active `aliases.json`. |
| `pseudo_show_log` | Командная строка Robur | Показать путь к диагностическому логу и последние строки. |
| `pseudo_annotation_background_scale` | Alias из редактора (например, `кф`) или командная строка Robur | Коэффициент перекрытия фона выбранных мультивыносок. |

Alias'ы из `aliases.json` запускаются через QuickInput popup (первая буква или цифра в свободном командном состоянии) либо через `pseudo_command`. Прямой ввод alias в командной строке Robur, как в версиях до 0.8.0, не используется: динамическая регистрация alias-команд убрана, изменения alias'ов применяются сразу после `Сохранить`, без перезапуска Robur.

`pseudo_show_aliases` оставлен в коде скрыто для обратной совместимости, но удалён из публичного `.plugin` и bundled aliases.

## Коэффициент маски мультивыноски

1. Назначьте alias на `pseudo_annotation_background_scale` в редакторе (например, `кф`) — команду можно найти двойным кликом по колонке `Команда`.
2. Выберите одну или несколько мультивыносок Robur и вызовите alias.
3. Введите коэффициент от `1,00` до `5,00` (по умолчанию подставляется последний применённый).
4. Коэффициент расширяет фон-маску текста мультивыноски и сохраняется при штатном пересоздании текста, а также после сохранения и повторного открытия чертежа.

Работает только с мультивыносками Robur (`MapsLeaderEntity`); непомеченные мультивыноски не изменяются.

## Редактор

1. Откройте `Сервис -> Псевдокоманды...`.
2. Добавьте alias вручную или выберите Robur action двойным кликом по ячейке `Команда`/`Action`. Описание строки берётся из `description` выбранной action (fallback — `title`) и всегда соответствует последнему выбору.
3. `Импорт` загружает aliases из JSON в таблицу без записи active config; у каждой записи обязателен непустой `command`.
4. `Сохранить` записывает таблицу в active `aliases.json` и делает alias'ы доступными в QuickInput сразу.
5. `Отменить правки` откатывает несохранённые изменения; `Экспорт` сохраняет таблицу в readable JSON.

В редакторе — две независимые настройки, обе включены по умолчанию для новой конфигурации:

- `Быстрый ввод псевдокоманд` — popup alias у курсора; первым символом может быть буква или цифра (цифровой ряд и NumPad поддерживаются).
- `Space действует как Enter` — передаёт Space в активный `CadView` как штатный Enter: повторяет последнюю команду, подтверждает шаг, завершает команду.

QuickInput открывается только в свободном командном состоянии; Space-as-Enter работает и во время активной команды. Перехват ограничен `CadView`; Ctrl, Alt и Shift отключают преобразование Space для текущего нажатия. Явно сохранённые значения настроек не перезаписываются.

## Проверка после установки

- В меню Robur есть пункт `Сервис -> Псевдокоманды...`, окно редактора открывается.
- `О плагине` показывает актуальную версию и стадию (например, `0.8.0-rc.1`), путь фактически загруженной DLL и состояние лога — сверьте DLL path с установленной версией.
- Alias запускается через QuickInput сразу после `Сохранить` (без перезапуска Robur).
- Alias на штатную команду открывает её; для `options`, `dsettings`, `smdx_manager` используется безопасный сигнатурный запуск.
- Коэффициент маски: применяется к выбранным мультивыноскам, сохраняется при изменении текста/положения и после save/reopen; при `2,0` маска заметно шире, чем при `1,05`.
- При включённом Space-as-Enter Space повторяет, подтверждает и завершает команды так же, как физический Enter.

Реально выполненные проверки: полный ручной smoke функциональности в Robur Genplan 16.0.62.12 выполнен для dev.9 (включая отсутствие удалённой функциональности), офлайн-набор UI/settings тестов — 49 проверок. Для установленного 0.8.0-debug.tpm пользователь 2026-09-23 подтвердил установку, About, применение сохранённого alias без перезапуска и три адресных отказных сценария (быстрый Escape/смена фокуса при открытии popup, отказ записи settings.json, обработка нескольких мультивыносок при проблеме с одним объектом) без выявленных проблем; полный функциональный smoke этого TPM отдельно не заявлен. История проверок прошлых версий — в GitHub Releases.

## Ограничения

- Alias запускается только через QuickInput popup или `pseudo_command`; ввода alias в командной строке Robur нет.
- После запуска Robur или переключения модели QuickInput начинает принимать ввод после первого клика в область чертежа, когда фокус переходит в `CadView`. Пользователь подтвердил это поведение и оставил его без изменения.
- При включённом Space-as-Enter обычный пробел внутри активного `CadView` недоступен; используйте пробел с модификатором или временно отключите настройку.
- Перехват реализован process-local message filter, а не глобальным hook; при трёх последовательных ошибках фильтр отключается (fail-open).
- Команда коэффициента работает только с мультивыносками Robur (`MapsLeaderEntity`); значение коэффициента хранится в extension dictionary чертежа.
- Диагностический лог по умолчанию отключён; включите `Вести лог` в редакторе при диагностике.
- Горячие клавиши по умолчанию не назначаются.
- Плагин не создаёт геометрию; изменяет свойства существующих примитивов (маска) и настройки.
- Версия `0.8.0` — RC: техническая сборка не означает Stable; для регулярного использования доступна `v0.7.0`.

## Сборка из исходников

Требуется установленный Robur 16.0 с DLL SDK. Если Robur установлен не в стандартную папку, передайте путь через `-RoburInstallDir`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-tpm.ps1 -Configuration Release -RoburInstallDir "C:\Program Files\Topomatic Robur Genplan 16.0"
```

Скрипт собирает Release-конфигурацию и создаёт `dist\RoburPseudoCommands-<version>.tpm` с `icons/`-записью и числовой `package.json.version`.

## Благодарности и источник идеи

Идея компактного popup-ввода команды и запуска по Enter/Space была почерпнута
из проекта [Y-Abramov/QuickCommands](https://github.com/Y-Abramov/QuickCommands).
Проект использовался как поведенческий референс; маршрутизация Robur actions,
host-owned повтор команды, настройки и защитные механизмы RoburPseudoCommands
реализованы и адаптированы отдельно.

## Лицензия

Исходный код RoburPseudoCommands распространяется по лицензии [MIT](LICENSE).
Topomatic Robur, его SDK, библиотеки и товарные знаки в состав проекта не входят
и регулируются условиями их правообладателей.

## Версия

- Текущая версия: `0.8.0-rc.1` (package `0.8.0`).
- Стадия: `RC`.
- Последняя проверенная стабильная версия: `v0.7.0`.
- Последний собранный TPM — `0.8.0-rc.1` (собран 2026-09-24, в host ещё не проверялся); предыдущий кандидат `0.8.0-debug` частично проверен в Robur Genplan 16.0.62.12 (установка, About, применение alias без перезапуска); полный функциональный smoke выполнен для dev.9.
- Последний опубликованный GitHub Release: [`v0.7.0`](https://github.com/kh987/RoburPseudoCommands/releases/tag/v0.7.0).

## Robur Docs / API-основание

- `Создание первого модуля` / https://help.topomatic.ru/v9/doku.php?id=developers:tutorial:module
- `Команды и меню` / https://help.topomatic.ru/v9/doku.php?id=developers:tutorial:cmdattribute
- `Ключ "actions"` / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:actions
- `Ключ "menubars"` / https://help.topomatic.ru/v9/doku.php?id=developers:references:core.plugin:menubars
- `Работа с иконками меню и элементов` / https://help.topomatic.ru/v9/doku.php?id=developers:references:icons
- `DwgMText.FillBoxScale` / https://help.topomatic.ru/v9/doku.php?id=developers:references:topomatic.dwg.entities.dwgmtext.fillboxscale
- `DwgMText` / https://help.topomatic.ru/v9/doku.php?id=developers:references:topomatic.dwg.entities.dwgmtext
- `DwgEntity.Regen` / https://help.topomatic.ru/v9/doku.php?id=developers:references:topomatic.dwg.entities.dwgentity.regen_system.eventargs
