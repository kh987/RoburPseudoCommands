# Workflow State

Plugin: RoburPseudoCommands
Cycle ID: 2026-09-02-feature-v0.8.0
Updated: 2026-09-20 12:16
State path: D:\Codex\RoburPseudoCommands\.promptpack\WORKFLOW_STATE.md

## Lifecycle
Current version: 0.8.0-dev.9
Current stage: Dev
Last stable: v0.7.0
Nearest gate: Dev → Debug

## Workflow
Change type: feature
Process profile: Deep
Review level: extended
Test level: extended
P2 status: approved
Draft revision: n/a
Draft status: n/a
Current step: S6 (host smoke 16.0.62.12) — артефакт и ручной сценарий подготовлены, ожидание пользователя
Status: awaiting-user
Execution mode: automatic-dev — поручение пользователя 2026-09-20: подшаги S3.1–S5 выполнены, достигнута граница поручения (подготовка host-проверки S6). Дальнейшее — только по действию пользователя.

## Profile modules
SYSTEM_UI.md: read — фактическое чтение, Prompt Pack SMB (сессии 2026-09-18/2026-09-20)
SYSTEM_GEOMETRY.md: read — фактическое чтение, Prompt Pack SMB (сессии 2026-09-18/2026-09-20)

## Evidence
Build: passed — dev.9 Release 0/0 против Robur Genplan 16.0; DLL product 0.8.0-dev.9, file 0.8.0.0
Deploy: stale — dev.8 установлен на reproduction PC; dev.9 ожидает установки (S6)
Runtime: stale — acceptance dev.8 (2026-09-18) неприменим к dev.9; повтор по smoke-матрице S6
UI: passed — offline Ui.Tests PASS checks=49 (dev.9); host UI-проверка — S6
Geometry: stale — визуальная проверка маски dev.8; повтор через alias кф в S6
Docs: pending — карточка/README отражают v0.7.0; синхронизация до Debug → Stabilization

## Cursor
Last completed action: S5 завершён (12:16): built-in alias кф удалён (Module.cs, AnnotationBackgroundScale.cs), .plugin description без «алиас: кф», InformationalVersion 0.8.0-dev.9, About dev.9; TPM собран и верифицирован; build 0/0; Ui.Tests PASS 49. Внутренний post-implementation контроль инвариантов пройден (grep-absence по всем трём комплексам, состав diff соответствует P2)
Next action: пользователь устанавливает dist/RoburPseudoCommands-0.8.0-dev.9.tpm на reproduction PC, назначает alias кф → pseudo_annotation_background_scale через редактор, перезапускает Robur и прогоняет smoke-матрицу S6 (сценарий передан в чате 2026-09-20)

## Open blockers
- none

## Deferred prerequisites
- Перенос полярного и меню/аварийного кода в будущие плагины — код зафиксирован в commit 41e8658; сами проекты вне скоупа цикла
- Копия RoburPseudoCommands.log с тестовой машины — сохранить по итогам S6 (критерий smoke)
- Синхронизация карточки PLUGIN_RoburPseudoCommands.md и README с составом 0.8.0 — до Debug → Stabilization
- Прогон promptpack_validator.py на машине с Python — перед release route (локально интерпретатор недоступен)
- Hardcoded «Стадия: Dev / …» в About (AliasEditorForm) — сверять/чистить перед Stable по SYSTEM_UI

## Known limitations
- Новые, удалённые или переименованные имена alias требуют перезапуска Robur (унаследовано из 0.7.0)

## References
P2: D:\Codex\RoburPseudoCommands\.promptpack\P2_2026-09-02-feature-v0.8.0.md
Workflow history: pending
Traceability: D:\Codex\RoburPseudoCommands\.promptpack\TRACEABILITY.md
Learning candidates: pending
Plugin card: \\smb\job\99_Болванка\02_Топоматик\Нейросети\PLAGINS\PLUGIN_RoburPseudoCommands.md
README: D:\Codex\RoburPseudoCommands\README.md
