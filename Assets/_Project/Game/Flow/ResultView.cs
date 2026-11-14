using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>Рядок-пояснення для екрана результату: ключ локалізації й число для {0} (або null, якщо числа немає).</summary>
    public readonly struct ResultLine : IEquatable<ResultLine>
    {
        public ResultLine(string key, long? number)
        {
            Key = key;
            Number = number;
        }

        public string Key { get; }

        public long? Number { get; }

        public bool IsEmpty => Key == null;

        public bool Equals(ResultLine other)
        {
            return string.Equals(Key, other.Key, StringComparison.Ordinal) && Number == other.Number;
        }

        public override bool Equals(object obj)
        {
            return obj is ResultLine other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Key == null ? 0 : Key.GetHashCode()) * 31 + (Number.HasValue ? Number.Value.GetHashCode() : 0);
        }
    }

    /// <summary>
    /// Модель екрана результату зміни (S1-10), без UnityEngine. Лише відображає <see cref="ShiftResult"/>
    /// за правилами GAME_DESIGN.md розд. 7 і 9; нічого не обчислює заново, крім пояснення «чому не ще одна зірка».
    /// </summary>
    public sealed class ResultView
    {
        private ResultView(
            bool won,
            int stars,
            long rewardCoins,
            string titleKey,
            ResultLine reason,
            ResultLine shortfall,
            ResultLine starHint,
            bool canRetry,
            bool canNext)
        {
            Won = won;
            Stars = stars;
            RewardCoins = rewardCoins;
            TitleKey = titleKey;
            Reason = reason;
            Shortfall = shortfall;
            StarHint = starHint;
            CanRetry = canRetry;
            CanNext = canNext;
        }

        public bool Won { get; }

        public int Stars { get; }

        /// <summary>Монети, що йдуть у гаманець: при поразці 0.</summary>
        public long RewardCoins { get; }

        public string TitleKey { get; }

        /// <summary>Головна причина поразки (lost.reason.*); порожньо при перемозі.</summary>
        public ResultLine Reason { get; }

        /// <summary>Скільки не вистачило до цілі (лише поразка, коли не вистачало); порожньо інакше.</summary>
        public ResultLine Shortfall { get; }

        /// <summary>Чому не наступна зірка (лише перемога з ★ або ★★); порожньо при ★★★ і поразці.</summary>
        public ResultLine StarHint { get; }

        /// <summary>«Ще раз» доступна завжди: той самий рівень, новий seed.</summary>
        public bool CanRetry { get; }

        /// <summary>«Далі» доступна лише після перемоги й коли наступний рівень є.</summary>
        public bool CanNext { get; }

        public static ResultView Build(ShiftResult result, LevelDefinition level, bool hasNextLevel)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (!result.Won)
            {
                ResultLine shortfall = result.Shortfall > 0L
                    ? new ResultLine(
                        level.Goal.Type == GoalType.Serve ? TextKeys.ResultShortfallServe : TextKeys.ResultShortfallCoins,
                        result.Shortfall)
                    : default(ResultLine);
                return new ResultView(
                    false, 0, 0L, TextKeys.ShiftResultLost, new ResultLine(result.LoseReason, null), shortfall,
                    default(ResultLine), true, false);
            }

            return new ResultView(
                true, result.Stars, result.RewardCoins, TextKeys.ShiftResultWon, default(ResultLine),
                default(ResultLine), StarHintOf(result, level), true, hasNextLevel);
        }

        private static ResultLine StarHintOf(ShiftResult result, LevelDefinition level)
        {
            if (result.Stars <= 1)
            {
                return new ResultLine(TextKeys.ResultWhyStar2, level.Stars.TwoCoins);
            }

            if (result.Stars == 2)
            {
                switch (level.Stars.ThreeCondition)
                {
                    case StarCondition.NoLostCustomers:
                        return new ResultLine(TextKeys.ResultWhyStar3NoLost, null);
                    case StarCondition.NoBurnt:
                        return new ResultLine(TextKeys.ResultWhyStar3NoBurnt, null);
                    case StarCondition.NoWrongDishes:
                        return new ResultLine(TextKeys.ResultWhyStar3NoWrong, null);
                    case StarCondition.Combo:
                        return new ResultLine(TextKeys.ResultWhyStar3Combo, level.Stars.ThreeComboValue);
                    default:
                        return default(ResultLine);
                }
            }

            return default(ResultLine);
        }

        /// <summary>Текст рядка: ключ + число (якщо є) через локалізатор.</summary>
        public static string Format(ILocalizer text, ResultLine line)
        {
            if (line.IsEmpty)
            {
                return string.Empty;
            }

            string template = text.Get(line.Key);
            return line.Number.HasValue ? string.Format(template, line.Number.Value) : template;
        }
    }
}
