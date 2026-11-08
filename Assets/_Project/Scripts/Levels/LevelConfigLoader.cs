using System;
using UnityEngine;

namespace PerfectParcel.Levels
{
    /// <summary>
    /// Завантажувач конфігурації рівнів із JSON або Resources.
    /// Забезпечує миттєву ініціалізацію та перемикання 30 рівнів гри.
    /// </summary>
    public static class LevelConfigLoader
    {
        private const string ResourcePath = "Levels/levels_config";
        private static LevelConfig[] _cachedLevels;

        public static LevelConfig[] LoadAllLevels()
        {
            if (_cachedLevels != null && _cachedLevels.Length > 0)
            {
                return _cachedLevels;
            }

            var jsonFile = Resources.Load<TextAsset>(ResourcePath);
            if (jsonFile == null)
            {
                Debug.LogError($"[LevelConfigLoader] Не вдалося знайти файл {ResourcePath} у Resources!");
                return Array.Empty<LevelConfig>();
            }

            return LoadFromJson(jsonFile.text);
        }

        public static LevelConfig[] LoadFromJson(string jsonText)
        {
            try
            {
                var wrapper = JsonUtility.FromJson<LevelsWrapper>(jsonText);
                if (wrapper != null && wrapper.levels != null)
                {
                    _cachedLevels = wrapper.levels;
                    Debug.Log($"[LevelConfigLoader] Успішно завантажено {_cachedLevels.Length} рівнів.");
                    return _cachedLevels;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelConfigLoader] Помилка десеріалізації JSON рівнів: {ex.Message}");
            }

            return Array.Empty<LevelConfig>();
        }

        public static LevelConfig GetLevelConfig(int levelIndex)
        {
            var all = LoadAllLevels();
            if (levelIndex >= 1 && levelIndex <= all.Length)
            {
                return all[levelIndex - 1];
            }
            return all.Length > 0 ? all[0] : null;
        }
    }
}
