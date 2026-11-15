# Реєстр ресурсів KEBAB STREET (ASSET_REGISTER)

> Створено в Q-06 (ui-art-designer, Sonnet), 2026-11-15, за ADR-032 (D-09, сценарій «А»). Головний реєстр: **кожен** шрифт, зображення, звук і стороння річ у репозиторії має тут рядок. Невідомі права = не використовуємо (`ART_STYLE_GUIDE.md`, розділ 9).
> Поділ: детальні таблиці плейсхолдер-PNG і WAV ведуться в `docs/art/ASSET_REGISTRY.md` (їх читають тести `ArtRegistryTests`, `SfxTests`); тут — підсумок, шрифти і все стороннє.
> Дати доступу до джерел: 2026-11-15. Витрати на ресурси: **0 USD**. AI-згенерованих ресурсів у проєкті **немає**.

## 1. Шрифти

| Файл | Що | Джерело / автор | Ліцензія | Комерційне використання | Атрибуція | Дата | URL перевірки |
|---|---|---|---|---|---|---|---|
| `Assets/_Project/Art/Fonts/Nunito-Regular.ttf` | Основний текст (вага 400). Статичний екземпляр змінного файлу `Nunito[wght].ttf` | Google Fonts, репозиторій `google/fonts`, `ofl/nunito` (коміт каталогу `8b0a1d0f`, 2026-03-12); автори: The Nunito Project Authors (Vernon Adams, Cyreal, Jacques Le Bailly). Екземпляр зроблено інструментом `fontTools.varLib.instancer` (`wght=400`, імена оновлено) | SIL Open Font License 1.1 | так (OFL: «use, embed, bundle with software, sell software»; заборонено лише продавати сам шрифт окремо) | У застосунку не вимагається; текст ліцензії лежить поруч: `Assets/_Project/Art/Fonts/OFL.txt` | 2026-11-15 | https://github.com/google/fonts/tree/main/ofl/nunito ; https://openfontlicense.org/open-font-license-official-text/ |
| `Assets/_Project/Art/Fonts/Nunito-ExtraBold.ttf` | Заголовки, кнопки, числа (вага 800). Екземпляр `wght=800` того ж файлу | те саме | OFL 1.1 | так | те саме | 2026-11-15 | те саме |
| `Assets/_Project/Art/Fonts/OFL.txt` | Текст ліцензії й авторське право шрифту (копія з `ofl/nunito/OFL.txt`) | там само | OFL 1.1 | — | — | 2026-11-15 | https://github.com/google/fonts/blob/main/ofl/nunito/OFL.txt |

**Перевірка ліцензії.** `METADATA.pb` у каталозі `ofl/nunito`: `license: "OFL"`. У `OFL.txt` Nunito **не оголошує Reserved Font Name** (рядок авторського права без «with Reserved Font Name»), тож статичні екземпляри можна поширювати; імена в файлах оновлено (`Nunito`, `Nunito ExtraBold`). Сторінка fonts.google.com/specimen/Nunito/license побудована на JavaScript і агентові не відкрилась — тому ліцензію підтверджено за `METADATA.pb` і `OFL.txt` у репозиторії Google Fonts (первинне джерело каталогу), а текст самої OFL — за openfontlicense.org.

**Покриття символів (перевірено скриптом по таблиці `cmap` обох файлів, L1).** Є: польські `ąćęłńóśźż ĄĆĘŁŃÓŚŹŻ`; усі українські літери разом з `ґ Ґ є Є і І ї Ї`; апостроф `'` і `’`; `₴ € — – « » … № · ≤ ×`. **Немає: `★` (U+2605)**, `✓`, `▶`, `●`. Через це в `en/uk/pl.json` зірки в тексті (`★★`) замінено словами («2 stars» / «2 зірки» / «2 gwiazdki»); зірки малюються лише спрайтами `icon_star_*`. Обмеження: скрипт перевіряє наявність символу, а не вигляд — вигляд на телефоні **НЕ ПЕРЕВІРЕНО (L3)**.

**Фактична залежність від версії Unity — НЕ ПЕРЕВІРЕНО локально** (Unity на цій машині немає): шрифт підключено через `-unity-font-definition: url("../../Art/Fonts/…ttf")` у `Resources/Theme/kebab_theme.uss`; чи завантажується він у збірці, покаже CI/скриншот (див. журнал Q-06).

