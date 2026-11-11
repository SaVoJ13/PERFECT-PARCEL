using UnityEngine;

namespace KebabStreet.Game
{
    /// <summary>
    /// Тексти з файлів <c>Resources/Localization/&lt;код&gt;.json</c>. Спершу обрана мова, потім запасна (EN),
    /// невідомий ключ повертається як "[ключ]".
    /// </summary>
    public sealed class JsonLocalizer : ILocalizer
    {
        public const string FallbackLanguage = "en";
        public const string ResourceFolder = "Localization/";

        private readonly LocalizationTable _primary;
        private readonly LocalizationTable _fallback;

        public JsonLocalizer(LocalizationTable primary, LocalizationTable fallback)
        {
            _primary = primary;
            _fallback = fallback;
        }

        /// <summary>Код мови за мовою системи: uk, pl, інакше en.</summary>
        public static string LanguageCodeOf(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.Polish: return "pl";
                default: return FallbackLanguage;
            }
        }

        /// <summary>Завантажує мову системи із запасною EN. Відсутній або зіпсований файл мови не валить гру.</summary>
        public static JsonLocalizer CreateForSystemLanguage()
        {
            string code = LanguageCodeOf(Application.systemLanguage);
            LocalizationTable fallback = Load(FallbackLanguage);
            LocalizationTable primary = code == FallbackLanguage ? fallback : Load(code);
            return new JsonLocalizer(primary, fallback);
        }

        private static LocalizationTable Load(string code)
        {
            var asset = Resources.Load<TextAsset>(ResourceFolder + code);
            if (asset == null)
            {
                Debug.LogError("Немає файлу мови: " + code);
                return null;
            }

            try
            {
                return LocalizationTable.Parse(asset.text);
            }
            catch (System.FormatException e)
            {
                Debug.LogError("Файл мови " + code + " пошкоджено: " + e.Message);
                return null;
            }
        }

        public string Get(string key)
        {
            string value;
            if (_primary != null && _primary.TryGet(key, out value))
            {
                return value;
            }

            if (_fallback != null && _fallback.TryGet(key, out value))
            {
                return value;
            }

            return "[" + key + "]";
        }
    }
}
