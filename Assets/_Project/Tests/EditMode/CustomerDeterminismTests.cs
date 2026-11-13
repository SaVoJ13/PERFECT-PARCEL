using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Детермінізм зміни з клієнтами: однаковий seed + однакова послідовність команд, тактів
    /// і кадрів → однакові знімки на кожному кроці. Плюс точний знімок після невеликого
    /// сценарію, порахований вручну, і властивості самих знімків.
    /// </summary>
    public sealed class CustomerDeterminismTests
    {
        private const int Steps = 3000;

        // 40 клієнтів, приходять приблизно кожні 0,7 с із випадковим запізненням до 0,6 с;
        // кожен третій — Марко (просить гострий соус), решта — Оля з простим кебабом.
        private static ShiftSimulation CreateShift(ulong seed)
        {
            var arrivals = new List<Arrival>();
            for (int i = 0; i < 40; i++)
            {
                arrivals.Add(new Arrival(
                    i * 700, i % 3 == 1 ? "marko" : "olya", CustomerTestKit.Plain(), 600, i % 5 == 0 ? 300 : 0));
            }

            CounterConfig counter = CustomerTestKit.CounterParams(arrivals, slots: 3, comboWindowMs: 2000);
            var grill = new GrillConfig(3, 100, 300, 0, 4, 0);
            var config = new ShiftConfig(
                ShiftTestKit.TicksPerSecond, 20, 30, 2, 3, grill, BoardTestKit.BoardParams(), counter);
            return new ShiftSimulation(config, seed, new RandomGoal());
        }

        private static bool HasBurntMeat(ShiftSimulation shift)
        {
            for (int i = 0; i < shift.Grill.SlotCount; i++)
            {
                if (shift.Grill.GetSlot(i).State == MeatState.Burnt)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// «Розумний» крок: стежить за грилем, збирає простий кебаб і видає його (кому — випадково).
        /// Рішення залежать лише від стану зміни і генератора сценарію, тож сценарій відтворюваний.
        /// </summary>
        private static void SensibleStep(ShiftSimulation shift, IRandom script)
        {
            if (shift.State != ShiftState.Running)
            {
                shift.Start();
                shift.Resume();
                shift.Continue();
                return;
            }

            if (HasBurntMeat(shift))
            {
                shift.DiscardBurntMeat();
            }

            if (shift.Grill.FreeSlotCount > 0)
            {
                shift.PlaceMeat();
            }

            Dish dish;
            switch (shift.Board.Check(out dish))
            {
                case DishCheck.Empty:
                    shift.AddIngredient(BoardTestKit.Pita);
                    break;
                case DishCheck.Incomplete:
                    shift.TakeMeat();
                    break;
                case DishCheck.Correct:
                    if (dish.OptionalMask != 0)
                    {
                        shift.RemoveIngredient();
                    }
                    else
                    {
                        shift.ServeDish(script.NextInt(-1, 3));
                    }

                    break;
                default:
                    shift.ClearBoard();
                    break;
            }

            shift.Tick();
        }

        private static void NoiseStep(ShiftSimulation shift, IRandom script)
        {
            switch (script.NextInt(0, 14))
            {
                case 0:
                    shift.Pause();
                    break;
                case 1:
                    shift.Resume();
                    break;
                case 2:
                    shift.PlaceMeat(script.NextInt(-1, 3));
                    break;
                case 3:
                    shift.TakeMeat(script.NextInt(-1, 4));
                    break;
                case 4:
                    shift.DiscardBurntMeat(script.NextInt(-1, 3));
                    break;
                case 5:
                    shift.CancelQueuedAction(script.NextInt(0, 3));
                    break;
                case 6:
                case 7:
                    shift.AddIngredient(script.NextInt(-1, 8));
                    break;
                case 8:
                    shift.RemoveIngredient(script.NextInt(-2, 7));
                    break;
                case 9:
                    shift.ClearBoard();
                    break;
                case 10:
                    // Слотів прилавка три: номери 3 і 4 — таких немає, -2 — команду буде відхилено.
                    shift.ServeDish(script.NextInt(-2, 5));
                    break;
                case 11:
                    shift.Random.NextUInt64();
                    break;
                case 12:
                    shift.Tick();
                    break;
                default:
                    shift.Advance(script.NextInt(0, 4000) / 100000d);
                    break;
            }
        }

        private static void ApplyStep(ShiftSimulation shift, IRandom script)
        {
            if (script.NextInt(0, 10) < 6)
            {
                SensibleStep(shift, script);
            }
            else
            {
                NoiseStep(shift, script);
            }
        }

        private static List<ShiftSnapshot> RunScript(ulong shiftSeed, ulong scriptSeed)
        {
            ShiftSimulation shift = CreateShift(shiftSeed);
            var script = new SplitMix64Random(scriptSeed);
            var snapshots = new List<ShiftSnapshot>(Steps);
            for (int i = 0; i < Steps; i++)
            {
                ApplyStep(shift, script);
                snapshots.Add(shift.CaptureSnapshot());
            }

            return snapshots;
        }

        [TestCase(5UL, 9UL)]
        [TestCase(6UL, 9UL)]
        [TestCase(2026UL, 7UL)]
        [TestCase(ulong.MaxValue, 0UL)]
        public void SameSeedAndSameScript_GiveEqualSnapshotsAtEveryStep(ulong shiftSeed, ulong scriptSeed)
        {
            List<ShiftSnapshot> first = RunScript(shiftSeed, scriptSeed);
            List<ShiftSnapshot> second = RunScript(shiftSeed, scriptSeed);

            Assert.AreEqual(Steps, first.Count);
            for (int i = 0; i < Steps; i++)
            {
                Assert.AreEqual(first[i], second[i], "Розбіжність на кроці " + i);
            }
        }

        [Test]
        public void TwoShiftsSteppedSideBySide_StayEqual_AndDoNotShareState()
        {
            ShiftSimulation first = CreateShift(5UL);
            ShiftSimulation second = CreateShift(5UL);
            var firstScript = new SplitMix64Random(9UL);
            var secondScript = new SplitMix64Random(9UL);

            for (int i = 0; i < Steps; i++)
            {
                ApplyStep(first, firstScript);
                ApplyStep(second, secondScript);
                Assert.AreEqual(first.CaptureSnapshot(), second.CaptureSnapshot(), "Розбіжність на кроці " + i);
            }
        }

        [Test]
        public void DifferentShiftSeed_GivesDifferentCustomers()
        {
            // Seed зміни визначає запізнення приходів, тож клієнти приходять інакше.
            Assert.AreNotEqual(
                CreateShift(5UL).CaptureSnapshot().Counter, CreateShift(6UL).CaptureSnapshot().Counter);
            Assert.AreEqual(
                CreateShift(5UL).CaptureSnapshot().Counter, CreateShift(5UL).CaptureSnapshot().Counter);
        }

        [TestCase(5UL, 9UL)]
        [TestCase(2026UL, 7UL)]
        public void Script_ReallyServesLosesAndAnnoysCustomers(ulong shiftSeed, ulong scriptSeed)
        {
            // Захист самого тесту: сценарій справді проходить через видачу, хибну страву і втрату клієнта.
            List<ShiftSnapshot> snapshots = RunScript(shiftSeed, scriptSeed);
            CounterSnapshot last = snapshots[Steps - 1].Counter;

            Assert.Greater(last.ArrivedCount, 3);
            Assert.Greater(last.ServedCount, 0L);
            Assert.Greater(last.LeftCount, 0L);
            Assert.Greater(last.WrongDishes, 0L);
            Assert.Greater(last.IncomeCoins, 0L);
            Assert.AreNotEqual(ShiftState.Intro, snapshots[Steps - 1].State);
        }

        [Test]
        public void CustomersDoNotUseShiftRandom_DuringTheShift()
        {
            ShiftSimulation shift = CreateShift(5UL);
            ulong afterCreation = shift.CaptureSnapshot().RandomState;
            shift.Start();

            Assert.AreNotEqual(5UL, afterCreation, "Запізнення приходів узято з генератора зміни");
            for (int i = 0; i < 1500; i++)
            {
                SensibleStep(shift, new SplitMix64Random((ulong)i));
            }

            Assert.AreEqual(afterCreation, shift.CaptureSnapshot().RandomState);
        }

        [Test]
        public void SmallScenario_GivesExactSnapshot()
        {
            // Очікувані значення пораховано вручну за правилами, а не знято з коду.
            // Слот 0 — Марко (120 тактів, просить гострий соус), слот 1 — Оля; третій клієнт приходить на такті 30.
            ShiftSimulation shift = CustomerTestKit.Running(
                CustomerTestKit.At(0, "marko"), CustomerTestKit.At(0), CustomerTestKit.At(500));

            // Такт 1: клієнти стають у слоти; м'ясо на гриль і піта на дошку.
            Assert.IsTrue(shift.PlaceMeat(0));
            Assert.IsTrue(shift.AddIngredient(BoardTestKit.Pita));
            GrillTestKit.Tick(shift, 1);

            // Такти 2–7: м'ясо готується. Такт 8: м'ясо на дошку.
            GrillTestKit.Tick(shift, BoardTestKit.CookTicks);
            Assert.IsTrue(shift.TakeMeat());
            GrillTestKit.Tick(shift, 1);

            // Такт 9: простий кебаб Маркові — хибна страва: 112 − 30 = 82 такти терпіння.
            Assert.IsTrue(shift.ServeDish(0));
            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(ShiftActionResult.DishNotAccepted, shift.LastActionResult);

            // Такт 10: той самий кебаб Олі — 20 монет, комбо 1.
            Assert.IsTrue(shift.ServeDish(1));
            GrillTestKit.Tick(shift, 1);

            // До такту 40: на такті 30 третій клієнт займає слот 1.
            CustomerTestKit.TickTo(shift, 40L);

            var customers = new[]
            {
                new CustomerSnapshot(CustomerState.Waiting, 0, 0L, 51L, 0L, 1L, 0L),
                new CustomerSnapshot(CustomerState.Served, CounterSlots.None, 0L, 591L, 0L, 0L, 20L),
                new CustomerSnapshot(CustomerState.Waiting, 1, 30L, 590L, 0L, 0L, 0L),
            };
            var counter = new CounterSnapshot(
                customers, 3, 3, 1L, 0L, 0L, 1L, 1L, 1L, 10L, 20L, 0L, 0L, DeliveryResult.Served, 20L);
            var grill = new GrillSnapshot(
                new[] { MeatState.Empty, MeatState.Empty }, new[] { 0L, 0L }, 1L, 1L, 0L, 0L, 0L);
            var actions = new ActionQueueSnapshot(
                null, 0L, 4L, 1L, new ShiftAction(ShiftActionType.ServeDish, 1), ShiftActionResult.Done);
            var board = new BoardSnapshot(null, 2L, 0L, 0L, 1L, 0L, new Dish(0, 0, BoardTestKit.KebabPrice));

            Assert.AreEqual(
                new ShiftSnapshot(
                    ShiftState.Running,
                    40L,
                    CustomerTestKit.DurationSec * 60L - 40L,
                    0L,
                    false,
                    ShiftTestKit.Seed,
                    grill,
                    actions,
                    board,
                    counter),
                shift.CaptureSnapshot());
            Assert.AreEqual(20L, shift.CoinsEarned);
        }

        [Test]
        public void CustomerSnapshot_EqualityCoversEveryField()
        {
            var snapshot = new CustomerSnapshot(CustomerState.Waiting, 1, 2L, 3L, 4L, 5L, 6L);
            var same = new CustomerSnapshot(CustomerState.Waiting, 1, 2L, 3L, 4L, 5L, 6L);

            Assert.IsTrue(snapshot.Equals(same));
            Assert.IsTrue(snapshot.Equals((object)same));
            Assert.AreEqual(snapshot.GetHashCode(), same.GetHashCode());
            Assert.IsFalse(snapshot.Equals(new CustomerSnapshot(CustomerState.Left, 1, 2L, 3L, 4L, 5L, 6L)));
            Assert.IsFalse(snapshot.Equals(new CustomerSnapshot(CustomerState.Waiting, 9, 2L, 3L, 4L, 5L, 6L)));
            Assert.IsFalse(snapshot.Equals(new CustomerSnapshot(CustomerState.Waiting, 1, 9L, 3L, 4L, 5L, 6L)));
            Assert.IsFalse(snapshot.Equals(new CustomerSnapshot(CustomerState.Waiting, 1, 2L, 9L, 4L, 5L, 6L)));
            Assert.IsFalse(snapshot.Equals(new CustomerSnapshot(CustomerState.Waiting, 1, 2L, 3L, 9L, 5L, 6L)));
            Assert.IsFalse(snapshot.Equals(new CustomerSnapshot(CustomerState.Waiting, 1, 2L, 3L, 4L, 9L, 6L)));
            Assert.IsFalse(snapshot.Equals(new CustomerSnapshot(CustomerState.Waiting, 1, 2L, 3L, 4L, 5L, 9L)));
            Assert.IsFalse(snapshot.Equals("не знімок"));
            Assert.AreEqual("Waiting:1@2:3+4:5:6", snapshot.ToString());
        }

        private static CounterSnapshot Sample(
            CustomerSnapshot[] customers,
            int arrived = 1,
            int seated = 2,
            long served = 3L,
            long left = 4L,
            long lost = 5L,
            long wrong = 6L,
            long combo = 7L,
            long maxCombo = 8L,
            long lastServe = 9L,
            long income = 10L,
            long tips = 11L,
            long comboCoins = 12L,
            DeliveryResult lastDelivery = DeliveryResult.WrongDish,
            long lastPaid = 13L)
        {
            return new CounterSnapshot(
                customers, arrived, seated, served, left, lost, wrong, combo, maxCombo, lastServe, income, tips,
                comboCoins, lastDelivery, lastPaid);
        }

        [Test]
        public void CounterSnapshot_EqualityCoversEveryField()
        {
            var one = new CustomerSnapshot(CustomerState.Waiting, 0, 0L, 50L, 0L, 0L, 0L);
            var two = new CustomerSnapshot(CustomerState.Arriving, -1, 5L, 60L, 0L, 0L, 0L);
            CustomerSnapshot[] customers = { one, two };
            CounterSnapshot snapshot = Sample(customers);

            Assert.IsTrue(snapshot.Equals(Sample(new[] { one, two })));
            Assert.IsTrue(snapshot.Equals((object)Sample(customers)));
            Assert.AreEqual(snapshot.GetHashCode(), Sample(new[] { one, two }).GetHashCode());

            Assert.IsFalse(snapshot.Equals(Sample(new[] { two, one })));
            Assert.IsFalse(snapshot.Equals(Sample(new[] { one })));
            Assert.IsFalse(snapshot.Equals(Sample(null)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, arrived: 99)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, seated: 99)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, served: 99L)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, left: 99L)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, lost: 99L)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, wrong: 99L)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, combo: 99L)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, maxCombo: 99L)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, lastServe: 99L)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, income: 99L)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, tips: 99L)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, comboCoins: 99L)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, lastDelivery: DeliveryResult.Served)));
            Assert.IsFalse(snapshot.Equals(Sample(customers, lastPaid: 99L)));
            Assert.IsFalse(snapshot.Equals(default(CounterSnapshot)));
            Assert.IsFalse(snapshot.Equals("не знімок"));
        }

        [Test]
        public void CounterSnapshot_CopiesItsArray_AndChecksNumbers()
        {
            var one = new CustomerSnapshot(CustomerState.Waiting, 0, 0L, 50L, 0L, 0L, 0L);
            CustomerSnapshot[] customers = { one };
            CounterSnapshot snapshot = Sample(customers);

            customers[0] = new CustomerSnapshot(CustomerState.Left, -1, 0L, 0L, 0L, 0L, 0L);

            Assert.AreEqual(1, snapshot.CustomerCount);
            Assert.AreEqual(one, snapshot.GetCustomer(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => snapshot.GetCustomer(1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => snapshot.GetCustomer(-1));
            Assert.AreEqual(0, default(CounterSnapshot).CustomerCount);
            Assert.AreEqual(
                default(CounterSnapshot),
                new CounterSnapshot(
                    new CustomerSnapshot[0], 0, 0, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0L, DeliveryResult.None, 0L));
        }

        [Test]
        public void CounterSnapshot_ToString_IsReadable_AndCultureIndependent()
        {
            var customers = new[]
            {
                new CustomerSnapshot(CustomerState.Waiting, 0, 0L, 51L, 0L, 1L, 0L),
                new CustomerSnapshot(CustomerState.Served, CounterSlots.None, 0L, 591L, 0L, 0L, 20L),
            };
            var snapshot = new CounterSnapshot(
                customers, 2, 2, 1L, 0L, 0L, 1L, 1L, 1L, 1234L, 20L, 0L, 0L, DeliveryResult.Served, 20L);

            Assert.AreEqual(
                "[Waiting:0@0:51+0:1:0 Served:-1@0:591+0:0:20] arrived=2 seated=2 served=1 left=0 lost=0 wrong=1"
                + " combo=1 maxCombo=1 lastServe=1234 income=20 tips=0 comboCoins=0 last=Served:20",
                snapshot.ToString());
        }

        [Test]
        public void ShiftSnapshot_EqualityAndToString_CoverCounter()
        {
            var one = new CustomerSnapshot(CustomerState.Waiting, 0, 0L, 50L, 0L, 0L, 0L);
            CounterSnapshot counter = Sample(new[] { one });
            CounterSnapshot otherCounter = Sample(new[] { one }, combo: 99L);
            var board = new BoardSnapshot(new[] { 0 }, 1L, 0L, 0L, 0L, 0L, default(Dish));
            var snapshot = new ShiftSnapshot(
                ShiftState.Running, 10L, 20L, 30L, false, 40UL, default(GrillSnapshot), default(ActionQueueSnapshot),
                board, counter);
            var same = new ShiftSnapshot(
                ShiftState.Running, 10L, 20L, 30L, false, 40UL, default(GrillSnapshot), default(ActionQueueSnapshot),
                board, counter);
            var withoutCounter = new ShiftSnapshot(
                ShiftState.Running, 10L, 20L, 30L, false, 40UL, default(GrillSnapshot), default(ActionQueueSnapshot),
                board);

            Assert.IsTrue(snapshot.Equals(same));
            Assert.AreEqual(snapshot.GetHashCode(), same.GetHashCode());
            Assert.AreEqual(counter, snapshot.Counter);
            Assert.IsFalse(snapshot.Equals(new ShiftSnapshot(
                ShiftState.Running, 10L, 20L, 30L, false, 40UL, default(GrillSnapshot), default(ActionQueueSnapshot),
                board, otherCounter)));
            Assert.IsFalse(snapshot.Equals(withoutCounter));
            Assert.AreEqual(default(CounterSnapshot), withoutCounter.Counter);

            StringAssert.EndsWith(" board=" + board + " counter=" + counter, snapshot.ToString());
            StringAssert.DoesNotContain("counter=", withoutCounter.ToString());
            StringAssert.EndsWith(" board=" + board, withoutCounter.ToString());
        }
    }
}
