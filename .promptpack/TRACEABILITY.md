# Traceability

Plugin: RoburPseudoCommands
Cycle ID: 2026-09-02-feature-v0.8.0
Updated: 2026-09-18 09:37
Traceability path: D:\Codex\RoburPseudoCommands\.promptpack\TRACEABILITY.md
P2: D:\Codex\RoburPseudoCommands\.promptpack\P2_2026-09-02-feature-v0.8.0.md
P2 status: approved

| ID | Требование P2 | Реализация | Проверка | Evidence | Статус |
|---|---|---|---|---|---|
| R1 | Функциональность псевдокоманд v0.7.0 сохраняется без изменения поведения | Module.cs, DynamicAliasCommandFactory.cs, KeyInterceptor.cs, AliasEditorForm.cs, QuickInputForm.cs (сохраняемое ядро) | offline UI/settings + host smoke dev.9 | offline checks=94 (dev.8); повтор после S3–S5 pending | Implemented |
| R2 | Обход штатного механизма вызова/регистрации работает на 16.0.62.12 | DynamicAliasCommandFactory.cs + Module.cs (dispatch) | host smoke dev.9: alias на штатную команду | dispatch подтверждён на 16.0.62.12 в acceptance dev.8 | Implemented |
| R3 | Команда pseudo_annotation_background_scale сохраняет поведение; built-in alias кф удалён; пользовательский alias работает через стандартный dispatch | AnnotationBackgroundScale.cs (команда реализована); удаление built-in alias — S5 (Module.cs:256-257, 269, 321-322) | smoke dev.9: alias назначен в редакторе, после рестарта команда отрабатывает | acceptance команды dev.8 пройден; удаление alias pending | Planned |
| R4 | Полярные привязки полностью удалены (код, UI, настройки, .plugin, тесты) | planned: S3 | offline сборка/тесты + host smoke | pending | Planned |
| R5 | Заплатка кнопок/меню и аварийные маршруты полностью удалены | planned: S4 | offline сборка/тесты + host smoke | pending | Planned |
| R6 | Выводимый код сохранён до удаления (коммит dev.8) | checkpoint commit 41e8658 до S3 | наличие коммита до первого удаляющего шага | commit 41e8658eacb346b4e25b87f30dd57419326ea3a3 | Verified |
| R7 | Сборка, упаковка и host smoke успешны; пользовательские конфиги принимаются | planned: S5–S6 | build 0/0 + smoke-матрица | pending | Planned |
