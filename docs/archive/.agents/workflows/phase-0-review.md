# РОБОЧИЙ ПРОЦЕС: РЕВ'Ю ТА ЗДАЧА PHASE 0 (PHASE 0 REVIEW)

> **Призначення:** Контроль якості та підготовка документації Phase 0 до затвердження Власником  
> **Головна мета:** Гарантувати 100% відповідність документів вимогам Roadmap 1.4 та отримати статус `Owner Approved` для старту розробки (Phase 1).  
> **Учасники:** Agent 0, Agent 5 (QA аудит), Власник проєкту  

---

## КРОК 1. ВНУТРІШНІЙ АУДИТ ДОКУМЕНТАЦІЇ (AGENT 0)

Agent 0 перевіряє наявність та цілісність усіх документів за чеклістом:

- [ ] `docs/product/MASTER_SPEC.md`
- [ ] `docs/product/ROADMAP.md`
- [ ] `docs/design/GAME_DESIGN.md` (чітко визначені правила сортування, 5 типів посилок)
- [ ] `docs/design/LEVEL_DESIGN.md` (всі 30 рівнів описані в таблиці)
- [ ] `docs/design/ECONOMY_DESIGN.md` (монети, 5-10 покращень, 10 ачівок, 3 щоденні квести)
- [ ] `docs/design/UI_SPECIFICATION.md` (макети 6 екранів, орієнтація портрет)
- [ ] `docs/design/ART_STYLE_GUIDE.md` (кольори, палітри, шрифти, стилістика)
- [ ] `docs/design/ASSET_REGISTER.md` (реєстр ассетів, ліцензії)
- [ ] `docs/architecture/ARCHITECTURE.md` (патерни, структура проекту, збереження)
- [ ] `docs/architecture/TECHNICAL_SPEC.md` (версії Android, SDK, інструментів)
- [ ] `docs/monetization/MONETIZATION.md` (Rewarded + Interstitial правила)
- [ ] `docs/monetization/PRIVACY_CHECKLIST.md` (GDPR, UMP, Google Play Data Safety)
- [ ] `docs/android/ANDROID_BUILD_GUIDE.md` (Gradle, середовище)
- [ ] `docs/qa/TEST_PLAN.md` (план перевірки для всіх 30 рівнів)

---

## КРОК 2. НЕЗАЛЕЖНИЙ АУДИТ ЯКОСТІ (AGENT 5)

Agent 5 проводить перевірку документаційного пакету:
1. Чи немає взаємних суперечностей (наприклад, різна кількість рівнів або назв посилок у різних документах).
2. Чи є чіткі Acceptance Criteria для кожного рівня та функції.
3. Чи зафіксовані невідомі параметри згідно з «Правилом невідомих даних».
4. Чи відсутні приховані вимоги написання коду для Власника.

Якщо все чисто, Agent 5 присвоює статус: **`QA Verified: Phase 0 Documentation Complete`**.

---

## КРОК 3. ПІДГОТОВКА ЗВІТУ ДЛЯ ВЛАСНИКА

Agent 0 формує офіційний документ:
- Коротке резюме пакету документів (Executive Summary).
- Результати порівняння технологічного стеку та фінальна рекомендація (наприклад, Unity 2D LTS vs Godot 4).
- Опис того, що буде створено в Phase 1 (перший робочий прототип APK із конвеєром).
- Питання для Власника:
  1. *Чи затверджуєте ви обраний технологічний стек?*
  2. *Чи затверджуєте ви пакет документації Phase 0?*
  3. *Чи даєте дозвіл на початок робіт над Phase 1 (Технічний прототип)?*

---

## КРОК 4. ОФІЦІЙНЕ ЗАТВЕРДЖЕННЯ (OWNER APPROVAL)

1. Власник надає погодження: **`Approved`**.
2. Agent 0:
   - Вносить запис у `docs/governance/DECISIONS.md`.
   - Оновлює статус у `docs/governance/CURRENT_STATE.md` на:  
     `Phase 0: Owner Approved | Next: Phase 1 (Technical Prototype)`.
   - Розпочинає формування завдань для Phase 1.
