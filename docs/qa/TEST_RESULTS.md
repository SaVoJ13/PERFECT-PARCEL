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
