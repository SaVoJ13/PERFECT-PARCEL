using System;
using System.Collections.Generic;
using UnityEngine;
using PerfectParcel.Storage;

namespace PerfectParcel.Economy
{
    /// <summary>
    /// Менеджер 7 покращень складу.
    /// Керує купівлею, матрицею цін, розрахунком ефектів та синхронізацією із SaveManager.
    /// </summary>
    public class UpgradesManager : MonoBehaviour
    {
        public static UpgradesManager Instance { get; private set; }

        public const int MaxUpgradeTier = 5;

        // Матриця вартості прокачки згідно з ECONOMY_DESIGN.md [Tier 1..5]
        private static readonly Dictionary<UpgradeType, int[]> UpgradePrices = new Dictionary<UpgradeType, int[]>
        {
            { UpgradeType.SmoothBelt,         new[] { 100, 250, 500, 1000, 2000 } },
            { UpgradeType.SafetyBuffer,       new[] { 200, 500, 1200, 2500, 5000 } },
            { UpgradeType.MagneticGuides,     new[] { 150, 350, 750, 1500, 3000 } },
            { UpgradeType.AutoScanner,        new[] { 250, 600, 1200, 2400, 4500 } },
            { UpgradeType.CoinMultiplier,     new[] { 300, 750, 1500, 3000, 6000 } },
            { UpgradeType.CushionedBin,       new[] { 200, 450, 900, 1800, 3500 } },
            { UpgradeType.ExpressDeliveryFee, new[] { 250, 500, 1000, 2000, 4000 } }
        };

        public event Action<UpgradeType, int> OnUpgradePurchased;

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

        public int GetUpgradeTier(UpgradeType type)
        {
            if (SaveManager.Instance?.CurrentData == null) return 0;
            string id = type.ToString();
            var item = SaveManager.Instance.CurrentData.upgrades.Find(u => u.upgradeId == id);
            return item != null ? item.currentTier : 0;
        }

        public int GetUpgradePrice(UpgradeType type)
        {
            int currentTier = GetUpgradeTier(type);
            if (currentTier >= MaxUpgradeTier) return -1; // Максимальний рівень

            if (UpgradePrices.TryGetValue(type, out var prices))
            {
                return prices[currentTier];
            }
            return 999999;
        }

        public bool CanAfford(UpgradeType type)
        {
            int price = GetUpgradePrice(type);
            if (price < 0) return false;
            return SaveManager.Instance != null && SaveManager.Instance.Coins >= price;
        }

        public bool PurchaseUpgrade(UpgradeType type)
        {
            if (!CanAfford(type)) return false;

            int price = GetUpgradePrice(type);
            if (SaveManager.Instance.SpendCoins(price))
            {
                int nextTier = GetUpgradeTier(type) + 1;
                string id = type.ToString();

                var item = SaveManager.Instance.CurrentData.upgrades.Find(u => u.upgradeId == id);
                if (item == null)
                {
                    item = new UpgradeProgressData { upgradeId = id, currentTier = nextTier };
                    SaveManager.Instance.CurrentData.upgrades.Add(item);
                }
                else
                {
                    item.currentTier = nextTier;
                }

                SaveManager.Instance.SaveDataAtomic();
                OnUpgradePurchased?.Invoke(type, nextTier);
                Debug.Log($"[UpgradesManager] Придбано покращення {type} до рівня {nextTier}!");
                return true;
            }

            return false;
        }

        #region Gameplay Modifiers
        /// <summary>
        /// Додаткові серця / життя з покращення SafetyBuffer (+1 на Tier 3, +2 на Tier 5).
        /// </summary>
        public int GetBonusLives()
        {
            int tier = GetUpgradeTier(UpgradeType.SafetyBuffer);
            if (tier >= 5) return 2;
            if (tier >= 3) return 1;
            return 0;
        }

        /// <summary>
        /// Модифікатор швидкості конвеєра (-3% за рівень SmoothBelt).
        /// </summary>
        public float GetSpeedReductionModifier()
        {
            int tier = GetUpgradeTier(UpgradeType.SmoothBelt);
            return 1f - (tier * 0.03f);
        }

