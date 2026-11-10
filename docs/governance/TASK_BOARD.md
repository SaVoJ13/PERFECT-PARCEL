# TASK BOARD — KEBAB STREET

**Етап:** 0 — Новий проєкт і стабільна збірка
**Оновлено:** 2026-11-10
Статуси: `Backlog` → `In Progress` → `Ready for QA` → `QA Verified` → `Done` | `Blocked: D-xx`
Картки: `docs/tasks/<ID>.md` (створює Агент 0 через `/new-task` перед стартом)

## Етап 0

| ID | Завдання | Агент | Залежить | Доказ | Статус |
|---|---|---|---|---|---|
| E0-01 | Старий проєкт → гілка `legacy-perfect-parcel` + тег `perfect-parcel-final`; з `main` прибрати старі `Assets/`, `ProjectSettings/`, `Packages/`; `.agents/`, `.cursorrules`, документи 1.4/1.5 → `docs/archive/`; застосувати пакет 2.0; новий `README.md` | Агент 0 | рішення Власника (дано) | L0 + QA | Done |
| E0-02 | Перевірити CI і секрети ліцензії Unity на старому коді (до видалення) або на порожній збірці; записати результат | platform-engineer | E0-01 | L1 або опис помилки | Done |
| E0-03 | Новий проєкт Unity 6.3 LTS (2D, URP), точний патч у `TECH_STACK.md`; target API 36, min API за D-02; збірки asmdef і папки за `ARCHITECTURE.md` | platform-engineer | E0-02 | L1 | Done (PR #6, `3c08d41`; CI на `main` — запуск 42867369466) |
| E0-04 | CI: на кожен PR — APK + автотести EditMode; на `main` — APK + AAB; артефакти зберігаються; + BUG-011 (перевірити кеш `Library` на дані підпису), BUG-010 (перевірка файлів у корені `Assets/`, `.gitignore` для `Assets/Editor/**`) | platform-engineer | E0-03 | L1 | QA Verified (PR #8, чекає злиття Власником) |
| E0-05 | Каркас гри: bootstrap → завантаження → меню → порожня сцена зміни; UI за D-03; сцени створюються Editor-скриптом | gameplay-developer | E0-04, D-03 | L1 | Backlog |
| E0-06 | `KebabStreet.Core`: `ShiftSimulation` з тактом і seed, таймер зміни; перші тести EditMode | gameplay-developer | E0-04 | L2 | Ready for QA (PR #9, чернетка; після PR #8) |
| E0-07 | Збереження зі `schemaVersion`, резервною копією, відновленням; `IClock`; тести | gameplay-developer | E0-06 | L2 | Backlog (🔥 ВАЖКЕ, Fable) |
| E0-08 | Локалізація за ключами (EN/UK/PL, поки кілька рядків меню) + тест «усі ключі є в усіх мовах» | gameplay-developer + ui-art-designer | E0-05 | L2 | Backlog |
| E0-09 | Інтерфейси `IAdsService`, `IPurchaseService`, `IAnalyticsService` + заглушки лише для Editor/тестів; у релізі кнопки реклами/покупок приховані до Етапу 3 | platform-engineer | E0-06 | L2 | Backlog |
| E0-10 | `docs/design/GAME_DESIGN.md` і `LEVEL_DESIGN.md` для рівнів 1–5 (специфікація станцій, рецептів, клієнтів, конфіги-чернетки) — підготовка Етапу 1 | game-designer | E0-01 | L0 + QA | Done |
| E0-11 | `ART_STYLE_GUIDE.md` (напрям стилю, палітра, персонажі, правило образів) + варіанти для D-09 (AI-генерація / художник: вартість, права) | ui-art-designer | E0-01 | L0 + QA | Done |
| E0-12 | Встановити APK на Samsung через `adb`, скриншоти меню і порожньої зміни, logcat без `Exception` | qa-release | E0-05 | L3 | Backlog |
| E0-13 | Ворота Етапу 0 (`/phase-gate 0`) + Власник сам запускає гру | qa-release + Власник | усі вище | L4 | Backlog |
| E0-14 | Зміцнення CI: BUG-013 (тести на тому ж образі, що й збірка; повернути перевірку `Packages`), BUG-014 (сторонні дії закріпити SHA коміту), BUG-015 (AAB не вивантажувати публічним артефактом або визнати псевдонім нетаємним) | platform-engineer | E0-04 | L1 | Backlog |

## Етап 1 (картки створюються після `Owner Approved` Етапу 0)
Кандидати: гриль і стан м'яса; піта, салат, соуси, видача; клієнти з терпінням і черга; особливі прохання; рівні 1–5 у конфігах; бот-тест; екран результату й зірки; навчання; плейсхолдер-графіка і звуки; плейтест.
