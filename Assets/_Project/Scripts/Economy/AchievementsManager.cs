using System;
using System.Collections.Generic;
using UnityEngine;
using PerfectParcel.Core;
using PerfectParcel.Storage;

namespace PerfectParcel.Economy
{
    [Serializable]
    public class AchievementInfo
    {
        public string id;
        public string title;
        public string description;
        public int rewardCoins;
    }

    /// <summary>
    /// Менеджер 10 внутрішньоігрових досягнень MVP згідно з ECONOMY_DESIGN.md.
    /// Відстежує події сортування, прогресу рівнів та виплачує нагороди.
    /// </summary>
    public class AchievementsManager : MonoBehaviour
    {
        public static AchievementsManager Instance { get; private set; }

        public static readonly List<AchievementInfo> AllAchievements = new List<AchievementInfo>
        {
            new AchievementInfo { id = "ACH-01", title = "Перша коробка", description = "Успішно відсортувати найпершу посилку", rewardCoins = 50 },
            new AchievementInfo { id = "ACH-02", title = "Стажер тижня", description = "Завершити всі рівні Tier 1 (1–5)", rewardCoins = 150 },
            new AchievementInfo { id = "ACH-03", title = "Швидкі пальці", description = "Досягти комбо у 20 посилок поспіль без помилок", rewardCoins = 200 },
            new AchievementInfo { id = "ACH-04", title = "Кришталевий дотик", description = "Пройти рівень із крихкими посилками без жодного розбиття", rewardCoins = 300 },
            new AchievementInfo { id = "ACH-05", title = "Вантажник вищого класу", description = "Відсортувати сумарно 100 посилок", rewardCoins = 400 },
            new AchievementInfo { id = "ACH-06", title = "Золоті руки", description = "Набрати 50 зірок сумарно на карті рівнів", rewardCoins = 500 },
            new AchievementInfo { id = "ACH-07", title = "Перфекціоніст", description = "Пройти 10 рівнів на максимальні 3 зірки", rewardCoins = 600 },
            new AchievementInfo { id = "ACH-08", title = "Модернізатор", description = "Повністю прокачати будь-яке покращення до 5 рівня", rewardCoins = 750 },
            new AchievementInfo { id = "ACH-09", title = "Складський магнат", description = "Накопичити на балансі сумарно 10,000 монет", rewardCoins = 1000 },
            new AchievementInfo { id = "ACH-10", title = "Легенда логістики", description = "Пройти всі 30 рівнів гри", rewardCoins = 2000 }
        };

        public event Action<AchievementInfo> OnAchievementUnlocked;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ParcelSortedEvent>(OnParcelSorted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ParcelSortedEvent>(OnParcelSorted);
        }

        private void OnParcelSorted(ParcelSortedEvent e)
        {
            if (e.IsCorrect)
            {
                UnlockAchievement("ACH-01"); // Перша коробка

                // ACH-05: сумарно 100 посилок (лічильник зберігається разом із сейвом)
                var save = SaveManager.Instance?.CurrentData;
                if (save != null)
                {
                    save.totalParcelsSorted++;
                    if (save.totalParcelsSorted >= 100) UnlockAchievement("ACH-05");
                }

                if (e.CurrentCombo >= 20)
                {
                    UnlockAchievement("ACH-03"); // Швидкі пальці
                }
            }
        }

        public void CheckLevelProgressionAchievements(int levelIndex, int stars, bool levelHadFragile = false, bool fragileBroken = false)
        {
            if (levelHadFragile && !fragileBroken) UnlockAchievement("ACH-04"); // Кришталевий дотик

            if (levelIndex >= 5) UnlockAchievement("ACH-02");
            if (levelIndex >= 30) UnlockAchievement("ACH-10");

            if (SaveManager.Instance != null)
            {
                if (SaveManager.Instance.Coins >= 10000) UnlockAchievement("ACH-09"); // Складський магнат
                int totalStars = SaveManager.Instance.GetTotalStars();
                if (totalStars >= 50) UnlockAchievement("ACH-06");

                // Підрахунок ідеальних рівнів
                int perfectCount = 0;
                for (int i = 1; i <= 30; i++)
                {
                    if (SaveManager.Instance.GetLevelStars(i) >= 3) perfectCount++;
                }
                if (perfectCount >= 10) UnlockAchievement("ACH-07");
            }
        }

        public bool IsUnlocked(string achievementId)
        {
            if (SaveManager.Instance?.CurrentData == null) return false;
            var item = SaveManager.Instance.CurrentData.achievements.Find(a => a.id == achievementId);
            return item != null && item.isUnlocked;
        }

        public bool UnlockAchievement(string achievementId)
        {
            if (IsUnlocked(achievementId)) return false;

            var info = AllAchievements.Find(a => a.id == achievementId);
            if (info == null) return false;

            if (SaveManager.Instance?.CurrentData != null)
            {
                var entry = SaveManager.Instance.CurrentData.achievements.Find(a => a.id == achievementId);
                if (entry == null)
                {
                    entry = new AchievementSaveData
                    {
                        id = achievementId,
                        isUnlocked = true,
                        unlockDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
                    };
                    SaveManager.Instance.CurrentData.achievements.Add(entry);
                }
                else
                {
                    entry.isUnlocked = true;
                    entry.unlockDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                }

                SaveManager.Instance.AddCoins(info.rewardCoins);
                OnAchievementUnlocked?.Invoke(info);
                Debug.Log($"[AchievementsManager] Розблоковано досягнення: {info.title}! Нагорода: +{info.rewardCoins} монет.");
                return true;
            }

            return false;
        }
    }
}