        /// <summary>
        /// Бонус до монет (+5% за рівень CoinMultiplier).
        /// </summary>
        public float GetCoinMultiplier()
        {
            int tier = GetUpgradeTier(UpgradeType.CoinMultiplier);
            return 1f + (tier * 0.05f);
        }

        /// <summary>
        /// Шанс порятунку крихкої посилки (10% за рівень CushionedBin).
        /// </summary>
        public float GetCushionSaveChance()
        {
            int tier = GetUpgradeTier(UpgradeType.CushionedBin);
            return tier * 0.10f;
        }

        /// <summary>
        /// Додаткові монети за термінову посилку (+2 монети за рівень ExpressDeliveryFee).
        /// </summary>
        public int GetExpressBonusCoins()
        {
            int tier = GetUpgradeTier(UpgradeType.ExpressDeliveryFee);
            return tier * 2;
        }
        #endregion

        public static string GetUpgradeName(UpgradeType type)
        {
            string key = "";
            switch (type)
            {
                case UpgradeType.SmoothBelt: key = "upg_smooth_belt"; break;
                case UpgradeType.SafetyBuffer: key = "upg_safety_buffer"; break;
                case UpgradeType.MagneticGuides: key = "upg_magnetic_guides"; break;
                case UpgradeType.AutoScanner: key = "upg_auto_scanner"; break;
                case UpgradeType.CoinMultiplier: key = "upg_coin_multiplier"; break;
                case UpgradeType.CushionedBin: key = "upg_cushioned_bin"; break;
                case UpgradeType.ExpressDeliveryFee: key = "upg_express_fee"; break;
            }

            if (Localization.LocalizationManager.Instance != null && !string.IsNullOrEmpty(key))
            {
                return Localization.LocalizationManager.Instance.Get(key);
            }

            switch (type)
            {
                case UpgradeType.SmoothBelt: return "Плавний конвеєр";
                case UpgradeType.SafetyBuffer: return "Буфер безпеки";
                case UpgradeType.MagneticGuides: return "Магнітний сортувальник";
                case UpgradeType.AutoScanner: return "Швидкий авто-сканер";
                case UpgradeType.CoinMultiplier: return "Золотий бонус";
                case UpgradeType.CushionedBin: return "М'яка подушка";
                case UpgradeType.ExpressDeliveryFee: return "Преміальний контракт";
                default: return type.ToString();
            }
        }

        public static string GetUpgradeDescription(UpgradeType type)
        {
            string key = "";
            switch (type)
            {
                case UpgradeType.SmoothBelt: key = "upg_smooth_belt_desc"; break;
                case UpgradeType.SafetyBuffer: key = "upg_safety_buffer_desc"; break;
                case UpgradeType.MagneticGuides: key = "upg_magnetic_guides_desc"; break;
                case UpgradeType.AutoScanner: key = "upg_auto_scanner_desc"; break;
                case UpgradeType.CoinMultiplier: key = "upg_coin_multiplier_desc"; break;
                case UpgradeType.CushionedBin: key = "upg_cushioned_bin_desc"; break;
                case UpgradeType.ExpressDeliveryFee: key = "upg_express_fee_desc"; break;
            }

            if (Localization.LocalizationManager.Instance != null && !string.IsNullOrEmpty(key))
            {
                return Localization.LocalizationManager.Instance.Get(key);
            }

            switch (type)
            {
                case UpgradeType.SmoothBelt: return "Зменшує швидкість стрічки (-4% за рівень).";
                case UpgradeType.SafetyBuffer: return "Розширює тайм-аут дедлайну лінії падіння (+0.3с за рівень).";
                case UpgradeType.MagneticGuides: return "Розширює зону детекції жестів (+5% за рівень).";
                case UpgradeType.AutoScanner: return "Прискорює сканування секретних посилок (+15% за рівень).";
                case UpgradeType.CoinMultiplier: return "Додає бонус до всіх зароблених монет (+8% за рівень).";
                case UpgradeType.CushionedBin: return "Знижує втрату життів від крихких посилок.";
                case UpgradeType.ExpressDeliveryFee: return "Додаткові монети (+25% за рівень) за термінові посилки.";
                default: return "";
            }
        }
    }
}
