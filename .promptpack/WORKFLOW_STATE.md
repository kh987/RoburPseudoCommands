# Workflow State

Plugin: RoburPseudoCommands
Cycle ID: 2026-09-02-feature-v0.8.0
Updated: 2026-09-24 13:38
State path: D:\Codex\RoburPseudoCommands\.promptpack\WORKFLOW_STATE.md

## Lifecycle
Current version: 0.8.0
Current stage: Stable
Last stable: v0.8.0 (предыдущая v0.7.0)
Nearest gate: Stable → P2 (плановая работа — новый цикл через 22_WORKFLOW_CHANGE_REQUEST) / Hotfix

## Workflow
Change type: feature
Process profile: Deep
Review level: extended
Test level: extended
P2 status: approved
Draft revision: n/a
Draft status: n/a
Current step: Stable v0.8.0 опубликована (GitHub Release 2026-09-24, опубликованный asset сверен с dist); цикл 2026-09-02-feature-v0.8.0 полностью закрыт
Status: awaiting-user

## Profile modules
SYSTEM_UI.md: read — фактическое чтение, Prompt Pack SMB (сессии 2026-09-18/2026-09-20)
SYSTEM_GEOMETRY.md: read — фактическое чтение, Prompt Pack SMB (сессии 2026-09-18/2026-09-20)

## Evidence
Build: passed — dev.9 Release 0/0 (S5/S6); metadata 0.8.0-debug пересобрана 0/0 при APPLY; чистка Stabilization 2026-09-21 — build 0/0, Ui.Tests 49 PASS; тест-кандидат dist/RoburPseudoCommands-0.8.0-debug.tpm собран 2026-09-22 — 17 entries, ProductVersion 0.8.0-debug, строка «Stabilization» в DLL, SHA-256 a632c5e4606d443fc460b14ad68a7ad54558a1ba9350f6fca65d006d232e2ffa; повторная локальная сборка 2026-09-23 — 0 warning / 0 error, Ui.Tests 49 PASS, TPM не перепаковывался; APPLY Stabilization → RC 2026-09-24 — метаданные 0.8.0-rc.1 (InformationalVersion + About-строка), Release-сборка 0/0, Ui.Tests 49 PASS; RC-артефакт dist/RoburPseudoCommands-0.8.0-rc.1.tpm собран и верифицирован 2026-09-24 (build-tpm.ps1) — 17 entries, package 0.8.0, ProductVersion 0.8.0-rc.1, About «RC / 0.8.0-rc.1» в DLL (строки Stabilization нет), SHA-256 ff3b73904c2a64c869f5ddaa19e18f5e33a776cbb325994c8d504ec21fb39784; APPLY RC → Stable 2026-09-24 — метаданные 0.8.0 (InformationalVersion + About «Stable»), Release-сборка 0/0, Ui.Tests 49 PASS (код идентичен проверенному rc.1 ff3b7390…9784, отличается только версия в метаданных); Stable-артефакт dist/RoburPseudoCommands-0.8.0.tpm собран и верифицирован 2026-09-24 (build-tpm.ps1) — 17 entries, package 0.8.0, ProductVersion 0.8.0, About «Stable / 0.8.0» в DLL (строк rc.1/Stabilization нет), SHA-256 8714fd6343775ee5a09facf3aba9509af6ba3cc099b9f5ecdd81e9cc46ecb2a8; валидатор (--tpm 0.8.0): TPM-осмотр без findings, PLG-VERSION-001 остался форматным (state/card «0.8.0» vs csproj «v0.8.0», P2 «0.8.0 (Stable)» — суть: все источники 0.8.0)
Deploy: passed — dev.9.tpm чистая установка, DLL загружена (2026-09-20 12:23)
Runtime: passed — smoke-матрица S6 полностью ок (пользователь, 2026-09-20, Robur Genplan 16.0.62.12); лог без exception/error; установленный кандидат 0.8.0-debug.tpm — host-сессия 2026-09-23 (пользователь): установка ок, About «Стадия: Stabilization / 0.8.0-debug», restart-free apply и три адресных отказных сценария подтверждены без выявленных проблем; rc.1 (SHA-256 ff3b7390…9784) — host-сессия 2026-09-24: пользователь установил 0.8.0-rc.1.tpm в Robur Genplan 16.0.62.12 и проверил — все ручные сценарии пройдены без ошибок; Stable-TPM 0.8.0.tpm (SHA-256 8714fd63…b2a8) — контрольная установка в Robur Genplan 16.0.62.12 2026-09-24: установка и проверка — всё работает (пользователь)
UI: passed — offline checks=49; host: редактор/About проверены в smoke; пользователь 2026-09-23 подтвердил быстрый Escape/смену фокуса при открытии popup и отказ записи settings.json на установленном 0.8.0-debug.tpm без выявленных проблем
Geometry: passed — маска мультивыноски через alias кф: 2,0/1,05, Recreate, save/reopen (пользователь, dev.9); адресный сценарий с несколькими мультивыносками и проблемой одного объекта на установленном 0.8.0-debug.tpm проверен пользователем 2026-09-23 без выявленных проблем
Docs: current — P2/TRACEABILITY/README/карточка синхронизированы с фактическим popup-only, версией 0.8.0-rc.1 и её host-подтверждением (2026-09-24)

