using System;
using UnityEngine;
using PerfectParcel.Storage;

namespace PerfectParcel.Monetization
{
    /// <summary>
    /// Головний менеджер монетизаційної реклами (AdMob / Rewarded & Interstitial).
    /// Забезпечує виклик винагородної реклами для подвоєння виграшу, щоденного бонусу,
    /// скрині спонсора та другого шансу, а також контролює міжсторінкову рекламу
    /// з урахуванням довічного привілею No-Ads та тайм-аутів.
    /// </summary>
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }

        public const string PlacementDoubleWin = "double_win";
        public const string PlacementSponsorChest = "sponsor_chest";
        public const string PlacementDailyReward = "daily_reward_double";
        public const string PlacementSecondChance = "second_chance";

        // Тестові рекламні ідентифікатори AdMob від Google
        private const string AndroidRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
        private const string AndroidInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";

        [Header("Pacing Settings")]
        [SerializeField] private float interstitialCooldownSeconds = 90f;
        [SerializeField] private int minLevelForInterstitial = 4;
        [SerializeField] private int levelsIntervalForInterstitial = 3;

        private float _lastInterstitialTime = -999f;
        private int _completedLevelsSinceLastAd = 0;

        public bool IsRewardedAdLoaded { get; private set; } = true;
        public bool IsInterstitialLoaded { get; private set; } = true;

        public event Action<string> OnRewardedAdCompleted;

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

        private void Start()
        {
            _lastInterstitialTime = Time.realtimeSinceStartup;
            LoadRewardedAd();
            LoadInterstitialAd();
        }

        #region Rewarded Video
        public void LoadRewardedAd()
        {
            // У симуляторі або в Editor реклама завжди готова
            IsRewardedAdLoaded = true;
            Debug.Log("[AdsManager] Завантаження Rewarded Ad ініційовано (Ready).");
        }

        /// <summary>
        /// Показ добровільної реклами за винагороду.
        /// </summary>
        public void ShowRewardedAd(Action onUserEarnedReward, string placementTag = "default", Action onAdClosed = null)
        {
            if (!IsRewardedAdLoaded)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad ще не завантажилася. Повторна спроба у фоні.");
                LoadRewardedAd();
                return;
            }

            Debug.Log($"[AdsManager] Показ Rewarded Ad для плейсменту: [{placementTag}]...");

            // В Editor або при офлайн симуляції одразу викликаємо винагороду
            onUserEarnedReward?.Invoke();
            OnRewardedAdCompleted?.Invoke(placementTag);
            onAdClosed?.Invoke();

            // Скидання таймера Interstitial після добровільного перегляду реклами
            _lastInterstitialTime = Time.realtimeSinceStartup;

            // Перезавантаження наступного креативу
            LoadRewardedAd();
        }
        #endregion

        #region Interstitial Ads
        public void LoadInterstitialAd()
        {
            IsInterstitialLoaded = true;
            Debug.Log("[AdsManager] Завантаження Interstitial Ad ініційовано (Ready).");
        }

        /// <summary>
        /// Перевіряє, чи дозволено показ міжсторінкової реклами в поточний момент.
        /// </summary>
        public bool CanShowInterstitial(int levelIndex)
        {
            // 1. Привілей покупця (No-Ads): якщо купувався будь-який IAP пакет - заборонено назавжди!
            if (SaveManager.Instance?.CurrentData != null && SaveManager.Instance.CurrentData.hasPurchasedAnyIAP)
            {
                return false;
            }

            // 2. Перші рівні онбордингу без реклами
            if (levelIndex < minLevelForInterstitial)
            {
                return false;
            }

            // 3. Перевірка тайм-ауту (не частіше ніж раз на 90 секунд)
            if (Time.realtimeSinceStartup - _lastInterstitialTime < interstitialCooldownSeconds)
            {
                return false;
            }

            // 4. Перевірка інтервалу рівнів (раз на 3 рівні)
            if (_completedLevelsSinceLastAd < levelsIntervalForInterstitial)
            {
                return false;
            }

            return IsInterstitialLoaded;
        }

        /// <summary>
        /// Фіксація проходження рівня для розрахунку інтервалів реклами.
        /// </summary>
        public void RecordLevelCompleted(int levelIndex, Action onAdClosed = null)
        {
            _completedLevelsSinceLastAd++;

            if (CanShowInterstitial(levelIndex))
            {
                ShowInterstitialAd(onAdClosed);
            }
            else
            {
                onAdClosed?.Invoke();
            }
        }

        private void ShowInterstitialAd(Action onAdClosed)
        {
            Debug.Log("[AdsManager] Показ міжсторінкової реклами (Interstitial Ad)...");
            _lastInterstitialTime = Time.realtimeSinceStartup;
            _completedLevelsSinceLastAd = 0;

            onAdClosed?.Invoke();
            LoadInterstitialAd();
        }
        #endregion
    }
}
