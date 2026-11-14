using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>Тап гравця, що перетворюється на команду в черзі дій Core (S1-08; графіка станцій — S1-09).</summary>
    public enum ShiftTap
    {
        PlaceMeat = 1,
        TakeMeat = 2,
        DiscardBurntMeat = 3,
        AddIngredient = 4,
        RemoveIngredient = 5,
        ClearBoard = 6,
        ServeDish = 7,
        CancelQueuedAction = 8,
    }

    /// <summary>
    /// Адаптер реального часу до <see cref="ShiftSimulation"/> (S1-08). Без UnityEngine: контролер
    /// передає тривалість кадру (unscaledDeltaTime), адаптер віддає її симуляції, а та виконує такти
    /// фіксованого кроку з обмеженням наздоганяння (<c>maxCatchUpTicks</c>, захист від «спіралі»).
    /// Пауза не накопичує часу; перший кадр після відновлення відкидається, щоб довгий кадр
    /// після згортання не з'їв зміну. Кінець зміни повідомляється рівно один раз.
    /// Правил гри тут немає: цілі, зірки й поразка — у <see cref="ShiftEvaluator"/>.
    /// </summary>
    public sealed class ShiftDriver
    {
        /// <summary>Будь-який слот гриля, верхній предмет дошки, будь-який клієнт (усі три константи Core дорівнюють -1).</summary>
        public const int AnyTarget = -1;

        private readonly Action<ShiftResult> _onFinished;
        private bool _began;
        private bool _paused;
        private bool _discardNextFrame;

        public ShiftDriver(LevelDefinition level, ulong seed, Action<ShiftResult> onFinished = null)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Seed = seed;
            _onFinished = onFinished;
            Evaluator = ShiftEvaluator.ForLevel(level);
            Simulation = new ShiftSimulation(level.Shift, seed, Evaluator);
        }

        public LevelDefinition Level { get; }

        public ulong Seed { get; }

        public ShiftSimulation Simulation { get; }

        public ShiftEvaluator Evaluator { get; }

        public bool IsPaused => _paused;

        public bool IsFinished { get; private set; }

        /// <summary>Результат зміни; має сенс лише після <see cref="IsFinished"/>.</summary>
        public ShiftResult Result { get; private set; }

        /// <summary>
        /// Ціль виконано, а зміна ще триває і ★★ ще не набрано (рівні з endOnGoal=false):
        /// екран показує напис shift.goal_done_star2.
        /// </summary>
        public bool GoalDoneStarHint { get; private set; }

        /// <summary>Починає зміну (Intro → Running). false, якщо вже почата.</summary>
        public bool Begin()
        {
            if (_began)
            {
                return false;
            }

            _began = Simulation.Start();
            return _began;
        }

        /// <summary>
        /// Ставить гру на паузу або знімає її (згортання, втрата фокуса). Повторний виклик з тим
        /// самим значенням нічого не робить. Повертає поточний стан паузи.
        /// </summary>
        public bool SetPaused(bool paused)
        {
            if (!_began || IsFinished || paused == _paused)
            {
                return _paused;
            }

            if (paused)
            {
                _paused = Simulation.Pause();
            }
            else if (Simulation.Resume())
            {
                _paused = false;
                _discardNextFrame = true;
            }

            return _paused;
        }

        /// <summary>
        /// Віддає симуляції час кадру в секундах; повертає, скільки тактів виконано. На паузі, до
        /// початку і після кінця — 0. NaN і від'ємний час вважаються нулем.
        /// </summary>
        public int Frame(double deltaSeconds)
        {
            if (!_began || IsFinished || _paused)
            {
                return 0;
            }

            if (_discardNextFrame)
            {
                _discardNextFrame = false;
                return 0;
            }

            if (double.IsNaN(deltaSeconds) || deltaSeconds < 0d)
            {
                deltaSeconds = 0d;
            }

            int ticks = Simulation.Advance(deltaSeconds);
            if (Simulation.State == ShiftState.Won || Simulation.State == ShiftState.Lost)
            {
                Finish();
            }
            else
            {
                GoalDoneStarHint = !Level.Goal.EndOnGoal
                    && Evaluator.IsGoalReached(Simulation)
                    && Simulation.CoinsEarned < Level.Stars.TwoCoins;
            }

            return ticks;
        }

        /// <summary>
        /// Тап гравця → команда в черзі дій Core. На паузі й поза зміною тапи відкидаються (false);
        /// true — команду прийнято до черги (чи вдасться вона — вирішує Core на такті).
        /// </summary>
        public bool Tap(ShiftTap tap, int argument = AnyTarget)
        {
            if (!_began || IsFinished || _paused)
            {
                return false;
            }

            switch (tap)
            {
                case ShiftTap.PlaceMeat:
                    return Simulation.PlaceMeat(argument);
                case ShiftTap.TakeMeat:
                    return Simulation.TakeMeat(argument);
                case ShiftTap.DiscardBurntMeat:
                    return Simulation.DiscardBurntMeat(argument);
                case ShiftTap.AddIngredient:
                    return Simulation.AddIngredient(argument);
                case ShiftTap.RemoveIngredient:
                    return Simulation.RemoveIngredient(argument);
                case ShiftTap.ClearBoard:
                    return Simulation.ClearBoard();
                case ShiftTap.ServeDish:
                    return Simulation.ServeDish(argument);
                case ShiftTap.CancelQueuedAction:
                    return Simulation.CancelQueuedAction(argument);
                default:
                    return false;
            }
        }

        private void Finish()
        {
            IsFinished = true;
            GoalDoneStarHint = false;
            Result = Evaluator.Evaluate(Simulation);
            if (_onFinished != null)
            {
                _onFinished(Result);
            }
        }
    }
}
