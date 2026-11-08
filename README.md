# PERFECT PARCEL

**Casual 2D Parcel Sorting Puzzle / Light Warehouse Management Game for Android**

---

## 📦 Огляд проєкту
PERFECT PARCEL — це мобільна 2D-гра, у якій гравець керує сортуванням посилок на складському конвеєрі.
Гра поєднує інтуїтивне керування однією рукою, поступове зростання складності, локальну систему збереження та м'яку монетизацію через рекламу (Rewarded + Interstitial).

- **Платформа:** Android (API 24+)
- **Модель розробки:** Автономна команда із 6 спеціалізованих AI-агентів під управлінням Власника проєкту.
- **Поточна фаза:** Phase 0 — Планування та архітектура.
- **Дорожня карта:** [perfect_parcel_roadmap_v1_4.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/perfect_parcel_roadmap_v1_4.md)

---

## 🤖 Система агентів (Multi-Agent System)
Керування розробкою здійснюється згідно з [AGENTS.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/AGENTS.md):
- **Agent 0:** Project Manager & Chief Architect
- **Agent 1:** Game Designer
- **Agent 2:** UI/UX & Art Designer
- **Agent 3:** Gameplay Developer
- **Agent 4:** Platform & Monetization Engineer
- **Agent 5:** QA & Release Engineer

Детальні конфігурації та інструкції знаходяться у каталозі [`.agents/`](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/.agents/).

---

## 📁 Структура проєкту
```
PERFECT-PARCEL/
├── AGENTS.md                 # Головний файл системи управління агентами
├── .agents/
│   ├── agents/               # Профілі 6 спеціалізованих агентів
│   ├── rules/                # 6 обов'язкових правил розробки та субординації
│   └── workflows/            # 5 стандартних робочих процесів (SOР)
└── docs/
    ├── product/              # Бачення, обсяг MVP, дорожня карта
    ├── design/               # Геймдизайн, рівні, економіка, UI, арт-гайд
    ├── architecture/         # Архітектура клієнта, технічна специфікація
    ├── monetization/         # Модель реклами, приватність, GDPR/UMP
    ├── android/              # Інструкції збірки та налаштування оточення
    ├── governance/           # Поточний стан, журнал рішень, завдання, логи сесій
    ├── qa/                   # План тестування, звіти, реєстр багів, релізний чекліст
    └── templates/            # Шаблони завдань, рішень, звітів
```
