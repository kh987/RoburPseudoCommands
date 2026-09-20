# Workflow State

Plugin: RoburPseudoCommands
Cycle ID: 2026-09-02-feature-v0.8.0
Updated: 2026-09-20 15:34
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
Current step: Debug-стадия; документация: карточка синхронизирована, README pending
Status: awaiting-user

## Profile modules
SYSTEM_UI.md: read — фактическое чтение, Prompt Pack SMB (сессии 2026-09-18/2026-09-20)
SYSTEM_GEOMETRY.md: read — фактическое чтение, Prompt Pack SMB (сессии 2026-09-18/2026-09-20)

## Evidence
Build: passed — dev.9 Release 0/0 (S5/S6); metadata 0.8.0-debug пересобрана 0/0 при APPLY
Deploy: passed — dev.9.tpm чистая установка, DLL загружена (2026-09-20 12:23)
Runtime: passed — smoke-матрица S6 полностью ок (пользователь, 2026-09-20, Robur Genplan 16.0.62.12); лог без exception/error
UI: passed — offline checks=49; host: редактор/About проверены в smoke
Geometry: passed — маска мультивыноски через alias кф: 2,0/1,05, Recreate, save/reopen (пользователь)
Docs: частично — карточка PLUGIN_RoburPseudoCommands.md синхронизирована 2026-09-20 (BOM+LF, побайтово проверена); README.md всё ещё v0.7.0 — pending

## Cursor
Last completed action: карточка PLUGIN_RoburPseudoCommands.md синхронизирована с составом 0.8.0-debug (команды/actions/архитектура popup-only/ограничения/проверка/версии/решения); попутно исправлен дефект двойного BOM в state/P2/карточке (повторная нормализация добавляла BOM поверх существующего; процедура исправлена: снять все BOM → добавить один)
Next action: синхронизировать README.md с составом 0.8.0; затем — подготовка CHECK Debug → Stabilization (запуски за пользователем)

## Open blockers
- none

## Deferred prerequisites
- Синхронизация README.md с составом 0.8.0 — до Debug → Stabilization
- Прогон promptpack_validator.py на машине с Python — перед release route (локально интерпретатор недоступен)
- Перенос полярного и меню/аварийного кода в будущие плагины — код зафиксирован в commit 41e8658; сами проекты вне скоупа цикла (карточка PLUGIN_RoburCommandFix.md уже создана пользователем)
- Hardcoded «Стадия: Debug / …» в About (AliasEditorForm) — сверять при сменах стадии; чистить перед Stable
- Наблюдение: в копии лога dev.9 нет строк «annotation background scale applied» (лог скопирован сразу после рестарта) — не блокирует; перезаписать копию по итогам следующей host-сессии

## Known limitations
- Alias запускается только через QuickInput popup или `pseudo_command`; прямого ввода alias в командной строке Robur нет (popup-only, 0.8.0). Изменения alias'ов применяются сразу после «Сохранить»/«Перечитать», перезапуск Robur не требуется (формулировки карточки и редактора; runtime-проверка restart-free apply отдельно не выполнялась — smoke делал рестарт)

## References
P2: D:\Codex\RoburPseudoCommands\.promptpack\P2_2026-09-02-feature-v0.8.0.md
Workflow history: pending (GATE_APPLIED не записывался: history не используется активным проектом)
Traceability: D:\Codex\RoburPseudoCommands\.promptpack\TRACEABILITY.md
Learning candidates: pending
Plugin card: \\smb\job\99_Болванка\02_Топоматик\Нейросети\PLAGINS\PLUGIN_RoburPseudoCommands.md
README: D:\Codex\RoburPseudoCommands\README.md