## Cursor
Last completed action: GitHub Release v0.8.0 опубликован (https://github.com/kh987/RoburPseudoCommands/releases/tag/v0.8.0, тег v0.8.0, asset RoburPseudoCommands-0.8.0.tpm); опубликованный asset скачан и сверен — SHA-256 8714fd6343775ee5a09facf3aba9509af6ba3cc099b9f5ecdd81e9cc46ecb2a8 совпал с dist (2026-09-24)
Next action: цикл 2026-09-02-feature-v0.8.0 полностью закрыт; новая плановая задача — отдельный запуск 22_WORKFLOW_CHANGE_REQUEST.md (открытых задач нет)

## Open blockers
- none

## Deferred prerequisites
- promptpack_validator.py (scope all, с --csproj/--tpm) запущен 2026-09-23 и повторно 2026-09-24 без skip/override: FAIL — PLG-VERSION-001 и PLG-HISTORY-001; 18 WARN. Оценка выполнена в CHECK 2026-09-24: PLG-VERSION-001 — санкционированное §14 отображение (полная версия в InformationalVersion/About, числовые package/Version без суффикса), закрывается при сборке rc.1; PLG-HISTORY-001 — history осознанно не используется проектом; статические WARN (PLG-TRACE-003, PLG-CMD-001/002, PLG-ROBUR-002) — ограничения статанализа, компенсированы host-evidence на кандидате 2026-09-23; PP-LIFE-002 — строка реестра обновляется только через 75_PLUGIN_ALIGNMENT_AUDIT (вне APPLY).
- Адресная host-проверка трёх отказных сценариев завершена пользователем 2026-09-23 на установленном 0.8.0-debug.tpm в Robur Genplan 16.0.62.12 без выявленных проблем; статический путь раннего возврата при ожидающем popup остаётся фактом для оценки в новом RC CHECK.
- Перенос полярного и меню/аварийного кода в будущие плагины — код зафиксирован в commit 41e8658; сами проекты вне скоупа цикла (карточка PLUGIN_RoburCommandFix.md уже создана пользователем)
- Hardcoded «Стадия: …» в About (AliasEditorForm) — синхронизирована со Stable 2026-09-24 при APPLY; автоматизация/вынос остаются deferred (смена стадии требует правки строки и пересборки)
- Разрешено (CHECK 2026-09-21): в оригинальном RoburPseudoCommands.log строки «annotation background scale applied» есть (7 шт., сессия smoke 2026-09-20); отсутствие строк в копии — артефакт снятия лога сразу после рестарта

## Known limitations
- Alias запускается только через QuickInput popup или `pseudo_command`; прямого ввода alias в командной строке Robur нет (popup-only, 0.8.0). Изменения alias'ов применяются сразу после «Сохранить»/«Перечитать», перезапуск Robur не требуется — подтверждено в host 2026-09-23 (пользователь, кандидат 0.8.0-debug)
- После запуска Robur или переключения модели QuickInput начинает принимать ввод после первого клика в область чертежа, когда фокус переходит в CadView; пользователь подтвердил и принял это ограничение 2026-09-23.

## References
P2: D:\Codex\RoburPseudoCommands\.promptpack\P2_2026-09-02-feature-v0.8.0.md
Workflow history: pending (GATE_APPLIED не записывался: history не используется активным проектом)
Traceability: D:\Codex\RoburPseudoCommands\.promptpack\TRACEABILITY.md
Learning candidates: pending
Plugin card: \\smb\job\99_Болванка\02_Топоматик\Нейросети\PLAGINS\PLUGIN_RoburPseudoCommands.md
README: D:\Codex\RoburPseudoCommands\README.md
