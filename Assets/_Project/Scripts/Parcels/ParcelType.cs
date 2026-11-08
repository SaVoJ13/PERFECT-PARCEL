using UnityEngine;
using PerfectParcel.Conveyor;

namespace PerfectParcel.Parcels
{
    /// <summary>
    /// 5 типів посилок згідно зі специфікацією GAME_DESIGN.md.
    /// </summary>
    public enum ParcelType
    {
        Standard = 0,   // Базовий картон, нормальна швидкість
        Express = 1,    // Помаранчева, +40% швидкості, висока нагорода
        Fragile = 2,    // Біла, крихка, -2 життя при помилці
        Heavy = 3,      // Дерев'яна, важка, -30% швидкості, довгий свайп
        Secured = 4     // Металевий сейф, вимагає тапу-сканування перед свайпом
    }

    /// <summary>
    /// Модель даних для конкретного екземпляра посилки на конвеєрі.
    /// </summary>
    [System.Serializable]
    public class ParcelData
    {
        public int Id;
        public ParcelType Type;
        public SortingDirection TargetDirection;
        public float SpeedModifier;
        public int BasePoints;
        public int BaseCoins;
        public bool IsScanned; // Для типу Secured

        public Color DirectionColor
        {
            get
            {
                switch (TargetDirection)
                {
                    case SortingDirection.Left:
                        return new Color(0.15f, 0.39f, 0.92f); // 🔵 Синій
                    case SortingDirection.Straight:
                        return new Color(0.06f, 0.73f, 0.51f); // 🟢 Зелений
                    case SortingDirection.Right:
                        return new Color(0.94f, 0.27f, 0.27f); // 🔴 Червоний
                    default:
                        return Color.white;
                }
            }
        }
    }
}
