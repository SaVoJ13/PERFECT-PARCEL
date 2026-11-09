---
name: qa-review
description: Незалежна перевірка PR або завдання KEBAB STREET агентом qa-release перед злиттям у main.
disable-model-invocation: true
argument-hint: [номер PR або ID завдання]
context: fork
agent: qa-release
---

Проведи незалежну перевірку для: $ARGUMENTS

Дій строго за розділом «Перевірка PR» своєї ролі. Результат поверни у такому вигляді:

```
QA REVIEW — <PR/TASK-ID>
Вердикт: QA Verified | Changes Requested | Blocked
Коміт: <sha>   CI: <посилання, статус>
Рівень доказу: потрібен Lx / отримано Ly
Критерії приймання:
  - <критерій> — ✅ / ❌ — <доказ>
Знайдені проблеми:
  - BUG-### [P0–P3] <суть> — кроки, очікувано/фактично, доказ
Файли поза зоною агента: <немає / список>
Не перевірено: <що і чому>
```

Запиши результат у `docs/qa/TEST_RESULTS.md`, нові помилки — у `docs/qa/BUG_REPORT.md`.
