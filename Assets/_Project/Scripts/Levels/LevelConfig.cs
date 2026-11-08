using System;
using UnityEngine;
using PerfectParcel.Parcels;

namespace PerfectParcel.Levels
{
    /// <summary>
    /// Конфігурація окремого ігрового рівня.
    /// Повністю відповідає матриці рівнів із LEVEL_DESIGN.md.
    /// </summary>
    [Serializable]
    public class LevelConfig
    {
        public int levelIndex;
        public int tier;
        public string levelName;
        public float conveyorSpeed;
        public float spawnInterval;
        public int targetParcels;
        public float timeLimit;

        [Header("Parcel Type Weights (Total = 100%)")]
        public float standardPercent;
        public float expressPercent;
        public float fragilePercent;
        public float heavyPercent;
        public float securedPercent;

        [Header("Rules & Rewards")]
        public int maxLives = 3;
        public int baseCoinsReward;
        public int requiredGateStars; // Мінімальна кількість зірок для відкриття тіру

        /// <summary>
        /// Обирає випадковий тип посилки згідно з ваговими коефіцієнтами поточного рівня.
        /// </summary>
        public ParcelType RollParcelType()
        {
            float roll = UnityEngine.Random.Range(0f, 100f);
            float cumulative = 0f;

            cumulative += standardPercent;
            if (roll < cumulative) return ParcelType.Standard;

            cumulative += expressPercent;
            if (roll < cumulative) return ParcelType.Express;

            cumulative += fragilePercent;
            if (roll < cumulative) return ParcelType.Fragile;

            cumulative += heavyPercent;
            if (roll < cumulative) return ParcelType.Heavy;

            return ParcelType.Secured;
        }
    }

    [Serializable]
    public class LevelsWrapper
    {
        public LevelConfig[] levels;
    }
}
