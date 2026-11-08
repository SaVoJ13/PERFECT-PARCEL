using System;
using UnityEngine;
using PerfectParcel.Storage;

namespace PerfectParcel.Economy
{
    /// <summary>
    /// Менеджер щоденних нагород за вхід (7-денний цикл).
    /// Відповідає за перевірку стріка відвідувань через DateTime.UtcNow,
    /// видачу винагород (100..2500 монет) та інтеграцію Rewarded Ads для подвоєння нагороди.
    /// Працює 100% локально та офлайн без бекенду.
    /// </summary>
    public class DailyRewardManager : MonoBehaviour
    {
        public static DailyRewardManager Instance { get; private set; }

        public static readonly int[] DailyRewardsSchedule = { 100, 200, 400, 700, 1100, 1700, 2500 };

        public event Action OnRewardClaimed;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public int CurrentStreakDay
        {
            get
            {
                if (SaveManager.Instance?.CurrentData == null) return 1;
                EvaluateStreakStatus(out int streakDay, out _);
                return streakDay;
            }
        }

        public bool IsRewardAvailableToday
        {
            get
            {
                if (SaveManager.Instance?.CurrentData == null) return false;
                EvaluateStreakStatus(out _, out bool canClaim);
                return canClaim;
            }
        }

        public int GetRewardForDay(int dayIndex) // 1..7
        {
            int idx = Mathf.Clamp(dayIndex - 1, 0, DailyRewardsSchedule.Length - 1);
            return DailyRewardsSchedule[idx];
        }

        public int GetCurrentAvailableReward()
        {
            return GetRewardForDay(CurrentStreakDay);
        }

        /// <summary>
        /// Перевіряє статус стріка відвідувань на основі дати останнього отримання (UTC).
        /// </summary>
        public void EvaluateStreakStatus(out int streakDay, out bool canClaimToday)
        {
            streakDay = SaveManager.Instance.DailyStreakDay;
            if (streakDay < 1 || streakDay > 7) streakDay = 1;

            string lastClaimStr = SaveManager.Instance.LastDailyRewardClaimDate;
            if (string.IsNullOrEmpty(lastClaimStr))
            {
                // Гравець жодного разу не отримував нагороду
                streakDay = 1;
                canClaimToday = true;
                return;
            }

            if (DateTime.TryParse(lastClaimStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastClaimDate))
            {
                DateTime nowUtc = DateTime.UtcNow.Date;
                DateTime lastUtcDate = lastClaimDate.Date;

                int dayDiff = (int)(nowUtc - lastUtcDate).TotalDays;

                if (dayDiff == 0)
                {
                    // Вже отримано сьогодні
                    canClaimToday = false;
                }
                else if (dayDiff == 1)
                {
                    // Наступний день серії
                    canClaimToday = true;
                }
                else
                {
                    // Пропущено понад 1 день (> 48h) — серія скидається на День 1
                    streakDay = 1;
                    canClaimToday = true;
                }
            }
            else
            {
                canClaimToday = true;
            }
        }

        /// <summary>
        /// Отримання щоденної нагороди.
        /// </summary>
        /// <param name="doubleRewardWithAd">Якщо true, нагорода подвоюється за перегляд Rewarded Video</param>
        /// <returns>Кількість нарахованих монет</returns>
        public int ClaimDailyReward(bool doubleRewardWithAd = false)
        {
            if (!IsRewardAvailableToday)
            {
                Debug.LogWarning("[DailyRewardManager] Щоденна нагорода вже була отримана сьогодні.");
                return 0;
            }

            EvaluateStreakStatus(out int currentDay, out _);
            int baseReward = GetRewardForDay(currentDay);
            int totalGranted = doubleRewardWithAd ? baseReward * 2 : baseReward;

            // Нарахування монет
            SaveManager.Instance.AddCoins(totalGranted);

            // Визначення наступного дня стріка (цикл 1..7)
            int nextStreak = (currentDay >= 7) ? 1 : currentDay + 1;
            string nowUtcStr = DateTime.UtcNow.ToString("o");

            SaveManager.Instance.RecordDailyRewardClaim(nextStreak, nowUtcStr);

            Debug.Log($"[DailyRewardManager] Успішно отримано нагороду за День {currentDay}: +{totalGranted} монет (Подвоєння: {doubleRewardWithAd}). Наступний день: {nextStreak}");
            OnRewardClaimed?.Invoke();

            return totalGranted;
        }

        public TimeSpan GetTimeUntilNextDayUtc()
        {
            DateTime tomorrowUtc = DateTime.UtcNow.Date.AddDays(1);
            return tomorrowUtc - DateTime.UtcNow;
        }
    }
}
