# ТЕХНІЧНА СПЕЦИФІКАЦІЯ СИСТЕМИ (TECHNICAL SPECIFICATION)

> **Проєкт:** PERFECT PARCEL  
> **Версія:** 1.0 (Phase 0)  
> **Рушій:** Unity 2022.3 LTS (затверджено Власником за ADR-005)  
> **Відповідальний за наповнення:** Agent 4 (Platform & Monetization Engineer)  
> **Куратор:** Agent 0 (Project Manager & Chief Architect)  
> **Статус:** In Progress (TASK-P0-11)  

---

## 1. СИСТЕМНІ ТА АПАРАТНІ ВИМОГИ

| Параметр | Значення для MVP | Коментар |
| :--- | :--- | :--- |
| **Цільова ОС** | Android 7.0 (Nougat) до Android 15 | Покриває 98%+ активних Android-пристроїв у світі |
| **Minimum SDK** | API Level 24 | Мінімум для сучасних бібліотек Google Play |
| **Target SDK** | **API Level 34+** (Android 14/15) | Сувора обов'язкова вимога Google Play Console для публікації |
| **Архітектура CPU** | ARM64 (`arm64-v8a`) + ARMv7 (`armeabi-v7a`) | 64-бітний обов'язковий стандарт Google Play |
| **Графічний API** | OpenGLES3 / Vulkan | Пріоритет: OpenGLES3 для бюджетних чіпсетів (Mali/Adreno) |
| **Цільовий фреймрейт** | **60 FPS** (фіксований VSync) | Допустиме просідання: не нижче 55 FPS під час спавну ефектів |
| **Використання RAM** | До **150–180 МБ** | Гарантує роботу на пристроях з 2 ГБ оперативної пам'яті |
| **Розмір інсталятора** | До **45–55 МБ (AAB / APK)** | Швидке завантаження через мобільні мережі |

---

## 2. КОНФІГУРАЦІЯ UNITY 2022.3 LTS ТА БУДІВЕЛЬНОГО СЕРЕДОВИЩА

- **Версія Unity:** `2022.3.x LTS`
- **Скриптовий бекенд:** **IL2CPP** (вимога Google Play для ARM64 64-bit бінарників)
- **C++ Compiler:** Clang / Android NDK `r23b` (або версія, рекомендована Unity для 2022.3)
- **Java Development Kit (JDK):** OpenJDK 11 (вбудований у дистрибутив Unity Hub)
- **Gradle Version:** Gradle 7.x+ (вбудований Unity Gradle)
- **Стиснення текстур:** **ASTC (Adaptive Scalable Texture Compression)** для оптимального балансу пам'яті та якості на мобільних GPU.

---

## 3. МОНЕТИЗАЦІЙНІ ТА СЕРВІСНІ SDK

1. **Google Mobile Ads Unity Plugin (AdMob):**
   - Версія: Актуальна стабільна версія (v9.x+).
   - Інтегровані формати: Rewarded Video Ads, Interstitial Ads (без банерів у геймплеї).
2. **Google User Messaging Platform (UMP) SDK:**
   - Для відповідності вимогам IAB TCF v2.2 та європейському регламенту GDPR.
   - Діалог згоди показується лише при першому запуску для користувачів із ЄС/ЄЕЗ/Великобританії.
3. **Google Play Core (In-App Review):**
   - Опціональний нативний віджет оцінки гри після проходження 10 рівня без виходу в браузер.
