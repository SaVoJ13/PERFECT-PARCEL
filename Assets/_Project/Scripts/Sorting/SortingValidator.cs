using UnityEngine;
using PerfectParcel.Conveyor;
using PerfectParcel.Core;
using PerfectParcel.Parcels;

namespace PerfectParcel.Sorting
{
    /// <summary>
    /// Валідатор правильності сортування.
    /// Перевіряє відповідність напрямку, розраховує комбо, нараховує очки та списує життя.
    /// </summary>
    public class SortingValidator : MonoBehaviour
    {
        private int _currentCombo = 0;
        private int _totalScore = 0;
        private int _totalCoins = 0;
        private int _sortedParcelsCount = 0;

        public int CurrentCombo => _currentCombo;
        public int TotalScore => _totalScore;
        public int TotalCoins => _totalCoins;
        public int SortedParcelsCount => _sortedParcelsCount;

        public bool ValidateSorting(ParcelController parcel, SortingDirection chosenDirection, out int points, out int coins, out int livesPenalty)
        {
            points = 0;
            coins = 0;
            livesPenalty = 0;

            if (parcel == null || parcel.Data == null) return false;

            bool isCorrect = (parcel.Data.TargetDirection == chosenDirection);

            if (isCorrect)
            {
                _currentCombo++;
                _sortedParcelsCount++;

                // Множник комбо: +10% очок за кожні 5 комбо
                float comboMultiplier = 1f + (_currentCombo / 5) * 0.1f;
                points = Mathf.RoundToInt(parcel.Data.BasePoints * comboMultiplier);
                coins = parcel.Data.BaseCoins;

                // Апгрейди складу: Express Delivery Fee (лише термінові) та Coin Multiplier
                var upgrades = Economy.UpgradesManager.Instance;
                if (upgrades != null)
                {
                    float coinFactor = upgrades.GetCoinMultiplier();
                    if (parcel.Data.Type == ParcelType.Express) coinFactor *= upgrades.GetExpressCoinMultiplier();
                    coins = Mathf.RoundToInt(coins * coinFactor);
                }

                _totalScore += points;
                _totalCoins += coins;
            }
            else
            {
                _currentCombo = 0; // Скидання комбо при помилці

                // Спеціальний штраф: для крихкої посилки 2 життя, для інших 1 життя
                livesPenalty = (parcel.Data.Type == ParcelType.Fragile) ? 2 : 1;

                // Cushioned Bin: шанс зменшити штраф за крихку посилку до 1 життя
                if (parcel.Data.Type == ParcelType.Fragile && Economy.UpgradesManager.Instance != null &&
                    Random.value < Economy.UpgradesManager.Instance.GetCushionSaveChance())
                {
                    livesPenalty = 1;
                }

                if (parcel.Data.Type == ParcelType.Fragile)
                {
                    Audio.AudioManager.Instance?.PlaySFX(Audio.SoundEffect.FragileBreak);
                }
                else
                {
                    Audio.AudioManager.Instance?.PlaySFX(Audio.SoundEffect.ErrorMistake);
                }
            }

            EventBus.Publish(new ParcelSortedEvent
            {
                ParcelId = parcel.Data.Id,
                IsCorrect = isCorrect,
                PointsEarned = points,
                CoinsEarned = coins,
                CurrentCombo = _currentCombo,
                ParcelType = parcel.Data.Type
            });

            return isCorrect;
        }

        public void ResetStats()
        {
            _currentCombo = 0;
            _totalScore = 0;
            _totalCoins = 0;
            _sortedParcelsCount = 0;
        }
    }
}
