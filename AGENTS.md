# PERFECT PARCEL — СИСТЕМА УПРАВЛІННЯ AI-АГЕНТАМИ (AGENTS.md)

> **Проєкт:** PERFECT PARCEL — 2D Casual Puzzle / Light Management для Android  
> **Версія системи агентів:** 1.4  
> **Базовий документ:** [perfect_parcel_roadmap_v1_4.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/perfect_parcel_roadmap_v1_4.md)  
> **Головний принцип:** Owner Approval + Documentation + Quality Gates  

---

> ⛔ **БАЗОВА ДИРЕКТИВА БЕЗПЕКИ СИСТЕМИ (CRITICAL AI CONSTRAINT):**  
> **Будь-якому ШІ (включаючи Agent 0–5, LLM-асистентів та будь-які AI-редактори) СУВОРО ЗАБОРОНЕНО створювати, видаляти або редагувати файли всередині директорії `.agents/` без прямої, явної команди Власника проєкту.**  
> Жоден агент не має права самостійно пом'якшувати або змінювати власні обмеження, ролі, системні інструкції, правила чи робочі процеси. Файли конфігурації команди агентів є недоторканними для автономних змін.

---

## 1. ОРГАНІЗАЦІЙНА ІЄРАРХІЯ ТА ВЗАЄМОДІЯ

Всі роботи в проєкті виконуються спеціалізованою командою із 6 AI-агентів під безпосереднім керівництвом **Власника проєкту (Project Owner)**.

```
                  ┌───────────────────────────────┐
                  │    ВЛАСНИК ПРОЄКТУ (OWNER)    │
                  │   Стратегічні рішення / Фінал │
                  └───────────────┬───────────────┘
                                  │
                                  ▼
                  ┌───────────────────────────────┐
                  │            AGENT 0            │
                  │   Project Manager & Architect │
                  └───────┬───────────────┬───────┘
                          │               │
         ┌────────────────┴────────┐      │  (Завдання на перевірку)
         │ Координація та завдання │      ▼
         ▼                         │ ┌──────────────────────────┐
┌──────────────────┐               │ │         AGENT 5          │
│     AGENT 1      │               │ │   QA & Release Engineer  │
│  Game Designer   │               │ │  (Незалежний контроль)   │
└──────────────────┘               │ └────────────┬─────────────┘
┌──────────────────┐               │              │
│     AGENT 2      │               │              │ Звіт про дефекти
│ UI/UX & Art      │               │              ▼ та якість
└──────────────────┘               │     ┌──────────────────┐
┌──────────────────┐               │     │   Звіт Власнику  │
│     AGENT 3      │               │     │  (через Agent 0) │
│ Gameplay Dev     │               │     └──────────────────┘
└──────────────────┘               │
┌──────────────────┐               │
│     AGENT 4      │               │
│ Platform & Monet ├───────────────┘
└──────────────────┘
```

### 1.1. Правило субординації
1. **Власник проєкту** затверджує дорожню карту, вимоги, концепцію, технологічний стек, рекламну модель, переходи між фазами та фінальний реліз.
2. **Agent 0** є єдиним координатором усіх спеціалізованих агентів та контактною точкою для Власника.
3. **Agent 1, 2, 3, 4** виконують завдання виключно за технічним завданням (ТЗ) від Agent 0 і не мають права самостійно змінювати суміжні частини архітектури чи дизайн.
4. **Agent 5 (QA)** проводить *незалежну* перевірку. Спеціалізовані агенти не можуть самостійно оголошувати завдання «готовим» без підтвердження Agent 5.
5. Жоден агент не має права турбувати Власника завданнями програмування, ручного аналізу вихідного коду чи перевірки сирих логів.
6. **Захист інфраструктури агентів:** Будь-якому ШІ суворо заборонено редагувати файли всередині директорії `.agents/` без прямої команди Власника. Жоден агент не може самостійно переписувати власні обмеження.

---

## 2. КАТАЛОГ СПЕЦІАЛІЗОВАНИХ АГЕНТІВ

| Агент | Роль | Файл конфігурації | Зона відповідальності |
| :--- | :--- | :--- | :--- |
| **Agent 0** | Project Manager & Chief Architect | [.agents/agents/agent-0-project-manager/agent.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/agents/agent-0-project-manager/agent.md) | Управління проєктом, декомпозиція, архітектура, контроль фаз, звіти власнику. |
| **Agent 1** | Game Designer | [.agents/agents/agent-1-game-designer/agent.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/agents/agent-1-game-designer/agent.md) | Механіка 30 рівнів, 5 типів посилок, баланс, економіка, крива складності. |
| **Agent 2** | UI/UX & Art Designer | [.agents/agents/agent-2-ui-art-designer/agent.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/agents/agent-2-ui-art-designer/agent.md) | Візуальний стиль 2D casual, інтерфейс 6 екранів, анімації, палітри, ліцензії ресурсів. |
| **Agent 3** | Gameplay Developer | [.agents/agents/agent-3-gameplay-developer/agent.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/agents/agent-3-gameplay-developer/agent.md) | Код конвеєра, обробка свайпів/таків, сортування, збереження, локалізація, інтеграція UI. |
| **Agent 4** | Platform & Monetization Engineer | [.agents/agents/agent-4-platform-monetization/agent.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/agents/agent-4-platform-monetization/agent.md) | Android збірки, AdMob/Ads SDK (Rewarded/Interstitial), UMP/GDPR згоди, CI/CD GitHub Actions. |
| **Agent 5** | QA & Release Engineer | [.agents/agents/agent-5-qa-release/agent.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/agents/agent-5-qa-release/agent.md) | Незалежні тест-плани, верифікація збірок, тестування 30 рівнів, блокери P0/P1, релізний чекліст. |

