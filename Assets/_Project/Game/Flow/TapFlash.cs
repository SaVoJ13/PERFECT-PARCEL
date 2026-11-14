using System;

namespace KebabStreet.Game
{
    /// <summary>
    /// Миттєве підсвічування натиснутого елемента (S1-21, ADR-027): тап позначає елемент, і той світиться
    /// <see cref="FlashMs"/> мс часу зміни. Час — такти симуляції, як у підказках <see cref="ShiftInput"/>:
    /// детерміновано і без UnityEngine; на паузі такти стоять, тож підсвічування не згасає. Пам'ять
    /// виділяється лише в конструкторі.
    /// </summary>
    public sealed class TapFlash
    {
        /// <summary>Скільки світиться натиснутий елемент, мс часу зміни (вигляд, не баланс).</summary>
        public const int FlashMs = 150;

        private readonly long[][] _until;
        private readonly long _flashTicks;

        public TapFlash(int grillSlots, int ingredients, int boardPositions, int customers, long flashTicks)
        {
            if (grillSlots < 0 || ingredients < 0 || boardPositions < 0 || customers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(grillSlots));
            }

            if (flashTicks < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(flashTicks));
            }

            _flashTicks = flashTicks;
            _until = new long[(int)FeedbackTarget.Customer + 1][];
            _until[(int)FeedbackTarget.None] = new long[0];
            _until[(int)FeedbackTarget.GrillSlot] = new long[grillSlots];
            _until[(int)FeedbackTarget.Station] = new long[ingredients];
            _until[(int)FeedbackTarget.BoardItem] = new long[boardPositions];
            _until[(int)FeedbackTarget.Clear] = new long[1];
            _until[(int)FeedbackTarget.Customer] = new long[customers];
        }

        /// <summary>Скільки тактів світиться елемент.</summary>
        public long FlashTicks => _flashTicks;

        /// <summary>Позначає елемент натиснутим у момент <paramref name="nowTicks"/>. false — такого елемента немає.</summary>
        public bool Mark(FeedbackTarget target, int index, long nowTicks)
        {
            long[] slots = SlotsOf(target);
            if (slots == null || index < 0 || index >= slots.Length)
            {
                return false;
            }

            slots[index] = nowTicks + _flashTicks;
            return true;
        }

        /// <summary>Чи світиться елемент у момент <paramref name="nowTicks"/>.</summary>
        public bool IsLit(FeedbackTarget target, int index, long nowTicks)
        {
            long[] slots = SlotsOf(target);
            return slots != null && index >= 0 && index < slots.Length && nowTicks < slots[index];
        }

        private long[] SlotsOf(FeedbackTarget target)
        {
            int kind = (int)target;
            return kind <= 0 || kind >= _until.Length ? null : _until[kind];
        }
    }
}
