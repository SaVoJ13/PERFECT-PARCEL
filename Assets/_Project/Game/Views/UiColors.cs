using UnityEngine;

namespace KebabStreet.Game
{
    /// <summary>Пара «колір тексту / колір фону» для перевірки контрасту тестом (S1-17).</summary>
    public readonly struct ContrastPair
    {
        public ContrastPair(string name, Color text, Color background, bool large)
        {
            Name = name;
            Text = text;
            Background = background;
            Large = large;
        }

        public string Name { get; }

        public Color Text { get; }

        public Color Background { get; }

        /// <summary>Великий текст (від 24 px жирним чи 32 px): достатньо 3:1; інакше 4.5:1.</summary>
        public bool Large { get; }
    }

    /// <summary>
    /// Кольори тексту й підкладок екранів (S1-17, читабельність). Усі пари «текст на фоні», задані статично,
    /// зібрано в <see cref="Pairs"/>; EditMode-тест рахує їхній контраст за WCAG (4.5:1; 3:1 для великого).
    /// Напівпрозорі кольори рахуються поверх білого: це найгірший випадок для світлого тексту.
    /// </summary>
    public static class UiColors
    {
        public static readonly Color White = Color.white;
        public static readonly Color Ink = new Color(0.10f, 0.06f, 0.03f);
        public static readonly Color Espresso = new Color(0.231f, 0.137f, 0.078f);
        public static readonly Color NightPlum = new Color(0.169f, 0.106f, 0.180f);
        public static readonly Color Saffron = new Color(1f, 0.718f, 0.012f);
        public static readonly Color Coin = new Color(1f, 0.79f, 0.2f);
        public static readonly Color Warning = new Color(0.96f, 0.62f, 0.04f);

        /// <summary>Підкладка під написи верхньої смуги: білий текст на білих смугах навісу не читався.</summary>
        public static readonly Color HudBackdrop = new Color(0.231f, 0.137f, 0.078f, 0.80f);

        /// <summary>Підкладка під написи станції збирання: на дерев'яному фоні сірі й бурштинові написи зникали.</summary>
        public static readonly Color LabelBackdrop = new Color(0.231f, 0.137f, 0.078f, 0.92f);

        /// <summary>Зелений текст «страва готова» на підкладці (світліший за зелений фон кнопок).</summary>
        public static readonly Color ReadyText = new Color(0.45f, 0.90f, 0.55f);

        public static readonly Color StationButton = new Color(0.61f, 0.35f, 0.17f);
        public static readonly Color ItemButton = new Color(0.42f, 0.27f, 0.19f);
        public static readonly Color ClearButton = new Color(0.45f, 0.12f, 0.12f);
        public static readonly Color QueueButton = new Color(0.25f, 0.25f, 0.25f);

        public static readonly Color CustomerCard = new Color(0.30f, 0.20f, 0.14f);
        public static readonly Color CustomerFreeCard = new Color(0.20f, 0.15f, 0.12f);
        public static readonly Color ChipStep = new Color(0.61f, 0.35f, 0.17f);
        public static readonly Color ChipRequested = new Color(0.12f, 0.50f, 0.24f);
        public static readonly Color ChipExcluded = new Color(0.25f, 0.25f, 0.25f);

        /// <summary>Підпис «клієнт пішов» на картці (світліший за червону рамку, яка не годиться для тексту).</summary>
        public static readonly Color LeftText = new Color(1f, 0.60f, 0.55f);

        public static readonly Color GrillEmpty = new Color(0.42f, 0.27f, 0.19f);
        public static readonly Color GrillRaw = new Color(0.91f, 0.51f, 0.48f);
        public static readonly Color GrillCooking = new Color(0.80f, 0.42f, 0.30f);
        public static readonly Color GrillDone = new Color(0.12f, 0.50f, 0.24f);
        public static readonly Color GrillWarning = new Color(0.96f, 0.62f, 0.04f);
        public static readonly Color GrillBurnt = new Color(0.10f, 0.08f, 0.08f);

        public static readonly Color Plaque = new Color(0.20f, 0.13f, 0.09f, 0.96f);

