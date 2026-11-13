using System;
using System.Collections.Generic;
using System.Globalization;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>Роль значка в бульбашці замовлення. Числові значення — номери в таблицях показу.</summary>
    public enum OrderIconKind
    {
        /// <summary>Обов'язковий крок рецепта (піта, м'ясо).</summary>
        Step = 0,

        /// <summary>Необов'язковий інгредієнт, який клієнт просить (салат, соус; «завжди» типу клієнта).</summary>
        Requested = 1,

        /// <summary>Інгредієнт, який клієнт просить НЕ класти («без цибулі»): значок перекреслено.</summary>
        Excluded = 2,
    }

    /// <summary>Один значок замовлення: номер інгредієнта в дошці зміни і його роль.</summary>
    public readonly struct OrderIcon : IEquatable<OrderIcon>
    {
        public OrderIcon(int ingredient, OrderIconKind kind)
        {
            Ingredient = ingredient;
            Kind = kind;
        }

        /// <summary>Номер інгредієнта в <see cref="BoardConfig"/>.</summary>
        public int Ingredient { get; }

        public OrderIconKind Kind { get; }

        public bool Equals(OrderIcon other)
        {
            return Ingredient == other.Ingredient && Kind == other.Kind;
        }

        public override bool Equals(object obj)
        {
            return obj is OrderIcon other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Ingredient * 397) ^ (int)Kind;
        }

        public override string ToString()
        {
            return Kind + ":" + Ingredient.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Заповнення одного сердечка терпіння.</summary>
    public enum HeartFill
    {
        Empty = 0,
        Half = 1,
        Full = 2,
    }

    /// <summary>Терпіння клієнта для показу: сердечка (з половинками), смужка у відсотках, «майже пішов».</summary>
    public readonly struct PatienceView : IEquatable<PatienceView>
    {
        public PatienceView(int hearts, int halfHearts, int percent, bool low, bool grace)
        {
            Hearts = hearts;
            HalfHearts = halfHearts;
            Percent = percent;
            Low = low;
            Grace = grace;
        }

        /// <summary>Скільки сердечок показано всього.</summary>
        public int Hearts { get; }

        /// <summary>Скільки половинок сердечок заповнено (0..2 × Hearts).</summary>
        public int HalfHearts { get; }

        /// <summary>Заповнення смужки, 0..100.</summary>
        public int Percent { get; }

        /// <summary>Лишилось менше одного сердечка: клієнт ось-ось піде (ART_STYLE_GUIDE 4.3).</summary>
        public bool Low { get; }

        /// <summary>Пільговий час: терпіння ще не спадає.</summary>
        public bool Grace { get; }

        /// <summary>Заповнення сердечка за номером (0 — ліве).</summary>
        public HeartFill GetHeart(int index)
        {
            int filled = HalfHearts - (2 * index);
            return filled >= 2 ? HeartFill.Full : (filled == 1 ? HeartFill.Half : HeartFill.Empty);
        }

        public bool Equals(PatienceView other)
        {
            return Hearts == other.Hearts
                && HalfHearts == other.HalfHearts
                && Percent == other.Percent
                && Low == other.Low
                && Grace == other.Grace;
        }

        public override bool Equals(object obj)
        {
            return obj is PatienceView other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Hearts;
                hash = (hash * 397) ^ HalfHearts;
                hash = (hash * 397) ^ Percent;
                hash = (hash * 397) ^ (Low ? 1 : 0);
                hash = (hash * 397) ^ (Grace ? 2 : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1} half-hearts {2}%{3}{4}",
                HalfHearts,
                2 * Hearts,
                Percent,
                Low ? " low" : string.Empty,
                Grace ? " grace" : string.Empty);
        }
    }

    /// <summary>Що показано в слоті прилавка. Лише значення, без об'єктів Unity.</summary>
    public readonly struct CustomerSlotView : IEquatable<CustomerSlotView>
    {
        public CustomerSlotView(int customerId, int typeIndex, PatienceView patience, bool serveQueued)
        {
            CustomerId = customerId;
            TypeIndex = typeIndex;
            Patience = patience;
            ServeQueued = serveQueued;
        }

        /// <summary>Порожній слот.</summary>
        public static CustomerSlotView Free => new CustomerSlotView(CounterSlots.None, -1, default(PatienceView), false);

        /// <summary>Номер клієнта в розкладі або <see cref="CounterSlots.None"/> — слот порожній.</summary>
        public int CustomerId { get; }

        /// <summary>Номер типу клієнта в <see cref="CounterConfig"/>; -1 — слот порожній.</summary>
        public int TypeIndex { get; }

        public PatienceView Patience { get; }

        /// <summary>Видача страви саме цьому слоту вже стоїть у черзі дій.</summary>
        public bool ServeQueued { get; }

        public bool Occupied => CustomerId != CounterSlots.None;

        public bool Equals(CustomerSlotView other)
        {
            return CustomerId == other.CustomerId
                && TypeIndex == other.TypeIndex
                && Patience.Equals(other.Patience)
                && ServeQueued == other.ServeQueued;
        }

        public override bool Equals(object obj)
        {
            return obj is CustomerSlotView other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CustomerId;
                hash = (hash * 397) ^ TypeIndex;
                hash = (hash * 397) ^ Patience.GetHashCode();
                hash = (hash * 397) ^ (ServeQueued ? 1 : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return Occupied
                ? "#" + CustomerId.ToString(CultureInfo.InvariantCulture) + " " + Patience + (ServeQueued ? " serve" : string.Empty)
                : "free";
        }
    }

    /// <summary>
    /// Модель вигляду прилавка (S1-09b): стан Core → дані для показу (терпіння сердечками, значки
    /// замовлення, черга) і тап по клієнту → команда «видати страву цьому клієнту». Правил гри тут
    /// немає: чи візьме клієнт страву, вирішує Core на такті.
    /// </summary>
    public static class CounterView
    {
        /// <summary>
        /// Скільки сердечок терпіння малювати (ART_STYLE_GUIDE 4.3: «три сердечка»). Це вигляд, а не
        /// баланс: час терпіння береться з customers.json, сердечка лише ділять його порівну.
        /// </summary>
        public const int DefaultHearts = 3;

        private const int FullPercent = 100;

        /// <summary>
        /// Терпіння для показу. Шкала ділиться порівну на 2 × hearts половинок; заповнена половинка
        /// округлюється вгору (повне сердечко зникає лише тоді, коли його частина справді спливла).
        /// Залишок 0 або менше — порожньо (клієнт пішов).
        /// </summary>
        public static PatienceView Patience(long leftTicks, long totalTicks, long graceLeftTicks, int hearts)
        {
            if (hearts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hearts));
            }

            if (totalTicks <= 0L || leftTicks <= 0L)
            {
                return new PatienceView(hearts, 0, 0, false, false);
            }

            if (leftTicks > totalTicks)
            {
                leftTicks = totalTicks;
            }

            int units = 2 * hearts;
            int halves = (int)CeilDiv(leftTicks * units, totalTicks);
            int percent = (int)CeilDiv(leftTicks * FullPercent, totalTicks);
            bool low = leftTicks * hearts < totalTicks;
            return new PatienceView(hearts, halves, percent, low, graceLeftTicks > 0L);
        }

        /// <summary>Терпіння клієнта; той, що пішов, — порожньо, обслужений — як був у момент видачі.</summary>
        public static PatienceView Patience(Customer customer, int hearts)
        {
            if (customer == null)
            {
                throw new ArgumentNullException(nameof(customer));
            }

            long left = customer.State == CustomerState.Left ? 0L : customer.PatienceLeftTicks;
            return Patience(left, customer.PatienceTicks, customer.GraceLeftTicks, hearts);
        }

        /// <summary>Вигляд слота прилавка з живої симуляції (без виділення пам'яті: для щокадрового показу).</summary>
        public static CustomerSlotView FromSimulation(ShiftSimulation simulation, int slot, int hearts)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            Counter counter = simulation.Counter;
            if (counter == null || !counter.Slots.IsValidSlot(slot))
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            Customer customer = counter.GetCustomerInSlot(slot);
            if (customer == null)
            {
                return CustomerSlotView.Free;
            }

            bool queued = false;
            int count = simulation.QueuedActionCount;
            for (int i = 0; i < count; i++)
            {
                ShiftAction action = simulation.GetQueuedAction(i);
                if (action.Type == ShiftActionType.ServeDish && action.Slot == slot)
                {
                    queued = true;
                    break;
                }
            }

            return new CustomerSlotView(customer.Id, customer.TypeIndex, Patience(customer, hearts), queued);
        }

        /// <summary>Скільки клієнтів чекає за межами слотів (0 — черги немає або прилавка немає).</summary>
        public static int QueueLength(ShiftSimulation simulation)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            return simulation.Counter == null ? 0 : simulation.Counter.QueueLength;
        }

        /// <summary>
        /// Значки замовлення: спершу обов'язкові кроки рецепта, потім необов'язкові, які клієнт
        /// просить (разом із «завжди» його типу), потім ті, які класти не можна. Біти масок — як у
        /// <see cref="Dish.OptionalMask"/>.
        /// </summary>
        public static OrderIcon[] OrderOf(BoardConfig board, int recipeIndex, int requestedMask, int excludeMask)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (recipeIndex < 0 || recipeIndex >= board.RecipeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(recipeIndex));
            }

            Recipe recipe = board.GetRecipe(recipeIndex);
            var icons = new List<OrderIcon>(recipe.Length);
            for (int i = 0; i < recipe.StepCount; i++)
            {
                icons.Add(new OrderIcon(board.IndexOfIngredient(recipe.GetStep(i)), OrderIconKind.Step));
            }

            AddOptional(icons, board, recipe, requestedMask, OrderIconKind.Requested);
            AddOptional(icons, board, recipe, excludeMask, OrderIconKind.Excluded);
            return icons.ToArray();
        }

        /// <summary>Значки замовлення клієнта.</summary>
        public static OrderIcon[] OrderOf(BoardConfig board, Customer customer)
        {
            if (customer == null)
            {
                throw new ArgumentNullException(nameof(customer));
            }

            return OrderOf(board, customer.RecipeIndex, customer.RequestedMask, customer.ExcludeMask);
        }

        /// <summary>
        /// Яку команду шле тап по слоту: «видати страву цьому клієнту». false — слот порожній або
        /// видача йому вже в черзі. Чи готова страва і чи візьме її клієнт — вирішує Core.
        /// </summary>
        public static bool TryGetTap(CustomerSlotView view, out ShiftTap tap)
        {
            tap = default(ShiftTap);
            if (!view.Occupied || view.ServeQueued)
            {
                return false;
            }

            tap = ShiftTap.ServeDish;
            return true;
        }

        private static void AddOptional(List<OrderIcon> icons, BoardConfig board, Recipe recipe, int mask, OrderIconKind kind)
        {
            for (int bit = 0; bit < recipe.OptionalCount; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                {
                    icons.Add(new OrderIcon(board.IndexOfIngredient(recipe.GetOptional(bit)), kind));
                }
            }
        }

        private static long CeilDiv(long numerator, long denominator)
        {
            return (numerator + denominator - 1L) / denominator;
        }
    }
}
