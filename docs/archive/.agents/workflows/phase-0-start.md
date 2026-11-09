# РОБОЧИЙ ПРОЦЕС: СТАРТ ТА ПІДГОТОВКА PHASE 0 (PHASE 0 START)

> **Призначення:** Стандартна процедура ініціалізації Phase 0 («Планування та архітектура»)  
> **Головна мета:** Сформувати повний, узгоджений пакет проєктної документації (12+ документів) до написання будь-якого коду гри.  
> **Відповідальний координатор:** Agent 0  

---

## КРОК 1. ІНІЦІАЛІЗАЦІЯ ПРОЄКТУ ТА СТРУКТУРИ ДИРЕКТОРІЙ

1. Agent 0 перевіряє наявність базової структури каталогу:
   - Кореневі конфігурації: `AGENTS.md`, `.gitignore`, `README.md`.
   - Каталог агентів: `.agents/agents/` (6 агентів).
   - Правила та робочі процеси: `.agents/rules/`, `.agents/workflows/`.
   - Каталог документації: `docs/` із підрозділами (`product/`, `design/`, `architecture/`, `monetization/`, `android/`, `governance/`, `qa/`, `templates/`).
2. Ініціалізує первинні версії файлів управління:
   - `docs/governance/CURRENT_STATE.md` (фіксує старт Phase 0).
   - `docs/governance/TASK_BOARD.md` (первинний беклог завдань).
   - `docs/governance/DECISIONS.md` (перші архітектурні рішення).
   - `docs/governance/SESSION_LOG.md` (запис про старт).

---

## КРОК 2. ДЕКОМПОЗИЦІЯ ЗАВДАНЬ ДЛЯ АГЕНТІВ

Agent 0 формує ТЗ для кожного спеціалізованого агента на розробку ключових документів Phase 0:

1. **Для Agent 1 (Game Designer):**
   - Розробити `docs/design/GAME_DESIGN.md` (правила сортування, посилки, жести).
   - Розробити `docs/design/LEVEL_DESIGN.md` (структура 30 рівнів).
   - Розробити `docs/design/ECONOMY_DESIGN.md` (монети, 5–10 покращень, ачівки).

2. **Для Agent 2 (UI/UX & Art):**
   - Розробити `docs/design/ART_STYLE_GUIDE.md` (стиль, колірна гама, шрифти).
   - Розробити `docs/design/UI_SPECIFICATION.md` (макети та логіка 6 екранів).
   - Розробити `docs/design/ASSET_REGISTER.md` (структура обліку графіки та аудіо).

3. **Для Agent 0 & Agent 3 (Architecture & Gameplay):**
   - Розробити `docs/architecture/ARCHITECTURE.md` (модульність, системи, Save/Load).
   - Провести порівняльний аналіз рушіїв (Unity 2D vs Godot vs Flutter/Flame) та зафіксувати в ADR.

4. **Для Agent 4 (Platform & Monetization):**
   - Розробити `docs/architecture/TECHNICAL_SPEC.md` (версії Android, Gradle, SDK).
   - Розробити `docs/monetization/MONETIZATION.md` (Rewarded/Interstitial логіка).
   - Розробити `docs/android/ANDROID_BUILD_GUIDE.md` (середовище збірки, CI/CD).
   - Розробити `docs/monetization/PRIVACY_CHECKLIST.md` (GDPR, UMP, Play Console).

5. **Для Agent 5 (QA & Release):**
   - Розробити `docs/qa/TEST_PLAN.md` (матриця тест-кейсів для 30 рівнів).
   - Підготувати шаблони `TEST_RESULTS.md`, `BUG_REPORT.md`, `RELEASE_CHECKLIST.md`.

---

## КРОК 3. АНАЛІЗ ТА ПОРІВНЯННЯ ТЕХНОЛОГІЧНОГО СТЕКУ

1. Agent 0 організовує оцінку рушіїв за критеріями:
   - Продуктивність та стабільність на Android.
   - Простота та швидкість реалізації 2D казуального сортувальника.
   - Надійність інтеграції Google Mobile Ads SDK та UMP.
   - Можливості для автономної роботи AI-агентів.
   - Розмір збірки (APK size overhead).
   - Вартість та ліцензійні обмеження.
2. Підготовка рекомендації для Власника у вигляді Architectural Decision Record (ADR).

---

## КРОК 4. ПЕРЕХІД ДО ЕТАПУ РЕЦЕНЗУВАННЯ

Після готовності чорнових варіантів усіх документів статус змінюється на `Ready for Review` і запускається воркфлоу `phase-0-review.md`.
