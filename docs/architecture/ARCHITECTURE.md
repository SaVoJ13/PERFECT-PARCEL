# СИСТЕМНА АРХІТЕКТУРА КЛІЄНТА (ARCHITECTURE SPECIFICATION)

> **Проєкт:** PERFECT PARCEL  
> **Фаза:** Phase 0 — Планування та архітектура  
> **Відповідальні:** Agent 0 (Chief Architect), Agent 3 (Gameplay Developer)  
> **Базовий порівняльний документ:** [ENGINE_COMPARISON.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/docs/architecture/ENGINE_COMPARISON.md)  

---

## 1. АРХІТЕКТУРНІ ПРИНЦИПИ СИСТЕМИ
1. **Model-View-Presenter (MVP) / Decoupled Systems:** Повне відділення бізнес-логіки сортування, правил та економіки від візуалізації (спрайтів, частинок, звуків).
2. **Event-Driven Architecture (Event Bus):** Підсистеми комунікують через події (`OnParcelSorted`, `OnSortingError`, `OnLevelCompleted`, `OnCoinBalanceChanged`) без жорстких циклічних посилань.
3. **Object Pooling (Zero Allocation in Gameplay Loop):** Усі посилки, ефекти частинок та плаваючі цифри очок перевикористовуються через пул об'єктів для запобігання Garbage Collection (GC) фризів на Android.
4. **Offline-First & Data Integrity:** Стан гри та прогрес зберігаються локально в JSON із захистом від пошкодження (хешування або версіонування схеми). Мережеві сервіси (AdMob/UMP) працюють повністю асинхронно та ізольовано.

---

## 2. ОСНОВНІ ПІДСИСТЕМИ ГРИ

```
┌────────────────────────────────────────────────────────┐
│                      GAME MANAGER                      │
│             (Керування життєвим циклом гри)            │
└───────────┬────────────────────────────────┬───────────┘
            │                                │
            ▼                                ▼
┌───────────────────────┐        ┌───────────────────────┐
│     CONVEYOR CORE     │        │     META & ECONOMY    │
│  - BeltMovementSystem │        │  - WalletService      │
│  - ParcelSpawnerPool  │        │  - UpgradesManager    │
│  - InputRouter        │        │  - QuestAchievements  │
│  - SortingValidator   │        │  - SaveLoadService    │
└───────────┬───────────┘        └───────────┬───────────┘
            │                                │
            ▼                                ▼
┌────────────────────────────────────────────────────────┐
│                 EVENT BUS & AUDIO/VFX                  │
└───────────────────────────┬────────────────────────────┘
                            │
                            ▼
┌────────────────────────────────────────────────────────┐
│                     UI CONTROLLER                      │
│      (Main Menu, Gameplay, Results, Upgrades, UI)      │
└────────────────────────────────────────────────────────┘
```

---

## 3. ЗАТВЕРДЖЕНИЙ ТЕХНОЛОГІЧНИЙ СТЕК (ЗГІДНО З ADR-005)
На основі рішення Власника проєкту офіційно зафіксовано:
- **Ігровий рушій:** **Unity 2022.3 LTS (Long Term Support)**
- **Мова розробки та рантайм:** C# (.NET Standard 2.1)
- **Цільова платформа:** Android (Minimum API Level: 24 — Android 7.0; Target API Level: 34+ — Android 14/15)
- **Графічний пайплайн:** Universal Render Pipeline (URP 2D) для оптимальної продуктивності та стабільних 60 FPS на мобільних GPU
- **Система збірки:** Gradle (вбудований у Unity 2022.3 LTS), підтримка Android App Bundle (AAB) та спліт-APK для архітектур ARM64 та ARMv7
- **Монетизація та приватність:** Офіційний Google Mobile Ads Unity Plugin (AdMob) + нативний Google UMP SDK (Consent Management Platform)
- **CI/CD автоматизація:** GitHub Actions з використанням Docker-образів Game-CI (`unityci/editor:2022.3.*-android`)

---

## 4. СТРУКТУРА КОДОВОЇ БАЗИ КЛІЄНТА (UNITY 2022.3)

