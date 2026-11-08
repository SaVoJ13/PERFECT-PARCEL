# ІНСТРУКЦІЯ ЗБІРКИ ТА CI/CD ДЛЯ ANDROID (ANDROID BUILD GUIDE)

> **Проєкт:** PERFECT PARCEL  
> **Платформа:** Android (API 24..34+)  
> **Рушій:** Unity 2022.3 LTS  
> **Відповідальний за наповнення:** Agent 4 (Platform & Monetization Engineer)  
> **Куратор:** Agent 0 (Project Manager & Chief Architect)  
> **Статус:** In Progress (TASK-P0-14)  

---

## 1. СТРАТЕГІЯ АВТОНОМНОЇ ЗБІРКИ ЧЕРЕЗ GITHUB ACTIONS (CI/CD)

Головний принцип Roadmap 1.4: **Власник не повинен збирати білди вручну на своєму ПК.** Збірка APK та AAB відбувається автоматично у хмарі GitHub Actions за допомогою перевіреного відкритого інструментарію **Game-CI**.

### Базовий пайплайн GitHub Actions (`.github/workflows/build-android.yml`):
```yaml
name: Build Android APK/AAB

on:
  push:
    branches: [ main, develop ]
  workflow_dispatch:

jobs:
  buildAndroid:
    name: Build for Android (Unity 2022.3)
    runs-on: ubuntu-latest
    steps:
      - name: Checkout Repository
        uses: actions/checkout@v4
        with:
          lfs: true

      - name: Cache Library
        uses: actions/cache@v4
        with:
          path: Library
          key: Library-Android-${{ hashFiles('Assets/**', 'Packages/**', 'ProjectSettings/**') }}
          restore-keys: |
            Library-Android-

      - name: Unity Builder (Game-CI)
        uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          targetPlatform: Android
          buildMethod: BuildScript.BuildAndroidApk
          versioning: Semantic

      - name: Upload APK Artifact
        uses: actions/upload-artifact@v4
        with:
          name: PerfectParcel-Test-APK
          path: build/Android/*.apk
          retention-days: 14
```

---

## 2. НАЛАШТУВАННЯ КЛЮЧІВ ПІДПИСУ (RELEASE KEYSTORE)

1. **Генерація Keystore:**
   - Створюється стандартний файл підпису `perfect_parcel_release.keystore` з 2048-бітним RSA ключем та терміном дії 25+ років:
     ```bash
     keytool -genkey -v -keystore perfect_parcel_release.keystore -alias perfectparcel -keyalg RSA -keysize 2048 -validity 10000
     ```
2. **Безпека секретів:**
   - 🚫 **Категорично заборонено пушити `*.keystore` або `*.jks` у репозиторій Git!**
   - Файл підпису кодується у Base64 і додається у **GitHub Secrets** репозиторію як `ANDROID_KEYSTORE_BASE64`.
   - Паролі до Keystore та Alias передаються виключно через `ANDROID_KEYSTORE_PASS` та `ANDROID_KEYALIAS_PASS`.

---

## 3. ОПТИМІЗАЦІЯ РОЗМІРУ ТА PROGUARD / R8

У файлі `Assets/Plugins/Android/proguard-user.txt` фіксуються правила збереження класів для AdMob та UMP:
```pro
# Google Mobile Ads Unity Plugin
-keep class com.google.android.gms.ads.** { *; }
-keep interface com.google.android.gms.ads.** { *; }

# Google User Messaging Platform (UMP)
-keep class com.google.android.ump.** { *; }

# Unity Player
-keep class com.unity3d.player.** { *; }
```
Це гарантує коректну роботу реклами при ввімкненій R8 мінімізації та дозволяє зменшити розмір релізного AAB на 15–20%.
