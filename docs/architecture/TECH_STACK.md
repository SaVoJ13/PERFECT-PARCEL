# TECH STACK — зафіксовані версії

> Власник документа: Агент 4. Будь-яка зміна — окрема картка + запис у `DECISIONS.md` + зелений CI.
> «TBD» заповнюється в завданнях E0-03 / E0-04 / Етапу 3 з посиланням на офіційне джерело і датою доступу.

## Рушій і збірка
| Компонент | Версія | Джерело / дата | Примітка |
|---|---|---|---|
| Unity | 6.3 LTS — точний патч TBD (E0-03) | unity.com/releases/unity-6/support | Одна версія скрізь: `ProjectVersion.txt`, CI, документи |
| Universal Render Pipeline | TBD (сумісна з Unity 6.3) | | |
| Input System | TBD | | |
| Unity Test Framework | TBD | | Автотести EditMode |
| UI | UI Toolkit (якщо D-03 = так) | | |
| Android min API | 24 (якщо D-02 = так) | | |
| Android target API | 36 | support.google.com/googleplay/android-developer/answer/11926878 | Обов'язково з 31.08.2026 |
| Скриптовий бекенд / архітектури | IL2CPP / ARM64 (+ARMv7 — рішення за розміром) | | |
| CI | GitHub Actions + game-ci (версії дій TBD) | | |

## Монетизація і сервіси (Етап 3)
| Компонент | Версія | Ліцензія | Навіщо | Вплив на розмір |
|---|---|---|---|---|
| Google Mobile Ads Unity Plugin | TBD | | Реклама | |
| Google UMP (у складі Ads SDK) | TBD | | Згода GDPR | |
| Unity IAP (Google Play Billing) | TBD | | Покупки | |
| Firebase Analytics / Crashlytics (якщо D-04) | TBD | | Аналітика, краші | |
| External Dependency Manager | TBD | | Android-залежності SDK | |

## Інструменти розробки (ПК Власника)
| Інструмент | Навіщо |
|---|---|
| Claude Code | Агенти. Моделі (ADR-016, економний режим Pro): усі — Sonnet, qa-release — Opus; Opus на одне завдання для архітектури або після двох невдач |
| Git, GitHub CLI (`gh`) | PR, CI, артефакти |
| Android platform-tools (`adb`) | Встановлення на телефон, скриншоти, логи |
| Python 3 | `tools/economy_sim.py` |
