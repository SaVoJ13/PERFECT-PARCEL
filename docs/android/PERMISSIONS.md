# Дозволи Android (KEBAB STREET)

Правило: мінімум дозволів; кожен - з причиною. Змінює Агент 4.

| Дозвіл | Звідки | Навіщо | Рівень | Запит користувачу |
|---|---|---|---|---|
| `android.permission.VIBRATE` | `Assets/Plugins/Android/KebabStreetPermissions.androidlib/src/main/AndroidManifest.xml` (Gradle зливає з головним маніфестом) | Q-05i, QB-ALL-14: короткі тактильні відгуки; перемикач «Вібрація» в паузі (`ks.settings.haptics`) | звичайний (ADR-030) | Ні |

Інших нових дозволів Q-05i не додає. Реклама, покупки й аналітика (Етап 3) - окремі записи з їхніх карток.

## Вібрація (Q-05i)
- Код: `Game/Flow/Haptics.cs` (`IHapticsService`, `HapticsRouter`, `HapticsHub`, `HapticsCatalog`), `Game/Views/AndroidHapticsService.cs`.
- Android-реалізація: `View.performHapticFeedback(int)` на `decorView` активності з константами `HapticFeedbackConstants`: легкий - `VIRTUAL_KEY` (1); сильний - `CONFIRM` (16, API 30+), на старіших `LONG_PRESS` (0). Прапорці не змінюються, тож діє системне налаштування вібровідгуку дотику. Без SDK і нових бібліотек.
- Заглушка `NullHapticsService` - у редакторі, тестах і не-Android збірках (`#if UNITY_ANDROID && !UNITY_EDITOR`); успіху не імітує.
- Ліміти (QB-ALL-14): події симуляції (каса, шипіння, пригоріло, клієнт пішов) - не частіше 2 разів на секунду; тапи - не частіше 12,5/с; різні події в межах 50 мс - один відгук, сильніший витісняє слабший. Пауза - без вібрації.
- Відповідність подій: легкий - тап, оплата, шматок на гриль, замкнений рівень; сильний - пригоріле, «майже пригоріле», клієнт пішов, зірка результату; без вібрації - тіки таймера, монети, дзвіночок, радість.

## Джерела (доступ 2026-11-15)
- Значення й рівні API констант `HapticFeedbackConstants` (VIRTUAL_KEY=1, LONG_PRESS=0, CONFIRM=16, API 30): https://developer.android.com/reference/android/view/HapticFeedbackConstants
- Структура `.androidlib` і злиття маніфестів: https://docs.unity3d.com/ja/6000.0/Manual/android-library-plugin-create.html (сторінка 6.0; сторінки 6.3 для створення не знайдено - діє збірка CI), https://docs.unity3d.com/Manual/android-library-project-import.html
- Те, що `VIBRATE` має рівень «normal», - за ADR-030; на сторінці Manifest.permission деталі не вдалося прочитати: НЕ ПЕРЕВІРЕНО окремо. Чи потрібен він для `performHapticFeedback`, - НЕ ПЕРЕВІРЕНО (Android-документація цього не стверджує); дозвіл додано за ADR-030.
- Перелік дозволів готового APK друкує крок CI «Print APK Info» (`aapt2 dump badging`).

## На пристрої
НЕ ПЕРЕВІРЕНО (телефон відключено): відчуття відгуків, поведінка при вимкненому системному вібровідгуку.
