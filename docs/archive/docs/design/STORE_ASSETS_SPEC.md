# СПЕЦИФІКАЦІЯ МАРКЕТИНГОВИХ МАТЕРІАЛІВ GOOGLE PLAY (STORE ASSETS SPECIFICATION)

> **Проєкт:** PERFECT PARCEL  
> **Фаза:** Phase 3 — Підготовка до релізу  
> **Відповідальний за підготовку:** Agent 2 (UI/UX & Art Designer)  
> **Куратор:** Agent 0 (Project Manager & Chief Architect)  
> **Цільова платформа:** Google Play Console (Android)  

---

## 1. ТЕХНІЧНІ ВИМОГИ GOOGLE PLAY CONSOLE (2026)

| Тип матеріалу | Роздільна здатність | Формат файлу | Макс. розмір | Особливості та вимоги |
| :--- | :---: | :---: | :---: | :--- |
| **Іконка додатка (App Icon)** | **512 × 512 px** | PNG (32-bit з альфа) | 1,024 KB | Чіткий квадрат без ручного скруглення кутів (Google Play накладає радіус скруглення 20% автоматично). |
| **Головний банер (Feature Graphic)** | **1024 × 500 px** | JPEG або PNG (без альфа) | 15 MB | Безпечна зона (Safe Zone): 15% від країв. Горизонтальна орієнтація. Візуальний центр композиції. |
| **Скриншоти гри (Phone Screenshots)** | **1080 × 1920 px** (9:16) | PNG / JPEG | 8 MB / файл | Від 4 до 8 вертикальних скриншотів із маркетинговими заголовками вгорі. |

---

## 2. ІКОНКА ДОДАТКА (APP ICON — 512×512 PX)

### 2.1. Візуальна концепція
- **Центральний елемент:** Соковита стилізована крафтова коробка з круглими фасками, перев'язана золотисто-жовтою стрічкою, з білою адресною наліпкою.
- **Динаміка:** Навколо коробки у швидкісному русі розташовані 3 кольорові стрілки-маркери (🔵 Синя, 🟢 Зелена, 🔴 Червона).
- **Стиль:** 2D Casual / Stylized 3D render, насичені теплі кольори, м'який контурний обрис.

### 2.2. Готовий AI-промпт для генерації
```text
mobile game app icon, stylized cute 2d casual cardboard delivery box with rounded corners, bright yellow packaging tape and white barcode label, encircled by 3 glossy colorful direction arrows (blue, emerald green, vibrant red), warehouse logistics theme, vibrant saturated colors, soft lighting, clean solid dark indigo blue background, modern vector app icon, high quality 512x512, centered composition, no text
```

---

## 3. ГОЛОВНИЙ БАНЕР (FEATURE GRAPHIC — 1024×500 PX)

### 3.1. Візуальна концепція
- **Ліва частина:** Конвеєрна стрічка, що згинається у перспективі, на якій рухаються 3 типи коробок (помаранчева Express із блискавкою, біла порцелянова Fragile та темний дубовий ящик 20 KG).
- **Центральна частина:** Глянцевий 3D-логотип гри: **«PERFECT PARCEL»** із соковитими золотистими літерами, білою контурною обводкою та бліками.
- **Права частина:** Сортувальні канали, що випромінюють приємне сяйво, розлітаються золоті монети та зірочки.
- **Фон:** Стилізований затишний теплий склад у легкій розмитості (Depth of Field).

### 3.2. Готовий AI-промпт для генерації
```text
wide horizontal feature banner for casual mobile game, vibrant stylized warehouse background, moving industrial conveyor belt in dynamic perspective, colorful delivery parcels: cardboard box, orange express box with hazard stripes, white fragile box with red wine glass stamp, floating shiny gold coins and stars, big bold glossy 3D cartoon title text "PERFECT PARCEL" in center with golden bevel and white stroke, cheerful colorful lighting, professional game marketing art, 1024x500 resolution, safe margins
```