        /// <summary>Кнопки плашки навчання: «Chili» палітри (ART_STYLE_GUIDE), білий текст ≥ 4.5:1.</summary>
        public static readonly Color TutorialButton = new Color(0.761f, 0.255f, 0.047f);

        public static readonly Color Overlay = new Color(0.10f, 0.07f, 0.06f, 0.92f);

        public static readonly ContrastPair[] Pairs =
        {
            new ContrastPair("Рівень/таймер/ціль на смузі", White, HudBackdrop, false),
            new ContrastPair("Монети на смузі", Coin, HudBackdrop, false),
            new ContrastPair("Напис станції", White, StationButton, false),
            new ContrastPair("Напис предмета дошки", White, ItemButton, false),
            new ContrastPair("Очистити дошку", White, ClearButton, false),
            new ContrastPair("Черга дій", White, QueueButton, false),
            new ContrastPair("Страва/черга на підкладці", White, LabelBackdrop, false),
            new ContrastPair("Підказка на підкладці", Warning, LabelBackdrop, false),
            new ContrastPair("Комбо на підкладці", Coin, LabelBackdrop, false),
            new ContrastPair("Страва готова на підкладці", ReadyText, LabelBackdrop, false),
            new ContrastPair("Картка клієнта", White, CustomerCard, false),
            new ContrastPair("Вільний слот", White, CustomerFreeCard, false),
            new ContrastPair("Позначка замовлення", White, ChipStep, false),
            new ContrastPair("Позначка потрібно", White, ChipRequested, false),
            new ContrastPair("Позначка без", White, ChipExcluded, false),
            new ContrastPair("Нагорода на картці", Coin, CustomerCard, false),
            new ContrastPair("Попередження на картці", Warning, CustomerCard, false),
            new ContrastPair("Клієнт пішов", LeftText, CustomerCard, false),
            new ContrastPair("Гриль: порожньо", White, GrillEmpty, false),
            new ContrastPair("Гриль: сире", GrillText(GrillRaw), GrillRaw, false),
            new ContrastPair("Гриль: смажиться", GrillText(GrillCooking), GrillCooking, false),
            new ContrastPair("Гриль: готово", GrillText(GrillDone), GrillDone, false),
            new ContrastPair("Гриль: пригорає", GrillText(GrillWarning), GrillWarning, false),
            new ContrastPair("Гриль: згоріло", GrillText(GrillBurnt), GrillBurnt, false),
            new ContrastPair("Плашка навчання", White, Plaque, false),
            new ContrastPair("Кнопки навчання", White, TutorialButton, false),
            new ContrastPair("Кнопка меню/результату", Espresso, Saffron, false),
            new ContrastPair("Заголовок/текст екрана", White, NightPlum, false),
            new ContrastPair("Повідомлення екрана", Saffron, NightPlum, false),
            new ContrastPair("Текст на затемненні паузи", White, Overlay, false),
        };

        /// <summary>Текст на кнопці гриля за її фоном: світлі стани (сире, смажиться, пригорає) — темний, решта — білий.</summary>
        public static Color GrillText(Color background)
        {
            return background == GrillRaw || background == GrillCooking || background == GrillWarning ? Ink : White;
        }

        /// <summary>Відносна яскравість sRGB за WCAG 2.x.</summary>
        public static float Luminance(Color c)
        {
            return (0.2126f * Linear(c.r)) + (0.7152f * Linear(c.g)) + (0.0722f * Linear(c.b));
        }

        /// <summary>Контраст двох кольорів (1..21); альфа змішується з білим.</summary>
        public static float Contrast(Color text, Color background)
        {
            Color bg = Blend(background, Color.white);
            Color fg = text.a < 1f ? Blend(text, bg) : text;
            float a = Luminance(fg) + 0.05f;
            float b = Luminance(bg) + 0.05f;
            return a > b ? a / b : b / a;
        }

        private static Color Blend(Color top, Color under)
        {
            float a = top.a;
            return new Color(
                (top.r * a) + (under.r * (1f - a)),
                (top.g * a) + (under.g * (1f - a)),
                (top.b * a) + (under.b * (1f - a)),
                1f);
        }

        private static float Linear(float v)
        {
            return v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        }
    }
}
