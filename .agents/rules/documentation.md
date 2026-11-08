# ПРАВИЛО: СТАНДАРТИ ТА ВЕДЕННЯ ДОКУМЕНТАЦІЇ (DOCUMENTATION STANDARDS)

> **Статус:** Обов'язкове для всіх AI-агентів проєкту PERFECT PARCEL  
> **Версія:** 1.4  
> **Джерело:** Розділ 5 Roadmap 1.4  

---

## 1. ПРИНЦИП «ДОКУМЕНТАЦІЯ ЯК ЄДИНЕ ДЖЕРЕЛО ПРАВДИ»

Документація є обов'язковою складовою розробки PERFECT PARCEL. Код, який не відповідає документації, вважається дефектним. Документація, яка не відповідає коду, вважається застарілою і вимагає негайного оновлення.

---

## 2. ГОЛОВНІ ДОКУМЕНТИ ТА ЇХ РОЗМІЩЕННЯ В `docs/`

| Категорія | Файл | Призначення | Відповідальний |
| :--- | :--- | :--- | :--- |
| **Product** | `docs/product/MASTER_SPEC.md` | Головна специфікація продукту | Agent 0 |
| **Product** | `docs/product/ROADMAP.md` | Етапи та дорожня карта | Agent 0 |
| **Governance** | `docs/governance/CURRENT_STATE.md` | Поточний стан проєкту (Single Source of Truth) | Agent 0 |
| **Governance** | `docs/governance/DECISIONS.md` | Журнал важливих архітектурних рішень (ADR) | Agent 0 |
| **Governance** | `docs/governance/TASK_BOARD.md` | Стан і прогрес завдань | Agent 0 |
| **Governance** | `docs/governance/SESSION_LOG.md` | Хроніка робочих сесій | Agent 0 |
| **Design** | `docs/design/GAME_DESIGN.md` | Ігрові механіки, взаємодії та правила | Agent 1 |
| **Design** | `docs/design/LEVEL_DESIGN.md` | Детальні параметри 30 рівнів | Agent 1 |
| **Design** | `docs/design/ECONOMY_DESIGN.md` | Баланс монет, покращення, нагороди | Agent 1 |
| **Design** | `docs/design/UI_SPECIFICATION.md` | Специфікація 6 екранів, ергономіка | Agent 2 |
| **Design** | `docs/design/ART_STYLE_GUIDE.md` | Візуальний стиль, колірні палітри | Agent 2 |
| **Design** | `docs/design/ASSET_REGISTER.md` | Реєстр графіки/шрифтів/звуків та ліцензій | Agent 2 |
| **Architecture**| `docs/architecture/ARCHITECTURE.md` | Загальна архітектура системи | Agent 0 / Agent 3 |
| **Architecture**| `docs/architecture/TECHNICAL_SPEC.md` | Технічна специфікація та конфігурація | Agent 4 / Agent 0 |
| **Monetization**| `docs/monetization/MONETIZATION.md` | Рекламна модель та сценарії викликів | Agent 4 |
| **Monetization**| `docs/monetization/PRIVACY_CHECKLIST.md`| GDPR, UMP згода, політика конфіденційності | Agent 4 |
| **Android** | `docs/android/ANDROID_BUILD_GUIDE.md` | Інструкція зі збірки APK/AAB та SDK | Agent 4 |
| **QA** | `docs/qa/TEST_PLAN.md` | Комплексний план тестування | Agent 5 |
| **QA** | `docs/qa/TEST_RESULTS.md` | Протоколи та результати тестів | Agent 5 |
| **QA** | `docs/qa/BUG_REPORT.md` | Реєстр помилок (P0–P3) | Agent 5 |
| **QA** | `docs/qa/RELEASE_CHECKLIST.md` | Фінальний контрольний список перед релізом | Agent 5 |

---

## 3. СТАНДАРТ ДОКУМЕНТА `CURRENT_STATE.md`

`CURRENT_STATE.md` є найважливішим документом для відновлення контексту. Він оновлюється наприкінці кожної сесії та після кожної важливої події.

**Обов'язкові розділи:**
1. Поточна фаза проєкту та статус.
2. Остання завершена контрольна точка.
3. Затверджений технологічний стек та версії.
4. Останні коміти та змінені файли.
5. Посилання на ключові документи.
6. Виконані завдання / Завдання в процесі.
7. Відомі помилки та блокери (P0/P1).
8. **Наступні 3 конкретні кроки.**
9. Запитання, що очікують рішення Власника.

---

## 4. СТАНДАРТ ЖУРНАЛУ РІШЕНЬ `DECISIONS.md` (ADR)

Кожне ключове рішення фіксується за схемою:
- **ID:** (наприклад, `ADR-001`)
- **Дата:** РРРР-ММ-ДД
- **Контекст і проблема:** Чому виникло питання.
- **Розглянуті варіанти:** Варіант А проти Варіанта Б (плюси й мінуси).
- **Прийняте рішення:** Що обрано та чому.
- **Ризики та наслідки:** Вплив на проєкт.
- **Хто затвердив:** (Agent 0 / Власник).
- **Зачеплені документи/файли:** Список шляхів.
