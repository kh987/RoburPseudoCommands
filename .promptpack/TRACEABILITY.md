# Traceability

Plugin: RoburPseudoCommands
Cycle ID: 2026-09-02-feature-v0.8.0
Updated: 2026-09-20 12:16
Traceability path: D:\Codex\RoburPseudoCommands\.promptpack\TRACEABILITY.md
P2: D:\Codex\RoburPseudoCommands\.promptpack\P2_2026-09-02-feature-v0.8.0.md
P2 status: approved

| ID | Требование P2 | Реализация | Проверка | Evidence | Статус |
|---|---|---|---|---|---|
| R1 | Функциональность псевдокоманд v0.7.0 сохраняется без изменения поведения | Module.cs, DynamicAliasCommandFactory.cs, KeyInterceptor.cs, AliasEditorForm.cs, QuickInputForm.cs (сохраняемое ядро) | offline UI/settings + host smoke dev.9 | offline checks=94 (dev.8) → 76 (S3) → 75 (S4.1) → 50 (S4.2) → 49 (S5); host smoke pending | Implemented |
| R2 | Обход штатного механизма вызова/регистрации работает на 16.0.62.12 | DynamicAliasCommandFactory.cs + Module.cs (dispatch); snapshot-ядро (TryExecute/IsCancellation) и ProtectedCommandArguments сохранены | host smoke dev.9: alias на штатную команду | dispatch подтверждён на 16.0.62.12 в acceptance dev.8; offline-проверки ProtectedCommandArguments PASS | Implemented |
| R3 | Команда pseudo_annotation_background_scale сохраняет поведение; built-in alias кф удалён; пользовательский alias работает через стандартный dispatch | S5 завершён: [cmd] команда сохранена; built-in ветки кф удалены из Module.cs и AnnotationBackgroundScale.cs; .plugin description без «алиас: кф» | smoke dev.9: alias назначен в редакторе, после рестарта команда отрабатывает | acceptance команды dev.8 пройден; offline: build 0/0, checks=49, кф в исходниках отсутствует; host smoke pending | Implemented |
| R4 | Полярные привязки полностью удалены (код, UI, настройки, .plugin, тесты) | S3 завершён: 7 исходников + tests/PolarPatch.Tests удалены (исходники в 41e8658); Module/PluginSettings/AliasEditorForm/csproj/.plugin/Ui.Tests вычищены | host smoke dev.9 (полярные команды не находятся) + offline | build 0/0; Ui.Tests PASS; grep-absence (polar/полярн) чисто | Implemented |
| R5 | Заплатка кнопок/меню и аварийные маршруты полностью удалены | S4 завершён: S4.1 (нс/entry points, emergency-ветка диспетчера) + S4.2 (KeyInterceptor без emergency/protected, EmergencyCommandForm удалён, registry slim, safeDeleteUndo удалён) | host smoke dev.9 (нс/аварийные не находятся) + offline | build 0/0; Ui.Tests PASS 50; юзер-facing аварийные строки отсутствуют; сохранены только идентификаторы snapshot-ядра (P1-1) | Implemented |
| R6 | Выводимый код сохранён до удаления (коммит dev.8) | checkpoint commit 41e8658 до S3 | наличие коммита до первого удаляющего шага | commit 41e8658eacb346b4e25b87f30dd57419326ea3a3 | Verified |
| R7 | Сборка, упаковка и host smoke успешны; пользовательские конфиги принимаются | S5: build 0/0 (dev.9); TPM собран штатным скриптом и верифицирован (17 entries, icons/, 5 actions, DLL 0.8.0-dev.9); host smoke — S6 | build 0/0 + smoke-матрица (S6) | dist/RoburPseudoCommands-0.8.0-dev.9.tpm; SHA-256 2AABFC765F65B444CF2B16B04114B99B8EB3EADB91DEC6A200C3C47A14EDF35E; smoke pending | Implemented |
