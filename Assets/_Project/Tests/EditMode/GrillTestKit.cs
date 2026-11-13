using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Спільні заготовки для тестів гриля (значення — лише для тестів, не баланс гри).
    /// За 60 тактів на секунду: приготування 120 тактів, вікно до пригорання 180,
    /// попередження за 60 тактів, дії миттєві.
    /// </summary>
    internal static class GrillTestKit
    {
        public const int Slots = 3;
        public const int CookMs = 2000;
        public const int BurnAfterDoneMs = 3000;
        public const int WarnBeforeBurnMs = 1000;
        public const int BurntPenaltyCoins = 7;
        public const int QueueMax = 4;
        public const int DurationSec = 60;

        public const long CookTicks = 120L;
        public const long BurnTicks = 180L;
        public const long WarnTicks = 60L;
        public const long BurntAtTicks = CookTicks + BurnTicks;

        public static GrillConfig GrillParams(
            int slots = Slots,
            int cookMs = CookMs,
            int burnAfterDoneMs = BurnAfterDoneMs,
            int warnBeforeBurnMs = WarnBeforeBurnMs,
            int burntPenaltyCoins = BurntPenaltyCoins,
            int actionMs = 0)
        {
            return new GrillConfig(slots, cookMs, burnAfterDoneMs, warnBeforeBurnMs, burntPenaltyCoins, actionMs);
        }

        public static ShiftConfig Config(
            GrillConfig grill = null,
            int actionQueueMax = QueueMax,
            int durationSec = DurationSec,
            int continueBonusSec = ShiftTestKit.ContinueBonusSec)
        {
            return new ShiftConfig(
                ShiftTestKit.TicksPerSecond,
                ShiftTestKit.MaxCatchUpTicks,
                durationSec,
                continueBonusSec,
                actionQueueMax,
                grill ?? GrillParams());
        }

        /// <summary>Нова зміна з грилем у стані Intro.</summary>
        public static ShiftSimulation Create(
            GrillConfig grill = null,
            int actionQueueMax = QueueMax,
            bool goalAchieved = true,
            int durationSec = DurationSec)
        {
            return new ShiftSimulation(
                Config(grill, actionQueueMax, durationSec), ShiftTestKit.Seed, new FixedGoal(goalAchieved));
        }

        /// <summary>Нова зміна з грилем, уже переведена в Running.</summary>
        public static ShiftSimulation CreateRunning(
            GrillConfig grill = null,
            int actionQueueMax = QueueMax,
            bool goalAchieved = true,
            int durationSec = DurationSec)
        {
            ShiftSimulation shift = Create(grill, actionQueueMax, goalAchieved, durationSec);
            shift.Start();
            return shift;
        }

        /// <summary>Виконує рівно задану кількість тактів; зміна має лишатися в Running.</summary>
        public static void Tick(ShiftSimulation shift, long ticks)
        {
            for (long i = 0; i < ticks; i++)
            {
                Assert.IsTrue(shift.Tick(), "Такт не виконано: зміна не в Running");
            }
        }

        /// <summary>
        /// Кладе м'ясо в слот і виконує один такт (дії в цих тестах миттєві):
        /// після виклику м'ясо сире, вік 0.
        /// </summary>
        public static void Place(ShiftSimulation shift, int slot = GrillSlot.Any)
        {
            Assert.IsTrue(shift.PlaceMeat(slot), "Команду «покласти» не прийнято");
            Tick(shift, 1);
            Assert.AreEqual(ShiftActionResult.Done, shift.LastActionResult, "М'ясо не покладено");
        }

        /// <summary>Кладе м'ясо в слот і доводить його до заданого віку в тактах.</summary>
        public static void PlaceAndAge(ShiftSimulation shift, int slot, long ageTicks)
        {
            Place(shift, slot);
            Tick(shift, ageTicks);
            Assert.AreEqual(ageTicks, shift.Grill.GetSlot(slot).AgeTicks, "Підготовка тесту: вік м'яса");
        }

        public static MeatState StateOf(ShiftSimulation shift, int slot)
        {
            return shift.Grill.GetSlot(slot).State;
        }
    }
}
