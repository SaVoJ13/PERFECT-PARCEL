using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
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
            Background, MeatRaw, MeatCooking, MeatDone, MeatWarning, MeatBurnt,
            "ing_pita", "ing_meat", "ing_salad", "ing_onion", "ing_sauce_garlic", "ing_sauce_hot",
            HeartFull, HeartEmpty, Coin, StarFull, StarEmpty, Lock, Pause, Menu,
            "chr_olya", "chr_marko", "chr_hanna", Ali,
        };

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
