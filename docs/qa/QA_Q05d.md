# QA Q-05d: гриль і дошка (пара, дим, політ, «+N», монети) - PR #63

Дата: 2026-11-15. Коміт: f751487. Рівень доказу: L2 (CI). L3: НЕ ПЕРЕВІРЕНО: телефон відключено, скриншотів і logcat немає.

## Вердикт: QA Verified (L2), візуальна якість - НЕ ПЕРЕВІРЕНО (L3)

## Перевірено
| Пункт | Результат | Доказ |
|---|---|---|
| Файли diff | Лише Game/Views (AssemblyPanel, CounterPanel, FxLayer, GrillPanel, ShiftScreen, UiMotion), Tests/EditMode/FxTests, docs (TASK_BOARD, картка). Core, Config, Theme, .github, .claude не чіпано | git diff --stat main...HEAD |
| Тести не послаблено | У Tests лише 2 нові файли (FxTests.cs + .meta), існуючі тести не змінено | diff --stat Tests |
| CI на HEAD | Run 43660498417, headSha f751487, success; testcasecount=1463 passed=1463 failed=0 skipped=0. pr checks: EditMode pass; APK на момент перевірки pending | gh run view, лог |
| .meta | Є для FxLayer.cs і FxTests.cs | diff |
| Пул і ліміт | 5 пулів 40+30+24+8+8 = 110 <= 150, елементи створюються в конструкторі; Take() відкидає зайве з лічильником | FxLayer.cs |
| Алокації щокадру | Update/Take/Start працюють з масивами struct, без new/LINQ/лямбд; монети 3-8 за виплату, рядок "+N" - разова алокація на оплату (прийнятно) | код |
| Seed | System.Random(seed), seed = driver.Seed | FxLayer, ShiftScreen |
| Тапи | Root, усі частинки і Label - PickingMode.Ignore | код |
| Лише transform/opacity | Через MotionTarget; visibility замість display | код |
| Reduced | Steam/Smoke/Fly/монети повертають false; «+N» без підйому; Drop/DonePop/Pulse/Shake спокійні | UiMotion/FxLayer |
| Частоти | WarnPulse 2 Гц, shake обмежено MaxFlashHz (3), DonePulse 0,8 Гц | код |
| Пауза | Emission step = clamp(dt) = 0 на паузі, бо Now стоїть; частинки не створюються; Update рухається за Now | GrillPanel, UiMotion |
| Витоки | Планувальників/підписок немає; шар - дитина root екрана, зникає з ним; Clear() є | код |
| Core/геймплей | Не змінено; Payment лише читає події Served | diff |

## Зауваження (не блокують)
- P3: `UiMotion` має статичний стан паузи. Вихід з екрана під час паузи лишає `Paused=true` до наступного `new ShiftScreen` (ResetPause у конструкторі) або підміни Clock. Зараз Now читає лише екран зміни, тож ризику немає, але нові споживачі Now (меню) заморозяться. Рекомендація: скидати паузу при виході зі зміни.
- P3: тести, що створюють панелі без ShiftScreen після тесту з паузою, залежать від порядку (CI зелений).
- P2 (заявлено розробником, не реалізовано): шари страви стопкою, звуки, вібрація, політ готової страви до клієнта, чорні плями; лічильник монет не синхронізовано з приходом монет. QB-пункти SHIFT-13/18/23 і частково SHIFT-14 не закриті повністю.
- Пара/дим: емісія спрацьовує на кожен слот (3-6/с на слот); сумарно пул обмежено 40/30, тож при заповненні пул відкидає нові (видимих провалів не перевірено, L3).
- НЕ ПЕРЕВІРЕНО: UI-бот L01-L05 і ReadabilityTests окремо (входять до 1463 зелених, окремо не відкривав), вигляд на пристрої, продуктивність на телефоні.
