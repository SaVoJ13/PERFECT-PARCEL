using System;
using System.Globalization;

namespace KebabStreet.Core
{
    /// <summary>
    /// Вид дії гравця в черзі дій (GAME_DESIGN.md, розд. 2). Числові значення входять
    /// у знімок стану — не змінювати порядок.
    /// </summary>
    public enum ShiftActionType
    {
        /// <summary>Дії немає (значення за замовчуванням).</summary>
        None = 0,

        /// <summary>Покласти сире м'ясо на гриль.</summary>
        PlaceMeat = 1,

        /// <summary>Зняти готове м'ясо з гриля (на дошку).</summary>
        TakeMeat = 2,

        /// <summary>Викинути пригоріле м'ясо у смітник.</summary>
        DiscardBurntMeat = 3,
    }

    /// <summary>
    /// Чим закінчилося виконання дії з черги. Усе, крім <see cref="Done"/>, означає:
    /// дію проігноровано, стан станцій не змінився. Числові значення входять у знімок.
    /// </summary>
    public enum ShiftActionResult
    {
        /// <summary>Ще жодної дії не виконано.</summary>
        None = 0,

        /// <summary>Дію виконано.</summary>
        Done = 1,

        /// <summary>Покласти м'ясо: вільних слотів немає.</summary>
        GrillFull = 2,

        /// <summary>Покласти м'ясо в названий слот: він зайнятий.</summary>
        SlotOccupied = 3,

        /// <summary>Зняти або викинути: названий слот порожній.</summary>
        SlotEmpty = 4,

        /// <summary>Зняти: м'ясо ще сире.</summary>
        MeatRaw = 5,

        /// <summary>Зняти: м'ясо пригоріло, його можна лише викинути.</summary>
        MeatBurnt = 6,

        /// <summary>Викинути: м'ясо не пригоріле.</summary>
        MeatNotBurnt = 7,

        /// <summary>Зняти без номера слота: готового м'яса немає.</summary>
        NoDoneMeat = 8,

        /// <summary>Викинути без номера слота: пригорілого м'яса немає.</summary>
        NoBurntMeat = 9,
    }

    /// <summary>Дія гравця в черзі: вид і номер слота (<see cref="GrillSlot.Any"/> — без номера).</summary>
    public readonly struct ShiftAction : IEquatable<ShiftAction>
    {
        public ShiftAction(ShiftActionType type, int slot)
        {
            Type = type;
            Slot = slot;
        }

        /// <summary>Вид дії.</summary>
        public ShiftActionType Type { get; }

        /// <summary>Номер слота або <see cref="GrillSlot.Any"/>.</summary>
        public int Slot { get; }

        public bool Equals(ShiftAction other)
        {
            return Type == other.Type && Slot == other.Slot;
        }

        public override bool Equals(object obj)
        {
            return obj is ShiftAction other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Type * 397) ^ Slot;
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", Type, Slot);
        }
    }
}
