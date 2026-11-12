using System;
using System.Globalization;

namespace KebabStreet.Core
{
    /// <summary>
    /// Повний знімок стану зміни. Дві симуляції з однаковою конфігурацією і рівними
    /// знімками поводяться однаково за однакових подальших команд (якщо умова цілі
    /// детермінована). Лише цілі числа, тож порівняння точне.
    /// </summary>
    public readonly struct ShiftSnapshot : IEquatable<ShiftSnapshot>
    {
        public ShiftSnapshot(
            ShiftState state,
            long elapsedTicks,
            long remainingTicks,
            long pendingSubTicks,
            bool continueUsed,
            ulong randomState)
            : this(
                state,
                elapsedTicks,
                remainingTicks,
                pendingSubTicks,
                continueUsed,
                randomState,
                default(GrillSnapshot),
                default(ActionQueueSnapshot))
        {
        }

        public ShiftSnapshot(
            ShiftState state,
            long elapsedTicks,
            long remainingTicks,
            long pendingSubTicks,
            bool continueUsed,
            ulong randomState,
            GrillSnapshot grill,
            ActionQueueSnapshot actions)
            : this(
                state,
                elapsedTicks,
                remainingTicks,
                pendingSubTicks,
                continueUsed,
                randomState,
                grill,
                actions,
                default(BoardSnapshot))
        {
        }

        public ShiftSnapshot(
            ShiftState state,
            long elapsedTicks,
            long remainingTicks,
            long pendingSubTicks,
            bool continueUsed,
            ulong randomState,
            GrillSnapshot grill,
            ActionQueueSnapshot actions,
            BoardSnapshot board)
        {
            State = state;
            ElapsedTicks = elapsedTicks;
            RemainingTicks = remainingTicks;
            PendingSubTicks = pendingSubTicks;
            ContinueUsed = continueUsed;
            RandomState = randomState;
            Grill = grill;
            Actions = actions;
            Board = board;
        }

        /// <summary>Стан зміни.</summary>
        public ShiftState State { get; }

        /// <summary>Скільки тактів зміна вже відпрацювала.</summary>
        public long ElapsedTicks { get; }

        /// <summary>Скільки тактів лишилось до кінця таймера.</summary>
        public long RemainingTicks { get; }

        /// <summary>Залишок часу, менший за такт, який накопичив Advance (мільйонні частки такту).</summary>
        public long PendingSubTicks { get; }

        /// <summary>Чи використано продовження в цій спробі.</summary>
        public bool ContinueUsed { get; }

        /// <summary>Стан генератора випадковості.</summary>
        public ulong RandomState { get; }

        /// <summary>Стан гриля; значення за замовчуванням — гриля немає.</summary>
        public GrillSnapshot Grill { get; }

        /// <summary>Стан черги дій гравця.</summary>
        public ActionQueueSnapshot Actions { get; }

        /// <summary>Стан дошки; значення за замовчуванням — дошки немає або вона ще не використовувалась.</summary>
        public BoardSnapshot Board { get; }

        public bool Equals(ShiftSnapshot other)
        {
            return State == other.State
                && ElapsedTicks == other.ElapsedTicks
                && RemainingTicks == other.RemainingTicks
                && PendingSubTicks == other.PendingSubTicks
                && ContinueUsed == other.ContinueUsed
                && RandomState == other.RandomState
                && Grill.Equals(other.Grill)
                && Actions.Equals(other.Actions)
                && Board.Equals(other.Board);
        }

        public override bool Equals(object obj)
        {
            return obj is ShiftSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)State;
                hash = (hash * 397) ^ ElapsedTicks.GetHashCode();
                hash = (hash * 397) ^ RemainingTicks.GetHashCode();
                hash = (hash * 397) ^ PendingSubTicks.GetHashCode();
                hash = (hash * 397) ^ (ContinueUsed ? 1 : 0);
                hash = (hash * 397) ^ RandomState.GetHashCode();
                hash = (hash * 397) ^ Grill.GetHashCode();
                hash = (hash * 397) ^ Actions.GetHashCode();
                hash = (hash * 397) ^ Board.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            string text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} elapsed={1} remaining={2} pending={3} continueUsed={4} random=0x{5:X16}",
                State, ElapsedTicks, RemainingTicks, PendingSubTicks, ContinueUsed, RandomState);

            // Зміна без гриля і без дій друкується так само, як до появи станцій.
            bool noBoard = Board.Equals(default(BoardSnapshot));
            if (Grill.Equals(default(GrillSnapshot)) && Actions.Equals(default(ActionQueueSnapshot)) && noBoard)
            {
                return text;
            }

            // Зміна без дошки друкується так само, як до її появи.
            text = text + " grill=" + Grill + " actions=" + Actions;
            return noBoard ? text : text + " board=" + Board;
        }
    }
}
