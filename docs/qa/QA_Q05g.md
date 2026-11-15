# QA Q-05g — звук: мікс, ducking, синтезовані SFX (PR #66)

Дата: 2026-11-15. Коміт: 4fafbf0. Рівень доказу: L2. Вердикт: **QA Verified (L2)**. На слух (L3) — НЕ ПЕРЕВІРЕНО.

| Перевірка | Доказ | Результат |
|---|---|---|
| Межі файлів | `git diff --stat main...HEAD`: лише Game/Flow, Game/Views, Tests/EditMode, DECISIONS, TASK_BOARD, картка. Core, Resources/Config, .github, Packages, ProjectSettings — без змін | є |
| Нові аудіофайли | жодного wav/ogg/mp3 у diff; синтез кодом (SfxSynth), ліцензійних питань немає | є |
| SfxTests.cs | diff: лише сигнатура `Play(cue,pitch)` + порожні `SetLoop/SetVolumes` у заглушці; у тесті каталогу синтезовані клипи пропускаються (`IsSynth` → NameOf null) — файлова перевірка для 6 файлових звуків збережена; `Assert.AreEqual(AllNames.Length, loads)` без змін | не послаблено |
| Тест шва петлі | коміт 086246e: було порівняння шва з піком, стало шов ≤ макс. кроку між сусідніми відліками (+0,0001). Для шуму це коректна межа (шов не помітніший за звичайний перехід). Окремий тест `MusicLoop_EndsNearSilence` (початок/кінець < 0,002) лишився | коректне виправлення |
| Ліміти SfxRouter | 50 мс (`MinIntervalSec`), CoinTick 1/15 с, ≤3 копії (`MaxConcurrent`), ducking x0,40 з плавним Tick, пул 6 (`PoolSize`) — прочитано код і тести Limiter_*/Ducking_*/CoinTick_* | є |
| Алокації в гарячих шляхах | grep `new`/ToString/LINQ/Format у Sfx.cs і SfxMix.cs: `new` лише в конструкторах/Install; Read — структура. У UnitySfxSink конкатенація імені лише при першому створенні клипа (кеш) | немає щокадрових алокацій |
| AudioListener (BUG-054) | `AttachListener` не змінено; нові джерела (пул, петлі) — AudioSource, не слухачі; тест OverlapLayoutTests (1 слухач) зелений | не зламано |
| Пауза/фокус | OnApplicationPause/Focus -> ApplyPause -> Router.Paused -> Refresh вимикає петлі (SetLoop false -> Pause); тест `Loops_OnlyWhenWantedAndNotPausedOrDisabled`, `Director_Music_PlaysDuringShift_StopsOnPauseAndStop` | є |
| Stop() наприкінці зміни | ShiftController: `if IsFinished -> _sfx.Stop()`; OnDestroy -> StopLoops | є |
| Музика в тестах | тести йдуть через заглушку ISfxSink; реальний UnitySfxSink створюється лише в `EnsureInstalled` при `Application.isPlaying` | не лунає |
| Подвійне дублювання | SfxDirector читає лише лічильники стану (не Core-події); тест `Router_NoDuplicateInSameInstant_ForEveryCue` | немає |
| CI на HEAD | run 43670465630 (pull_request, коміт 4fafbf0): EditMode pass, Build Android APK pass; лог: testcasecount=1553, passed=1553, failed=0 (>=1553) | є |

## Зауваження (не блокують)
- P3: уже запущені разові звуки (пул з 6 джерел) не ставляться на паузу при паузі гри — стишуються лише новою відсутністю запитів; звуки короткі (<1 с), тож практично непомітно.
- P3: `JitterPitch` ±3–6% застосовано до Sizzle/Cash/Happy/Sad/Tap/CoinTick; перевірено лише на рівні коду/тесту, не на слух.
- Код ніде не запускався локально; якість звуку, гучність балансу, ducking і відчуття музики — **НЕ ПЕРЕВІРЕНО (L3, на слух): потрібен телефон у воротах E1**.
