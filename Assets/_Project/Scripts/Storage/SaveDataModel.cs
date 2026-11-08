using System;
using System.Collections.Generic;

namespace PerfectParcel.Storage
{
    [Serializable]
    public class LevelProgressData
    {
        public int levelIndex;
        public bool isUnlocked;
        public int stars;
        public int highScore;
    }

    [Serializable]
    public class UpgradeProgressData
    {
        public string upgradeId;
        public int currentTier; // 0..5
    }

    [Serializable]
    public class AchievementSaveData
    {
        public string id;
        public bool isUnlocked;
        public string unlockDate;
    }

    [Serializable]
    public class DailyQuestSaveData
    {
        public string id;
        public string description;
        public int currentProgress;
        public int targetAmount;
        public int rewardCoins;
        public bool isClaimed;
    }

    [Serializable]
    public class GameSettingsData
    {
        public float musicVolume = 0.8f;
        public float sfxVolume = 1.0f;
        public bool hapticsEnabled = true;
        public string languageCode = ""; // Якщо порожньо - автовизначення при першому старті: "en", "ua", "pl", "de", "es", "ru"
    }

    /// <summary>
    /// Головна модель даних локального збереження гри (SaveData).
    /// Повністю відповідає специфікації Розділу 5 в ARCHITECTURE.md.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int schemaVersion = 1;
        public string lastSaveTimestamp = "";
        public int coins = 0;
        public int highestUnlockedLevel = 1;

        public List<LevelProgressData> levels = new List<LevelProgressData>();
        public List<UpgradeProgressData> upgrades = new List<UpgradeProgressData>();
        public List<AchievementSaveData> achievements = new List<AchievementSaveData>();

        public string lastQuestGenerationDate = "";
        public List<DailyQuestSaveData> dailyQuests = new List<DailyQuestSaveData>();
        public bool allDailyQuestsClaimedBonusGiven = false;

        // Щоденні нагороди за вхід (7-денний цикл, 1..7)
        public int currentDailyStreakDay = 1;
        public string lastDailyRewardClaimDate = "";

        public GameSettingsData settings = new GameSettingsData();
        public bool hasPurchasedAnyIAP = false; // Вимикає примусовий Interstitial Ad (No-Ads privilege)
        public string checksum = "";

        /// <summary>
        /// Створює дефолтний початковий профіль гравця (Level 1 відкрито, 30 рівнів у базі).
        /// </summary>
        public static SaveData CreateDefault()
        {
            var data = new SaveData
            {
                schemaVersion = 1,
                lastSaveTimestamp = DateTime.UtcNow.ToString("o"),
                coins = 100, // Початковий вітальний бонус
                highestUnlockedLevel = 1,
                settings = new GameSettingsData()
            };

            // Ініціалізація 30 рівнів
            for (int i = 1; i <= 30; i++)
            {
                data.levels.Add(new LevelProgressData
                {
                    levelIndex = i,
                    isUnlocked = (i == 1),
                    stars = 0,
                    highScore = 0
                });
            }

            return data;
        }
    }
}
