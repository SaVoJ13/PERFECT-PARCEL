using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Розклад приходу клієнтів: лише такти і переданий генератор. «Золоті» значення запізнень
    /// пораховано незалежною моделлю SplitMix64 на Node.js (BigInt), а не знято з коду C#.
    /// За 60 тактів на секунду: приходи на 0, 500, 1000, 1000 і 4000 мс (такти 0, 30, 60, 60, 240)
    /// із запізненням до 60, 0, 120, 30 і 6 тактів.
    /// </summary>
    public sealed class ArrivalScheduleTests
    {
        private static readonly long[] BaseTicks = { 0L, 30L, 60L, 60L, 240L };
        private static readonly long[] JitterTicks = { 60L, 0L, 120L, 30L, 6L };

        private static Arrival[] JitteredArrivals()
        {
            Order plain = CustomerTestKit.Plain();
            return new[]
            {
                new Arrival(0, "olya", plain, 1000, 0),
                new Arrival(500, "olya", plain, 0, 0),
                new Arrival(1000, "olya", plain, 2000, 0),
                new Arrival(1000, "olya", plain, 500, 0),
                new Arrival(4000, "olya", plain, 100, 0),
            };
        }

        private static ArrivalSchedule Jittered()
        {
            return new ArrivalSchedule(JitteredArrivals());
        }

        [Test]
        public void Schedule_KeepsArrivalsInGivenOrder()
        {
            ArrivalSchedule schedule = Jittered();

            Assert.AreEqual(5, schedule.Count);
            Assert.AreEqual(500, schedule.Get(1).AtMs);
            Assert.AreEqual("olya", schedule.Get(1).Customer);
            Assert.AreEqual("kebab_pita", schedule.Get(1).Order.Recipe);
            Assert.AreEqual(2000, schedule.Get(2).JitterMs);
            Assert.AreEqual(0, schedule.Get(2).GraceMs);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => schedule.Get(5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => schedule.Get(-1));
            Assert.AreEqual(0, new ArrivalSchedule(null).Count);
        }

        [Test]
        public void Schedule_CopiesItsList()
        {
            Arrival[] arrivals = JitteredArrivals();
            var schedule = new ArrivalSchedule(arrivals);

            arrivals[0] = null;

            Assert.IsNotNull(schedule.Get(0));
        }

        [Test]
        public void WithoutJitter_TicksComeStraightFromConfig_AndGeneratorIsNotTouched()
        {
            var schedule = new ArrivalSchedule(new[]
            {
                CustomerTestKit.At(0),
                CustomerTestKit.At(8),
                CustomerTestKit.At(9),
                CustomerTestKit.At(1000),
                CustomerTestKit.At(2500),
            });
            var random = new SplitMix64Random(77UL);

            // 8 мс = 0,48 такту → 0; 9 мс = 0,54 такту → 1 (округлення до найближчого такту).
            CollectionAssert.AreEqual(new[] { 0L, 0L, 1L, 60L, 150L }, schedule.ResolveTicks(60, random));
            Assert.AreEqual(77UL, random.State);
            CollectionAssert.AreEqual(new[] { 0L, 0L, 1L, 60L, 150L }, schedule.ResolveTicks(60, null));
            CollectionAssert.AreEqual(new[] { 0L, 1L, 1L, 120L, 300L }, schedule.ResolveTicks(120, null));
        }

        [TestCase(20261110UL, new[] { 15L, 30L, 143L, 75L, 241L }, 8709371129893951818UL)]
        [TestCase(1UL, new[] { 26L, 30L, 123L, 79L, 240L }, 8709371129873690709UL)]
        [TestCase(2UL, new[] { 42L, 30L, 86L, 72L, 241L }, 8709371129873690710UL)]
        public void WithJitter_TicksMatchGoldenValues(ulong seed, long[] expected, ulong stateAfter)
        {
            var random = new SplitMix64Random(seed);

            long[] ticks = Jittered().ResolveTicks(60, random);

            CollectionAssert.AreEqual(expected, ticks);
            Assert.AreEqual(stateAfter, random.State, "Одне число генератора на кожен запис із запізненням");
        }

        [Test]
        public void WithJitter_EveryTickIsInsideItsWindow_ForManySeeds()
        {
            ArrivalSchedule schedule = Jittered();
            bool anyLate = false;
            for (ulong seed = 0UL; seed < 200UL; seed++)
            {
                long[] ticks = schedule.ResolveTicks(60, new SplitMix64Random(seed));
                for (int i = 0; i < ticks.Length; i++)
                {
                    Assert.GreaterOrEqual(ticks[i], BaseTicks[i]);
                    Assert.LessOrEqual(ticks[i], BaseTicks[i] + JitterTicks[i]);
                    anyLate |= ticks[i] > BaseTicks[i];
                }
            }

            Assert.IsTrue(anyLate);
        }

        [Test]
        public void WithJitter_SameSeedSameTicks_DifferentSeedDifferentTicks()
        {
            ArrivalSchedule schedule = Jittered();

            CollectionAssert.AreEqual(
                schedule.ResolveTicks(60, new SplitMix64Random(5UL)),
                schedule.ResolveTicks(60, new SplitMix64Random(5UL)));
            CollectionAssert.AreNotEqual(
                schedule.ResolveTicks(60, new SplitMix64Random(1UL)),
                schedule.ResolveTicks(60, new SplitMix64Random(2UL)));
        }

        [Test]
        public void WithJitter_NeedsGenerator()
        {
            Assert.Throws<System.ArgumentNullException>(() => Jittered().ResolveTicks(60, null));
        }

        [Test]
        public void OrderByTick_SortsByTick_AndKeepsScheduleOrderForEqualTicks()
        {
            CollectionAssert.AreEqual(
                new[] { 1, 0, 3, 2, 4 }, ArrivalSchedule.OrderByTick(new[] { 42L, 30L, 86L, 72L, 241L }));
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2, 3 }, ArrivalSchedule.OrderByTick(new[] { 5L, 5L, 5L, 5L }));
            CollectionAssert.AreEqual(
                new[] { 3, 1, 2, 0 }, ArrivalSchedule.OrderByTick(new[] { 9L, 7L, 7L, 1L }));
            CollectionAssert.AreEqual(new int[0], ArrivalSchedule.OrderByTick(new long[0]));
            Assert.Throws<System.ArgumentNullException>(() => ArrivalSchedule.OrderByTick(null));
        }

        [Test]
        public void Shift_WithoutJitter_DoesNotTouchItsGenerator()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0), CustomerTestKit.At(1000));

            Assert.AreEqual(ShiftTestKit.Seed, shift.CaptureSnapshot().RandomState);
            GrillTestKit.Tick(shift, 700);
            Assert.AreEqual(ShiftTestKit.Seed, shift.CaptureSnapshot().RandomState, "Клієнти не беруть випадковості в такті");
        }

        [Test]
        public void Shift_TakesJitterFromItsSeed_OnceAtCreation()
        {
            ShiftSimulation shift = CustomerTestKit.RunningWith(
                CustomerTestKit.CounterParams(JitteredArrivals()), seed: 2UL);

            Assert.AreEqual(8709371129873690710UL, shift.CaptureSnapshot().RandomState);
            Assert.AreEqual(42L, CustomerTestKit.CustomerOf(shift, 0).ArrivalTick);
            Assert.AreEqual(30L, CustomerTestKit.CustomerOf(shift, 1).ArrivalTick);
            Assert.AreEqual(86L, CustomerTestKit.CustomerOf(shift, 2).ArrivalTick);
            Assert.AreEqual(72L, CustomerTestKit.CustomerOf(shift, 3).ArrivalTick);
            Assert.AreEqual(241L, CustomerTestKit.CustomerOf(shift, 4).ArrivalTick);

            GrillTestKit.Tick(shift, 700);
            Assert.AreEqual(8709371129873690710UL, shift.CaptureSnapshot().RandomState);
        }

        [Test]
        public void Shift_SeatsCustomersInOrderOfActualArrival()
        {
            // Seed 2: клієнт 1 приходить на такті 30, клієнт 0 — на 42, клієнт 3 — на 72, клієнт 2 — на 86.
            ShiftSimulation shift = CustomerTestKit.RunningWith(
                CustomerTestKit.CounterParams(JitteredArrivals()), seed: 2UL);

            CustomerTestKit.TickTo(shift, 29L);
            Assert.AreEqual(0, shift.Counter.ArrivedCount);

            CustomerTestKit.TickTo(shift, 30L);
            Assert.AreEqual(1, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(CustomerState.None, CustomerTestKit.StateOf(shift, 0));

            CustomerTestKit.TickTo(shift, 42L);
            Assert.AreEqual(0, shift.Counter.Slots.GetCustomer(1));

            CustomerTestKit.TickTo(shift, 71L);
            Assert.AreEqual(0, shift.Counter.QueueLength);
            CustomerTestKit.TickTo(shift, 86L);
            Assert.AreEqual(2, shift.Counter.QueueLength);
            Assert.AreEqual(3, shift.Counter.GetQueued(0).Id);
            Assert.AreEqual(2, shift.Counter.GetQueued(1).Id);

            // Клієнт 1 (у слоті з такту 30) іде на такті 630 — його слот займає клієнт 3.
            CustomerTestKit.TickTo(shift, 30L + CustomerTestKit.OlyaPatienceTicks);
            Assert.AreEqual(CustomerState.Left, CustomerTestKit.StateOf(shift, 1));
            Assert.AreEqual(3, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(2, shift.Counter.GetQueued(0).Id);
        }

        [Test]
        public void Shift_DifferentSeeds_GiveDifferentArrivals_SameSeedTheSame()
        {
            CounterConfig counter = CustomerTestKit.CounterParams(JitteredArrivals());
            ShiftSimulation first = CustomerTestKit.RunningWith(counter, seed: 1UL);
            ShiftSimulation same = CustomerTestKit.RunningWith(counter, seed: 1UL);
            ShiftSimulation other = CustomerTestKit.RunningWith(counter, seed: 2UL);

            for (int i = 0; i < 300; i++)
            {
                first.Tick();
                same.Tick();
                other.Tick();
                Assert.AreEqual(first.CaptureSnapshot(), same.CaptureSnapshot(), "Такт " + (i + 1));
            }

            Assert.AreNotEqual(first.CaptureSnapshot().Counter, other.CaptureSnapshot().Counter);
        }
    }
}
