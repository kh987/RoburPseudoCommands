﻿# Workflow State

Plugin: RoburPseudoCommands
Cycle ID: 2026-09-02-feature-v0.8.0
Updated: 2026-09-20 15:12
State path: D:\Codex\RoburPseudoCommands\.promptpack\WORKFLOW_STATE.md

## Lifecycle
Current version: 0.8.0-debug
Current stage: Debug
Last stable: v0.7.0
Nearest gate: Debug → Stabilization

## Workflow
Change type: feature
Process profile: Deep
Review level: extended
Test level: extended
P2 status: approved
Draft revision: n/a
Draft status: n/a
Current step: Debug-стадия открыта (GATE_APPLIED Dev → Debug, 2026-09-20); работа — verification/diagnostics/defect fixing без новой функциональности
Status: awaiting-user

## Profile modules
SYSTEM_UI.md: read — фактическое чтение, Prompt Pack SMB (сессии 2026-09-18/2026-09-20)
SYSTEM_GEOMETRY.md: read — фактическое чтение, Prompt Pack SMB (сессии 2026-09-18/2026-09-20)

## Evidence
Build: passed — dev.9 Release 0/0 (S5/S6); пересборка с суффиксом 0.8.0-debug выполнена при APPLY (0/0)
Deploy: passed — dev.9.tpm чистая установка, DLL загружена (2026-09-20 12:23); артефакт 0.8.0-debug будет собран при следующей поставке
Runtime: passed — smoke-матрица S6 полностью ок (пользователь, 2026-09-20, Robur Genplan 16.0.62.12); лог без exception/error
UI: passed — offline checks=49; host: редактор/About проверены в smoke
Geometry: passed — маска мультивыноски через alias кф: 2,0/1,05, Recreate, save/reopen (пользователь)
Docs: pending — карточка/README отражают v0.7.0; синхронизация до Debug → Stabilization

## Cursor
Last completed action: APPLY подтверждённого перехода Dev → Debug (2026-09-20): version 0.8.0-debug, stage Debug; обновлены P2 (lifecycle), WORKFLOW_STATE, csproj InformationalVersion, About-строка; evidence не повышался переходом (статусы уже Verified по факту S6)
Next action: Debug-стадия — синхронизация карточки PLUGIN_RoburPseudoCommands.md и README с составом 0.8.0 (подготовка отсечки Debug → Stabilization); запуск за пользователем

## Open blockers
- none

## Deferred prerequisites
- Синхронизация карточки PLUGIN_RoburPseudoCommands.md и README с составом 0.8.0 — до Debug → Stabilization
- Прогон promptpack_validator.py на машине с Python — перед release route (локально интерпретатор недоступен)
- Перенос полярного и меню/аварийного кода в будущие плагины — код зафиксирован в commit 41e8658; сами проекты вне скоупа цикла
- Hardcoded «Стадия: Debug / …» в About (AliasEditorForm) — сверять/чистить перед Stable по SYSTEM_UI
- Наблюдение: в копии лога dev.9 нет строк «annotation background scale applied» (лог скопирован сразу после рестарта) — не блокирует; перезаписать копию по итогам следующей host-сессии

## Known limitations
- Новые, удалённые или переименованные имена alias требуют перезапуска Robur (унаследовано из 0.7.0)

## References
P2: D:\Codex\RoburPseudoCommands\.promptpack\P2_2026-09-02-feature-v0.8.0.md
Workflow history: pending (GATE_APPLIED не записывался: history не используется активным проектом)
Traceability: D:\Codex\RoburPseudoCommands\.promptpack\TRACEABILITY.md
Learning candidates: pending
Plugin card: \\smb\job\99_Болванка\02_Топоматик\Нейросети\PLAGINS\PLUGIN_RoburPseudoCommands.md
README: D:\Codex\RoburPseudoCommands\README.md
