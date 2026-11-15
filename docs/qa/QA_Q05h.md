# QA Q-05h: пауза і навчання (PR #67)

Вердикт: **QA Verified (L2)**. L3: НЕ ПЕРЕВІРЕНО (потрібен телефон: вигляд паузи, прожектора, руки, перемикачів, «Заново»).
HEAD: d1589b5. Дата: 2026-11-15.

| Перевірка | Результат | Доказ |
|---|---|---|
| diff --stat main...HEAD: Core, Resources/Config, .github, Packages, ProjectSettings, схема SaveData | не змінені | 22 файли: лише Game/Flow, Game/Views, Game/Text, Localization, Tests, docs |
| Налаштування лише PlayerPrefs | так: GameSettings + PlayerPrefsSettingsStore; тест `Assert.IsNull(typeof(SaveData).GetProperty("MusicOn"/"HapticsEnabled"))` | PauseSettingsTests.cs:135-136 |
| OnRestart | викликає лише після підтвердження; `SaveTutorial()` + існуюче `Context.RetryShift()` -> `Flow.Replay()` (той самий шлях, що «Ще раз»); правила/зірки/збереження результату не чіпає; guard `!IsFinished` проти нарахування після кінця; нова сцена створює новий драйвер, подвійного старту немає (L2 за кодом) | ShiftController.cs:166-177 |
| «Меню» без втрати прогресу | потребує підтвердження (PauseConfirm.Menu); далі старий OnExit | PausePanel.cs:254-263 |
| Тапи під паузою | панель паузи накриває екран; драйвер на паузі (SetPaused), логіка не тікає | ShiftController.ApplyPause |
| Прожектор і кнопка паузи | область затемнення виключає верхню смугу (`_keepFree`) | TutorialOverlay.cs:335-346 |
| Зависання при відсутній цілі / NaN | при null/NaN/нульових розмірах `UpdateSpotlight` виходить, `_dim` лишається display:none (блокування вмикається лише з ApplySpotlight) -> екран не блокується; плашка має кнопки пропуску | TutorialOverlay.cs:314-333 |
| TutorialDirector / tutorial.json | не змінені (файли поза diff); overlay лише читає Current/Model | git diff --stat |
| Пропуск з паузи | `_tutorial.SkipAll()` + `onTutorialChanged` = SaveTutorial, як для «Пропустити все» | ShiftScreen.cs |
| Тести не послаблено | ShiftScreenTests/TutorialDirectorTests не змінені; Tests diff: +711/-0, лише додавання | git diff --stat |
| CI на HEAD d1589b5 | EditMode success: 1588/1588 passed, 0 failed; Build Android APK success (run 43673102335) | gh run view |

## Зауваги (не блокують)
- P3: QB-PAUSE-03 (приглушення музики), blur, 3-2-1, QB-TUT-04 (галочка) не зроблені, розробник заявив чесно.
- P3: «Заново» під час паузи не перевірено автотестом на рівні ShiftController (немає Unity-сцени); перевіряється на L3.
- Прожектор/ПК-розмітка на реальному екрані (worldBound до першого layout) НЕ ПЕРЕВІРЕНО (L3).
