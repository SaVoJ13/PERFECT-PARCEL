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
