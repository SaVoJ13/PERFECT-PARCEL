using KebabStreet.Core;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Проганяє бота через одну зміну рівня: створює <see cref="ShiftSimulation"/> з конфігу рівня,
    /// перед кожним тактом дає боту поставити дію, потім виконує такт. Грає до кінця таймера,
    /// якщо не програв раніше через ліміт втрат. Однаковий рівень + seed + профіль дають
    /// однаковий результат.
    /// </summary>
    internal static class BotRunner
    {
        // Окремий генератор бота: біти seed змішуються з константою, щоб не збігатися зі стрічкою зміни.
        private const ulong BotSeedMask = 0xB07B07B07B07B07BUL;

        public static BotResult Run(LevelDefinition level, ulong seed, BotProfile profile)
        {
            var goal = new BotGoal(level.Goal, level.Lose);
            var sim = new ShiftSimulation(level.Shift, seed, goal);
            var bot = new BotPlayer(sim, profile, seed ^ BotSeedMask);
            sim.Start();
            while (sim.State == ShiftState.Running && !goal.Failed)
            {
                bot.Step();
                sim.Tick();
                goal.Observe(sim);
            }

            BotOutcome outcome = goal.Achieved
                ? BotOutcome.Won
                : (goal.Failed ? BotOutcome.TooManyLost : BotOutcome.TimeUp);

            var result = new BotResult();
            result.LevelId = level.Id;
            result.Seed = seed;
            result.Profile = profile.Name;
            result.Outcome = outcome;
            result.GoalType = level.Goal.Type;
            result.GoalTarget = level.Goal.Target;
            result.MaxLost = level.Lose.MaxLostCustomers;
            result.TicksPerSecond = level.Shift.TicksPerSecond;
            result.DurationTicks = level.Shift.DurationTicks;
            result.GoalTick = goal.AchievedTick;
            result.EndTick = sim.ElapsedTicks;
            result.Served = sim.ServedCount;
            result.Coins = sim.CoinsEarned;
            result.Lost = sim.LostCustomers;
            result.Left = sim.Counter.LeftCount;
            result.WrongDishes = sim.WrongDishes;
            result.Burnt = sim.BurntCount;
            result.MaxCombo = sim.MaxCombo;
            result.Mistakes = bot.MistakeCount;
            result.ExecutedActions = sim.ExecutedActionCount;
            result.IgnoredActions = sim.IgnoredActionCount;
            result.FinalSnapshot = sim.CaptureSnapshot();
            return result;
        }
    }
}
