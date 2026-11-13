using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>
    /// Усі тапи екрана зміни (S1-09c) в одному місці: гриль, клієнт, станція, предмет на дошці,
    /// очищення, скасування дії з черги. Тап проходить фільтр моделі вигляду і йде лише в
    /// <see cref="ShiftDriver.Tap"/>; правил гри тут немає. Панелі UI і тестовий бот користуються
    /// тими самими методами. Тут же — коротка підказка, чому тап нічого не дав: тап по м'ясу, що
    /// готується (BUG-037), або дія, яку Core проігнорував (<see cref="ShiftSimulation.LastActionResult"/>).
    /// Без UnityEngine; <see cref="Update"/> не виділяє пам'яті.
    /// </summary>
    public sealed class ShiftInput
    {
        /// <summary>Скільки показувати підказку, мс часу зміни (вигляд, не баланс).</summary>
        public const int HintMs = 1500;

        private readonly long _hintTicks;
        private long _ignored;
        private long _hintUntil;

        public ShiftInput(ShiftDriver driver)
        {
            Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _hintTicks = TickTime.FromMs(HintMs, driver.Simulation.Config.TicksPerSecond);
            _ignored = driver.Simulation.IgnoredActionCount;
        }

        public ShiftDriver Driver { get; }

        /// <summary>Ключ локалізації підказки, яку зараз показувати; null — підказки немає.</summary>
        public string HintKey { get; private set; }

        /// <summary>Тап по слоту гриля: покласти, зняти або викинути — за виглядом слота.</summary>
        public bool TapGrillSlot(int slot)
        {
            ShiftSimulation simulation = Driver.Simulation;
            if (simulation.Grill == null || slot < 0 || slot >= simulation.Grill.SlotCount)
            {
                return false;
            }

            ShiftTap tap;
            GrillSlotView view = GrillView.FromSimulation(simulation, slot);
            if (GrillView.TryGetTap(view, out tap))
            {
                return Driver.Tap(tap, slot);
            }

            // GAME_DESIGN розд. 4: «взяти Raw не можна (дія ігнорується, підказка)».
            if (!view.ActionQueued && CanTap())
            {
                ShowHint(TextKeys.HintMeatNotReady);
            }

            return false;
        }

        /// <summary>Тап по клієнту: видати страву з дошки саме йому.</summary>
        public bool TapCustomer(int slot)
        {
            ShiftSimulation simulation = Driver.Simulation;
            if (simulation.Counter == null || !simulation.Counter.Slots.IsValidSlot(slot))
            {
                return false;
            }

            ShiftTap tap;
            CustomerSlotView view = CounterView.FromSimulation(simulation, slot, CounterView.DefaultHearts);
            return CounterView.TryGetTap(view, out tap) && Driver.Tap(tap, slot);
        }

        /// <summary>Тап по станції: покласти інгредієнт на дошку.</summary>
        public bool TapStation(int ingredient)
        {
            return Driver.Tap(ShiftTap.AddIngredient, ingredient);
        }

        /// <summary>Тап по інгредієнту на дошці: зняти саме його (без штрафу, PROVISIONAL 17).</summary>
        public bool TapBoardItem(int position)
        {
            Board board = Driver.Simulation.Board;
            return board != null && position >= 0 && position < board.ItemCount
                && Driver.Tap(ShiftTap.RemoveIngredient, position);
        }

        /// <summary>Тап «Очистити дошку» (у смітник, зі штрафом за конфігом).</summary>
        public bool TapClearBoard()
        {
            Board board = Driver.Simulation.Board;
            return board != null && board.ItemCount > 0 && Driver.Tap(ShiftTap.ClearBoard);
        }

        /// <summary>Тап по дії в черзі: скасувати її (PROVISIONAL 14).</summary>
        public bool TapQueuedAction(int index)
        {
            return Driver.Tap(ShiftTap.CancelQueuedAction, index);
        }

        /// <summary>Підхоплює дію, яку Core проігнорував, і гасить старі підказки. Викликається щокадру.</summary>
        public void Update()
        {
            ShiftSimulation simulation = Driver.Simulation;
            if (simulation.IgnoredActionCount != _ignored)
            {
                _ignored = simulation.IgnoredActionCount;
                string key = AssemblyView.HintKeyOf(simulation.LastActionResult);
                if (key != null)
                {
                    ShowHint(key);
                }
            }

            if (HintKey != null && simulation.ElapsedTicks >= _hintUntil)
            {
                HintKey = null;
            }
        }

        private bool CanTap()
        {
            return Driver.Simulation.State == ShiftState.Running && !Driver.IsPaused && !Driver.IsFinished;
        }

        private void ShowHint(string key)
        {
            HintKey = key;
            _hintUntil = Driver.Simulation.ElapsedTicks + _hintTicks;
        }
    }
}
