# QA Q-05c (PR #62) — рух клієнтів і HUD

Дата: 2026-11-15. Перевіряв: Агент 5 (QA). Коміт: 26ec61d. Рівень доказу: L2. Вердикт: **QA Verified (L2)**.

## Перевірено (факти)
| Пункт | Результат | Доказ |
|---|---|---|
| diff main...HEAD | 9 файлів: Game/Views (CounterPanel, ShiftScreen, UiColors, UiMotion+.meta), Tests/EditMode/MotionTests+.meta, TASK_BOARD, картка. Core, Config, .github, .claude, Resources/Theme не чіпано | `git diff --stat main...HEAD`, grep по Core/Config/.github/.claude порожній |
| CI HEAD 26ec61d | EditMode tests pass, Build Android APK pass | `gh pr checks 62`, run 43656665298 |
| Кількість тестів | 1443/1443 passed, 0 failed/skipped (було 1426, +17) | рядок `<test-run ... total="1443" passed="1443" failed="0"` у логу |
| Тести не послаблено | У наявних тестових файлах змін немає (лише додано MotionTests, +519 рядків). Нічого не вимкнено | `git diff --stat -- Assets/_Project/Tests` |
| CounterPanel/CoinsShownText | Тепер повертає ціль (`_coinsTarget`); на екрані `CoinsAnimatedText`. Користувачі: NoWordsScreenTests (`"0"`, regex цифр) і MotionTests, усі зелені. UI-бот його не використовує | grep по Assets |
| QB-ALL-16 | `MotionTarget` пише лише style.scale/translate/opacity; колір таймера міняється лише при зміні стану попередження. Розмітка не перераховується | UiMotion.cs, ShiftScreen.UpdateMotion |
| Витоки schedule | `schedule`/`experimental.animation` не використовуються взагалі: рух рахується в `Refresh()` щокадру з `UiMotion.Now`, підписок немає, відписувати нічого | grep, код |
| Ідемпотентність | `MotionTarget.Set` ігнорує зміну < 0,001, лічильник Writes; тест «без дублювання» | MotionTests |
| Статичний Clock/Reduced | SetUp/TearDown у MotionTests скидають Reduced=false і Clock | MotionTests.cs:23-35 |
| <=3 Гц | Shake 2,5 Гц, `Min(ShakeHz, MaxFlashHz)`; пульс таймера 1 Гц | UiMotion.cs |
| Reduced | Усі функції повертають стан спокою; тест для кожної + HUD + відхід/монети | MotionTests:173, 245, 335 |
| Перекриття відходу/приходу | Новий клієнт у слоті скидає `_leaveAt`, `DrawCustomer` перемальовує портрет; відхід лише при <=4 слотів; є тести Leaving/Arriving | CounterPanel.UpdateMotion, MotionTests:385-439 |
| ExpertUiBot | Рух застосовується до внутрішніх `_portrait`/`_heartsRow`/Feedback, а не до кнопки картки; кнопка слота, її позиція й PickingMode не змінюються, тапи по слотах не залежать від анімації. UI-бот L01-L05 і ReadabilityTests у зелених 1443 | код, CI |

## Зауваження (не блокують)
- P3: `CoinsShownText` тепер означає «ціль», а не «що показано»; назва вводить в оману, NoWordsScreenTests перевіряє ціль, а не текст на екрані (аніма-текст теж цифровий). Рекомендація: перейменувати окремою дрібною правкою.
- P3: дихання/тремтіння/пульс йдуть за `realtimeSinceStartup`, тож на паузі клієнти далі дихають і тремтять (таймер на паузі не пульсує). Узгодити з дизайном.
- P3: `UiMotion.Reduced` не підключено до налаштувань (кнопки немає, заявлено у картці); доступність «менше анімацій» для гравця відсутня.
- P3: TearDown повертає `Clock` лямбдою, а не `DefaultClock` (еквівалентно).
- Щокадрові записи style.scale/translate для кожного зайнятого слота (дихання/тремтіння): продуктивність на слабкому телефоні НЕ ПЕРЕВІРЕНО.

## НЕ ПЕРЕВІРЕНО
- L3: НЕ ПЕРЕВІРЕНО: телефон відключено. Візуальна якість (дихання, тремтіння, виїзд/відхід, пульс таймера, набігання монет), кадрова частота, контраст Ember Red на екрані — потрібні скриншоти/відео при підключенні телефона.
- Звук, значок-хмарка, вирази облич, політ страви (QB-SHIFT-10/13/16 частково) не реалізовано, заявлено у картці.