---

## 4. ШАБЛОНИ 5 МАРКЕТИНГОВИХ СКРИНШОТІВ (1080×1920 PX)

Кожен скриншот складається з:
1. Верхньої кольорової плашки із соковитим коротким заголовком (2–4 слова).
2. Реального кадру гри (геймплей, магазин, нагороди) в центрі.
3. Елементів Game Juice (пальчик зі свайпом, літаючі зірки чи монети).

### Скриншот 1: Швидке та захоплююче сортування
- **Візуал кадру:** Активний конвеєр, посилка на рівні лінії сортування, жест свайпу вправо, виліт очок `+250 COMBO!`.
- **Заголовки (локалізовані):**
  - 🇬🇧 **EN:** SWIPE & SORT! Master the Conveyor Belt
  - 🇺🇦 **UA:** СВАЙПАЙ ТА СОРТУЙ! Підкори швидкісний конвеєр
  - 🇵🇱 **PL:** PRZESUWAJ I SORTUJ! Zostań mistrzem taśmociągu
  - 🇩🇪 **DE:** WISCHEN & SORTIEREN! Meistere das Förderband
  - 🇪🇸 **ES:** ¡DESLIZA Y CLASIFICA! Domina la cinta transportadora
  - 🌐 **RU:** СВАЙПАЙ И СОРТИРУЙ! Покори конвейер склада

### Скриншот 2: Різноманіття вантажів
- **Візуал кадру:** Усі 5 типів посилок (Standard, Express, Fragile, Heavy, Mystery) на конвеєрі з піктограмами правил.
- **Заголовки (локалізовані):**
  - 🇬🇧 **EN:** 5 UNIQUE PARCELS! Fragile, Heavy & Express
  - 🇺🇦 **UA:** 5 ТИПІВ ПОСИЛОК! Термінові, важкі та крихкі вантажі
  - 🇵🇱 **PL:** 5 TYPÓW PACZEK! Pilne, ciężkie i delikatne
  - 🇩🇪 **DE:** 5 EINZIGARTIGE PAKETE! Eilig, schwer und zerbrechlich
  - 🇪🇸 **ES:** ¡5 TIPOS DE PAQUETES! Frágiles, pesados y express
  - 🌐 **RU:** 5 ТИПОВ ПОСЫЛОК! Срочные, тяжелые и хрупкие

### Скриншот 3: Модернізація складу (Мета-гра)
- **Візуал кадру:** Екран `UpgradesScreen` із картками прокачки конвеєра, автосканера та золотого множника.
- **Заголовки (локалізовані):**
  - 🇬🇧 **EN:** UPGRADE YOUR WAREHOUSE! 7 Powerful Boosts
  - 🇺🇦 **UA:** МОДЕРНІЗУЙ ВЛАСНИЙ СКЛАД! 7 потужних покращень
  - 🇵🇱 **PL:** ULEPSZAJ SWÓJ MAGAZYN! 7 potężnych ulepszeń
  - 🇩🇪 **DE:** VERBESSERE DEIN LAGER! 7 mächtige Upgrades
  - 🇪🇸 **ES:** ¡MEJORA TU ALMACÉN! 7 mejoras poderosas
  - 🌐 **RU:** УЛУЧШАЙ СВОЙ СКЛАД! 7 мощных апгрейдов

### Скриншот 4: Щоденні нагороди та досягнення
- **Візуал кадру:** Відкритий модал 7-денного календаря з підсвіченою кнопкою `[ 🎬 ЗАБРАТИ 2X ]` та великою скринею 7-го дня.
- **Заголовки (локалізовані):**
  - 🇬🇧 **EN:** 7-DAY LOGIN STREAK! Double Rewards with Video
  - 🇺🇦 **UA:** 7-ДЕННІ НАГОРОДИ! Подвоюй виграш у 2 рази
  - 🇵🇱 **PL:** 7-DNIOWE NAGRODY! Podwajaj wygrane dzięki wideo
  - 🇩🇪 **DE:** 7-TAGE BELOHNUNGEN! Verdopple Gewinne mit Video
  - 🇪🇸 **ES:** ¡RECOMPENSAS DE 7 DÍAS! Duplica ganancias con vídeo
  - 🌐 **RU:** 7-ДНЕВНЫЕ НАГРАДЫ! Удваивай монеты за видео

