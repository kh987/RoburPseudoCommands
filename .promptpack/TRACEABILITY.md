# Traceability

Plugin: RoburPseudoCommands
Cycle ID: 2026-09-02-feature-v0.8.0
Updated: 2026-09-20 12:05
Traceability path: D:\Codex\RoburPseudoCommands\.promptpack\TRACEABILITY.md
P2: D:\Codex\RoburPseudoCommands\.promptpack\P2_2026-09-02-feature-v0.8.0.md
P2 status: approved

| ID | Требование P2 | Реализация | Проверка | Evidence | Статус |
|---|---|---|---|---|---|
| R1 | Функциональность псевдокоманд v0.7.0 сохраняется без изменения поведения | Module.cs, DynamicAliasCommandFactory.cs, KeyInterceptor.cs, AliasEditorForm.cs, QuickInputForm.cs (сохраняемое ядро) | offline UI/settings + host smoke dev.9 | offline checks=94 (dev.8) → 76 (S3) → 75 (S4.1); повтор после S4.2–S5 и host smoke pending | Implemented |
| R2 | Обход штатного механизма вызова/регистрации работает на 16.0.62.12 | DynamicAliasCommandFactory.cs + Module.cs (dispatch); S4.1: signature-safe маршрут (RequiresPackedHandler → TryExecute) и тихая отмена (IsCancellation) сохранены | host smoke dev.9: alias на штатную команду | dispatch подтверждён на 16.0.62.12 в acceptance dev.8; offline-проверки ProtectedCommandArguments PASS | Implemented |
| R3 | Команда pseudo_annotation_background_scale сохраняет поведение; built-in alias кф удалён; пользовательский alias работает через стандартный dispatch | AnnotationBackgroundScale.cs (команда реализована); удаление built-in alias — S5 (Module.cs, ветки кф в диспетчере) | smoke dev.9: alias назначен в редакторе, после рестарта команда отрабатывает | acceptance команды dev.8 пройден; удаление alias pending | Planned |
| R4 | Полярные привязки полностью удалены (код, UI, настройки, .plugin, тесты) | S3 завершён: 7 исходников + tests/PolarPatch.Tests удалены (исходники в 41e8658); Module/PluginSettings/AliasEditorForm/csproj/.plugin/Ui.Tests вычищены | host smoke dev.9 (полярные команды не находятся) + offline | build 0/0; Ui.Tests PASS checks=76; grep-absence (polar/полярн) чисто | Implemented |
| R5 | Заплатка кнопок/меню и аварийные маршруты полностью удалены | S4.1 выполнен: ветки нс/IsSafeSettingsAlias удалены (Module.cs, SafeSettingsCommand.cs), emergency-ветка диспетчера удалена, .plugin 5 actions; S4.2 (KeyInterceptor/Form/registry-slim/PluginSettings) pending | offline сборка/тесты + host smoke | build 0/0 (S4.1); Ui.Tests PASS 75; absence нс/pseudo_safe_application_settings чисто | Planned |
| R6 | Выводимый код сохранён до удаления (коммит dev.8) | checkpoint commit 41e8658 до S3 | наличие коммита до первого удаляющего шага | commit 41e8658eacb346b4e25b87f30dd57419326ea3a3 | Verified |
| R7 | Сборка, упаковка и host smoke успешны; пользовательские конфиги принимаются | planned: S5–S6 | build 0/0 + smoke-матрица | pending | Planned |
