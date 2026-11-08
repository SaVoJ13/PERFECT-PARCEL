# ПЛАН ІНТЕГРАЦІЇ ГРАФІЧНИХ АССЕТІВ (ASSET INTEGRATION PLAN)

> **Проєкт:** PERFECT PARCEL  
> **Фаза:** Підготовка до Phase 2 (Розробка повного MVP)  
> **Відповідальний:** Agent 2 (UI/UX & Art Designer)  
> **Куратор:** Agent 0 (Project Manager & Chief Architect)  
> **Базовий стиль:** [ART_STYLE_GUIDE.md](file:///C:/Users/cocro/Downloads/PERFECT%20PARCEL/docs/design/ART_STYLE_GUIDE.md)  
> **Ліцензійний статус:** 100% Free for Commercial Use (CC0 / Public Domain / SIL OFL)  

---

## 1. БАЗОВІ ДЖЕРЕЛА ТА ПАКЕТИ ВІДКРИТИХ РЕСУРСІВ (CC0)

Для заміни процедурних кольорових квадратів на професійну 2D Casual графіку відібрано офіційні, ліцензійно чисті відкриті пакети:

1. **Kenney Game Assets (Creative Commons Zero — CC0):**
   - **Kenney UI Pack (Space / Industrial):** Векторні кнопки, рамки, сердечка, зірочки та монети.  
     *Джерело:* `https://kenney.nl/assets/ui-pack` (CC0).
   - **Kenney Sokoban / Warehouse Pack:** Спрайти дерев'яних ящиків, металевих кейсів та елементів конвеєра.  
     *Джерело:* `https://kenney.nl/assets/sokoban` (CC0).
   - **Kenney Audio (Interface Sounds & Digital Audio):** Звуки тапу, клацання кнопок, звуки успіху й помилки.  
     *Джерело:* `https://kenney.nl/assets/interface-sounds` (CC0).

2. **Google Fonts (SIL Open Font License — OFL):**
   - **Fredoka / Nunito ExtraBold:** Соковиті округлі шрифти для заголовків та очок.
   - **Rubik:** Читабельний геометричний гротеск для локалізації (EN/PL/UA).

---

## 2. СПЕЦИФІКАЦІЯ СПРАЙТІВ 5 ТИПІВ КОРОБОК ДЛЯ ПЕРЕХОДУ В PHASE 2

Кожен спрайт оптимізується під розмір **256x256 px** (PNG з прозорістю, PPU = 128, стиснення ASTC у Unity):

| № | Назва посилки | Візуальні деталі (Art Specification) | Колір та акценти | Промпт для генерації (AI Art Prompt) |
| :-: | :--- | :--- | :--- | :--- |
| **1** | **Standard Parcel** | Картонна коробка з крафтовою фактурою, товсті округлі контури, пакувальна стрічка по центру, біла адресна наліпка. | Основний: `#D97706`<br>Стрічка: адаптивна під лінію | `2d casual game asset, stylized brown cardboard delivery box, rounded corners, packaging tape, white shipping label, clean thick vector outline, flat shaded, mobile game asset, transparent background, isolated, 256x256` |
| **2** | **Express Parcel** | Яскрава спортивна коробка, діагональні жовто-чорні смуги швидкості, піктограма блискавки, круглий LED-індикатор таймера. | Основний: `#FB923C`<br>Смуги: `#FACC15` | `2d casual mobile game asset, urgent express shipping parcel, vibrant orange color, hazard caution stripes, yellow lightning bolt icon, rounded corners, clean thick lineart, isometric 2d view, isolated, transparent background` |
| **3** | **Fragile Parcel** | Білосніжна порцелянова коробка, пінопластові захисні куточки, великий червоний штамп келиха та стрілки "THIS SIDE UP". | Основний: `#F8FAFC`<br>Штамп: `#EF4444` | `2d casual game icon, fragile cargo parcel, clean white packaging box, big red fragile wine glass stamp, delicate shipping stickers, soft bevel, modern mobile puzzle style, clean vector outline, transparent background` |
| **4** | **Heavy Freight** | Масивний темний ящик із міцних дубових дощок, залізні ковані куточки з заклепками, трафаретний напис "20+ KG". | Основний: `#78350F`<br>Метал: `#4B5563` | `2d casual mobile game icon, heavy wooden freight crate, solid dark oak wood planks, steel reinforced corner brackets, iron bolts, industrial stencil font 20kg, thick cartoon outlines, transparent background` |
| **5** | **Secured Case** | Броньований титановий кейс/дипломат, матовий метал, цифровий кодовий замок з підсвічуванням або лазерний штрих-код. | Основний: `#94A3B8`<br>Замок: `#38BDF8` | `2d casual mobile game asset, high security sci-fi shipping case, brushed titanium silver container, digital keypad lock, glowing blue led indicator, futuristic high-tech courier brief, rounded bevel, transparent background` |

---

## 3. СПРАЙТИ КОНВЕЄРА ТА ЗОНИ СОРТУВАННЯ

- **Стрічка конвеєра (`Conveyor_Belt_Tile.png`):**
  - Безшовна текстура 128x128 px, яка повторюється по вертикалі (Tiled Sprite).
  - Фон темно-вугільний `#1E232A` з рухомими світлими шевронами `#3A4454`, що спрямовані вниз.
- **Бічні ролики та каркас (`Conveyor_Rails.png`):**
  - Металеві напрямні рейки з матового алюмінію `#CBD5E1` з тінями.
- **3 сортувальні канали (Chutes / Arrows):**
  - 🔵 **Лівий жолоб:** Синій напрямний козирок із білим символом кола (Circle).
  - 🟢 **Центральний жолоб:** Зелений козирок із білим символом трикутника (Triangle).
  - 🔴 **Правий жолоб:** Червоний козирок із білим символом ромба (Diamond).

---

## 4. СТРУКТУРА ЗБЕРІГАННЯ В ПРОЄКТІ UNITY
Усі підготовлені ассети розміщуються за шляхом:
```
Assets/_Project/Sprites/
├── Parcels/            # parcel_standard.png, parcel_express.png, etc.
├── Conveyor/           # belt_tile.png, rails_left.png, rails_right.png, chutes.png
├── UI/                 # buttons.png, hearts.png, coins.png, stars.png
└── Atlases/            # SpriteAtlas_Gameplay (об'єднання в 1 атлас для 1 Draw Call)
```
Це гарантує скорочення кількості викликів малювання (Draw Calls) до 2–4 на кадр, що забезпечує стабільні 60 FPS на будь-якому бюджетному пристрої.