### Скриншот 5: 100% Офлайн-гра будь-де
- **Візуал кадру:** Карта 30 рівнів із зірками та значок літака / офлайн-доступності.
- **Заголовки (локалізовані):**
  - 🇬🇧 **EN:** PLAY OFFLINE ANYWHERE! 30 Exciting Levels
  - 🇺🇦 **UA:** ГРАЙ ОФЛАЙН У ДОРОЗІ! 30 захопливих рівнів
  - 🇵🇱 **PL:** GRAJ OFFLINE WSZĘDZIE! 30 wciągających poziomów
  - 🇩🇪 **DE:** ÜBERALL OFFLINE SPIELEN! 30 spannende Level
  - 🇪🇸 **ES:** ¡JUEGA SIN CONEXIÓN! 30 niveles emocionantes
  - 🌐 **RU:** ИГРАЙ ОФЛАЙН ВЕЗДЕ! 30 захватывающих уровней

---

## 5. ТЕКСТОВИЙ ОПИС ДЛЯ СТОРІНКИ GOOGLE PLAY

### Короткий опис (Short Description — до 80 символів):
- 🇬🇧 **EN:** Fast-paced warehouse puzzle! Swipe parcels, upgrade your warehouse & play offline!
- 🇺🇦 **UA:** Сортуй вантажі, модернізуй склад та грай 100% офлайн! Захоплива головоломка.

### Повний опис (Full Description):
```text
Ласкаво просимо до PERFECT PARCEL — соковитої казуальної головоломки, де саме ви керуєте найжвавішим складом доставки у місті!

Конвеєрна стрічка невпинно рухається, посилки прибувають щомиті, а місто чекає на свої замовлення. Ваше завдання — за лічені секунди визначити правильний канал сортування та влучним свайпом відправити вантаж за адресою!

ОСОБЛИВОСТІ ГРИ:

📦 5 УНІКАЛЬНИХ ТИПІВ ВАНТАЖУ:
• Стандартні крафтові коробки для швидкого старту.
• Термінові посилки (Express) — мчать на високій швидкості!
• Крихкий вантаж (Fragile) — обережно, розіб'ється при помилці!
• Важкі ящики (Heavy Freight 20kg) — потребують сильного свайпу.
• Спеціальні кейси (Mystery) — тапни для сканування перед сортуванням!

🛠️ 7 ПОТУЖНИХ СКЛАДСЬКИХ ПОКРАЩЕНЬ:
Заробляйте монети за проходження змін і прокачуйте своє обладнання: плавний конвеєр, магнітні напрямні, буфери безпеки, швидкі авто-сканери та золоті помножувачі доходу!

🎁 ЩОДЕННІ ПОДАРУНКИ ТА ДЕЛІКАТНА МОНЕТИЗАЦІЯ:
Заходьте щодня за 7-денним календарем нагород! Подвоюйте виграші переглядом відео та скористайтеся вигідними пакетами монет у магазині, які назавжди відключають примусову рекламу (No-Ads).

📴 100% ОФЛАЙН-ГРА:
Жодного обов'язкового інтернету, жодної реєстрації чи акаунтів. Грайте в дорозі, у метро або на відпочинку — гра завжди готова до роботи!

🌐 6 МОВ ІНТЕРФЕЙСУ:
Повна підтримка української, англійської, польської, німецької, іспанської та інших мов із можливістю швидкого перемикання.

Чи зможете ви пройти всі 30 рівнів і отримати статус «Легенда логістики»? 
Завантажуйте PERFECT PARCEL просто зараз і починайте зміну!
```
