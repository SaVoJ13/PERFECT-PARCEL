using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Детермінізм зміни з грилем і дошкою: однаковий seed + однакова послідовність команд,
    /// тактів і кадрів → однакові знімки на кожному кроці. Плюс точний знімок після
    /// невеликого сценарію, порахований вручну, і властивості самих знімків.
    /// </summary>
    public sealed class BoardDeterminismTests
    {
        private const int Steps = 3000;

        /// <summary>Отримувач без власного стану: забирає страву лише для парних номерів клієнтів.</summary>
        private sealed class EvenTargetReceiver : IDishReceiver
        {
            public bool TryReceive(Dish dish, int target)
            {
                return target >= 0 && target % 2 == 0;
            }
        }

        // За 60 тактів на секунду: хліб 3 такти, соус 2, салат 0, дошка 1, смітник 6, гриль 3.
        private static ShiftSimulation CreateShift(ulong seed)
        {
            var grill = new GrillConfig(3, 500, 700, 200, 4, 50);
            BoardConfig board = BoardTestKit.BoardParams(
                stations: BoardTestKit.Stations(breadMs: 50, sauceMs: 33, saladMs: 0, boardMs: 17, binMs: 100));
            var config = new ShiftConfig(ShiftTestKit.TicksPerSecond, 20, 5, 2, 3, grill, board);
            return new ShiftSimulation(config, seed, new RandomGoal(), new EvenTargetReceiver());
        }

        private static void ApplyStep(ShiftSimulation shift, IRandom script)
        {
            switch (script.NextInt(0, 24))
            {
                case 0:
                    shift.Start();
                    break;
                case 1:
                    shift.Pause();
                    break;
                case 2:
                    shift.Resume();
                    break;
                case 3:
                    shift.Continue();
                    break;
                case 4:
                case 5:
                    shift.PlaceMeat(script.NextInt(-1, 3));
                    break;
                case 6:
                case 7:
                    shift.TakeMeat(script.NextInt(-1, 4));
                    break;
                case 8:
                    shift.DiscardBurntMeat(script.NextInt(-1, 3));
                    break;
                case 9:
                    shift.CancelQueuedAction(script.NextInt(0, 3));
                    break;
                case 10:
                case 11:
                case 12:
                    // Номери -1 і 7 — такого інгредієнта немає: команда має бути відхилена.
                    shift.AddIngredient(script.NextInt(-1, 8));
                    break;
                case 13:
                    shift.RemoveIngredient(script.NextInt(-2, 7));
                    break;
                case 14:
                    shift.ClearBoard();
                    break;
                case 15:
                case 16:
                    shift.ServeDish(script.NextInt(-2, 4));
                    break;
                case 17:
                    shift.Random.NextUInt64();
                    break;
                case 18:
                    shift.Tick();
                    break;
                default:
                    shift.Advance(script.NextInt(0, 4000) / 100000d);
                    break;
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
        public void Script_ReallyUsesTheBoard()
        {
            // Захист самого тесту: сценарій справді кладе інгредієнти на дошку.
            List<ShiftSnapshot> snapshots = RunScript(5UL, 9UL);
            ShiftSnapshot last = snapshots[Steps - 1];

            Assert.Greater(last.Board.AddedCount, 0L);
            Assert.Greater(last.Actions.ExecutedCount, 0L);
            Assert.AreNotEqual(ShiftState.Intro, last.State);
        }

        [Test]
        public void BoardDoesNotUseShiftRandom()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            ulong before = shift.CaptureSnapshot().RandomState;

            BoardTestKit.Build(shift, BoardTestKit.Pita, BoardTestKit.Meat, BoardTestKit.Salad, BoardTestKit.Salad);
            BoardTestKit.Run(shift, shift.ServeDish());
            BoardTestKit.Run(shift, shift.RemoveIngredient());
            BoardTestKit.Run(shift, shift.ServeDish());
            BoardTestKit.Run(shift, shift.AddIngredient(BoardTestKit.Hot));
            BoardTestKit.Run(shift, shift.ClearBoard());

            Assert.AreEqual(before, shift.CaptureSnapshot().RandomState);
        }

        [Test]
        public void SmallScenario_GivesExactSnapshot()
        {
            // Очікувані значення пораховано вручну за правилами, а не знято з коду.
            ShiftSimulation shift = BoardTestKit.CreateRunning();

            // Такт 1: м'ясо на гриль і піта на дошку.
            Assert.IsTrue(shift.PlaceMeat(0));
            Assert.IsTrue(shift.AddIngredient(BoardTestKit.Pita));
            GrillTestKit.Tick(shift, 1);

            // Такти 2–7: м'ясо готується.
            GrillTestKit.Tick(shift, BoardTestKit.CookTicks);

            // Такт 8: м'ясо на дошку і салат двічі (другий — зайвий).
            Assert.IsTrue(shift.TakeMeat());
            Assert.IsTrue(shift.AddIngredient(BoardTestKit.Salad));
            Assert.IsTrue(shift.AddIngredient(BoardTestKit.Salad));
            GrillTestKit.Tick(shift, 1);

            // Такт 9: видати не можна — зайвий інгредієнт.
            Assert.IsTrue(shift.ServeDish(1));
            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(ShiftActionResult.DishExtraIngredient, shift.LastActionResult);

            // Такт 10: зняти зайвий салат і видати.
            Assert.IsTrue(shift.RemoveIngredient());
            Assert.IsTrue(shift.ServeDish(1));
            GrillTestKit.Tick(shift, 1);

            // Такт 11: гострий соус на порожню дошку і все у смітник.
            Assert.IsTrue(shift.AddIngredient(BoardTestKit.Hot));
            Assert.IsTrue(shift.ClearBoard());
            GrillTestKit.Tick(shift, 1);

            var grill = new GrillSnapshot(
                new[] { MeatState.Empty, MeatState.Empty }, new[] { 0L, 0L }, 1L, 1L, 0L, 0L, 0L);
            var actions = new ActionQueueSnapshot(
                null, 0L, 9L, 1L, new ShiftAction(ShiftActionType.ClearBoard, 0), ShiftActionResult.Done);
            var board = new BoardSnapshot(
                null,
                5L,
                1L,
                1L,
                1L,
                BoardTestKit.ClearPenalty,
                new Dish(0, 1, BoardTestKit.KebabPrice + BoardTestKit.SaladExtra));

            Assert.AreEqual(
                new ShiftSnapshot(
                    ShiftState.Running,
                    11L,
                    GrillTestKit.DurationSec * 60L - 11L,
                    0L,
                    false,
                    ShiftTestKit.Seed,
                    grill,
                    actions,
                    board),
                shift.CaptureSnapshot());
        }

        [Test]
        public void SnapshotInTheMiddleOfAssembly_ShowsItemsInOrder()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            BoardTestKit.Build(shift, BoardTestKit.Pita, BoardTestKit.Hot, BoardTestKit.Garlic);

            BoardSnapshot snapshot = shift.CaptureSnapshot().Board;

            Assert.AreEqual(3, snapshot.ItemCount);
            Assert.AreEqual(BoardTestKit.Pita, snapshot.GetItem(0));
            Assert.AreEqual(BoardTestKit.Hot, snapshot.GetItem(1));
            Assert.AreEqual(BoardTestKit.Garlic, snapshot.GetItem(2));
            Assert.AreEqual(new BoardSnapshot(new[] { 0, 5, 4 }, 3L, 0L, 0L, 0L, 0L, default(Dish)), snapshot);
            Assert.AreEqual(shift.Board.CaptureSnapshot(), snapshot);
        }

        [Test]
        public void BoardSnapshot_EqualityCoversEveryField()
        {
            int[] items = { 0, 1, 4 };
            var dish = new Dish(0, 4, 24);
            var snapshot = new BoardSnapshot(items, 1L, 2L, 3L, 4L, 5L, dish);
            var same = new BoardSnapshot(new[] { 0, 1, 4 }, 1L, 2L, 3L, 4L, 5L, new Dish(0, 4, 24));

            Assert.IsTrue(snapshot.Equals(same));
            Assert.IsTrue(snapshot.Equals((object)same));
            Assert.AreEqual(snapshot.GetHashCode(), same.GetHashCode());

            Assert.IsFalse(snapshot.Equals(new BoardSnapshot(new[] { 0, 4, 1 }, 1L, 2L, 3L, 4L, 5L, dish)));
            Assert.IsFalse(snapshot.Equals(new BoardSnapshot(new[] { 0, 1 }, 1L, 2L, 3L, 4L, 5L, dish)));
            Assert.IsFalse(snapshot.Equals(new BoardSnapshot(null, 1L, 2L, 3L, 4L, 5L, dish)));
            Assert.IsFalse(snapshot.Equals(new BoardSnapshot(items, 9L, 2L, 3L, 4L, 5L, dish)));
            Assert.IsFalse(snapshot.Equals(new BoardSnapshot(items, 1L, 9L, 3L, 4L, 5L, dish)));
            Assert.IsFalse(snapshot.Equals(new BoardSnapshot(items, 1L, 2L, 9L, 4L, 5L, dish)));
            Assert.IsFalse(snapshot.Equals(new BoardSnapshot(items, 1L, 2L, 3L, 9L, 5L, dish)));
            Assert.IsFalse(snapshot.Equals(new BoardSnapshot(items, 1L, 2L, 3L, 4L, 9L, dish)));
            Assert.IsFalse(snapshot.Equals(new BoardSnapshot(items, 1L, 2L, 3L, 4L, 5L, new Dish(0, 4, 25))));
            Assert.IsFalse(snapshot.Equals(default(BoardSnapshot)));
            Assert.IsFalse(snapshot.Equals("не знімок"));
        }

        [Test]
        public void BoardSnapshot_CopiesItsArray_AndChecksPositions()
        {
            int[] items = { 2, 3 };
            var snapshot = new BoardSnapshot(items, 0L, 0L, 0L, 0L, 0L, default(Dish));

            items[0] = 6;

            Assert.AreEqual(2, snapshot.GetItem(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => snapshot.GetItem(2));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => snapshot.GetItem(-1));
            Assert.AreEqual(default(BoardSnapshot), new BoardSnapshot(new int[0], 0L, 0L, 0L, 0L, 0L, default(Dish)));
            Assert.AreEqual(0, default(BoardSnapshot).ItemCount);
        }

        [Test]
        public void Dish_EqualityCoversEveryField()
        {
            var dish = new Dish(1, 5, 30);

            Assert.IsTrue(dish.Equals(new Dish(1, 5, 30)));
            Assert.IsTrue(dish.Equals((object)new Dish(1, 5, 30)));
            Assert.AreEqual(dish.GetHashCode(), new Dish(1, 5, 30).GetHashCode());
            Assert.IsFalse(dish.Equals(new Dish(0, 5, 30)));
            Assert.IsFalse(dish.Equals(new Dish(1, 4, 30)));
            Assert.IsFalse(dish.Equals(new Dish(1, 5, 31)));
            Assert.IsFalse(dish.Equals("не страва"));
            Assert.AreEqual("1/5/30", dish.ToString());
        }

        [Test]
        public void ShiftSnapshot_EqualityCoversBoard()
        {
            var board = new BoardSnapshot(new[] { 0 }, 1L, 0L, 0L, 0L, 0L, default(Dish));
            var otherBoard = new BoardSnapshot(new[] { 6 }, 1L, 0L, 0L, 0L, 0L, default(Dish));
            var snapshot = new ShiftSnapshot(
                ShiftState.Running, 10L, 20L, 30L, false, 40UL, default(GrillSnapshot), default(ActionQueueSnapshot), board);
            var same = new ShiftSnapshot(
                ShiftState.Running, 10L, 20L, 30L, false, 40UL, default(GrillSnapshot), default(ActionQueueSnapshot), board);

            Assert.IsTrue(snapshot.Equals(same));
            Assert.AreEqual(snapshot.GetHashCode(), same.GetHashCode());
            Assert.AreEqual(board, snapshot.Board);
            Assert.IsFalse(snapshot.Equals(new ShiftSnapshot(
                ShiftState.Running, 10L, 20L, 30L, false, 40UL, default(GrillSnapshot), default(ActionQueueSnapshot), otherBoard)));
            Assert.IsFalse(snapshot.Equals(new ShiftSnapshot(ShiftState.Running, 10L, 20L, 30L, false, 40UL)));
            Assert.AreEqual(
                default(BoardSnapshot),
                new ShiftSnapshot(
                    ShiftState.Running, 10L, 20L, 30L, false, 40UL, default(GrillSnapshot), default(ActionQueueSnapshot)).Board);
        }

        [Test]
        public void ShiftSnapshot_ToString_ShowsBoard_AndIsCultureIndependent()
        {
            var grill = new GrillSnapshot(new[] { MeatState.Raw }, new[] { 12L }, 1L, 0L, 0L, 0L, 0L);
            var actions = new ActionQueueSnapshot(
                new[] { new ShiftAction(ShiftActionType.ServeDish, 2) },
                1L, 3L, 0L, new ShiftAction(ShiftActionType.AddIngredient, 4), ShiftActionResult.Done);
            var board = new BoardSnapshot(new[] { 0, 1, 4 }, 1234L, 1L, 2L, 3L, 6L, new Dish(0, 5, 29));
            var snapshot = new ShiftSnapshot(ShiftState.Paused, 1500L, 300L, 0L, false, 255UL, grill, actions, board);

            Assert.AreEqual(
                "Paused elapsed=1500 remaining=300 pending=0 continueUsed=False random=0x00000000000000FF"
                + " grill=[Raw:12] placed=1 taken=0 burnt=0 discarded=0 penalty=0"
                + " actions=[ServeDish:2] progress=1 executed=3 ignored=0 last=AddIngredient:4>Done"
                + " board=[0 1 4] added=1234 removed=1 cleared=2 served=3 penalty=6 last=0/5/29",
                snapshot.ToString());
        }
    }
}
