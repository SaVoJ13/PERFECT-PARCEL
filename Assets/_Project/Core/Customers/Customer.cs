namespace KebabStreet.Core
{
    /// <summary>
    /// Стан клієнта (GAME_DESIGN.md, розд. 6): Arriving → Waiting → Served | Left.
    /// Числові значення входять у знімок стану — не змінювати порядок.
    /// </summary>
    public enum CustomerState
    {
        /// <summary>Ще не прийшов (його час за розкладом не настав).</summary>
        None = 0,

        /// <summary>Прийшов і чекає вільного слота в черзі; терпіння не спадає.</summary>
        Arriving = 1,

        /// <summary>Стоїть у слоті прилавка, замовлення видно, терпіння спадає.</summary>
        Waiting = 2,

        /// <summary>Отримав свою страву і заплатив.</summary>
        Served = 3,

        /// <summary>Терпіння скінчилось — пішов без страви.</summary>
        Left = 4,
    }

    /// <summary>
    /// Клієнт зміни: тип, замовлення, стан і терпіння в цілих тактах. Зовні доступний лише
    /// для читання: стан змінюють тільки такт симуляції і видача страви.
    /// </summary>
    public sealed class Customer
    {
        private readonly int _forbiddenMask;

        internal Customer(
            int id,
            int typeIndex,
            int recipeIndex,
            int requestedMask,
            int excludeMask,
            int forbiddenMask,
            long arrivalTick,
            long patienceTicks,
            long graceTicks)
        {
            Id = id;
            TypeIndex = typeIndex;
            RecipeIndex = recipeIndex;
            RequestedMask = requestedMask;
            ExcludeMask = excludeMask;
            _forbiddenMask = forbiddenMask;
            ArrivalTick = arrivalTick;
            PatienceTicks = patienceTicks;
            GraceTicks = graceTicks;
            PatienceLeftTicks = patienceTicks;
            GraceLeftTicks = graceTicks;
            Slot = CounterSlots.None;
            State = CustomerState.None;
        }

        /// <summary>Номер клієнта: його місце в розкладі приходу, від 0.</summary>
        public int Id { get; }

        /// <summary>Номер типу клієнта в <see cref="CounterConfig"/>.</summary>
        public int TypeIndex { get; }

        /// <summary>Замовлення: номер рецепта в <see cref="BoardConfig"/>.</summary>
        public int RecipeIndex { get; }

        /// <summary>Замовлення: необов'язкові інгредієнти, які мають бути у страві (біти як у Dish.OptionalMask).</summary>
        public int RequestedMask { get; }

        /// <summary>Замовлення: необов'язкові інгредієнти, які клієнт просив не класти («без цибулі»).</summary>
        public int ExcludeMask { get; }

        /// <summary>Такт, на якому клієнт приходить.</summary>
        public long ArrivalTick { get; }

        /// <summary>Усе терпіння клієнта, тактів.</summary>
        public long PatienceTicks { get; }

        /// <summary>Скільки тактів у слоті терпіння ще не спадає.</summary>
        public long GraceTicks { get; }

        /// <summary>Стан клієнта.</summary>
        public CustomerState State { get; private set; }

        /// <summary>Номер слота прилавка або <see cref="CounterSlots.None"/>.</summary>
        public int Slot { get; private set; }

        /// <summary>Скільки терпіння лишилось, тактів.</summary>
        public long PatienceLeftTicks { get; private set; }

        /// <summary>Скільки пільгових тактів (без спадання терпіння) лишилось.</summary>
        public long GraceLeftTicks { get; private set; }

        /// <summary>Скільки хибних страв клієнтові пропонували.</summary>
        public long WrongDishCount { get; private set; }

        /// <summary>Скільки монет клієнт заплатив (разом із чайовими і комбо).</summary>
        public long PaidCoins { get; private set; }

        /// <summary>
        /// Чи відповідає страва замовленню: той самий рецепт, є все, що просили, і немає
        /// того, чого класти не можна.
        /// </summary>
        public bool Accepts(Dish dish)
        {
            return dish.RecipeIndex == RecipeIndex
                && (dish.OptionalMask & RequestedMask) == RequestedMask
                && (dish.OptionalMask & _forbiddenMask) == 0;
        }

        internal void Arrive()
        {
            State = CustomerState.Arriving;
        }

        internal void Seat(int slot)
        {
            State = CustomerState.Waiting;
            Slot = slot;
        }

        /// <summary>Один такт у слоті; true — терпіння скінчилось.</summary>
        internal bool TickPatience()
        {
            if (GraceLeftTicks > 0L)
            {
                GraceLeftTicks--;
                return false;
            }

            PatienceLeftTicks--;
            return PatienceLeftTicks <= 0L;
        }

        /// <summary>Хибна страва: клієнт втрачає терпіння; true — терпіння скінчилось.</summary>
        internal bool Complain(long penaltyTicks)
        {
            WrongDishCount++;
            PatienceLeftTicks = PatienceLeftTicks > penaltyTicks ? PatienceLeftTicks - penaltyTicks : 0L;
            return PatienceLeftTicks <= 0L;
        }

        internal void Serve(long paidCoins)
        {
            State = CustomerState.Served;
            Slot = CounterSlots.None;
            PaidCoins = paidCoins;
        }

        internal void Leave()
        {
            State = CustomerState.Left;
            Slot = CounterSlots.None;
            PatienceLeftTicks = 0L;
        }
    }
}
