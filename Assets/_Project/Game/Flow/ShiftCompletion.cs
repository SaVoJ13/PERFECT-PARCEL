using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>
    /// Запис підсумку зміни в профіль: найкращі зірки рівня й нагорода монетами. Спрацьовує рівно
    /// один раз на зміну (повторний виклик нічого не нараховує і не зберігає), тож нагороду не
    /// можна отримати двічі. Збереження викликається лише коли профіль справді змінився (перемога).
    /// </summary>
    public sealed class ShiftCompletion
    {
        private readonly ProfileSession _profile;
        private readonly LevelDefinition _level;

        public ShiftCompletion(ProfileSession profile, LevelDefinition level)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _level = level ?? throw new ArgumentNullException(nameof(level));
        }

        /// <summary>Чи підсумок уже записано.</summary>
        public bool Applied { get; private set; }

        /// <summary>Повідомлення від останнього запису (None — збережено або зберігати не було чого).</summary>
        public SaveNotice Notice { get; private set; }

        /// <summary>Записує підсумок. Повертає false, якщо це повторний виклик.</summary>
        public bool Apply(ShiftResult result)
        {
            if (Applied)
            {
                return false;
            }

            Applied = true;
            if (!result.Won)
            {
                return true;
            }

            SaveData data = _profile.Data;
            data.Coins += result.RewardCoins;
            int best;
            if (!data.LevelStars.TryGetValue(_level.Id, out best) || best < result.Stars)
            {
                data.LevelStars[_level.Id] = result.Stars;
            }

            Notice = _profile.Save();
            return true;
        }
    }

    /// <summary>Seed зміни: новий на кожну спробу, але відтворюваний із двох чисел (годинник, номер спроби).</summary>
    public static class ShiftSeeds
    {
        private const ulong Mix = 0x9E3779B97F4A7C15UL;

        public static ulong Next(long unixSec, int attempt)
        {
            unchecked
            {
                return (ulong)Math.Max(0L, unixSec) * Mix + (ulong)attempt;
            }
        }
    }
}