## 2. Графіка

Усі 24 PNG у `Assets/_Project/Resources/Art/` — плейсхолдери власного виготовлення: `tools/art/generate_placeholder_art.ps1` (прості фігури; без зовнішніх зображень, шрифтів, AI). Ліцензія `Original` (авторське право проєкту), комерційне використання так, атрибуція не потрібна, дата 2026-11-13. Побудовано по-файлово в `docs/art/ASSET_REGISTRY.md`.

| Група | Файли | Джерело | Ліцензія | Дата |
|---|---|---|---|---|
| Фон | `bg_bar.png` (Q-06d: вікно на вулицю, гірлянда, полиці, прилавок), `bg_menu.png` (нічна вулиця й будка) | власний код | Original | 2026-11-16 |
| Стани м'яса | `icon_meat_raw/cooking/done/warning/burnt.png` | власний код | Original | 2026-11-13 |
| Інгредієнти | `ing_pita/meat/salad/onion/sauce_garlic/sauce_hot.png` | власний код | Original | 2026-11-13 |
| HUD-значки | `icon_heart_full/empty`, `icon_coin`, `icon_star_full/empty`, `icon_lock`, `icon_pause`, `icon_menu` | власний код | Original | 2026-11-13 |
| Портрети | `chr_olya/marko/hanna/ali.png` (вигадані образи, без етнічних атрибутів і реальних людей) | власний код | Original | 2026-11-13 |

Графіка, намальована під час виконання кодом (градієнти, віньєтки, тіні, відблиски, частинки): `Assets/_Project/Game/Views/UiDecor.cs`, `FxLayer.cs`, `TapDecor.cs` та ін. — власний код, зовнішніх файлів немає.

## 3. Звуки

6 файлів `Assets/_Project/Resources/Audio/sfx_*.wav` — синтезовані власним скриптом `tools/audio/generate_placeholder_sfx.ps1`; ліцензія `Original`, комерційне використання так, атрибуція не потрібна, 2026-11-13. По-файлово — `docs/art/ASSET_REGISTRY.md`. Музики немає.

## 4. Ресурси третіх сторін

| Ресурс | Статус | Ліцензія (перевірено) | Примітка |
|---|---|---|---|
| Nunito (шрифт) | **додано** (розділ 1) | OFL 1.1 | — |
| Kenney Particle Pack (https://kenney.nl/assets/particle-pack, 80 файлів) | **НЕ додано в Q-06** | CC0 (сторінка набору: Creative Commons CC0; https://kenney.nl/support: «all game assets … are public domain licensed (CC0)», «free to use them, even in commercial projects», «Attribution is not required»; перевірено 2026-11-15) | Кнопка «Download» веде на `#inline-download` (завантаження через браузер); агент не мав дозволеного шляху для безпечного скачування без ручного кроку. Пару, дим і іскри вже зроблено кодом у Q-05 — видимий виграш від набору невеликий. Повернутися, якщо знадобляться готові спрайти |
| Kenney Emotes Pack (https://kenney.nl/assets/emotes-pack, 480 файлів) | **НЕ додано** | CC0 (ті ж умови Kenney; сторінку набору окремо не відкривали — **НЕ ПЕРЕВІРЕНО**) | Те саме: бульбашки емоцій краще робити кодом, щоб лишитися в одному стилі |
| game-icons.net, OpenGameArt | **не використовуємо** | game-icons: CC BY 3.0 (потрібна атрибуція); OpenGameArt: ліцензія окрема на кожен файл | Невідомі/умовні права = не беремо |

## 5. Правила внесення нового ресурсу
1. Спочатку рядок у реєстрі (файл, джерело, автор/інструмент, ліцензія, комерційне використання, атрибуція, дата, URL перевірки), потім файл у `Assets`.
2. Шрифт: лише OFL або подібна; покриття латиниці, кирилиці, польських літер перевіряти по `cmap` до додавання.
3. Ресурс, згенерований AI: додатково інструмент, модель, план, промпт, посилання на умови й дата перевірки. Зараз таких немає.
4. Тест `AssetRegisterTests` перевіряє, що кожен файл у `Art/Fonts` має рядок у цьому реєстрі.
