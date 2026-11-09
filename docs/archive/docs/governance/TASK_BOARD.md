# ДОШКА ЗАВДАНЬ (TASK BOARD) — PERFECT PARCEL

> **Поточна фаза:** PHASE 3 — ДИЗАЙН, ФІНАЛЬНА МОНЕТИЗАЦІЯ ТА РЕЛІЗНА ПІДГОТОВКА  
> **Оновлено:** 2026-11-08 (Сесія 11 — Старт Phase 3, ADR-009 Approved)  
> **Координатор:** Agent 0 (Project Manager & Chief Architect)  
> **Затверджений стек:** Unity 2022.3 LTS (C#) — URP 2D, Target SDK 34+  
> **Статус Phase 0:** 🟢 **COMPLETED (Owner Approved, ADR-006)**  
> **Статус Phase 1:** 🟢 **COMPLETED (Owner Approved, ADR-007)**  
> **Статус Phase 2:** 🟢 **COMPLETED (Owner Approved, ADR-009)**  
> **Статус Phase 3:** 🟡 **IN PROGRESS (Підготовка релізних білдів та сторінки Google Play)**  

---

## КАНБАН-МАТРИЦЯ

```
┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐
│     BACKLOG     │──►│   IN PROGRESS   │──►│  READY FOR QA   │──►│   QA VERIFIED   │──►│  OWNER APPROVED │
│                 │   │                 │   │  [PHASE 3]      │   │                 │   │                 │
└─────────────────┘   └─────────────────┘   └─────────────────┘   └─────────────────┘   └─────────────────┘
```

---

## 1. COMPLETED PHASES (ЗАКРИТІ ФАЗИ)

| Фаза | Опис результатів | Кількість артефактів | Підсумковий статус |
| :--- | :--- | :---: | :---: |
| **PHASE 0** | Повний пакет планування та архітектури (ADR-001..006) | 14 документів у `docs/` | 🟢 **Owner Approved** |
| **PHASE 1** | Технічний прототип (Конвеєр, Object Pool, 3 лінії, HUD, Game-CI CI/CD, 30 рівнів конфіг) | 18 C# скриптів, workflow, 7 PASS тестів | 🟢 **Owner Approved** |
| **PHASE 2** | Повний MVP: Сейви, 7 апгрейдів, 6 мов, 7-денні нагороди, AdMob, IAP, 2D арт, BGM + 8 SFX, Juice | 36 C# скриптів, 15 PNG спрайтів, QA звіт 84/84 PASS | 🟢 **Owner Approved (ADR-009)** |

---

## 2. PHASE 3: ДИЗАЙН, ФІНАЛЬНА МОНЕТИЗАЦІЯ ТА РЕЛІЗНА ПІДГОТОВКА (АКТИВНІ ЗАВДАННЯ)

| ID | Назва завдання | Відповідальний | Пріоритет | Залежності | DoD (Критерій готовності) | Статус |
| :--- | :--- | :--- | :---: | :--- | :--- | :---: |
| **TASK-P3-01** | Реєстрація ADR-009 та закриття Phase 2 | Agent 0 | **P0** | Директива Власника | У `docs/governance/DECISIONS.md` зареєстровано ADR-009; Phase 2 переведено у статус COMPLETED. | 🟢 **Done** |
| **TASK-P3-02** | Оновлення CI/CD workflow для збірки APK та AAB | Agent 4 | **P0** | Game-CI | Оновлено `.github/workflows/build-android.yml` та `BuildScript.cs` з підтримкою headless збірки Release AAB та Test APK з інжекцією Keystore. | 🟢 **Done** |
| **TASK-P3-03** | Інструкція з релізного підпису та налаштування секретів | Agent 4 | **P0** | TASK-P3-02 | Створено `docs/android/RELEASE_BUILD_INSTRUCTIONS.md` (генерація Keystore, Base64 конвертація, секрети GitHub Actions, бойові ID AdMob). | 🟢 **Done** |
| **TASK-P3-04** | Публічна політика конфіденційності (Privacy Policy) | Agent 4 | **P0** | PRIVACY_CHECKLIST | Створено двомовний документ `docs/monetization/PRIVACY_POLICY.md` (EN / UA) без збору персональних даних, COPPA та GDPR комплаєнс. | 🟢 **Done** |
| **TASK-P3-05** | Специфікація та промпти маркетингових ассетів Store | Agent 2 | **P0** | ART_STYLE_GUIDE | Створено `docs/design/STORE_ASSETS_SPEC.md` (іконка 512x512, Feature Graphic 1024x500, 5 скриншотів із перекладами, метадані). | 🟢 **Done** |
| **TASK-P3-06** | Актуалізація релізного чекліста під стан Phase 3 | Agent 5 | **P0** | TASK-P3-01..05 | Оновлено `docs/qa/RELEASE_CHECKLIST.md` (60 пунктів контролю якості, перевірено відповідність). | 🟢 **Done** |
| **TASK-P3-07** | Актуалізація реєстрів та координація запуску Phase 3 | Agent 0 | **P0** | TASK-P3-01..06 | Оновлено `CURRENT_STATE.md`, `TASK_BOARD.md`, `SESSION_LOG.md` (Сесія 11). | 🟢 **Done** |

---

## 3. ФІНАЛЬНІ КРОКИ ДО РЕЛІЗУ В GOOGLE PLAY (ЗА ВЛАСНИКОМ)
1. **Генерація Keystore:** Виконання 2 команд із `RELEASE_BUILD_INSTRUCTIONS.md` та додавання 4 секретів у GitHub.
2. **Маркетинговий арт:** Генерація іконки 512x512 та банера 1024x500 за готовими промптами з `STORE_ASSETS_SPEC.md`.
3. **Запуск релізної збірки:** Запуск workflow у GitHub Actions для отримання файлу `.aab`.
4. **Завантаження в Google Play Console:** Створення релізу у треку закритого або відкритого тестування.
