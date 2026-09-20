# Traceability

Plugin: RoburPseudoCommands
Cycle ID: 2026-09-02-feature-v0.8.0
Updated: 2026-09-20 12:35
Traceability path: D:\Codex\RoburPseudoCommands\.promptpack\TRACEABILITY.md
P2: D:\Codex\RoburPseudoCommands\.promptpack\P2_2026-09-02-feature-v0.8.0.md
P2 status: approved

| ID | Требование P2 | Реализация | Проверка | Evidence | Статус |
|---|---|---|---|---|---|
| R1 | Функциональность псевдокоманд v0.7.0 сохраняется без изменения поведения | Module.cs, DynamicAliasCommandFactory.cs, KeyInterceptor.cs, AliasEditorForm.cs, QuickInputForm.cs (сохраняемое ядро) | offline UI/settings + host smoke dev.9 | offline checks=49 (dev.9); smoke 2026-09-20 16.0.62.12: редактор 2 checkbox, QuickInput, Space-повтор, 83 alias'а пользователя приняты — подтверждено пользователем; лог без exception | Verified |
| R2 | Обход штатного механизма вызова/регистрации работает на 16.0.62.12 | DynamicAliasCommandFactory.cs + Module.cs (dispatch); snapshot-ядро (TryExecute/IsCancellation) и ProtectedCommandArguments сохранены | host smoke dev.9: alias на штатную команду | smoke 2026-09-20: alias на штатную команду срабатывает (пользователь); offline-проверки ProtectedCommandArguments PASS | Verified |
| R3 | Команда pseudo_annotation_background_scale сохраняет поведение; built-in alias кф удалён; пользовательский alias работает через стандартный dispatch | S5: [cmd] сохранена; built-in ветки кф удалены; .plugin description без «алиас: кф» | smoke dev.9: alias назначен в редакторе, после рестарта команда отрабатывает | smoke 2026-09-20: alias кф → pseudo_annotation_background_scale работает после рестарта; маска 2,0/1,05, Recreate, save/reopen — подтверждено пользователем; Harmony-патчи в логе dev.9 подключены (maps=16.0.62.12) | Verified |
| R4 | Полярные привязки полностью удалены (код, UI, настройки, .plugin, тесты) | S3 завершён: 7 исходников + tests/PolarPatch.Tests удалены (исходники в 41e8658) | host smoke: полярные команды не находятся | smoke 2026-09-20: pseudo_polar_* не находятся, полярной панели в редакторе нет (пользователь); offline grep-absence чисто | Verified |
| R5 | Заплатка кнопок/меню и аварийные маршруты полностью удалены | S4 завершён: S4.1 + S4.2 (KeyInterceptor, EmergencyCommandForm, registry slim, safeDeleteUndo) | host smoke: нс/аварийные не находятся | smoke 2026-09-20: нс не находится, Ctrl+Shift+F12 не открывает ничего, аварийных строк в About нет (пользователь); лог dev.9 — новый формат без protected/emergency | Verified |
| R6 | Выводимый код сохранён до удаления (коммит dev.8) | checkpoint commit 41e8658 до S3 | наличие коммита до первого удаляющего шага | commit 41e8658eacb346b4e25b87f30dd57419326ea3a3 | Verified |
| R7 | Сборка, упаковка и host smoke успешны; пользовательские конфиги принимаются | S5: build 0/0; TPM верифицирован; S6: smoke пройден | build 0/0 + smoke-матрица | dist/RoburPseudoCommands-0.8.0-dev.9.tpm (SHA-256 2AABFC76…4EDF35E); smoke 2026-09-20 все пункты ок; лог D:\Codex\RoburPseudoCommands\RoburPseudoCommands.log — 0 exception/error; aliases.json/settings.json приняты | Verified |
