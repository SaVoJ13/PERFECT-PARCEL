using System;
using System.Globalization;

namespace KebabStreet.Core
{
    /// <summary>
    /// Що показала перевірка зібраного на дошці (GAME_DESIGN.md, розд. 5).
    /// Числові значення можуть потрапляти в знімки — не змінювати порядок.
    /// </summary>
    public enum DishCheck
    {
        /// <summary>Дошка порожня.</summary>
        Empty = 0,

        /// <summary>Початок рецепта зібрано правильно, але обов'язкові кроки ще не всі.</summary>
        Incomplete = 1,

        /// <summary>Правильна готова страва: її можна видати.</summary>
        Correct = 2,

        /// <summary>Інгредієнти з рецепта, але порядок хибний (або пропущено крок).</summary>
        WrongOrder = 3,

        /// <summary>Є зайвий інгредієнт: його немає в рецепті або він покладений двічі.</summary>
        ExtraIngredient = 4,
    }

    /// <summary>
    /// Готова страва, яку дошка видає клієнтові: номер рецепта в конфігу, які необов'язкові
    /// інгредієнти додано (біт i — i-й необов'язковий інгредієнт рецепта) і ціна.
    /// </summary>
    public readonly struct Dish : IEquatable<Dish>
    {
        public Dish(int recipeIndex, int optionalMask, int priceCoins)
        {
            RecipeIndex = recipeIndex;
            OptionalMask = optionalMask;
            PriceCoins = priceCoins;
        }

        /// <summary>Номер рецепта в <see cref="BoardConfig"/>.</summary>
        public int RecipeIndex { get; }

        /// <summary>Додані необов'язкові інгредієнти: біт i — i-й у списку optional рецепта.</summary>
        public int OptionalMask { get; }

        /// <summary>Ціна страви: ціна рецепта + надбавки за додані необов'язкові інгредієнти, монет.</summary>
        public int PriceCoins { get; }

        /// <summary>Чи додано i-й необов'язковий інгредієнт рецепта.</summary>
        public bool HasOptional(int index)
        {
            return index >= 0 && index < 31 && (OptionalMask & (1 << index)) != 0;
        }

        public bool Equals(Dish other)
        {
            return RecipeIndex == other.RecipeIndex
                && OptionalMask == other.OptionalMask
                && PriceCoins == other.PriceCoins;
        }

        public override bool Equals(object obj)
        {
            return obj is Dish other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RecipeIndex;
                hash = (hash * 397) ^ OptionalMask;
                hash = (hash * 397) ^ PriceCoins;
                return hash;
            }
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0}/{1}/{2}", RecipeIndex, OptionalMask, PriceCoins);
        }
    }

    /// <summary>
    /// Хто отримує видану страву (клієнти прилавка — S1-03). Реалізація має бути
    /// детермінованою: без системного часу і власної випадковості.
    /// </summary>
    public interface IDishReceiver
    {
        /// <summary>
        /// Дошка видає готову страву. target — кому (номер слота прилавка) або
        /// <see cref="BoardTarget.AnyCustomer"/>. true — страву забрано (дошка звільняється);
        /// false — отримувача немає, страва лишається на дошці.
        /// Чи відповідає страва замовленню, вирішує отримувач.
        /// </summary>
        bool TryReceive(Dish dish, int target);
    }

    /// <summary>Особливі значення адреси в командах дошки.</summary>
    public static class BoardTarget
    {
        /// <summary>«Зняти інгредієнт»: останній покладений.</summary>
        public const int TopItem = -1;

        /// <summary>«Видати страву»: без названого клієнта.</summary>
        public const int AnyCustomer = -1;
    }
}