```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Core/               # GameManager, StateMachine, Bootstrapper
│   │   ├── Conveyor/           # ConveyorBeltController, BeltMovement, DecisionZone
│   │   ├── Parcels/            # ParcelBase, ParcelTypes (Standard, Express, Fragile, etc.), ParcelPool
│   │   ├── Sorting/            # SortingGate, DirectionValidator, InputRouter
│   │   ├── Economy/            # WalletService, UpgradesManager, DailyQuestsManager
│   │   ├── Storage/            # SaveLoadService, JsonEncryption, SaveDataModel
│   │   ├── UI/                 # BaseWindow, MainMenuUI, GameplayUI, ResultsUI, UpgradesUI
│   │   ├── Audio/              # SoundManager, SfxPlayer, MusicPlayer
│   │   └── Ads/                # AdsManager, RewardedAdController, InterstitialAdController, UmpConsentController
│   ├── Prefabs/
│   │   ├── Parcels/            # Префаби 5 типів посилок
│   │   ├── Conveyor/           # Елементи стрічки, розгалуження, ворота
│   │   └── UI/                 # Вікна, діалоги, HUD
│   ├── ScriptableObjects/      # Конфігурації 30 рівнів, параметри посилок, баланс цін
│   ├── Sprites/                # Спрайти, Sprite Atlases (UI, Parcels, Conveyor)
│   ├── Audio/                  # SFX (WAV/OGG), Music (OGG)
│   └── Settings/               # URP 2D Asset, Input Settings
```


---

## 5. СИСТЕМА ЛОКАЛЬНОГО ЗБЕРЕЖЕННЯ (SAVE/LOAD SYSTEM & DATA INTEGRITY)

Система збереження розроблена за стандартом **Offline-First Data Integrity** для запобігання втраті прогресу гравця за будь-яких умов (раптове вимкнення батареї, вивантаження ОС або збій запису).

### 5.1. Структура `SaveData.json`
Файл розміщується у захищеній внутрішній директорії додатка: `Application.persistentDataPath + "/SaveData.json"`.

```json
{
  "schemaVersion": 1,
  "lastSaveTimestamp": "2026-11-08T15:30:00Z",
  "coins": 450,
  "highestUnlockedLevel": 8,
  "levels": [
    { "levelIndex": 1, "isUnlocked": true, "stars": 3, "highScore": 1850 },
    { "levelIndex": 2, "isUnlocked": true, "stars": 3, "highScore": 2100 },
    { "levelIndex": 8, "isUnlocked": true, "stars": 0, "highScore": 0 }
  ],
  "upgrades": {
    "smooth_belt": 2,
    "safety_buffer": 1,
    "magnetic_guides": 1,
    "auto_scanner": 0,
    "coin_multiplier": 1,
    "cushioned_bin": 0,
    "express_fee": 1
  },
  "achievements": {
    "ACH-01": { "unlocked": true, "date": "2026-11-08" },
    "ACH-02": { "unlocked": true, "date": "2026-11-08" }
  },
  "dailyQuests": {
    "generationDate": "2026-11-08",
    "quests": [
      { "id": "quest_sort_50", "currentProgress": 35, "target": 50, "isClaimed": false },
      { "id": "quest_no_fail_3", "currentProgress": 2, "target": 3, "isClaimed": false },
      { "id": "quest_express_15", "currentProgress": 10, "target": 15, "isClaimed": false }
    ]
  },
  "settings": {
    "musicVolume": 0.8,
    "sfxVolume": 1.0,
    "hapticsEnabled": true,
    "language": "uk"
  },
  "checksum": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
}
```

### 5.2. Протокол безпечного атомарного запису (Atomic Write Pattern)
Прямий запис у `SaveData.json` заборонено, оскільки обрив живлення під час запису призведе до нульового або зламаного файлу. Використовується триступенева процедура:

```
[Об'єкт SaveData]
       │
       ▼
[1. Серіалізація в JSON + Розрахунок SHA256 хешу]
       │
       ▼
[2. Запис у тимчасовий файл SaveData.json.tmp]
       │
       ▼
[3. Верифікація розміру файлу (> 0 байт) та парсингу]
       │
       ▼
[4. Створення резервної копії: SaveData.json -> SaveData.json.bak]
       │
       ▼
[5. Атомарна заміна / перейменування: SaveData.json.tmp -> SaveData.json]
```

### 5.3. Захист від пошкодження та відновлення (Fallback Strategy)
1. **Перевірка контрольної суми (Tamper & Corruption Detection):**
   - Перед завантаженням розраховується SHA256 хеш вмісту файлу із закритим сольовим ключем. Якщо хеш не збігається — файл вважається пошкодженим.
2. **Автоматичне відновлення з бекапу:**
   - Якщо `SaveData.json` пошкоджено або видалено, система автоматично намагається прочитати `SaveData.json.bak`.
3. **Graceful Degrade (Ніколи не крашити гру):**
   - Якщо обидва файли пошкоджено, створюється чистий профіль із дефолтними значеннями (Рівень 1, 0 монет), а в лог записується попередження. Гра ніколи не завершується аварійно.
