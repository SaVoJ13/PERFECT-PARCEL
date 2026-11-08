using System.Collections.Generic;
using UnityEngine;

namespace PerfectParcel.Levels
{
    /// <summary>
    /// Реєстр та сховище всіх конфігурацій рівнів.
    /// Дозволяє отримувати параметри будь-якого з 30 рівнів за його індексом (1–30).
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "PerfectParcel/Level Database")]
    public class LevelDatabase : ScriptableObject
    {
        [SerializeField] private List<LevelConfig> levels = new List<LevelConfig>();

        public IReadOnlyList<LevelConfig> AllLevels => levels;

        public LevelConfig GetLevel(int index)
        {
            if (index < 1 || index > levels.Count)
            {
                Debug.LogWarning($"[LevelDatabase] Рівень {index} відсутній. Повертаємо рівень за замовчуванням.");
                return levels.Count > 0 ? levels[0] : null;
            }
            return levels[index - 1];
        }

        public void SetLevels(IEnumerable<LevelConfig> newLevels)
        {
            levels.Clear();
            levels.AddRange(newLevels);
        }
    }
}
