# Дозволи Android (KEBAB STREET)

Правило: мінімум дозволів (вимога Google Play); кожен - з причиною. Змінює Агент 4.

| Дозвіл | Звідки | Навіщо |
|---|---|---|
| `android.permission.INTERNET` | Unity додає автоматично (`ForceInternetPermission: 0` у `ProjectSettings` - не примусово; джерело додавання - НЕ ПЕРЕВІРЕНО, див. журнал Q-05i) | Не з Q-05i; з'ясувати до Етапу 3 (реклама й так потребує INTERNET) |

**VIBRATE не додається: `performHapticFeedback` дозволу не потребує** (джерело: офіційна документація Android - https://developer.android.com/develop/ui/views/haptics/haptic-feedback та https://developer.android.com/reference/android/os/Vibrator ; доступ 2026-11-15; дозвіл потрібен лише для прямих викликів `Vibrator`/`VibrationEffect`).

## Вібрація (Q-05i)
- Код: `Game/Flow/Haptics.cs` (`IHapticsService`, `HapticsRouter`, `HapticsHub`, `HapticsCatalog`), `Game/Views/AndroidHapticsService.cs`.
- Android: `View.performHapticFeedback(int)` на `decorView` з константами `HapticFeedbackConstants`: легкий - `VIRTUAL_KEY` (1); сильний - `CONFIRM` (16, API 30+), на старіших `LONG_PRESS` (0). Прапорці не змінюються - діє системне налаштування вібровідгуку. Без SDK.
- Заглушка `NullHapticsService` - редактор, тести, не-Android (`#if UNITY_ANDROID && !UNITY_EDITOR`); успіху не імітує.
- Ліміти (QB-ALL-14): події симуляції - не частіше 2/с; тапи - захист 80 мс; події в межах 50 мс - один відгук, сильніший витісняє слабший. Пауза - без вібрації.
- Легкий: тап, оплата, шматок на гриль, замкнений рівень; сильний: пригоріле, «майже пригоріле», клієнт пішов, зірка результату; без вібрації: тіки, монети, дзвіночок, радість.
- Константи: https://developer.android.com/reference/android/view/HapticFeedbackConstants (доступ 2026-11-15).

## На пристрої
НЕ ПЕРЕВІРЕНО (телефон відключено).
