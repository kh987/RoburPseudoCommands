# P2 — RoburPseudoCommands v0.7.0 Stable

Дата: 2026-08-30
Базовая версия: `v0.6.0 Stable`
Текущая версия: `v0.7.0`
Стадия: `Stable`; exact Stable TPM проверен пользователем в Robur Genplan 16.0
Функции `v0.7.0` заморожены; разрешены исправления ошибок, чистка, документация и ручные проверки.

## 1. Контекст

В `v0.6.0 Stable` aliases регистрируются как динамические команды Robur через
`PluginHostInitializator.GetTypes()`, динамические методы с `[cmd]` и
`PluginFactory.RegisterFunction(...)`.

У штатного command-line/dynamic-input слоя Robur выявлены ограничения:

- alias подтверждается Enter, но не Space;
- односимвольный alias может отображаться в dynamic input, но не запускаться по
  Enter до дополнительного левого клика мышью;
- варианты command id с завершающими Space/CR/LF не исправляют submit behavior;
- поведение зависит от внутреннего command layer и кэша Robur.

[Y-Abramov/QuickCommands](https://github.com/Y-Abramov/QuickCommands) подтвердил работоспособность альтернативного подхода:
перехват первой буквы до command line, собственный popup и запуск по Enter/Space.
Его повтор последней команды основан на собственной истории плагина и не
гарантирует совпадение с фактически последней командой Robur.

## 2. Цель

Реализовать два независимых отключаемых механизма:

1. **QuickInput для aliases** — собственный компактный ввод у курсора, где Enter
   и Space одинаково подтверждают alias, включая односимвольный.
2. **Space = штатный Enter** — когда QuickInput закрыт, передавать Space в
   активный `CadView` как настоящее нажатие Enter, включая активную команду.
   Повтор и текущий command-state выбирает сам Robur из своей внутренней истории,
   независимо от способа первоначального запуска команды.

Плагин не хранит собственную историю для реализации повтора.

## 3. Среда и совместимость

- Основной host: Topomatic Robur.
- Поддерживаемая версия MVP: Robur 16.0 после ручной проверки.
- Проверяемые редакции: Robur Road 16.0 и Robur Genplan 16.0.
- Следующая непроверенная версия: Robur 17.0 — `unknown`.
- Target runtime: .NET Framework; TargetFramework: `net48`; C#: `7.3`.
- SDK profile: `C:\Program Files\Topomatic Robur Road 16.0`.
- Зависимости: `Topomatic.ApplicationPlatform`, `Topomatic.Cad.View`,
  `Topomatic.Controls`, WinForms.
- Новые сторонние зависимости не добавляются.

## 4. Идентификаторы проекта

- Техническое имя: `RoburPseudoCommands`.
- Папка: `D:\Codex\RoburPseudoCommands`.
- Project: `RoburPseudoCommands.csproj`.
- Assembly/DLL: `RoburPseudoCommands.dll`.
- Namespace: `RoburPseudoCommands`.
- Module: `RoburPseudoCommands.Module`.
- Plugin host: `RoburPseudoCommands.ModulePluginHost`.
- `.plugin`: `RoburPseudoCommands.plugin`.
- Существующие command/action ids и menu placement не меняются.

## 5. Подключённые SYSTEM-модули

- `SYSTEM_CORE.md`, `SYSTEM_CONFIG.md`, `TEMPLATE_P2.md` — базовые правила,
  среда и P2.
- `SYSTEM_UI.md` — WinForms popup, keyboard input и настройки.
- `SYSTEM_PROJECT.md` — идентификаторы и карточка.
- `SYSTEM_RELEASE.md`, `73_VERSIONING_POLICY.md` — новая minor-функция от
  `v0.6.0 Stable`.
- `74_RELEASE_CHECKLIST.md` — применён для переходов Stabilization -> RC -> Stable.
- `SYSTEM_GEOMETRY.md` не подключается: geometry/model данные не меняются.

## 6. Функциональный контракт

### 6.1. Настройки

В `%AppData%\Topomatic\RoburPseudoCommands\settings.json` добавляются:

- `quickInputEnabled` — собственный ввод aliases у курсора;
- `spaceActsAsEnter` — передача подходящего Space в Robur как Enter.

Правила:

- отсутствующие поля `quickInputEnabled` и `spaceActsAsEnter` трактуются как `true`;
- явно сохранённые значения `true`/`false` не перезаписываются;
- ошибка чтения `settings.json` включает fail-safe: оба клавиатурных механизма `false`;
- существующий `logEnabled` сохраняется;
- настройки применяются без перезапуска Robur;
- в редакторе это два отдельных checkbox с tooltip.

### 6.2. QuickInput для aliases

QuickInput открывается только когда:

- `quickInputEnabled == true`;
- пришёл `WM_KEYDOWN` печатной буквы или цифры;
- нет Ctrl/Alt;
- popup ещё не открыт;
- фокус принадлежит `CadView` или его дочернему control;
- `CadView.IsGettingValue == false`.

Поведение:

1. Первая буква не передаётся dynamic input Robur.
2. Открывается один немодальный popup около курсора/активного `CadView`.
3. Первая буква уже находится в TextBox.
4. Enter или Space подавляются внутри TextBox, закрывают popup и запускают точный
   alias через существующий dispatcher.
5. Для action сохраняется приоритет `InvokeAction`; `Execute` остаётся fallback.
6. Односимвольный alias запускается по первой букве + Enter или Space.
7. Escape или потеря фокуса закрывают popup без запуска.
8. Неизвестный alias не запускается; popup остаётся для исправления.

Ограничение MVP: popup запускает только aliases из активного `aliases.json`.
Произвольные штатные команды Robur из popup не входят в этот P2.

### 6.3. Space как штатный Enter

Механизм активируется только когда:

- `spaceActsAsEnter == true`;
- QuickInput закрыт;
- пришёл `WM_KEYDOWN` Space;
- нет Ctrl/Alt и конфликтующих modifiers;
- фокус принадлежит `CadView` или его дочернему control;
- состояние `CadView.IsGettingValue` не ограничивает Space-as-Enter: преобразование
  действует и во время активной команды.

Поведение:

1. Исходный Space поглощается фильтром.
2. В то же сфокусированное окно асинхронно отправляется корректная пара
   `WM_KEYDOWN/WM_KEYUP` для Enter.
3. Обработку Enter и выбор команды для повтора выполняет Robur.
4. Плагин не определяет и не хранит последнюю команду.
5. При пустой строке Space должен повторить ту же команду, что пустой Enter.
6. Если штатный input уже содержит команду и остаётся в свободном command-state,
   Space должен подтвердить её так же, как Enter.
7. Во время активной команды Space подтверждает текущий шаг или завершает команду
   там же, где это делает физический Enter.

`IMessageFilter.PreFilterMessage(ref Message)` используется только для
фильтрации: Microsoft указывает, что переданный `Message` нельзя модифицировать,
поэтому прямое изменение `WParam` не применяется.

### 6.4. Приоритет и fail-open

1. Открытый QuickInput обрабатывает Enter/Space/Escape внутри себя.
2. При закрытом popup свободная буква или цифра может открыть QuickInput.
3. При закрытом popup свободный Space может быть передан Robur как Enter.
4. Во всех остальных случаях сообщение получает Robur без изменений.

- Исключение в message filter приводит к возврату `false`: клавиша уходит Robur.
- После трёх последовательных ошибок фильтр отсоединяется.
- Attach идемпотентен и не создаёт второй filter.
- Это не глобальный Windows hook; действие ограничено process/message loop Robur.
- `SendKeys`, `WH_KEYBOARD_LL` и закрытые controls command line не применяются.

### 6.5. Финальная чистка Stabilization `.3`

- Редактор требует непустой `command`, как и фактическая загрузка `AliasStore`; `action` остаётся дополнительным.
- Active aliases и settings записываются атомарно, предыдущая версия сохраняется как `.bak`.
- Повреждённый `aliases.json` не перезаписывается и не восстанавливается автоматически; пользователю показываются active path и путь ожидаемой backup-копии.
- При недоступном `%AppData%` плагин не пишет пользовательские данные рядом с DLL.
- `KeyInterceptor.Attach` публикует attached instance только после успешной регистрации message filter.
- Диагностический лог ротируется при 5 МБ, хранит одну копию `.1`; просмотр читает не более 64 КБ хвоста и показывает последние 40 строк.
- Пользовательские сценарии, dispatcher, `.plugin`, command/action ids, bundled aliases и формат JSON не изменяются.

### 6.6. Stabilization bugfix `.4`: описание выбранной action

- При каждом явном выборе Robur action поля `command`, `action` и `description` текущей строки синхронизируются с последним выбором.
- Для описания используется `.plugin/actions.description`; если оно пустое, используется `title`.
- Предыдущее описание другой выбранной команды не сохраняется.
- После выбора пользователь может вручную изменить описание до сохранения.
- Picker, `ActionResolver`, aliases JSON, dispatcher, `.plugin`, command/action ids и keyboard input не меняются.

## 7. Сохраняемое поведение

- Dynamic `[cmd]` aliases и `RegisterFunction(...)` не удаляются.
- Старый command-line путь остаётся fallback.
- Новые/удалённые/переименованные dynamic aliases требуют перезапуска Robur.
- Пустой Enter без popup остаётся полностью под управлением Robur.
- Action resolution, import/export, bundled aliases и AppData сохраняются.
- `.plugin`, command/action ids, menu и icons не меняются.
- При выключенных новых настройках поведение совпадает с `v0.6.0`.

## 8. Не входит в P2

- собственный `history.json` и повтор из истории плагина;
- profiles, MCP, ToolBridge и AbrModules;
- Ribbon, palette, context menu и file watcher;
- произвольные штатные команды из QuickInput popup;
- global keyboard hooks и вмешательство в закрытые Robur controls;
- изменение формата `aliases.json`;
- geometry/model/drawing operations.

## 9. Предполагаемые изменения файлов

Новые файлы:

- `KeyInterceptor.cs` — `IMessageFilter`, focus/gates, Space-to-Enter и fail-open.
- `QuickInputForm.cs` — компактный popup aliases.
- `CursorAnchor.cs` — поиск сфокусированного `CadView` и позиция popup.
- при необходимости `KeyboardInputTranslator.cs` — получение символа текущей
  раскладки через Unicode Win32 API.

Изменяемые файлы:

- `Module.cs` — идемпотентный Attach и безопасный общий запуск alias.
- `PluginSettings.cs` — два новых флага с обратной совместимостью.
- `AliasEditorForm.cs` — два checkbox и tooltips.
- `RoburPseudoCommands.csproj` и `package.json` — Dev-версия только после
  подтверждения P2 → Dev.
- `README.md` и карточка — только после подтверждённого runtime-поведения.

Без отдельной причины не изменяются:

- `RoburPseudoCommands.plugin`;
- command/action ids;
- bundled `aliases.json`;
- существующий active config пользователя.

## 10. API-основание и источники

Robur:

- `CadView.IsGettingValue` — публичный read-only гейт активного ввода:
  <https://help.topomatic.ru/v9/doku.php?id=developers:references:topomatic.cad.view.cadview.isgettingvalue>
- `CadView.UserCommand(String)` документирован, но справка не подтверждает, что
  пустая строка повторяет последнюю команду. Этот путь не принимается без
  отдельного живого эксперимента:
  <https://help.topomatic.ru/v9/doku.php?id=developers:references:topomatic.cad.view.cadview.usercommand_system.string>
- Существующие `InvokeAction`/`Execute`, `[cmd]`, `GetTypes()` и
  `RegisterFunction(...)` остаются в проверенном контракте `v0.6.0`.

WinForms/Win32:

- `IMessageFilter.PreFilterMessage`:
  <https://learn.microsoft.com/dotnet/api/system.windows.forms.imessagefilter.prefiltermessage>
- `Application.AddMessageFilter`:
  <https://learn.microsoft.com/dotnet/api/system.windows.forms.application.addmessagefilter>
- `WM_KEYDOWN`:
  <https://learn.microsoft.com/windows/win32/inputdev/wm-keydown>
- `PostMessage`:
  <https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-postmessagew>

Практический референс поведения:

- [Y-Abramov/QuickCommands](https://github.com/Y-Abramov/QuickCommands): `KeyInterceptor.cs`, `QuickInputForm.cs`.
- Переносится контракт поведения, но не ABR/MCP/profile инфраструктура и не
  собственная история QuickCommands.

## 11. Риски

### Высокий

- `PostMessage` Enter может пройти не тот внутренний маршрут Robur, что физический
  Enter, либо попасть не в тот дочерний HWND.
- Неправильный гейт способен поглотить Space активной команды.
- Немодальный popup может конфликтовать со сменой документа или lifecycle
  активного `CadView`.

### Средний

- Фокус может принадлежать вложенному control, а не самому `CadView`.
- Разные редакции/сборки Robur 16.0 могут различаться input lifecycle.
- Кириллица требует `ToUnicodeEx` с `CharSet.Unicode`.
- Ошибка закрытия popup может оставить неверный статический runtime-state.

### Низкий

- Старый settings JSON может не содержать новых полей; для подтверждённого UX-контракта они получают default `true`, но повреждённый JSON даёт fail-safe `false`.
- UI checkbox может расходиться с отсоединённым после ошибок filter; About или
  диагностика должны показывать runtime status.

## 12. План реализации после подтверждения

1. Поднять Dev metadata до `0.7.0-dev`, не меняя Stable tag `v0.6.0`.
2. Добавить settings с безопасными значениями по умолчанию.
3. Реализовать минимальный `KeyInterceptor` только с focus/gates,
   диагностикой и Space-to-Enter.
4. Собрать Dev DLL и вручную доказать равенство пустого Enter и Space после
   разных способов запуска команды.
5. Только после успеха Space-to-Enter добавить QuickInput popup.
6. Проверить односимвольные и многосимвольные aliases по Enter/Space.
7. Release build/TPM выполнять только после завершения Dev-проверок.

## 13. Критерии готовности MVP

- Проект собирается для `net48` против согласованного Robur 16.0 SDK.
- Новые настройки по умолчанию включены для новой конфигурации и legacy settings без этих полей; явно сохранённые значения сохраняются.
- При включённом `spaceActsAsEnter` пустой Space и пустой Enter повторяют одну и
  ту же фактически последнюю команду после запуска через:
  - Ribbon;
  - menu;
  - штатную command line;
  - dynamic alias;
  - QuickInput alias;
  - другой plugin, если Robur считает его команду повторяемой.
- QuickInput запускает многосимвольный alias по Enter и Space.
- QuickInput запускает односимвольный alias по Enter и Space без клика мышью.
- QuickInput запускает односимвольный и многосимвольный alias, начинающийся с цифры.
- Escape и клик вне popup отменяют ввод.
- При включённом `spaceActsAsEnter` активный `CadView.IsGettingValue` получает
  эквивалент Enter: Space подтверждает текущий шаг или завершает команду.
- Ctrl/Alt combinations, dialogs, editor и console не перехватываются.
- Ошибка filter не блокирует дальнейший ввод Robur.
- Повторная инициализация не создаёт несколько message filters.
- Existing active aliases/settings не перезаписываются.

## 14. Матрица ручной проверки Robur

| Сценарий | Enter | Space | Ожидаемый результат |
|---|---:|---:|---|
| Последняя команда запущена из Ribbon | повтор | повтор | одна команда |
| Последняя команда запущена из menu | повтор | повтор | одна команда |
| Последняя команда введена в Robur | повтор | повтор | одна команда |
| Последняя команда запущена dynamic alias | повтор | повтор | одна команда |
| QuickInput: `PL` | запуск | запуск | один target/action |
| QuickInput: односимвольный `L` | запуск | запуск | без клика мышью |
| QuickInput: `1` и `12` | запуск | запуск | цифровой ряд и NumPad с NumLock |
| Команда ожидает точку/значение | подтверждение | подтверждение | Space действует как физический Enter |
| Фокус в editor/dialog/console | штатно | штатно | filter не вмешивается |
| Ctrl/Alt + Space | штатно | штатно | комбинация не перехвачена |
| Новые настройки выключены | штатно | штатно | поведение `v0.6.0` |

Отдельно проверить:

- Robur Road 16.0 и Robur Genplan 16.0;
- смену документа и пересоздание `CadView`;
- включение/выключение настроек без перезапуска;
- DLL path и version в `О плагине`.

## 15. Отсечки и стадийность

Предыдущая опубликованная стабильная точка:

- commit `98ad11b Add logging toggle`;
- tag `v0.6.0`;
- GitHub Release `v0.6.0`.

Текущая проверенная Stable-точка:

- версия `v0.7.0`;
- exact Stable artifact собран из commit `d78baa7`;
- TPM проверен пользователем в Robur Genplan 16.0;
- tag и GitHub Release `v0.7.0` оформляются отдельно.

Новый цикл:

```text
v0.6.0 Stable
  -> P2 v0.7.0-dev
  -> Dev
  -> Debug
  -> Stabilization
  -> RC
  -> Stable v0.7.0
```

До выполненного ручного подтверждения exact Stable TPM было нельзя:

- называть функцию рабочей или Stable;
- заменять release `v0.6.0`;
- публиковать TPM/GitHub Release;
- включать keyboard interception по умолчанию;
- обновлять карточку как завершённую функцию.

## 16. Подтверждённые отсечки и результат

P2 и переход в Dev были подтверждены:

```text
P2 утверждаю. Отсечку P2 -> Dev подтверждаю. Реализуй MVP.
```

Mini-P2 для поведения Space во время активной команды также подтверждён и
реализован. Пользователь вручную проверил QuickInput, повтор последней команды,
подтверждение шага и завершение активной команды; результат: всё работает.

Переход `Dev -> Debug` подтверждён 2026-08-29. Носители версии Debug:

- package: `0.7.0` (числовой формат TPM);
- assembly/file: `0.7.0.0`;
- informational/UI: `0.7.0`;
- последняя стабильная версия остаётся `v0.6.0`.

Переход `Debug -> Stabilization` подтверждён 2026-08-29. Новые функции для
`v0.7.0` после этой отсечки не добавляются.

Mini-P2 Stabilization bugfix подтверждён 2026-08-29: назначенный alias,
начинающийся с цифры, должен открывать QuickInput так же, как alias с буквы.
Исправление ограничено распознаванием первого символа как буквы или цифры;
Space-as-Enter, регистрация aliases, `.plugin`, настройки и формат данных не меняются.

Mini-P2 UI/defaults подтверждён 2026-08-29 без возврата из Stabilization:

- стартовый размер редактора увеличивается, layout допускает перенос при DPI scaling;
- длинный путь к active aliases не вытесняет checkbox `Расширенно`;
- QuickInput и Space-as-Enter включены по умолчанию для новой/legacy конфигурации;
- явно сохранённые значения не перезаписываются;
- повреждённый settings-файл оставляет keyboard interception выключенным;
- `.plugin`, aliases, command/action ids и функциональная логика перехвата не меняются.

Финальная чистка `0.7.0-stabilization.3` подтверждена 2026-08-29 в объёме
пунктов 1.1–1.5 и 2.1–2.4 итогового аудита. Точная Release-сборка и ручная
runtime-проверка артефакта `.3` являются отдельными воротами; до их прохождения
стадия остаётся `Stabilization`.

Mini-P2 `0.7.0-stabilization.4` подтверждён 2026-08-30 после runtime-проверки
`.3`: при повторном выборе команды описание оставалось от предыдущей action.
Исправление ограничено синхронизацией description в редакторе. Release-сборка
итерации `.4` прошла с 0 warnings / 0 errors; точный TPM проверен пользователем
в Robur Genplan 16.0, включая повторный выбор action и обновление description.

Переход `Stabilization -> RC` подтверждён 2026-08-30 после финального аудита,
обязательной чистки, Release build `.4` и пользовательской runtime-проверки
точного TPM в Robur Genplan 16.0. В RC разрешены только исправления release blockers.

Точный TPM RC.1 проверен пользователем в Robur Genplan 16.0:
загрузка, aliases, QuickInput, Space-as-Enter, редактор и обновление description
работают корректно. Эта проверка не заменяет runtime-gate будущего Stable TPM.

Подготовка Stable artifact разрешена пользователем 2026-08-30 после успешной
проверки RC.1. Смена metadata на `0.7.0` не считается формальным переходом
в Stable, пока exact Stable TPM не проверен в Robur Genplan 16.0.

Точный Stable TPM `0.7.0`, собранный из commit `d78baa7`, проверен пользователем
2026-08-30 в Robur Genplan 16.0. Подтверждены загрузка, aliases, QuickInput,
Space-as-Enter, редактор, выбор action и обновление description. Переход
`RC -> Stable` завершён; стадия `v0.7.0` — `Stable`. Публикация tag и GitHub
Release остаётся отдельной операцией.
