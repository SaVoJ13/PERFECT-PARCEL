using System;
using System.Collections.Generic;
using UnityEngine;
using PerfectParcel.Core;
using PerfectParcel.Storage;

namespace PerfectParcel.Economy
{
    /// <summary>
    /// Менеджер щоденних завдань (Daily Quests).
    /// Генерує 3 квести щодня опівночі, оновлює прогрес та виплачує бонуси.
    /// </summary>
    public class DailyQuestsManager : MonoBehaviour
    {
        public static DailyQuestsManager Instance { get; private set; }

        public const int AllQuestsBonus = 200; // Бонус за виконання всіх 3 завдань

        public event Action OnQuestsUpdated;

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

        private void Start()
        {
            CheckAndGenerateQuests();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ParcelSortedEvent>(OnParcelSorted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ParcelSortedEvent>(OnParcelSorted);
        }

        public void CheckAndGenerateQuests()
        {
            if (SaveManager.Instance?.CurrentData == null) return;

            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (SaveManager.Instance.CurrentData.lastQuestGenerationDate != today ||
                SaveManager.Instance.CurrentData.dailyQuests.Count == 0)
            {
                GenerateNewDailyQuests(today);
            }
        }

        private void GenerateNewDailyQuests(string today)
        {
            var quests = new List<DailyQuestSaveData>
            {
                new DailyQuestSaveData
                {
                    id = "quest_sort_total",
                    description = "Відсортувати 50 будь-яких посилок",
                    currentProgress = 0,
                    targetAmount = 50,
                    rewardCoins = 75,
                    isClaimed = false
                },
                new DailyQuestSaveData
                {
                    id = "quest_no_fail",
                    description = "Пройти 3 рівні без жодної помилки",
                    currentProgress = 0,
                    targetAmount = 3,
                    rewardCoins = 100,
                    isClaimed = false
                },
                new DailyQuestSaveData
                {
                    id = "quest_special_parcels",
                    description = "Відсортувати 15 Термінових або Крихких посилок",
                    currentProgress = 0,
                    targetAmount = 15,
                    rewardCoins = 125,
                    isClaimed = false
                }
            };

            SaveManager.Instance.CurrentData.lastQuestGenerationDate = today;
            SaveManager.Instance.CurrentData.dailyQuests = quests;
            SaveManager.Instance.CurrentData.allDailyQuestsClaimedBonusGiven = false;
            SaveManager.Instance.SaveDataAtomic();

            OnQuestsUpdated?.Invoke();
            Debug.Log("[DailyQuestsManager] Згенеровано 3 нових щоденних завдання!");
        }

        private void OnParcelSorted(ParcelSortedEvent e)
        {
            if (SaveManager.Instance?.CurrentData == null) return;

            if (e.IsCorrect)
            {
                IncrementQuestProgress("quest_sort_total", 1);

                if (e.ParcelType == Parcels.ParcelType.Express || e.ParcelType == Parcels.ParcelType.Fragile)
                {
                    IncrementQuestProgress("quest_special_parcels", 1);
                }
            }
        }

        public void IncrementQuestProgress(string questId, int amount)
        {
            if (SaveManager.Instance?.CurrentData == null) return;

            var quest = SaveManager.Instance.CurrentData.dailyQuests.Find(q => q.id == questId);
            if (quest != null && !quest.isClaimed && quest.currentProgress < quest.targetAmount)
            {
                quest.currentProgress = Mathf.Min(quest.targetAmount, quest.currentProgress + amount);
                SaveManager.Instance.SaveDataAtomic();
                OnQuestsUpdated?.Invoke();
            }
        }

        public bool ClaimReward(string questId)
        {
            if (SaveManager.Instance?.CurrentData == null) return false;

            var quest = SaveManager.Instance.CurrentData.dailyQuests.Find(q => q.id == questId);
            if (quest != null && !quest.isClaimed && quest.currentProgress >= quest.targetAmount)
            {
                quest.isClaimed = true;
                SaveManager.Instance.AddCoins(quest.rewardCoins);

                // Перевірка, чи всі 3 квести забрано для нарахування супер-бонусу
                CheckAllQuestsClaimed();

                SaveManager.Instance.SaveDataAtomic();
                OnQuestsUpdated?.Invoke();
                return true;
            }

            return false;
        }

        private void CheckAllQuestsClaimed()
        {
            if (SaveManager.Instance.CurrentData.allDailyQuestsClaimedBonusGiven) return;

            var quests = SaveManager.Instance.CurrentData.dailyQuests;
            bool allClaimed = true;
            for (int i = 0; i < quests.Count; i++)
            {
                if (!quests[i].isClaimed) { allClaimed = false; break; }
            }

            if (allClaimed)
            {
                SaveManager.Instance.CurrentData.allDailyQuestsClaimedBonusGiven = true;
                SaveManager.Instance.AddCoins(AllQuestsBonus);
                Debug.Log($"[DailyQuestsManager] Усі завдання виконано! Нараховано супер-бонус +{AllQuestsBonus} монет!");
            }
        }

        public IReadOnlyList<DailyQuestSaveData> CurrentQuests =>
            SaveManager.Instance?.CurrentData != null ? SaveManager.Instance.CurrentData.dailyQuests : null;
    }
}
