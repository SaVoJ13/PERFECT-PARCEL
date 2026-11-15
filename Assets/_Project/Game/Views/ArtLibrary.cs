using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>Вираз обличчя клієнта (Q-06b): за станом терпіння.</summary>
    public enum ClientMood
    {
        Neutral = 0,
        Happy = 1,
        Worried = 2,
        Angry = 3,
    }

    /// <summary>
    /// Спрайти плейсхолдер-графіки (S1-12) з <c>Resources/Art</c> і тема USS з <c>Resources/Theme</c>.
    /// Лише вигляд: якщо спрайт чи тему не знайдено, елемент лишається з резервним кольором (без винятків).
    /// </summary>
    public static class ArtLibrary
    {
        /// <summary>Тека спрайтів відносно Resources.</summary>
        public const string ArtFolder = "Art/";

        /// <summary>Шлях теми USS відносно Resources (без розширення).</summary>
        public const string ThemePath = "Theme/kebab_theme";

        public const string Background = "bg_bar";
        public const string MenuBackground = "bg_menu";
        public const string MeatRaw = "icon_meat_raw";
        public const string MeatCooking = "icon_meat_cooking";
        public const string MeatDone = "icon_meat_done";
        public const string MeatWarning = "icon_meat_warning";
        public const string MeatBurnt = "icon_meat_burnt";
        public const string HeartFull = "icon_heart_full";
        public const string HeartEmpty = "icon_heart_empty";
        public const string Coin = "icon_coin";
        public const string StarFull = "icon_star_full";
        public const string StarEmpty = "icon_star_empty";
        public const string Lock = "icon_lock";
        public const string Pause = "icon_pause";
        public const string Menu = "icon_menu";
        public const string Ali = "chr_ali";

        /// <summary>Усі імена спрайтів (без .png); тест звіряє їх із файлами в Resources/Art і реєстром.</summary>
        public static readonly string[] AllNames =
        {
            Background, MenuBackground, MeatRaw, MeatCooking, MeatDone, MeatWarning, MeatBurnt,
            "ing_pita", "ing_meat", "ing_salad", "ing_onion", "ing_sauce_garlic", "ing_sauce_hot",
            HeartFull, HeartEmpty, Coin, StarFull, StarEmpty, Lock, Pause, Menu,
            "chr_olya", "chr_marko", "chr_hanna", Ali,
            "chr_olya_happy", "chr_olya_worried", "chr_olya_angry",
            "chr_marko_happy", "chr_marko_worried", "chr_marko_angry",
            "chr_hanna_happy", "chr_hanna_worried", "chr_hanna_angry",
        };

        /// <summary>Виріз терпіння, з якого клієнт «щасливий» (Q-06b): щойно прийшов або в пільговому часі.</summary>
        public const int HappyFromPercent = 85;

        /// <summary>Нижче цього відсотка терпіння клієнт нетерплячий (занепокоєний).</summary>
        public const int WorriedBelowPercent = 60;

        /// <summary>
        /// Вираз клієнта за станом терпіння (лише читання <see cref="PatienceView"/>, без логіки Core): Low (менше
        /// одного сердечка) — сердитий; менше 60% — занепокоєний; від 85% або пільговий час — щасливий; інакше нейтральний.
        /// </summary>
        public static ClientMood MoodOf(PatienceView patience)
        {
            if (patience.Low)
            {
                return ClientMood.Angry;
            }

            if (patience.Grace || patience.Percent >= HappyFromPercent)
            {
                return ClientMood.Happy;
            }

            return patience.Percent < WorriedBelowPercent ? ClientMood.Worried : ClientMood.Neutral;
        }

        /// <summary>Ім'я спрайта вираза: нейтральний — базове ім'я, решта — з суфіксом <c>_happy/_worried/_angry</c>.</summary>
        public static string MoodName(string portraitName, ClientMood mood)
        {
            if (string.IsNullOrEmpty(portraitName) || mood == ClientMood.Neutral)
            {
                return portraitName;
            }

            return portraitName + "_" + mood.ToString().ToLowerInvariant();
        }

        /// <summary>Зазор між смужкою іконок і написом кнопки.</summary>
        public const float IconTextGap = 24f;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static StyleSheet _theme;
        private static bool _themeLoaded;

        /// <summary>Спрайт за ім'ям або null, якщо його немає.</summary>
        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            Sprite sprite;
            if (!Cache.TryGetValue(name, out sprite))
            {
                sprite = Resources.Load<Sprite>(ArtFolder + name);
                Cache[name] = sprite;
            }

            return sprite;
        }

        /// <summary>Ім'я спрайта-портрета клієнта за ключем імені типу (<c>customer.olya.name</c> -> <c>chr_olya</c>).</summary>
        public static string PortraitName(string nameKey)
        {
            const string prefix = "customer.";
            const string suffix = ".name";
            if (string.IsNullOrEmpty(nameKey) || !nameKey.StartsWith(prefix) || !nameKey.EndsWith(suffix)
                || nameKey.Length <= prefix.Length + suffix.Length)
            {
                return null;
            }

            return "chr_" + nameKey.Substring(prefix.Length, nameKey.Length - prefix.Length - suffix.Length);
        }

        /// <summary>
        /// Ставить спрайт фоном елемента (повністю в межах елемента). Немає спрайта — фон не чіпає.
        /// true — спрайт знайдено.
        /// </summary>
        public static bool Apply(VisualElement element, string name, bool cover = false)
        {
            Sprite sprite = Get(name);
            if (element == null)
            {
                return false;
            }

            if (sprite == null)
            {
                element.style.backgroundImage = StyleKeyword.None;
                return false;
            }

            element.style.backgroundImage = new StyleBackground(sprite);
            element.style.backgroundSize = new BackgroundSize(cover ? BackgroundSizeType.Cover : BackgroundSizeType.Contain);
            element.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            element.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            element.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            return true;
        }

        /// <summary>
        /// Як <see cref="Apply"/>, але спрайт малюється фіксованим розміром від лівого верхнього кута
        /// (для заповнення сердечка: обрізається шириною елемента). true — спрайт знайдено.
        /// </summary>
        public static bool ApplyFixed(VisualElement element, string name, float size)
        {
            Sprite sprite = Get(name);
            if (element == null || sprite == null)
            {
                return false;
            }

            element.style.backgroundImage = new StyleBackground(sprite);
            element.style.backgroundSize = new BackgroundSize(new Length(size, LengthUnit.Pixel), new Length(size, LengthUnit.Pixel));
            element.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left);
            element.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
            element.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            return true;
        }

        /// <summary>
        /// Додає до батька квадратну іконку-спрайт (клас <c>ks-icon</c>). Немає спрайта — повертає null і нічого не додає
        /// (текстова альтернатива лишається в підписі).
        /// </summary>
        public static VisualElement AddIcon(VisualElement parent, string name, float size, bool first = false)
        {
            if (parent == null || Get(name) == null)
            {
                return null;
            }

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("ks-icon");
            icon.style.width = size;
            icon.style.height = size;
            icon.style.flexShrink = 0;
            Apply(icon, name);
            if (first)
            {
                parent.Insert(0, icon);
            }
            else
            {
                parent.Add(icon);
            }

            return icon;
        }

        /// <summary>
        /// S1-23: іконки кнопки зі своїм текстом (текст <c>Button.text</c> не є flex-елементом, тому діти кнопки
        /// лягали б поверх нього). Іконки йдуть рядком біля лівого краю, а кнопка отримує однакові бічні відступи
        /// на ширину рядка, тож напис лишається по центру і не перекривається. Немає жодного спрайта — нічого не
        /// міняє, повертає null.
        /// </summary>
        public static VisualElement AddLeadingIcons(VisualElement button, string[] names, float size, float margin)
        {
            if (button == null || names == null)
            {
                return null;
            }

            var strip = new VisualElement { pickingMode = PickingMode.Ignore, name = "icon-strip" };
            strip.style.position = Position.Absolute;
            strip.style.left = margin;
            strip.style.top = 0;
            strip.style.bottom = 0;
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.alignItems = Align.Center;
            int added = 0;
            for (int i = 0; i < names.Length; i++)
            {
                if (AddIcon(strip, names[i], size) != null)
                {
                    added++;
                }
            }

            if (added == 0)
            {
                return null;
            }

            button.Add(strip);
            // Напис по центру вільної частини: ліворуч місце під іконки і зазор, праворуч лише поле (S1-23, L3).
            button.style.paddingLeft = (added * size) + (2f * margin) + IconTextGap;
            button.style.paddingRight = margin;
            return strip;
        }

        /// <summary>
        /// Як <see cref="AddLeadingIcons"/>, але одна іконка зверху по центру кнопки, а напис нижче (вузькі кнопки
        /// станцій: слова не ламаються, бо ширина тексту не зменшується). Немає спрайта — null.
        /// </summary>
        public static VisualElement AddTopIcon(VisualElement button, string name, float size, float margin)
        {
            if (button == null || Get(name) == null)
            {
                return null;
            }

            var strip = new VisualElement { pickingMode = PickingMode.Ignore, name = "icon-strip" };
            strip.style.position = Position.Absolute;
            strip.style.left = 0;
            strip.style.right = 0;
            strip.style.top = margin;
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.justifyContent = Justify.Center;
            AddIcon(strip, name, size);
            button.Add(strip);
            button.style.paddingTop = size + (2f * margin);
            return strip;
        }

        /// <summary>Ім'я спрайта інгредієнта за його ідентифікатором (<c>ing_&lt;id&gt;</c>).</summary>
        public static string IngredientIcon(string ingredientId)
        {
            return "ing_" + ingredientId;
        }

        /// <summary>
        /// Q-05b: велика іконка по центру кнопки (значок замість слова). Повертає внутрішній елемент-іконку;
        /// спрайт ставиться через <see cref="ShowIcon"/>. Іконка прихована, доки спрайт не задано.
        /// Центральна смужка не ловить дотики і лежить поверх тіні/відблиску кнопки.
        /// </summary>
        public static VisualElement AddCenterIcon(VisualElement button, float size, string name)
        {
            if (button == null)
            {
                return null;
            }

            var strip = new VisualElement { pickingMode = PickingMode.Ignore, name = name + "-strip" };
            strip.style.position = Position.Absolute;
            strip.style.left = 0;
            strip.style.right = 0;
            strip.style.top = 0;
            strip.style.bottom = 0;
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.justifyContent = Justify.Center;
            strip.style.alignItems = Align.Center;
            var icon = new VisualElement { pickingMode = PickingMode.Ignore, name = name };
            icon.AddToClassList("ks-icon");
            icon.style.width = size;
            icon.style.height = size;
            icon.style.flexShrink = 0;
            icon.style.display = DisplayStyle.None;
            strip.Add(icon);
            button.Add(strip);
            return icon;
        }

        /// <summary>Ставить спрайт у іконку й показує її; немає спрайта (або name null) — ховає. true — показано.</summary>
        public static bool ShowIcon(VisualElement icon, string name)
        {
            if (icon == null)
            {
                return false;
            }

            bool shown = !string.IsNullOrEmpty(name) && Apply(icon, name);
            icon.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            return shown;
        }

        /// <summary>Підключає тему USS до кореня (повторне підключення нічого не міняє). Немає теми — нічого не робить.</summary>
        public static void ApplyTheme(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            if (!_themeLoaded)
            {
                _theme = Resources.Load<StyleSheet>(ThemePath);
                _themeLoaded = true;
            }

            if (_theme != null && !root.styleSheets.Contains(_theme))
            {
                root.styleSheets.Add(_theme);
            }

            root.AddToClassList("ks-root");
        }
    }
}
