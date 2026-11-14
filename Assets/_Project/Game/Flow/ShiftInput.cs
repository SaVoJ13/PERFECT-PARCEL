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
            ShiftSimulation sim = driver.Simulation;
            Flash = new TapFlash(
                sim.Grill == null ? 0 : sim.Grill.SlotCount,
                sim.Board == null ? 0 : sim.Board.Config.IngredientCount,
                sim.Board == null ? 0 : sim.Board.Capacity,
                sim.Counter == null ? 0 : sim.Counter.Slots.Count,
                Math.Max(1L, TickTime.FromMs(TapFlash.FlashMs, sim.Config.TicksPerSecond)));
        }

        public ShiftDriver Driver { get; }

        /// <summary>Миттєве підсвічування натиснутого (S1-21).</summary>
        public TapFlash Flash { get; }

        /// <summary>Дія, що зараз виконується (смужка прогресу, S1-21); оновлюється в <see cref="Update"/>.</summary>
        public ActionProgressView Progress { get; private set; }

        /// <summary>Чи світиться елемент зараз.</summary>
        public bool IsLit(FeedbackTarget target, int index)
        {
            return Flash.IsLit(target, index, Driver.Simulation.ElapsedTicks);
        }

        /// <summary>Скільки відсотків дії на цьому елементі відпрацьовано; -1 — на ньому дії немає.</summary>
        public int ProgressOf(FeedbackTarget target, int index)
        {
            return Progress.Is(target, index) ? Progress.Percent : -1;
        }

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

            MarkTapped(FeedbackTarget.GrillSlot, slot);
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

            MarkTapped(FeedbackTarget.Customer, slot);
            ShiftTap tap;
            CustomerSlotView view = CounterView.FromSimulation(simulation, slot, CounterView.DefaultHearts);
            return CounterView.TryGetTap(view, out tap) && Driver.Tap(tap, slot);
        }

        /// <summary>Тап по станції: покласти інгредієнт на дошку.</summary>
        public bool TapStation(int ingredient)
        {
            MarkTapped(FeedbackTarget.Station, ingredient);
            return Driver.Tap(ShiftTap.AddIngredient, ingredient);
        }

        /// <summary>Тап по інгредієнту на дошці: зняти саме його (без штрафу, PROVISIONAL 17).</summary>
        public bool TapBoardItem(int position)
        {
            Board board = Driver.Simulation.Board;
            if (board == null || position < 0 || position >= board.ItemCount)
            {
                return false;
            }

            MarkTapped(FeedbackTarget.BoardItem, position);
            return Driver.Tap(ShiftTap.RemoveIngredient, position);
        }

        /// <summary>Тап «Очистити дошку» (у смітник, зі штрафом за конфігом).</summary>
        public bool TapClearBoard()
        {
            Board board = Driver.Simulation.Board;
            if (board == null || board.ItemCount < 1)
            {
                return false;
            }

            MarkTapped(FeedbackTarget.Clear, 0);
            return Driver.Tap(ShiftTap.ClearBoard);
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

            Progress = ActionProgressView.FromSimulation(simulation);
        }

        private void MarkTapped(FeedbackTarget target, int index)
        {
            if (CanTap())
            {
                Flash.Mark(target, index, Driver.Simulation.ElapsedTicks);
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
