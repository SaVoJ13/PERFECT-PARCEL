using System;
using System.Collections.Generic;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>
    /// Виводить події навчання зі стану <see cref="ShiftSimulation"/> (S1-11). Core не має потоку подій, тож
    /// трекер порівнює стан із попереднім оглядом: лічильники гриля (покласти, зняти, викинути), стани слотів
    /// гриля (готове, пригоріле), остання виконана дія (додати інгредієнт), стани клієнтів (прийшов, замовлення
    /// видно, отримав страву). Нічого не змінює в симуляції. Огляд робиться щокадру; події з тактів кадру
    /// віддаються в порядку виявлення. Без UnityEngine; між оглядами нових об'єктів не створюється (рядки беруться
    /// з конфігів), крім внутрішніх масивів, створених один раз.
    /// </summary>
    public sealed class TutorialEventTracker
    {
        private readonly ShiftSimulation _simulation;
        private readonly MeatState[] _slotStates;
        private readonly CustomerState[] _customerStates;
        private bool _started;
        private long _placed;
        private long _taken;
        private long _discarded;
        private long _executed;

        public TutorialEventTracker(ShiftSimulation simulation)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            Grill grill = simulation.Grill;
            _slotStates = new MeatState[grill == null ? 0 : grill.SlotCount];
            Counter counter = simulation.Counter;
            _customerStates = new CustomerState[counter == null ? 0 : counter.CustomerCount];
            _placed = grill == null ? 0L : grill.PlacedCount;
            _taken = grill == null ? 0L : grill.TakenCount;
            _discarded = grill == null ? 0L : grill.DiscardedCount;
            _executed = simulation.ExecutedActionCount;
        }

        /// <summary>Додає у <paramref name="events"/> події, що відбулися від попереднього огляду.</summary>
        public void Observe(List<TutorialEvent> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (_simulation.State == ShiftState.Intro)
            {
                return;
            }

            if (!_started)
            {
                _started = true;
                events.Add(new TutorialEvent(TutorialEventKind.ShiftStarted));
            }

            ObserveActions(events);
            ObserveGrill(events);
            ObserveCounter(events);
        }

        private void ObserveActions(List<TutorialEvent> events)
        {
            Grill grill = _simulation.Grill;
            if (grill != null)
            {
                while (_placed < grill.PlacedCount)
                {
                    _placed++;
                    events.Add(new TutorialEvent(TutorialEventKind.PlaceMeat));
                }

                while (_taken < grill.TakenCount)
                {
                    _taken++;
                    events.Add(new TutorialEvent(TutorialEventKind.TakeMeat));
                }

                while (_discarded < grill.DiscardedCount)
                {
                    _discarded++;
                    events.Add(new TutorialEvent(TutorialEventKind.DiscardBurntMeat));
                }
            }

            long executed = _simulation.ExecutedActionCount;
            if (executed != _executed)
            {
                _executed = executed;
                ShiftAction last = _simulation.LastAction;
                Board board = _simulation.Board;
                if (board != null && last.Type == ShiftActionType.AddIngredient
                    && _simulation.LastActionResult == ShiftActionResult.Done
                    && last.Slot >= 0 && last.Slot < board.Config.IngredientCount)
                {
                    events.Add(new TutorialEvent(TutorialEventKind.AddIngredient, board.Config.GetIngredient(last.Slot).Id));
                }
            }
        }

        private void ObserveGrill(List<TutorialEvent> events)
        {
            Grill grill = _simulation.Grill;
            for (int i = 0; i < _slotStates.Length; i++)
            {
                MeatState state = grill.GetSlot(i).State;
                MeatState before = _slotStates[i];
                if (state == before)
                {
                    continue;
                }

                _slotStates[i] = state;
                if (state == MeatState.Done)
                {
                    events.Add(new TutorialEvent(TutorialEventKind.MeatDone));
                }
                else if (state == MeatState.Burnt)
                {
                    events.Add(new TutorialEvent(TutorialEventKind.MeatBurnt));
                }
            }
        }

        private void ObserveCounter(List<TutorialEvent> events)
        {
            Counter counter = _simulation.Counter;
            BoardConfig boardConfig = _simulation.Board == null ? null : _simulation.Board.Config;
            for (int i = 0; i < _customerStates.Length; i++)
            {
                Customer customer = counter.GetCustomer(i);
                CustomerState state = customer.State;
                CustomerState before = _customerStates[i];
                if (state == before)
                {
                    continue;
                }

                _customerStates[i] = state;
                string type = counter.Config.GetCustomerType(customer.TypeIndex).Id;
                if (before == CustomerState.None && state != CustomerState.None)
                {
                    events.Add(new TutorialEvent(TutorialEventKind.CustomerArrived, null, type));
                }

                bool becameVisible = state == CustomerState.Waiting
                    || (before < CustomerState.Waiting && (state == CustomerState.Served || state == CustomerState.Left));
                if (becameVisible && before < CustomerState.Waiting)
                {
                    AddOrderShown(events, customer, type, boardConfig);
                }

                if (state == CustomerState.Served)
                {
                    events.Add(new TutorialEvent(TutorialEventKind.ServeDish, null, type));
                }
            }
        }

        private static void AddOrderShown(List<TutorialEvent> events, Customer customer, string type, BoardConfig boardConfig)
        {
            bool any = false;
            if (boardConfig != null)
            {
                Recipe recipe = boardConfig.GetRecipe(customer.RecipeIndex);
                for (int bit = 0; bit < recipe.OptionalCount; bit++)
                {
                    if ((customer.RequestedMask & (1 << bit)) != 0)
                    {
                        any = true;
                        events.Add(new TutorialEvent(TutorialEventKind.OrderShown, recipe.GetOptional(bit), type));
                    }
                }
            }

            if (!any)
            {
                events.Add(new TutorialEvent(TutorialEventKind.OrderShown, null, type));
            }
        }
    }
}
