# LESSONS — уроки і пастки проєкту

> Пише будь-який агент, коли натрапив на проблему, що може повторитися. Коротко: проблема → причина → як правильно.
> Читати розділи, що стосуються твоєї роботи, перед початком (`CONSTITUTION.md`, 5.2).

## Процес
- **Симуляція ≠ тест.** У PERFECT PARCEL агенти звітували «84/84 PASS» без запуску гри. Правильно: лише докази за `DEFINITION_OF_DONE.md`.
- **Версії розійшлися** (три різні версії Unity в одному проєкті). Правильно: одна версія в `TECH_STACK.md`, зміна лише через `DECISIONS.md`.

## Windows / PowerShell
- PowerShell псує бінарний вивід через `>`. Для `adb exec-out screencap -p` використовувати `cmd /c "adb exec-out screencap -p > файл.png"`.

## Робоча папка (Windows)
- **Старі `Assets/`, `Packages/`, `ProjectSettings/` лежать у робочій папці як untracked** (залишок PERFECT PARCEL після E0-01; на `main` їх немає, повна копія — гілка `legacy-perfect-parcel`). Їх можна випадково закомітити або вони зіб'ють новий Unity-проєкт (E0-03).
- **Шлях до папки містить пробіл** (`PERFECT PARCEL`, а репозиторій — `PERFECT-PARCEL`). Скрипти, `adb`, Unity batchmode без лапок ламаються на пробілі. Завжди бери шлях у лапки.
- **Безпечне прибирання до E0-03 (пропозиція):** (1) переконатися, що вміст збігається з `legacy-perfect-parcel`; (2) найкраще — свіжий `git clone` у шлях без пробілів (напр. `C:\dev\kebab-street`), новий Unity-проєкт створювати там, стару папку не чіпати; (3) якщо лишаємось тут — не видаляти, а перемістити три папки в `..\PERFECT-PARCEL-old-backup`.

## Unity / CI
- Локальний `python` на ПК Власника відсутній (Windows-заглушка). Для `tools/economy_sim.py` потрібен встановлений Python — врахувати в E0-03/Етапі 1.
- (додавати по ходу)

## Android / adb / Google Play
- (додавати по ходу)

## Unity 6.3 / game-ci / збірка в CI (E0-03, 2026-11-10)
- **Ліцензія Unity не прив'язана до версії.** Секрет `UNITY_LICENSE` (.ulf, Personal), отриманий для 2022.3, активує 6000.3 без жодних дій. Повторна активація при зміні версії Unity не потрібна (game.ci/docs/github/activation).
- **Unity 6.3 не підтримує Android API 24.** Мінімум — API 25 (перелік `AndroidSdkVersions` починається з `AndroidApiLevel25`). Перед рішенням про min API звіряйся з вимогами конкретної версії Unity.
- **Файли редактора генерує лише CI.** Локального Unity 6.3 немає: `.meta`, `ProjectSettings/*.asset`, `packages-lock.json`, ресурси URP отримуємо тимчасовим кроком `upload-artifact` → `gh run download` → коміт. Новий скрипт чи папка без `.meta` → крок CI «Verify Project Files Are Committed» впаде і покаже, яких файлів бракує.
- **🔴 Не вивантажуй `ProjectSettings.asset` артефактом після збірки з підписом.** Unity записує в нього псевдонім ключа (секрет `ANDROID_KEYALIAS_NAME`) і шлях до keystore. Репозиторій публічний, артефакти доступні стороннім. `BuildScript` очищає ці поля після збірки (`ProjectSetup.ClearAndroidSigning`), але в разі аварії редактора вони можуть лишитися — перед вивантаженням завжди перевіряй/очищай.
- **Перше вмикання Input System ламає збірку в тому ж сеансі редактора:** `script class layout is incompatible between the editor and the player`. Причина — редактор ще скомпільований зі старим вводом. `activeInputHandler: 1` має бути вже закомічено в `ProjectSettings.asset` до запуску збірки. Те саме стосується будь-яких налаштувань, що змінюють компіляційні символи.
- **URP створює файли в корені `Assets`** (`UniversalRenderPipelineGlobalSettings.asset`, `DefaultVolumeProfile.asset`) — `ProjectSetup` переносить їх у `Assets/_Project/Settings`. Пакет `com.unity.test-framework.performance` (транзитивна залежність URP) під час збірки створює `Assets/Resources/PerformanceTestRun*.json` — вони в `.gitignore`, не комітити.
- **game-ci копіює свої скрипти в `Assets/Editor/…/UnityBuilderAction`** під час збірки — це не файли проєкту, не комітити.
- **Запуск воркфлоу з гілки** (`gh workflow run <файл> --ref <гілка>`) бере YAML із гілки. Помилка `Workflow does not have 'workflow_dispatch' trigger` означає зіпсований YAML у гілці (у нас — керівний символ `\x01`, що потрапив замість `\1` при генерації файлу скриптом). Після скриптових правок перевіряй файл на керівні символи.
- **Кеш `Library` зберігається лише після успішного завдання CI.** Поки запуск не зелений, кожна збірка IL2CPP — повна (≈20 хв); після — швидше.
- **Порожні значення в YAML Unity мають пробіл після двокрапки** (`AndroidKeystoreName: `). Ручне редагування без пробілу дає різницю з тим, що пише редактор.
- **Образ `unityci/editor:ubuntu-6000.3.28f1-android-3` збирає під target API 36** (`compileSdkVersion 36` у готовому APK); Gradle 9.3.1, AGP 9.1.1. Фактичні min/target API дивись у кроці CI «Print APK Info», а не лише в налаштуваннях.
- **Не роби висновків з обрізаного виводу.** Список папок SDK, обрізаний `head`, показав лише платформи 34/35/37.0 — і дав хибний висновок «платформи 36 немає». Перевіряй факт прямим способом (тут — `aapt2 dump badging` готового APK).
