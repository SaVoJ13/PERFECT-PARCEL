using System;
using System.Collections.Generic;

namespace KebabStreet.Core
{
    /// <summary>
    /// Інгредієнт (GAME_DESIGN.md, розд. 5): назва з конфіга, станція, з якої він береться,
    /// і надбавка до ціни страви, якщо його додано як необов'язковий. Незмінний об'єкт.
    /// Інгредієнт станції <see cref="StationKind.Grill"/> — готове м'ясо: воно потрапляє
    /// на дошку лише командою «зняти м'ясо з гриля».
    /// </summary>
    public sealed class Ingredient
    {
        public Ingredient(string id, StationKind station, int extraPriceCoins)
        {
            Id = id;
            Station = station;
            ExtraPriceCoins = extraPriceCoins;
        }

        /// <summary>Назва в конфігах («pita», «meat», «sauce_hot»…).</summary>
        public string Id { get; }

        /// <summary>Станція, з якої береться інгредієнт.</summary>
        public StationKind Station { get; }

        /// <summary>Надбавка до ціни страви за цей інгредієнт, коли він доданий як необов'язковий, монет.</summary>
        public int ExtraPriceCoins { get; }
    }

    /// <summary>
    /// Рецепт (recipes.json → recipes[id]): обов'язкові кроки у фіксованому порядку,
    /// необов'язкові інгредієнти (після всіх кроків, у порядку списку), ціна і рівень,
    /// з якого рецепт доступний. Незмінний об'єкт; інгредієнти названі так, як у конфігу.
    /// </summary>
    public sealed class Recipe
    {
        private readonly string[] _steps;
        private readonly string[] _optional;

        /// <summary>Списки копіюються; null означає порожній список.</summary>
        public Recipe(
            string id,
            IEnumerable<string> steps,
            IEnumerable<string> optional,
            int priceCoins,
            int unlockLevel)
        {
            Id = id;
            _steps = steps == null ? new string[0] : new List<string>(steps).ToArray();
            _optional = optional == null ? new string[0] : new List<string>(optional).ToArray();
            PriceCoins = priceCoins;
            UnlockLevel = unlockLevel;
        }

        /// <summary>Назва в конфігах («kebab_pita»).</summary>
        public string Id { get; }

        /// <summary>Ціна страви без необов'язкових інгредієнтів, монет (recipes[id].price).</summary>
        public int PriceCoins { get; }

        /// <summary>Номер рівня, з якого рецепт доступний.</summary>
        public int UnlockLevel { get; }

        /// <summary>Кількість обов'язкових кроків.</summary>
        public int StepCount => _steps.Length;

        /// <summary>Кількість необов'язкових інгредієнтів.</summary>
        public int OptionalCount => _optional.Length;

        /// <summary>Найбільша кількість інгредієнтів у страві за цим рецептом.</summary>
        public int Length => _steps.Length + _optional.Length;

        /// <summary>Інгредієнт обов'язкового кроку (0..StepCount-1).</summary>
        public string GetStep(int index)
        {
            if (index < 0 || index >= _steps.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _steps[index];
        }

        /// <summary>Необов'язковий інгредієнт (0..OptionalCount-1).</summary>
        public string GetOptional(int index)
        {
            if (index < 0 || index >= _optional.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _optional[index];
        }

        /// <summary>Інгредієнт за місцем у повному порядку збирання: спершу кроки, потім необов'язкові.</summary>
        public string GetItem(int index)
        {
            if (index < 0 || index >= Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return index < _steps.Length ? _steps[index] : _optional[index - _steps.Length];
        }
    }
}
