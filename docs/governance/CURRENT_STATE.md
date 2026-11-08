# CURRENT STATE — PERFECT PARCEL

> **Поточний стан проєкту (Single Source of Truth для відновлення контексту)**  
> **Останнє оновлення:** 2026-11-08 (Сесія 11 — Phase 3: Launch & Release Preparation)  
> **Оновлює:** Agent 0 (Project Manager & Chief Architect)  

---

## 1. ПОТОЧНА ФАЗА ТА СТАТУС
- **Активна фаза:** **PHASE 3 — ДИЗАЙН, ФІНАЛЬНА МОНЕТИЗАЦІЯ ТА РЕЛІЗНА ПІДГОТОВКА**
- **Поточний статус:** `Phase 3 In Progress: CI/CD AAB Workflow, Keystore Guide, Privacy Policy & Store Assets Ready`
- **Статус Phase 0:** 🟢 **COMPLETED (Owner Approved, ADR-006)**
- **Статус Phase 1:** 🟢 **COMPLETED (Owner Approved, ADR-007)**
- **Статус Phase 2:** 🟢 **COMPLETED (Owner Approved, ADR-009, QA 84/84 PASS)**
- **Затверджена архітектурна модель:** 100% Офлайн-першість, без реєстрації та без бекенду. Модель жорсткого дефіциту монет (ADR-008) із каналами подолання через Rewarded Ads та 3 пакети Google Play Billing (IAP).
- **Остання завершена контрольна точка (Старт Phase 3):**
  1. Зареєстровано рішення **ADR-009** — результати Phase 2 затверджено Власником проєкту, етап офіційно закрито.
  2. Оновлено `.github/workflows/build-android.yml` та `BuildScript.cs` для паралельної або роздільної збірки як тестового APK, так і релізного Android App Bundle (AAB) для Google Play Console із підтримкою підпису через змінні середовища.
  3. Розроблено вичерпну інструкцію [docs/android/RELEASE_BUILD_INSTRUCTIONS.md](file:///c:/Users/cocro/Downloads/PERFECT%20PARCEL/docs/android/RELEASE_BUILD_INSTRUCTIONS.md) щодо генерації `release.keystore`, налаштування секретів GitHub Actions та бойових ідентифікаторів AdMob.
  4. Складено фінальну двомовну політику конфіденційності [docs/monetization/PRIVACY_POLICY.md](file:///c:/Users/cocro/Downloads/PERFECT%20PARCEL/docs/monetization/PRIVACY_POLICY.md) (EN / UA) без збору персональних даних для розміщення за публічним посиланням.
  5. Створено маркетингову специфікацію [docs/design/STORE_ASSETS_SPEC.md](file:///c:/Users/cocro/Downloads/PERFECT%20PARCEL/docs/design/STORE_ASSETS_SPEC.md) з детальними AI-промптами для генерації іконки (512x512), головного банера (1024x500), 5 локалізованих скриншотів та повного опису для Google Play Store.
  6. Актуалізовано релізний чекліст [docs/qa/RELEASE_CHECKLIST.md](file:///c:/Users/cocro/Downloads/PERFECT%20PARCEL/docs/qa/RELEASE_CHECKLIST.md) (60 пунктів перевірки якості).

---

## 2. ТЕХНОЛОГІЧНИЙ СТЕК ТА ВЕРСІЇ
- **Ігровий рушій:** Unity 2022.3.20f1 LTS (C#)
- **Графіка:** Universal Render Pipeline (URP 2D), портретна орієнтація 1080x1920, 2D векторні спрайти 256x256
- **Платформа:** Android (Min SDK 24, Target SDK 34+, ARM64/ARMv7, IL2CPP)
- **Формат релізного білда:** Android App Bundle (.aab) та Android Package (.apk)
- **Монетизація:** Google Mobile Ads (AdMob) + Google UMP + Google Play Billing (IAP)
- **Локалізація:** Швидкий словниковий менеджер на 6 мов (EN, UA, PL, DE, ES, RU)
- **Аудіо:** Пул AudioSource із процедурним синтезатором PCM (Zero GC)
- **CI/CD:** GitHub Actions + Game-CI (`.github/workflows/build-android.yml`)
- **Архітектура:** Event-Driven (EventBus), Object Pooling (Zero GC in loop), Decoupled MVP, Atomic File Storage

---

## 3. СТАН ГОТОВНОСТІ ДО ПУБЛІКАЦІЇ В GOOGLE PLAY

| Компонент релізу | Стан готовності | Відповідальний |
| :--- | :---: | :--- |
| **Кодова база гри (30 рівнів, UI, аудіо, сейви)** | 🟢 **100% Готово (QA Verified)** | Agent 3 / Agent 5 |
| **Скрипти автоматичної збірки APK та AAB** | 🟢 **100% Готово (Game-CI workflow)** | Agent 4 |
| **Інструкція з підпису Keystore** | 🟢 **100% Готово (`RELEASE_BUILD_INSTRUCTIONS.md`)** | Agent 4 |
| **Політика конфіденційності (Privacy Policy)** | 🟢 **100% Готово (`PRIVACY_POLICY.md`)** | Agent 4 |
| **Специфікація арт-матеріалів Google Play** | 🟢 **100% Готово (`STORE_ASSETS_SPEC.md`)** | Agent 2 |
| **Релізний чекліст приймання** | 🟢 **100% Готово (`RELEASE_CHECKLIST.md`)** | Agent 5 |
| **Створення релізного Keystore** | 🟡 **Очікує виконання Власником (1 команда)** | Власник проєкту |
| **Генерація іконки 512x512 та банера 1024x500** | 🟡 **Очікує генерації за промптами** | Власник проєкту / Designer |

---

## 4. ВІДОМІ ПОМИЛКИ ТА БЛОКЕРИ
- **Відкриті блокери (P0):** **0**
- **Критичні дефекти (P1):** **0**
- **Мажорні/Мінорні дефекти:** **0**

---

## 5. НАСТУПНІ ДІЇ ДЛЯ РЕЛІЗУ
1. Створення файлу `release.keystore` та додавання 4 секретів у репозиторій GitHub згідно з [RELEASE_BUILD_INSTRUCTIONS.md](file:///c:/Users/cocro/Downloads/PERFECT%20PARCEL/docs/android/RELEASE_BUILD_INSTRUCTIONS.md).
2. Запуск workflow у GitHub Actions для генерації фінального підписаного `.aab`.
3. Завантаження `.aab` у Google Play Console (трек Closed Testing).
