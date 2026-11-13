using System;
using System.Collections.Generic;
using KebabStreet.Core;
using UnityEngine;

namespace KebabStreet.Game
{
    /// <summary>
    /// Тонка обгортка: читає JSON-конфіги з <c>Resources/Config</c> і віддає тексти в Core-завантажувач
    /// (<see cref="ConfigLoader"/>). Усієї логіки і перевірок тут немає. Відсутній файл — null у джерелі,
    /// а завантажувач повідомляє «файл не знайдено» з назвою документа.
    /// </summary>
    public static class ResourceConfigSource
    {
        public const string Folder = "Config/";
        public const string LevelsFolder = "Config/levels";

        /// <summary>Читає всі конфіги; рівні — усі файли з папки levels, за назвою.</summary>
        public static ConfigSources Load()
        {
            TextAsset[] levelAssets = Resources.LoadAll<TextAsset>(LevelsFolder);
            var levels = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < levelAssets.Length; i++)
            {
                levels.Add(new KeyValuePair<string, string>(levelAssets[i].name, levelAssets[i].text));
            }

            levels.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return new ConfigSources(
                ReadText("stations"),
                ReadText("recipes"),
                ReadText("customers"),
                ReadText("rules"),
                levels);
        }

        /// <summary>Завантажує й перевіряє всі рівні; помилки конфігів — у результаті, а не винятком.</summary>
        public static ConfigResult<LevelCatalog> LoadCatalog()
        {
            return ConfigLoader.LoadAll(Load());
        }

        /// <summary>Читає й перевіряє tutorial.json (S1-11); помилки — у результаті, а не винятком.</summary>
        public static TutorialLoadResult LoadTutorial()
        {
            return TutorialConfigLoader.Parse(ReadText("tutorial"));
        }

        private static string ReadText(string name)
        {
            var asset = Resources.Load<TextAsset>(Folder + name);
            return asset == null ? null : asset.text;
        }
    }
}
