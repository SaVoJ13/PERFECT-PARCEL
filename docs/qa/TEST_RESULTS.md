# Результати перевірок

| Тест | Рівень доказу | Результат | Дата | Коміт | Доказ |
|---|---|---|---|---|---|
| E0-01: `main` без старого Unity-проєкту, є `legacy-perfect-parcel` і тег `perfect-parcel-final` | L0 (`git ls-tree`, `git ls-remote`) | PASS | 2026-11-10 | `82e0c72` | `docs/qa/QA_E0-docs.md` розд. 2 |
| E0-02: 7 секретів CI | L1 (`gh secret list`) | PASS | 2026-11-10 | — | `docs/qa/QA_E0-docs.md` розд. 3 |
| E0-02: успішна збірка Android на старому коді | L1 (`gh run view 42520583192`) | PASS | 2026-11-10 | `bb511ca` | `docs/qa/QA_E0-docs.md` розд. 3 |
| E0-02: причина червоного `main` | L1 (`gh run view 42675541163 --log-failed`) | PASS | 2026-11-10 | `82e0c72` | `docs/qa/QA_E0-docs.md` розд. 3 |
| E0-10: GAME_DESIGN/LEVEL_DESIGN відповідають концепції | L0 | FAIL (BUG-001…003) | 2026-11-10 | `6c9b62d` | `docs/qa/QA_E0-docs.md` розд. 4 |
| E0-10: числа позначені припущеннями; тексти — ключами | L0 | PASS | 2026-11-10 | `6c9b62d` | `docs/qa/QA_E0-docs.md` розд. 4 |
| E0-11: палітра/розміри конкретні, контраст (перерахунок WCAG) | L0 | PASS | 2026-11-10 | `6c9b62d` | `docs/qa/QA_E0-docs.md` розд. 5 |
| E0-11: D-09 варіанти, наслідки, без витрат | L0 | PASS | 2026-11-10 | `6c9b62d` | `docs/qa/QA_E0-docs.md` розд. 5 |
| CI для гілки `docs/session-2026-11-10` | — | НЕ ПЕРЕВІРЕНО: PR не відкрито, зміни лише docs | 2026-11-10 | `6c9b62d` | — |
| E0-03: зелений CI для останнього коміту з кодом, артефакт з `.apk` | L1 (`gh run view 42859414899`, `gh run download`) | PASS | 2026-11-10 | `642c911` (далі до `8f905fe` лише docs) | `docs/qa/QA_E0-03.md` розд. 1; `docs/qa/evidence/E0-03/ci_42859414899.txt` |
| E0-03: id `com.savoj.kebabstreet`, target 36, IL2CPP, arm64-v8a, портрет | L1 (журнал CI + `aapt2` на завантаженому APK) | PASS | 2026-11-10 | `642c911` | `docs/qa/QA_E0-03.md` розд. 2 |
| E0-03: min API 24 | L1 (`aapt2`: `minSdkVersion 25`) | FAIL буквально (BUG-009, Unity 6.3 мінімум 25; рішення Власника №8) | 2026-11-10 | `642c911` | `docs/qa/QA_E0-03.md` розд. 7 |
| E0-03: `error CS` = 0, без помилок конвеєра рендерингу | L1 (журнал запуску) | PASS | 2026-11-10 | `642c911` | `docs/qa/QA_E0-03.md` розд. 1 |
| E0-03: одна версія Unity 6000.3.28f1, пакети = lock, джерела з датою | L0 (файли) + L1 (образ у журналі) | PASS | 2026-11-10 | `642c911` | `docs/qa/QA_E0-03.md` розд. 3 |
| E0-03: 6 asmdef за ARCHITECTURE розд. 2, Core `noEngineReferences` | L0 + L1 (компіляція в CI) | PASS (зауваження BUG-012) | 2026-11-10 | `642c911` | `docs/qa/QA_E0-03.md` розд. 4 |
| E0-03: папки за розд. 3, усі `.meta` закомічено | L0 (`git ls-files`) + L1 (крок «Verify Project Files Are Committed») | PASS (зауваження BUG-010) | 2026-11-10 | `642c911` | `docs/qa/QA_E0-03.md` розд. 5 |
| E0-03: немає секретів/keystore/ліцензій/Library в дереві й історії гілки; артефакти інциденту видалено; секрети в журналах замасковані | L0 (`git log -p`) + L1 (`gh api artifacts`, журнали 7 запусків) | PASS | 2026-11-10 | `8f905fe` | `docs/qa/QA_E0-03.md` розд. 6 |
| E0-03: вміст кешу `Library` без даних підпису | — | НЕ ПЕРЕВІРЕНО: кеш не читається поза воркфлоу (BUG-011) | 2026-11-10 | — | `docs/qa/QA_E0-03.md` розд. 9 |
| E0-03: нічого з PERFECT PARCEL поза `docs/` | L0 (`git grep`) | PASS | 2026-11-10 | `8f905fe` | `docs/qa/QA_E0-03.md` розд. 8 |
| E0-04: запуск `pull_request` на останньому коміті з кодом зелений; APK в артефакті | L1 (`gh run view 42895773257`, `gh run download`, `aapt2`/`apksigner`) | PASS | 2026-11-10 | `e172592` (також 42897537802 на `218f8ba`) | `docs/qa/QA_E0-04.md` розд. 1; `docs/qa/evidence/E0-04/ci_runs.txt` |
| E0-04: тести EditMode у CI — 3/3 (`ArchitectureSmokeTests.Core_DoesNotReferenceUnityEngine`, `Services_ReferenceCore`, `TestAssembly_SeesCoreAndServices`), результати в артефакті | L2 (журнал + `editmode-results.xml`) | PASS | 2026-11-10 | `e172592` | `docs/qa/QA_E0-04.md` розд. 1 |
| E0-04: червоний тест → червоний CI; тест відновлено без змін | L1 (`gh run view 42894080682`; `git diff 74577df..e172592 -- Assets` порожній) | PASS | 2026-11-10 | `9ea70d6` → `e172592` | `docs/qa/QA_E0-04.md` розд. 1 |
| E0-04: ручний запуск `buildAab=true` зелений, артефакт `.apk` + `.aab` | L1 (запуск 42897737655, запустив Агент 5; `unzip -l` AAB, `apksigner`) | PASS (запуск виконавця 42890146940 — `failure` через job тестів, усунено в `74577df`) | 2026-11-10 | `218f8ba` (код = `e172592`) | `docs/qa/QA_E0-04.md` розд. 1 |
| E0-04: BUG-011 — кеш без даних підпису, очищення до збереження, запобіжник | L1 (журнали 6 запусків, `action.yml` `post-if: success()`) | PASS для нових кешів; старий кеш `main` 10311011251 з даними — дія Власника | 2026-11-10 | `e172592` | `docs/qa/QA_E0-04.md` розд. 3, 5 |
| E0-04: BUG-010 — перевірка всього `Assets`, `.gitignore` для `Assets/Editor` | L0 (`git check-ignore`) + L1 (крок зелений) | PASS | 2026-11-10 | `e172592` | `docs/qa/QA_E0-04.md` розд. 1 |
| E0-04: значення секретів у журналах замасковані; секретів/keystore в комітах немає; PR без релізного ключа | L0 (`git log -p`) + L1 (класифікація журналів, debug-сертифікат APK з PR) | PASS (зауваження BUG-015) | 2026-11-10 | `218f8ba` | `docs/qa/QA_E0-04.md` розд. 3 |
| E0-04: PR із форка (порожні секрети) | — | НЕ ПЕРЕВІРЕНО: немає форка; оцінено за кодом скрипту | 2026-11-10 | — | `docs/qa/QA_E0-04.md` розд. 8 |
| E0-04: `push` у `main` збирає AAB | — | НЕ ПЕРЕВІРЕНО: можливо лише після злиття; умова в YAML | 2026-11-10 | — | `docs/qa/QA_E0-04.md` розд. 8 |
| E0-06: тести EditMode 111/111, пропущених 0, на голові PR; назви зіставлено з п. 8 картки | L2 (`gh run view 42905992259`, `editmode-results.xml` з артефакту 13805701220) | PASS | 2026-11-10 | `ace17cf` (код = `689d7c7`) | `docs/qa/QA_E0-06.md` розд. 2–3; `docs/qa/evidence/E0-06/ci_runs.txt`, `test_cases_run42903947527.txt` |
| E0-06: збірка APK і «Verify Project Files Are Committed» (обидва job) зелені; `.meta` без дублікатів GUID | L1 (запуски 42905992259, 42903947527, 42901783792) + L0 (`grep guid` по `Assets`) | PASS | 2026-11-10 | `ace17cf` | `docs/qa/QA_E0-06.md` розд. 3, 6 |
| E0-06: «золоті» значення SplitMix64 і фінальний знімок сценарію збігаються з незалежною реалізацією Агента 5 | L0 (Node.js `BigInt`, власний код QA) + L2 (ті самі значення в CI на Mono) | PASS | 2026-11-10 | `689d7c7` | `docs/qa/QA_E0-06.md` розд. 4; `docs/qa/evidence/E0-06/independent_*` |
| E0-06: Core без `UnityEngine`/`System.Random`/`DateTime`/`Stopwatch`/статичного стану; asmdef не змінено; межі файлів | L0 (`grep`, `git diff`) + L2 (`CoreRulesTests`, `ArchitectureSmokeTests`) | PASS (зауваження до тесту BUG-016) | 2026-11-10 | `ace17cf` | `docs/qa/QA_E0-06.md` розд. 1, 6 |
| E0-06: межові умови `Advance`/таймера/машини станів (читання коду) | L0 (рев'ю) + L2 (тести) | PASS (зауваження BUG-017, BUG-018) | 2026-11-10 | `689d7c7` | `docs/qa/QA_E0-06.md` розд. 5 |
| E0-06: детермінізм на IL2CPP/ARM64 (телефон) | — | НЕ ПЕРЕВІРЕНО: тести йдуть на Mono в редакторі; L3 для логіки не вимагається | 2026-11-10 | — | `docs/qa/QA_E0-06.md` розд. 8 |
