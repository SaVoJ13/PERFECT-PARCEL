# ІНСТРУКЦІЯ З РЕЛІЗНОЇ ЗБІРКИ ТА ПІДПИСУ (RELEASE BUILD INSTRUCTIONS)

> **Проєкт:** PERFECT PARCEL  
> **Фаза:** Phase 3 — Підготовка до релізу  
> **Відповідальний за підготовку:** Agent 4 (Platform & Monetization Engineer)  
> **Куратор:** Agent 0 (Project Manager & Chief Architect)  
> **Цільовий артефакт:** Android App Bundle (.aab) для Google Play Console та .apk для тестування  

---

## 1. СТВОРЕННЯ КЛЮЧА ПІДПИСУ (ANDROID KEYSTORE)

Для публікації в Google Play додаток має бути підписаний власним ключем. Створення ключа займає 1 хвилину через консоль:

### Крок 1.1: Генерація файлу `release.keystore`
Виконайте команду в PowerShell (потрібен встановлений Java JDK або Android Studio):
```powershell
keytool -genkey -v -keystore release.keystore -alias perfectparcel -keyalg RSA -keysize 2048 -validity 10000
```
*У процесі вас попросять задати пароль (запам'ятайте його!) та відповісти на стандартні запитання (ім'я, організація).*

### Крок 1.2: Конвертація файлу Keystore у рядок Base64
Щоб безпечно передати ключ у хмарний білдер GitHub Actions, перетворіть файл у текст:
```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("release.keystore")) | Set-Clipboard
```
*(Рядок Base64 автоматично скопіюється в буфер обміну).*

> ⚠️ **ВАЖЛИВО:** Збережіть файл `release.keystore` та пароль у надійному захищеному місці! Якщо втратити ключ, Google Play не дозволить випускати оновлення гри.

---

## 2. НАЛАШТУВАННЯ СЕКРЕТІВ У GITHUB ACTIONS

Перейдіть у ваш GitHub-репозиторій: **Settings ➔ Secrets and variables ➔ Actions ➔ New repository secret** і додайте такі змінні:

| Назва секрету (Secret Name) | Що вказувати (Value) | Обов'язковість |
| :--- | :--- | :---: |
| **`ANDROID_KEYSTORE_BASE64`** | Вміст буфера обміну (рядок Base64 ключа) | Для релізного підпису AAB |
| **`ANDROID_KEYSTORE_PASS`** | Пароль до файлу keystore | Для релізного підпису AAB |
| **`ANDROID_KEYALIAS_NAME`** | `perfectparcel` (аліас ключа) | Для релізного підпису AAB |
| **`ANDROID_KEYALIAS_PASS`** | Пароль до аліасу (зазвичай такий самий) | Для релізного підпису AAB |
| **`UNITY_EMAIL`** | Ваш логін (email) у Unity ID | Для Game-CI активації |
| **`UNITY_PASSWORD`** | Ваш пароль до акаунту Unity ID | Для Game-CI активації |
| **`UNITY_LICENSE`** | Вміст файлу безкоштовної ліцензії Game-CI | Для headless компілятора |

*Примітка: Якщо секрети Keystore не вказано, GitHub Actions збере повноцінний тестовий APK/AAB із дефолтним Debug-сертифікатом для ручного тестування.*

---

## 3. НАЛАШТУВАННЯ ADMOB ДЛЯ БОЙОВОГО РЕЛІЗУ

У коді проєкту `AdsManager.cs` вже встановлено офіційні тестові ідентифікатори Google AdMob:
- **Rewarded Ad Test Unit:** `ca-app-pub-3940256099942544/5224354917`
- **Interstitial Ad Test Unit:** `ca-app-pub-3940256099942544/1033173712`

> 🛑 **ПРАВИЛО БЕЗПЕКИ:** Ніколи не тестуйте гру на фізичному пристрої з реальними ідентифікаторами AdMob без режиму `AddTestDevice()`! Google блокує облікові записи за самокліки.

### Перехід на бойові ID перед фінальною публікацією:
1. Зареєструйте додаток у кабінеті **Google AdMob Console** (`https://admob.google.com`).
2. Створіть 2 рекламні блоки:
   - **Rewarded Video** (назва: "PP_Rewarded_Double")
   - **Interstitial** (назва: "PP_Interstitial_Paced")
3. Скопіюйте отримані ID `ca-app-pub-XXXXXXXXXXXXXXXX/YYYYYYYYYY` у константи класу `Assets/_Project/Scripts/Monetization/AdsManager.cs`.

---

## 4. ЗАПУСК ХМАРНОЇ ЗБІРКИ ТА ОТРИМАННЯ ФАЙЛІВ

1. У репозиторії перейдіть у вкладку **Actions**.
2. Оберіть workflow **Build Android APK & AAB (Game-CI)**.
3. Натисніть **Run workflow** (можна вибрати `all`, `apk` або `aab`).
4. За 8–15 хвилин хмарний сервер скомпілює проєкт і прикріпить готові артефакти у блоці **Artifacts**:
   - `PerfectParcel-Release-AAB` — готовий підписаний файл `.aab` для завантаження в Google Play Console.
   - `PerfectParcel-Test-APK` — файл `.apk` для миттєвого встановлення на Android-смартфон через кабель або месенджер.