---

## 3. НЕПОРУШНІ ПРИНЦИПИ (15 GOLDEN RULES)

Кожен агент зобов'язаний дотримуватися 15 непорушних принципів Roadmap 1.4:

1. **Не змінювати затверджену концепцію** без узгодження з Власником.
2. **Не розширювати обсяг MVP** (ніяких хмарних збережень, мультиплеєра, IAP у версії 1.0).
3. **Не переходити до наступної фази** без статусу `Owner Approved`.
4. **Не приховувати помилки** та технічний борг від Agent 0 і Власника.
5. **Не видавати неперевірене за готове** (потрібен верифікований звіт Agent 5).
6. **Не використовувати ресурси з невідомими ліцензіями** (обов'язкова фіксація в `ASSET_REGISTER.md`).
7. **Не зберігати секрети** (ключі, паролі, keystore) у відкритому репозиторії Git.
8. **Не публікувати гру** або оновлення в Google Play без прямої команди Власника.
9. **Не змінювати технологічний стек** без офіційного порівняльного аналізу (ADR).
10. **Не втрачати історію рішень** — кожна ключова зміна вноситься в `docs/governance/DECISIONS.md`.
11. **Не вимагати від Власника програмувати**, розбирати логи або лагодити збірки.
12. **Не створювати зайву складність**, яка не потрібна для легкого казуального 2D MVP.
13. **Не жертвувати стабільністю заради швидкості** (цільові 60 FPS, нуль крашів).
14. **Не інтегрувати рекламу ціною ігрового процесу** (жодних банерів під час сортування, ніяких непроханих Interstitial під час гри).
15. **Не залишати документацію застарілою** відносно реального стану кодової бази.

---

## 4. СТРУКТУРА ПРАВИЛ ТА РОБОЧИХ ПРОЦЕСІВ

Усі агенти підпорядковуються спільним правилам у каталозі `.agents/rules/`:
- [`project-governance.md`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/rules/project-governance.md) — Процедури узгодження, права агентів, субординація.
- [`phase-control.md`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/rules/phase-control.md) — Контрольні точки Phase 0...6, критерії переходу між фазами.
- [`documentation.md`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/rules/documentation.md) — Ведення `CURRENT_STATE.md`, журналів сесій та документації.
- [`git-security.md`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/rules/git-security.md) — Гілкувальна модель `main` / `feature`, захист секретів, CI/CD артефакти.
- [`quality-assurance.md`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/rules/quality-assurance.md) — Стандарти якості, класифікація багів (P0–P3), протокол QA.
- [`research-sources.md`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/rules/research-sources.md) — Правило невідомих даних, робота лише з перевіреними джерелами.

Робочі процеси регламентуються сценаріями у `.agents/workflows/`:
- [`phase-0-start.md`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/workflows/phase-0-start.md) — Порядок старту та підготовки фази 0.
- [`phase-0-review.md`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/workflows/phase-0-review.md) — Перевірка документації Phase 0 перед подачею Власнику.
- [`session-close.md`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/workflows/session-close.md) — Чекліст закриття робочої сесії та збереження стану.
- [`documentation-audit.md`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/workflows/documentation-audit.md) — Аудит відповідності коду та документації.
- [`bug-triage.md`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/workflows/bug-triage.md) — Процедура обробки помилок та пошук першопричини (Root Cause).

---

## 5. ОРГАНІЗАЦІЯ ДОКУМЕНТАЦІЇ В `docs/`

```
docs/
├── product/          # MASTER_SPEC.md, ROADMAP.md, бачення продукту
├── design/           # GAME_DESIGN.md, LEVEL_DESIGN.md, ECONOMY_DESIGN.md, UI/ART
├── architecture/     # ARCHITECTURE.md, TECHNICAL_SPEC.md, вибір рушія
├── monetization/     # MONETIZATION.md, PRIVACY_CHECKLIST.md, GDPR/UMP
├── android/          # ANDROID_BUILD_GUIDE.md, налаштування середовища, дозволи
├── governance/       # CURRENT_STATE.md, DECISIONS.md, TASK_BOARD.md, SESSION_LOG.md
├── qa/               # TEST_PLAN.md, TEST_RESULTS.md, BUG_REPORT.md, RELEASE_CHECKLIST.md
└── templates/        # Шаблони завдань, звітів, багрепортів та рішень
```

---

## 6. ПРОТОКОЛ ЗАПУСКУ РОБОЧОЇ СЕСІЇ ДЛЯ БУДЬ-ЯКОГО АГЕНТА

Перед виконанням будь-яких дій агент **ЗОБОВ'ЯЗАНИЙ**:
1. Прочитати [docs/governance/CURRENT_STATE.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/docs/governance/CURRENT_STATE.md) для відновлення контексту.
2. Перевірити [docs/governance/TASK_BOARD.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/docs/governance/TASK_BOARD.md) та виділені завдання.
3. Ознайомитися з обмеженнями власної ролі у відповідному файлі `.agents/agents/agent-X-*/agent.md`.
4. Перевірити наявність відкритих блокерів P0/P1.
5. Після завершення роботи оновити [CURRENT_STATE.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/docs/governance/CURRENT_STATE.md) і скласти звіт за стандартною формою.
