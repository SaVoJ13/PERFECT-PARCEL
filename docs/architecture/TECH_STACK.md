# TECH STACK — зафіксовані версії

> Власник документа: Агент 4. Будь-яка зміна — окрема картка + запис у `DECISIONS.md` + зелений CI.
> «TBD» заповнюється в завданнях E0-03 / E0-04 / Етапу 3 з посиланням на офіційне джерело і датою доступу.

## Рушій і збірка
Заповнено в E0-03, дата доступу до всіх джерел — 2026-11-10. «Журнал CI» = журнал збірки запуску GitHub Actions гілки `task/E0-03-unity-project` (посилання — в журналі картки `docs/tasks/E0-03.md`).

| Компонент | Версія | Джерело / дата | Примітка |
|---|---|---|---|
| Unity | **6000.3.28f1** (6.3 LTS, реліз 2026-11-03, ревізія `8f082d2fe476`) | https://unity.com/releases/editor/whats-new/6000.3.28f1 ; підтримка LTS: unity.com/releases/unity-6/support | Одна версія скрізь: `ProjectSettings/ProjectVersion.txt`, `UNITY_VERSION` у `.github/workflows/build-android.yml`, образ у `activation.yml`, цей документ |
| Образ редактора для CI | `unityci/editor:ubuntu-6000.3.28f1-android-3` (збірка), `…-base-3` (activation) | https://hub.docker.com/r/unityci/editor/tags (теги `ubuntu-6000.3.28f1-android-3`, `-3.2`, `-3.2.2` опубліковано 2026-11-03); журнал CI | Найновіший патч 6000.3.x, для якого є образ з Android; образу 6000.3.29 на дату доступу немає |
| Universal Render Pipeline | 17.3.0 (вбудований пакет редактора) | `Packages/packages-lock.json` | 2D Renderer; ресурси в `Assets/_Project/Settings/`, створюються кодом (`KebabStreet.Editor.ProjectSetup`) |
| Input System | 1.20.1 | `Packages/packages-lock.json`; версія, рекомендована редактором 6000.3.28f1 (його `PackageManager/Editor/manifest.json`, журнал CI) | `activeInputHandler: 1` — лише новий ввід |
| Unity Test Framework | 1.6.0 (вбудований пакет редактора) | `Packages/packages-lock.json` | Автотести EditMode (запуск у CI — E0-04) |
| Решта пакетів | транзитивні, зафіксовані в `Packages/packages-lock.json` | | У `manifest.json` лише URP, Input System, Test Framework і модулі: androidjni, animation, audio, imageconversion, jsonserialize, particlesystem, physics2d, uielements |
| UI | UI Toolkit (ADR-018), модуль `com.unity.modules.uielements` | | |
| Android min API | **25** (Android 7.1) — ADR-021 (замінює ADR-017) | https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/introducing/requirements-and-compatibility («Unity supports Android 7.1 (API level 25) and above»); перелік `AndroidSdkVersions` у 6000.3 починається з `AndroidApiLevel25` | Unity 6.3 не збирає під API 24. Потрібне рішення в `DECISIONS.md` (прийняти 25 або змінити версію Unity) |
| Android target API | 36 | support.google.com/googleplay/android-developer/answer/11926878 ; Unity: «You can now target Android API levels 35 and 36» (те саме джерело docs.unity.com) | Обов'язково з 31.08.2026. Перевірено на готовому APK (крок CI «Print APK Info», `aapt2 dump badging`): `targetSdkVersion 36`, `compileSdkVersion 36`, `minSdkVersion 25`, `arm64-v8a` |
| Скриптовий бекенд / архітектури | IL2CPP / ARM64 | `ProjectSettings/ProjectSettings.asset`, `ProjectSetup.cs` | +ARMv7 — окреме рішення за розміром |
| Орієнтація / колірний простір | портрет / Linear | `ProjectSetup.cs` | |
| Ідентифікатор застосунку | `com.savoj.kebabstreet` (робочий, до D-08) | картка E0-03 | Назва продукту `Kebab Street`, компанія `SaVoJ` |
| Gradle / Android Gradle Plugin | 9.3.1 / 9.1.1 (у складі образу редактора) | журнал CI («Gradle Version: '9.3.1' Android Plugin Version: '9.1.1'») | Таблиця сумісності в довіднику Unity 6.3 на дату доступу показує інші числа — НЕ ПЕРЕВІРЕНО, чому; діє факт із журналу CI |
| JDK / NDK / Android SDK | вбудовані в образ редактора (`…/PlaybackEngines/AndroidPlayer/{OpenJDK,NDK,SDK}`) | журнал CI | Точні версії JDK і NDK — НЕ ПЕРЕВІРЕНО (у журналі збірки не друкуються) |
| CI | GitHub Actions, `ubuntu-22.04`; `game-ci/unity-builder@v4`; `game-ci/unity-test-runner@v4.3.1` (EditMode, зафіксована точна версія; є v4.4.0 від 2026-09-09, не обрано — не перевірялась);  `actions/checkout@v4`, `actions/cache@v4`, `actions/upload-artifact@v4`; `jlumbroso/free-disk-space@v1.3.1` | `.github/workflows/build-android.yml`; https://game.ci/docs/github/activation/ («Licenses are not tied to a specific Unity version or platform») | Ліцензія Personal (`UNITY_LICENSE`) активує 6000.3.28f1 — перевірено в E0-03. Найновіша `unity-builder` — v6.0.0; оновлення — окрема картка |

## Монетизація і сервіси (Етап 3)
| Компонент | Версія | Ліцензія | Навіщо | Вплив на розмір |
|---|---|---|---|---|
| Google Mobile Ads Unity Plugin | TBD | | Реклама | |
| Google UMP (у складі Ads SDK) | TBD | | Згода GDPR | |
| Unity IAP (Google Play Billing) | TBD | | Покупки | |
| Firebase Analytics / Crashlytics (якщо D-04) | TBD | | Аналітика, краші | |
| External Dependency Manager | TBD | | Android-залежності SDK | |

## Інструменти розробки (ПК Власника)
| Інструмент | Навіщо |
|---|---|
| Claude Code | Агенти. Моделі (ADR-016, економний режим Pro): усі — Sonnet, qa-release — Opus; Opus на одне завдання для архітектури або після двох невдач |
| Git, GitHub CLI (`gh`) | PR, CI, артефакти |
| Android platform-tools (`adb`) | Встановлення на телефон, скриншоти, логи |
| Python 3 | `tools/economy_sim.py` |
