# TASK BOARD — KEBAB STREET

**Етап:** 0 — закрито (Owner Approved 2026-11-13). **Активний: Етап 1 — Вертикальний зріз**
**Оновлено:** 2026-11-13
Статуси: `Backlog` → `In Progress` → `Ready for QA` → `QA Verified` → `Done` | `Blocked: D-xx`
Картки: `docs/tasks/<ID>.md` (створює Агент 0 через `/new-task` перед стартом)

## Етап 0

| ID | Завдання | Агент | Залежить | Доказ | Статус |
|---|---|---|---|---|---|
| E0-01 | Старий проєкт → гілка `legacy-perfect-parcel` + тег `perfect-parcel-final`; з `main` прибрати старі `Assets/`, `ProjectSettings/`, `Packages/`; `.agents/`, `.cursorrules`, документи 1.4/1.5 → `docs/archive/`; застосувати пакет 2.0; новий `README.md` | Агент 0 | рішення Власника (дано) | L0 + QA | Done |
| E0-02 | Перевірити CI і секрети ліцензії Unity на старому коді (до видалення) або на порожній збірці; записати результат | platform-engineer | E0-01 | L1 або опис помилки | Done |
| E0-03 | Новий проєкт Unity 6.3 LTS (2D, URP), точний патч у `TECH_STACK.md`; target API 36, min API за D-02; збірки asmdef і папки за `ARCHITECTURE.md` | platform-engineer | E0-02 | L1 | Done (PR #6, `3c08d41`; CI на `main` — запуск 42867369466) |
| E0-04 | CI: на кожен PR — APK + автотести EditMode; на `main` — APK + AAB; артефакти зберігаються; + BUG-011 (перевірити кеш `Library` на дані підпису), BUG-010 (перевірка файлів у корені `Assets/`, `.gitignore` для `Assets/Editor/**`) | platform-engineer | E0-03 | L1 | Done (PR #8, злито 2026-11-11; AAB на `main` — запуск 42953812899) |
| E0-05 | Каркас гри: bootstrap → завантаження → меню → порожня сцена зміни; UI за D-03; сцени створюються Editor-скриптом | gameplay-developer | E0-04, D-03 | L1 | Done (PR #13, злито `c3dbe46`; `main` зелений — запуск 43051610295: 368 тестів, APK + AAB; L3 — E0-12) |
| E0-06 | `KebabStreet.Core`: `ShiftSimulation` з тактом і seed, таймер зміни; перші тести EditMode | gameplay-developer | E0-04 | L2 | Done (PR #9, злито 2026-11-11; запуск 42953848835) |
| E0-07 | Збереження зі `schemaVersion`, резервною копією, відновленням; `IClock`; тести | gameplay-developer | E0-06 | L2 | Done (PR #10, злито `8e8e7b5`; `main` зелений — запуск 43036942256: 330 тестів, APK + AAB) |
| E0-08 | Локалізація за ключами (EN/UK/PL, поки кілька рядків меню) + тест «усі ключі є в усіх мовах» | gameplay-developer + ui-art-designer | E0-05 | L2 | Done (PR #16, злито `c935006`; L3 — E0-12) |
| E0-09 | Інтерфейси `IAdsService`, `IPurchaseService`, `IAnalyticsService` + заглушки лише для Editor/тестів; у релізі кнопки реклами/покупок приховані до Етапу 3 | platform-engineer | E0-06 | L2 | Done (PR #17, злито `f75d4b2`) |
| E0-10 | `docs/design/GAME_DESIGN.md` і `LEVEL_DESIGN.md` для рівнів 1–5 (специфікація станцій, рецептів, клієнтів, конфіги-чернетки) — підготовка Етапу 1 | game-designer | E0-01 | L0 + QA | Done |
| E0-11 | `ART_STYLE_GUIDE.md` (напрям стилю, палітра, персонажі, правило образів) + варіанти для D-09 (AI-генерація / художник: вартість, права) | ui-art-designer | E0-01 | L0 + QA | Done |
| E0-12 | Встановити APK на Samsung через `adb`, скриншоти меню і порожньої зміни, logcat без `Exception` | qa-release | E0-05 | L3 | Done (L3 — QA в PR #20; L4 — Власник сам перевірив 5 пунктів 2026-11-12) |
| E0-13 | Ворота Етапу 0 (`/phase-gate 0`) + Власник сам запускає гру | qa-release + Власник | усі вище | L4 | Done (ворота пройдено 2026-11-13, Owner Approved 2026-11-13; ADR-024) |
| E0-14 | Зміцнення CI: BUG-013 (тести на тому ж образі, що й збірка; повернути перевірку `Packages`), BUG-014 (сторонні дії закріпити SHA коміту), BUG-015 (AAB не вивантажувати публічним артефактом або визнати псевдонім нетаємним) | platform-engineer | E0-04 | L1 | Done (PR #15, злито `3b23bdf`) |
| E0-15 | Збереження: BUG-025 (сервіс сам блокує `Save` після `Unavailable`/`NewerVersion` до наступного успішного `Load`, тест), BUG-026 (пошкоджений `.bak` при справному основному — у карантин). **До E0-05** | gameplay-developer | E0-07 | L2 | Done (PR #12, злито `488bb5d`) |
| E0-16 | Порожній екран на телефоні: UI не відображається (BUG-029 P0, BUG-030 P3); автотест «UI з'явився»; лог переходів сцен | gameplay-developer | E0-12 | L3 | Done (PR #20, злито `ba77abc`; `main` зелений — запуск 43087614701; BUG-029/030 Verified Fixed) |

## Етап 1 — Вертикальний зріз · In Progress (відкрито 2026-11-13; S1-01…S1-07 злито в `main` PR #29, коміт `3964084`)

### Виконані завдання
| ID | Завдання | Агент | Статус |
|---|---|---|---|
| S1-01 | Гриль і стан м'яса в Core | gameplay-developer (fable) | Done (злито у складі PR #29) |
| S1-02 | Збирання замовлення: піта, салат, соуси, видача | gameplay-developer (fable) | Done (злито у складі PR #29) |
| S1-03 | Клієнти: терпіння, черга, прохання, оплата | gameplay-developer (fable) | Done (злито у складі PR #29) |
| S1-04 | Рівні 1–5 у JSON, завантажувач, валідація | gameplay-developer (sonnet) | Done (злито у складі PR #29) |
| S1-05 | Бот-тест: рівні 1–5 розв'язні, детерміновано | gameplay-developer (sonnet) | Done (злито у складі PR #29) |
| S1-06 | Результат зміни й зірки в Core | gameplay-developer (sonnet) | Done (злито у складі PR #29) |
| S1-07 | Звіт балансу за ботом + дані навчання | game-designer (sonnet) | Done (злито у складі PR #29) |
### Активні та в черзі
| ID | Завдання | Агент | Статус |
|---|---|---|---|
| S1-08 | Game-обв'язка зміни, пауза, №27/№26 | gameplay-developer (sonnet) | QA Verified (PR #32, L2) |
| S1-09a | Екран зміни: каркас, таймер, гриль | gameplay-developer (opus) | QA Verified (PR #33, L2) |
| S1-09b | Екран зміни: прилавок, клієнти | gameplay-developer (opus) | QA Verified (PR #34, L2) |
| S1-09c | Екран зміни: збирання, видача | gameplay-developer (opus) | QA Verified (PR #35, L2) |
| S1-10 | Екран результату і вибір рівнів зі збереженням | gameplay-developer (sonnet) | QA Verified (PR #36, L2) |
| S1-11 | Навчання від дядька Алі | gameplay-developer (sonnet) | In Progress |

Далі (картки створюються окремо): екран результату й зірок, навчання (завантажувач `tutorial.json` + UI, BUG-036), Game-обв'язка симуляції з UI зміни, плейсхолдер-графіка і звуки, плейтест, пороги балансу після відповіді Власника (№26, №27).
