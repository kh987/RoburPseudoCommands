# Workflow State

Plugin: RoburPseudoCommands
Cycle ID: 2026-09-02-feature-v0.8.0
Updated: 2026-09-21 18:23
State path: D:\Codex\RoburPseudoCommands\.promptpack\WORKFLOW_STATE.md

## Lifecycle
Current version: 0.8.0-debug
Current stage: Stabilization
Last stable: v0.7.0
Nearest gate: Stabilization → RC

## Workflow
Change type: feature
Process profile: Deep
Review level: extended
Test level: extended
P2 status: approved
Draft revision: n/a
Draft status: n/a
Current step: Stabilization-стадия 0.8.0 (feature freeze применён 2026-09-21); чистка документации и подготовка к 72_FINAL_AUDIT
Status: awaiting-user

## Profile modules
SYSTEM_UI.md: read — фактическое чтение, Prompt Pack SMB (сессии 2026-09-18/2026-09-20)
SYSTEM_GEOMETRY.md: read — фактическое чтение, Prompt Pack SMB (сессии 2026-09-18/2026-09-20)

## Evidence
Build: passed — dev.9 Release 0/0 (S5/S6); metadata 0.8.0-debug пересобрана 0/0 при APPLY; чистка Stabilization 2026-09-21 — build 0/0, Ui.Tests 49 PASS (без нового TPM/deploy)
Deploy: passed — dev.9.tpm чистая установка, DLL загружена (2026-09-20 12:23)
Runtime: passed — smoke-матрица S6 полностью ок (пользователь, 2026-09-20, Robur Genplan 16.0.62.12); лог без exception/error
UI: passed — offline checks=49; host: редактор/About проверены в smoke
Geometry: passed — маска мультивыноски через alias кф: 2,0/1,05, Recreate, save/reopen (пользователь)
Docs: passed — карточка PLUGIN_RoburPseudoCommands.md и README.md синхронизированы с составом 0.8.0-debug (2026-09-20)

## Cursor
Last completed action: чистка Stabilization: опечатки README исправлены («Псевдокоманды», 2 места), строка стадии в About → «Stabilization» (AliasEditorForm.cs); build Release 0/0, Ui.Tests 49 PASS; commit 2f05b43
Next action: стабилизация 0.8.0 — подтвердить restart-free apply alias'ов в следующей host-сессии (или зафиксировать как известное ограничение); перед Stabilization → RC — 72_FINAL_AUDIT и локальный прогон promptpack_validator.py (Python 3.12.10 установлен)

## Open blockers
- none

## Deferred prerequisites
- Прогон promptpack_validator.py (scope promptpack + plugin) — перед release route (Python 3.12.10 установлен локально 2026-09-21)
- Перенос полярного и меню/аварийного кода в будущие плагины — код зафиксирован в commit 41e8658; сами проекты вне скоупа цикла (карточка PLUGIN_RoburCommandFix.md уже создана пользователем)
- Hardcoded «Стадия: …» в About (AliasEditorForm) — синхронизирована со Stabilization 2026-09-21 (commit 2f05b43; попадёт в следующий build, проверить в About при следующей host-сессии); перед Stable убрать/автоматизировать
- Разрешено (CHECK 2026-09-21): в оригинальном RoburPseudoCommands.log строки «annotation background scale applied» есть (7 шт., сессия smoke 2026-09-20); отсутствие строк в копии — артефакт снятия лога сразу после рестарта

## Known limitations
- Alias запускается только через QuickInput popup или `pseudo_command`; прямого ввода alias в командной строке Robur нет (popup-only, 0.8.0). Изменения alias'ов применяются сразу после «Сохранить»/«Перечитать», перезапуск Robur не требуется (формулировки карточки и редактора; runtime-проверка restart-free apply отдельно не выполнялась — smoke делал рестарт)

## References
P2: D:\Codex\RoburPseudoCommands\.promptpack\P2_2026-09-02-feature-v0.8.0.md
Workflow history: pending (GATE_APPLIED не записывался: history не используется активным проектом)
Traceability: D:\Codex\RoburPseudoCommands\.promptpack\TRACEABILITY.md
Learning candidates: pending
Plugin card: \\smb\job\99_Болванка\02_Топоматик\Нейросети\PLAGINS\PLUGIN_RoburPseudoCommands.md
README: D:\Codex\RoburPseudoCommands\README.md
