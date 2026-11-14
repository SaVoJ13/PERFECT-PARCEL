using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>
    /// Смужка прогресу дії (S1-21, ADR-027): яка дія гравця зараз виконується (перша в черзі Core),
    /// на якому елементі і скільки відсотків її тривалості (<c>actionTimeSec</c> станції) відпрацьовано.
    /// Лише читає <see cref="ShiftSimulation.HeadActionProgressTicks"/> і тривалості, які Core уже віддає;
    /// нічого не змінює. Дія з нульовою тривалістю (дошка, видача) виконується миттєво — смужки немає.
    /// Пауза не рухає прогрес, бо такти симуляції на паузі стоять. Без UnityEngine, без виділення пам'яті.
    /// </summary>
    public readonly struct ActionProgressView : IEquatable<ActionProgressView>
    {
        public ActionProgressView(FeedbackTarget target, int index, int percent)
        {
            Target = target;
            Index = index;
            Percent = percent;
        }

        /// <summary>Елемент, на якому йде дія; None — смужки немає.</summary>
        public FeedbackTarget Target { get; }

        /// <summary>Номер елемента (слот, інгредієнт, місце на дошці).</summary>
        public int Index { get; }

        /// <summary>Скільки відсотків дії відпрацьовано, 0..99.</summary>
        public int Percent { get; }

        public bool Active => Target != FeedbackTarget.None;

        /// <summary>Чи смужка належить цьому елементу.</summary>
        public bool Is(FeedbackTarget target, int index)
        {
            return Target == target && Index == index && Target != FeedbackTarget.None;
        }

        public static ActionProgressView FromSimulation(ShiftSimulation simulation)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (simulation.QueuedActionCount < 1)
            {
                return default(ActionProgressView);
            }

            ShiftAction action = simulation.GetQueuedAction(0);
            FeedbackTarget target;
            int index = action.Slot;
            long duration;
            switch (action.Type)
            {
                case ShiftActionType.PlaceMeat:
                case ShiftActionType.TakeMeat:
                case ShiftActionType.DiscardBurntMeat:
                    target = FeedbackTarget.GrillSlot;
                    duration = simulation.Grill == null ? 0L : simulation.Grill.ActionTicks;
                    break;
                case ShiftActionType.AddIngredient:
                    target = FeedbackTarget.Station;
                    duration = simulation.Board != null && simulation.Board.Config.IngredientCount > index && index >= 0
                        ? simulation.Board.AddActionTicks(index)
                        : 0L;
                    break;
                case ShiftActionType.RemoveIngredient:
                    target = FeedbackTarget.BoardItem;
                    duration = simulation.Board == null ? 0L : simulation.Board.BoardActionTicks;
                    break;
                case ShiftActionType.ClearBoard:
                    target = FeedbackTarget.Clear;
                    index = 0;
                    duration = simulation.Board == null ? 0L : simulation.Board.BinActionTicks;
                    break;
                case ShiftActionType.ServeDish:
                    target = FeedbackTarget.Customer;
                    duration = simulation.Board == null ? 0L : simulation.Board.BoardActionTicks;
                    break;
                default:
                    return default(ActionProgressView);
            }

            if (duration < 1L)
            {
                return default(ActionProgressView);
            }

            long done = simulation.HeadActionProgressTicks;
            int percent = done <= 0L ? 0 : (int)Math.Min(99L, done * 100L / duration);
            return new ActionProgressView(target, index, percent);
        }

        public bool Equals(ActionProgressView other)
        {
            return Target == other.Target && Index == other.Index && Percent == other.Percent;
        }

        public override bool Equals(object obj)
        {
            return obj is ActionProgressView other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((((int)Target * 397) ^ Index) * 397) ^ Percent;
        }
    }
}
