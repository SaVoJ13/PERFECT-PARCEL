---
name: start-session
description: Початок робочої сесії KEBAB STREET — відновити контекст з файлів, перевірити стан репозиторію, CI і телефона, запропонувати Власнику план сесії.
disable-model-invocation: true
---

Виконай по черзі і нічого не змінюй, поки не закінчиш:

1. Прочитай `docs/governance/CURRENT_STATE.md`, `docs/governance/TASK_BOARD.md`, `docs/governance/OWNER_INBOX.md`, останні 2 записи `docs/governance/SESSION_LOG.md`, останні рішення в `docs/governance/DECISIONS.md`, журнали карток зі статусом `In Progress`, `docs/governance/LESSONS.md`.
2. `git status`, `git branch --show-current`, `git log --oneline -5`. Якщо є незакомічені зміни — з'ясуй, чиї вони, і не втрачай їх.
3. `gh pr list --state open` і `gh run list --limit 5`. Якщо останній запуск CI на `main` червоний — це завдання №1 сесії.
4. `adb devices` — чи підключено телефон (лише інформація, не вимога).
5. Звір `CURRENT_STATE.md` з реальністю (гілки, PR, CI). Розбіжність — виправити `CURRENT_STATE.md` першою дією і згадати у звіті.
6. Напиши Власнику коротко українською:
   - де ми (етап, статус);
   - що змінилось з минулої сесії;
   - план цієї сесії: 1–3 завдання з `TASK_BOARD.md` з ID;
   - чи потрібне зараз рішення або дія Власника (якщо так — лише одна, у форматі `/owner-step`).
7. Не чекай підтвердження плану: за ADR-015 починай перше завдання одразу. Лише пункти з `CONSTITUTION.md` 3.2 чекають Власника — їх у `OWNER_INBOX.md`.
