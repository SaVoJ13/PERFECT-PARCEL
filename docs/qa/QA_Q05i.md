# QA Q-05i — Вібрація (PR #68, ADR-030 з уточненням)

Дата: 2026-11-15. Перевірив: Агент 5. Рівень: L2 (код, тести, лог CI, маніфест APK). L3 на пристрої: НЕ ПЕРЕВІРЕНО: телефон відключено.
Перевірено коміт HEAD edad430 (відрізняється від 4fec29a лише DECISIONS.md і журналом картки; код ідентичний).

## Вердикт: QA Verified (L2) з умовою
Умова: CI-запуск pull_request на HEAD edad430 (run 43676460546) на момент перевірки in_progress; Агент 0 має переконатись, що він зелений, перед злиттям. Зелений доказ: run 43675665793 (workflow_dispatch) на 4fec29a, код той самий.

## Таблиця
| Пункт | Результат | Доказ |
|---|---|---|
| Core, Resources/Config, .github, Packages, ProjectSettings не змінені | є | `git diff --stat main...HEAD` (14 файлів: Game/Flow, Game/Views, Tests, docs); порожній diff для цих тек |
| Assets/Plugins/** відсутній, VIBRATE не додано | є | `ls Assets/Plugins` - немає; у diff немає .androidlib |
| AndroidHapticsService лише під `#if UNITY_ANDROID && !UNITY_EDITOR` | є | код: Device-клас в #if; в редакторі Create() = NullHapticsService |
| Немає падінь без Activity/View | є | try/catch навколо всього, при помилці _failed=true і одне LogWarning; винятки в ігровий потік не виходять |
| HapticsRouter: 2/с (0.5 с на подію реального часу), тапи 80 мс, злиття 50 мс | є | Haptics.cs; тести Cue_RealtimeEvents_AtMostTwoPerSecond, Cue_UserTaps_NotThrottledAsRealtime, Cue_SameInstant_StrongBeatsLightButNotViceVersa |
| Вимкнено ks.settings.haptics | є | HapticsHub.Install за замовчуванням читає GameSettings.Shared.HapticsEnabled; тест HapticsHub_DefaultEnabledFollowsSettings |
| Пауза без вібрації | є | SfxRouter.Play викликає Cue лише при !_paused; тест SfxRouter_Paused_NoHaptics |
| Звук SfxRouter не змінено | є | зміна лише додає виклик Cue перед існуючою умовою; умова повернення Play не торкнута; SfxTests не змінювались (diff Tests: лише HapticsTests) |
| Тести не послаблено | є | у diff Tests лише додані файли (+233, 0 видалень) |
| EditMode >= 1603 | є | editmode-results.xml: total=1603 passed=1603 failed=0; HapticsTests 15/15 |
| APK: перелік дозволів без VIBRATE | є | розбір бінарного AndroidManifest.xml з APK артефакта 43675665793: лише android.permission.INTERNET, рядка VIBRATE немає (aapt недоступний; перевірено пошуком рядків у маніфесті) |
| Офіційна документація: performHapticFeedback без VIBRATE | є | https://developer.android.com/develop/ui/views/haptics/haptic-feedback (доступ 2026-11-15): "Unlike other haptic APIs, using HapticFeedbackConstants with a View doesn't require the VIBRATE permission"; також `Vibrator` вимагає дозвіл |
| PERMISSIONS.md і TECH_STACK.md правдиві | є | відповідають коду й документації; INTERNET чесно позначено як НЕ ПЕРЕВІРЕНО щодо джерела |
| Вібрація на пристрої | НЕ ПЕРЕВІРЕНО | телефон відключено |

## Зауваги (не блокують)
- P3: ADR-030 у DECISIONS.md має суперечливі рядки (початковий про VIBRATE і уточнення). Уточнення додано, суперечність задокументована; можна лишити.
- P3: константи Android (VIRTUAL_KEY=1, LONG_PRESS=0, CONFIRM=16) звірені з пам'яті, посилання на сторінку констант у PERMISSIONS.md; самі значення я окремо з офіційної сторінки не завантажував (сторінка велика). Реальну поведінку покаже L3.
- P3: стара вимога картки «VIBRATE єдиний новий дозвіл» замінена рішенням Агента 0; відображено в журналі.
- INTERNET присутній у APK, джерело невідоме (не з цього завдання); потрібно з'ясувати до Етапу 3.
- Не перевірено: відчуття/сила вібрації, поведінка на Samsung SM-S928B при вимкненому системному «вібровідгуку».
