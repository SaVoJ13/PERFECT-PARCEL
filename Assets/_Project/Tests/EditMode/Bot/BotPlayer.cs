using System.Collections.Generic;
using KebabStreet.Core;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Бот-гравець (S1-05): грає зміну лише публічними командами <see cref="ShiftSimulation"/>,
    /// як людина. Бачить поточний стан гриля, дошки й прилавка (те, що видно на екрані), але
    /// не бачить розкладу приходів наперед. Рішення приймає, лише коли черга дій порожня
    /// (тобто тримає в черзі не більше однієї дії, що не перевищує actionQueueMax).
    /// Політика: прибрати пригоріле; видати готову страву; прибрати хибне з дошки; покласти
    /// м'ясо, якщо клієнтів більше, ніж шматків на грилі; збирати страву для клієнта з
    /// найменшим залишком терпіння (піта, м'ясо, необов'язкове в порядку рецепта).
    /// Бот живе в тестовій збірці: у гру він не потрапляє.
    /// </summary>
    internal sealed class BotPlayer
    {
        private const int PermilleBase = 1000;
        private const int MistakeKinds = 3;

        private readonly ShiftSimulation _sim;
        private readonly BotProfile _profile;
        private readonly SplitMix64Random _random;
        private readonly List<int> _items = new List<int>(16);
        private int _waitTicks;

        public BotPlayer(ShiftSimulation sim, BotProfile profile, ulong seed)
        {
            _sim = sim;
            _profile = profile;
            _random = new SplitMix64Random(seed);
        }

        /// <summary>Скільки помилок бот зробив за зміну (лише профіль із MistakePermille більшим за 0).</summary>
        public long MistakeCount { get; private set; }

        /// <summary>Викликається один раз перед кожним тактом: бот ставить у чергу дію, якщо вирішив діяти.</summary>
        public void Step()
        {
            if (_sim.State != ShiftState.Running || _sim.QueuedActionCount > 0)
            {
                return;
            }

            if (_waitTicks > 0)
            {
                _waitTicks--;
                return;
            }

            if (_profile.MistakePermille > 0 && _random.NextInt(0, PermilleBase) < _profile.MistakePermille)
            {
                MistakeCount++;
                if (MakeMistake())
                {
                    return;
                }
            }

            if (Decide())
            {
                _waitTicks = _profile.ReactionTicks;
            }
        }

        private bool MakeMistake()
        {
            Board board = _sim.Board;
            switch (_random.NextInt(0, MistakeKinds))
            {
                case 0:
                    // Відволікся: нічого не робить певний час.
                    _waitTicks = _profile.DistractionTicks;
                    return true;
                case 1:
                    // Поклав не той інгредієнт (із доступних, крім м'яса).
                    if (board.IsFull)
                    {
                        return false;
                    }

                    int wrong = _random.NextInt(0, board.Config.IngredientCount);
                    if (!board.CanAdd(wrong) || !_sim.AddIngredient(wrong))
                    {
                        return false;
                    }

                    _waitTicks = _profile.ReactionTicks;
                    return true;
                default:
                    // Віддав готову страву не тому клієнтові.
                    Dish dish;
                    if (board.Check(out dish) != DishCheck.Correct)
                    {
                        return false;
                    }

                    Customer other = FindNotAccepting(dish);
                    if (other == null || !_sim.ServeDish(other.Slot))
                    {
                        return false;
                    }

                    _waitTicks = _profile.ReactionTicks;
                    return true;
            }
        }

        private bool Decide()
        {
            Counter counter = _sim.Counter;
            Board board = _sim.Board;
            Grill grill = _sim.Grill;

            // 1. Пригоріле м'ясо займає слот: викинути.
            for (int s = 0; s < grill.SlotCount; s++)
            {
                if (grill.GetSlot(s).State == MeatState.Burnt)
                {
                    return _sim.DiscardBurntMeat(s);
                }
            }

            // 2. Страва на дошці: видати, а якщо вона нікому не підходить і є кому служити — розібрати.
            int count = board.ItemCount;
            Customer target = null;
            if (count > 0)
            {
                Dish dish;
                if (board.Check(out dish) == DishCheck.Correct)
                {
                    Customer receiver = FindReceiver(dish);
                    if (receiver != null)
                    {
                        return _sim.ServeDish(receiver.Slot);
                    }
                }

                target = FindExtensionTarget(board);
                if (target == null && WaitingCount(counter) > 0)
                {
                    return _sim.RemoveIngredient();
                }
            }

            // 3. М'яса на грилі менше, ніж клієнтів, що чекають: покласти нове.
            if (grill.FreeSlotCount > 0 && SuppliedMeat(grill, board) < Demand(counter))
            {
                return _sim.PlaceMeat();
            }

            // 4. Збирати страву.
            if (count == 0)
            {
                target = FindMostUrgent();
            }

            if (target == null)
            {
                return false;
            }

            BuildItems(target, board);
            int next = _items[count];
            if (next == board.MeatIngredient)
            {
                return HasDoneMeat(grill) && _sim.TakeMeat();
            }

            return _sim.AddIngredient(next);
        }

        /// <summary>Скільки клієнтів стоїть у слотах прилавка.</summary>
        private static int WaitingCount(Counter counter)
        {
            int waiting = 0;
            for (int s = 0; s < counter.Slots.Count; s++)
            {
                if (counter.GetCustomerInSlot(s) != null)
                {
                    waiting++;
                }
            }

            return waiting;
        }

        /// <summary>Скільки страв із м'ясом потрібно: клієнти в слотах і ті з черги, що зараз стануть у слот.</summary>
        private static int Demand(Counter counter)
        {
            int queued = counter.QueueLength;
            int free = counter.Slots.FreeCount;
            return WaitingCount(counter) + (queued < free ? queued : free);
        }

        /// <summary>М'ясо, що вже є: сире й готове на грилі плюс шматок у страві на дошці.</summary>
        private static int SuppliedMeat(Grill grill, Board board)
        {
            int supplied = 0;
            for (int s = 0; s < grill.SlotCount; s++)
            {
                MeatState state = grill.GetSlot(s).State;
                if (state == MeatState.Raw || state == MeatState.Done)
                {
                    supplied++;
                }
            }

            for (int i = 0; i < board.ItemCount; i++)
            {
                if (board.GetItem(i) == board.MeatIngredient)
                {
                    supplied++;
                }
            }

            return supplied;
        }

        private static bool HasDoneMeat(Grill grill)
        {
            for (int s = 0; s < grill.SlotCount; s++)
            {
                if (grill.GetSlot(s).State == MeatState.Done)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Клієнт у слоті, якому підходить страва; з найменшим залишком терпіння.</summary>
        private Customer FindReceiver(Dish dish)
        {
            Counter counter = _sim.Counter;
            Customer best = null;
            for (int s = 0; s < counter.Slots.Count; s++)
            {
                Customer c = counter.GetCustomerInSlot(s);
                if (c != null && c.Accepts(dish) && (best == null || Urgency(c) < Urgency(best)))
                {
                    best = c;
                }
            }

            return best;
        }

        /// <summary>Клієнт у слоті, якому страва не підходить (для помилки «не тому»).</summary>
        private Customer FindNotAccepting(Dish dish)
        {
            Counter counter = _sim.Counter;
            for (int s = 0; s < counter.Slots.Count; s++)
            {
                Customer c = counter.GetCustomerInSlot(s);
                if (c != null && !c.Accepts(dish))
                {
                    return c;
                }
            }

            return null;
        }

        /// <summary>Клієнт із найменшим залишком терпіння (разом із пільгою); null, якщо нікого.</summary>
        private Customer FindMostUrgent()
        {
            Counter counter = _sim.Counter;
            Customer best = null;
            for (int s = 0; s < counter.Slots.Count; s++)
            {
                Customer c = counter.GetCustomerInSlot(s);
                if (c != null && (best == null || Urgency(c) < Urgency(best)))
                {
                    best = c;
                }
            }

            return best;
        }

        /// <summary>
        /// Клієнт, чия страва довша за те, що лежить на дошці, і починається з цього (дошка —
        /// початок його страви); з найменшим залишком терпіння.
        /// </summary>
        private Customer FindExtensionTarget(Board board)
        {
            Counter counter = _sim.Counter;
            Customer best = null;
            for (int s = 0; s < counter.Slots.Count; s++)
            {
                Customer c = counter.GetCustomerInSlot(s);
                if (c == null || (best != null && Urgency(c) >= Urgency(best)))
                {
                    continue;
                }

                BuildItems(c, board);
                if (_items.Count > board.ItemCount && IsPrefix(board))
                {
                    best = c;
                }
            }

            return best;
        }

        private bool IsPrefix(Board board)
        {
            for (int i = 0; i < board.ItemCount; i++)
            {
                if (board.GetItem(i) != _items[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Повний склад страви клієнта в порядку збирання: кроки рецепта, потім його необов'язкові.</summary>
        private void BuildItems(Customer customer, Board board)
        {
            _items.Clear();
            Recipe recipe = board.Config.GetRecipe(customer.RecipeIndex);
            for (int i = 0; i < recipe.StepCount; i++)
            {
                _items.Add(board.Config.IndexOfIngredient(recipe.GetStep(i)));
            }

            for (int k = 0; k < recipe.OptionalCount; k++)
            {
                if ((customer.RequestedMask & (1 << k)) != 0)
                {
                    _items.Add(board.Config.IndexOfIngredient(recipe.GetOptional(k)));
                }
            }
        }

        private static long Urgency(Customer customer)
        {
            return customer.PatienceLeftTicks + customer.GraceLeftTicks;
        }
    }
}
