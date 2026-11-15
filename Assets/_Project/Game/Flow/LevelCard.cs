using System;
using System.Collections.Generic;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>Рядок картки рівня: ключ тексту і число для шаблону {0}; <see cref="HasNumber"/> - чи є число.</summary>
    public readonly struct LevelCardLine
    {
        public LevelCardLine(int stars, string key, long number, bool hasNumber)
        {
            Stars = stars;
            Key = key;
            Number = number;
            HasNumber = hasNumber;
        }

        /// <summary>Скільки зірок цей рядок дає (0 - рядок цілі).</summary>
        public int Stars { get; }

        public string Key { get; }

        public long Number { get; }

        public bool HasNumber { get; }
    }

    /// <summary>
    /// Дані картки рівня перед стартом (Q-05f, QB-LVL-07): ціль і пороги зірок як ключі локалізації з числами з конфігу.
    /// Без UnityEngine, правил тут немає - лише перелік того, що показати.
    /// </summary>
    public static class LevelCard
    {
        public const string GoalServeKey = "ui.level.goal.serve";
        public const string GoalCoinsKey = "ui.level.goal.coins";
        public const string Star1Key = "ui.level.star1";
        public const string Star2Key = "ui.level.star2";
        public const string Star3NoLostKey = "ui.level.star3.no_lost";
        public const string Star3NoBurntKey = "ui.level.star3.no_burnt";
        public const string Star3NoWrongKey = "ui.level.star3.no_wrong";
        public const string Star3ComboKey = "ui.level.star3.combo";

        /// <summary>Рядки картки: ціль, потім зірки 1..3 (третя - лише якщо в конфігу є умова).</summary>
        public static LevelCardLine[] Lines(LevelDefinition level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            var lines = new List<LevelCardLine>
            {
                new LevelCardLine(0, level.Goal.Type == GoalType.Coins ? GoalCoinsKey : GoalServeKey, level.Goal.Target, true),
                new LevelCardLine(1, Star1Key, 0L, false),
                new LevelCardLine(2, Star2Key, level.Stars.TwoCoins, true),
            };

            switch (level.Stars.ThreeCondition)
            {
                case StarCondition.NoLostCustomers:
                    lines.Add(new LevelCardLine(3, Star3NoLostKey, 0L, false));
                    break;
                case StarCondition.NoBurnt:
                    lines.Add(new LevelCardLine(3, Star3NoBurntKey, 0L, false));
                    break;
                case StarCondition.NoWrongDishes:
                    lines.Add(new LevelCardLine(3, Star3NoWrongKey, 0L, false));
                    break;
                case StarCondition.Combo:
                    lines.Add(new LevelCardLine(3, Star3ComboKey, level.Stars.ThreeComboValue, true));
                    break;
                default:
                    break;
            }

            return lines.ToArray();
        }
    }
}
