using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Дошка разом із грилем і чергою дій: м'ясо потрапляє на дошку лише готовим,
    /// станції доступні за рівнем (параметр конфіга), кожна дія триває стільки,
    /// скільки задано для її станції.
    /// </summary>
    public sealed class BoardStationTests
    {
        private const int Pita = BoardTestKit.Pita;
        private const int Meat = BoardTestKit.Meat;
        private const int Salad = BoardTestKit.Salad;
        private const int Onion = BoardTestKit.Onion;
        private const int Garlic = BoardTestKit.Garlic;
        private const int Hot = BoardTestKit.Hot;
        private const long Cook = BoardTestKit.CookTicks;
        private const long Burn = BoardTestKit.BurnTicks;

        private static void Place(ShiftSimulation shift, int slot)
        {
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.PlaceMeat(slot)));
        }

        [Test]
        public void TakeMeat_Done_PutsMeatOnBoard_AndFreesGrillSlot()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            Place(shift, 0);
            GrillTestKit.Tick(shift, Cook);
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0), "Підготовка тесту");

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.TakeMeat(0)));

            CollectionAssert.AreEqual(new[] { Meat }, BoardTestKit.Items(shift));
            Assert.AreEqual(MeatState.Empty, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(1L, shift.Grill.TakenCount);
            Assert.AreEqual(1L, shift.Board.AddedCount);
        }

        [Test]
        public void TakeMeat_OnTheTickItBecomesDone_PutsItOnBoard()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            Place(shift, 0);
            GrillTestKit.Tick(shift, Cook - 1);
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0), "Підготовка тесту");

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.TakeMeat(0)));

            CollectionAssert.AreEqual(new[] { Meat }, BoardTestKit.Items(shift));
        }

        [Test]
        public void TakeMeat_Raw_IsIgnored_AndBoardStaysEmpty()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            Place(shift, 0);
            GrillTestKit.Tick(shift, Cook - 2);

            Assert.AreEqual(ShiftActionResult.MeatRaw, BoardTestKit.Run(shift, shift.TakeMeat(0)));

            Assert.AreEqual(0, shift.Board.ItemCount);
            Assert.AreEqual(default(BoardSnapshot), shift.Board.CaptureSnapshot());
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(0L, shift.Grill.TakenCount);
        }

        [Test]
        public void TakeMeat_Burnt_IsIgnored_AndBoardStaysEmpty()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            Place(shift, 0);
            GrillTestKit.Tick(shift, Cook + Burn - 1);
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 0), "Підготовка тесту");

            // На цьому такті м'ясо пригорає раніше, ніж виконується дія.
            Assert.AreEqual(ShiftActionResult.MeatBurnt, BoardTestKit.Run(shift, shift.TakeMeat(0)));
            Assert.AreEqual(ShiftActionResult.NoDoneMeat, BoardTestKit.Run(shift, shift.TakeMeat()));

            Assert.AreEqual(0, shift.Board.ItemCount);
            Assert.AreEqual(default(BoardSnapshot), shift.Board.CaptureSnapshot());
            Assert.AreEqual(MeatState.Burnt, GrillTestKit.StateOf(shift, 0));
            Assert.AreEqual(1L, shift.Grill.BurntCount);
        }

        [Test]
        public void TakeMeat_FromEmptyGrill_IsIgnored_AndBoardStaysEmpty()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();

            Assert.AreEqual(ShiftActionResult.SlotEmpty, BoardTestKit.Run(shift, shift.TakeMeat(1)));
            Assert.AreEqual(ShiftActionResult.NoDoneMeat, BoardTestKit.Run(shift, shift.TakeMeat()));

            Assert.AreEqual(default(BoardSnapshot), shift.Board.CaptureSnapshot());
        }

        [Test]
        public void TakeMeat_OntoFullBoard_IsIgnored_AndMeatStaysOnGrill()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            BoardTestKit.Build(shift, Pita, Pita, Pita, Pita, Pita, Pita);
            Place(shift, 1);
            GrillTestKit.Tick(shift, Cook);
            BoardSnapshot before = shift.Board.CaptureSnapshot();

            Assert.AreEqual(ShiftActionResult.BoardFull, BoardTestKit.Run(shift, shift.TakeMeat(1)));

            Assert.AreEqual(before, shift.Board.CaptureSnapshot());
            Assert.AreEqual(MeatState.Done, GrillTestKit.StateOf(shift, 1));
            Assert.AreEqual(0L, shift.Grill.TakenCount);

            // Звільнили місце — те саме м'ясо знімається.
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.RemoveIngredient()));
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.TakeMeat(1)));
            Assert.AreEqual(Meat, shift.Board.GetItem(BoardTestKit.Capacity - 1));
        }

        [Test]
        public void Meat_CannotBeAddedByStationCommand_OnlyFromGrill()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(shift.Board.CanAdd(Meat));
            Assert.IsFalse(shift.AddIngredient(Meat));

            Assert.AreEqual(before, shift.CaptureSnapshot());
        }

        [Test]
        public void WholeOrder_PitaWhileMeatCooks_ThenMeat_ThenServe()
        {
            var receiver = new FakeDishReceiver();
            ShiftSimulation shift = BoardTestKit.CreateRunning(receiver: receiver);

            Place(shift, 0);
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.AddIngredient(Pita)));
            GrillTestKit.Tick(shift, Cook - 1);
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.TakeMeat()));
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.AddIngredient(Garlic)));
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ServeDish(0)));

            Assert.AreEqual(new Dish(0, 4, BoardTestKit.KebabPrice + BoardTestKit.GarlicExtra), receiver.LastDish);
            Assert.AreEqual(0, shift.Board.ItemCount);
            Assert.AreEqual(0, shift.QueuedActionCount);
        }

        [Test]
        public void ShiftWithGrillOnly_HasNoBoard_AndRejectsBoardCommands()
        {
            ShiftSimulation shift = GrillTestKit.CreateRunning();
            GrillTestKit.PlaceAndAge(shift, 0, GrillTestKit.CookTicks);
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsNull(shift.Board);
            Assert.IsFalse(shift.AddIngredient(0));
            Assert.IsFalse(shift.RemoveIngredient());
            Assert.IsFalse(shift.ClearBoard());
            Assert.IsFalse(shift.ServeDish());
            Assert.AreEqual(before, shift.CaptureSnapshot());
            Assert.AreEqual(default(BoardSnapshot), before.Board);

            // Без дошки м'ясо знімається, як і раніше.
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.TakeMeat(0)));
            Assert.AreEqual(1L, shift.Grill.TakenCount);
        }

        [Test]
        public void ShiftWithBoardOnly_AcceptsBoardCommands_AndRejectsGrillCommands()
        {
            var board = new BoardConfig(
                1,
                0,
                new[] { new StationConfig(StationKind.Bread, 1, 0), new StationConfig(StationKind.Board, 1, 0) },
                new[] { new Ingredient("pita", StationKind.Bread, 0) },
                new[] { new Recipe("plain_pita", new[] { "pita" }, null, 5, 1) });
            var config = new ShiftConfig(60, 600, 10, 30, 2, null, board);
            var shift = new ShiftSimulation(config, ShiftTestKit.Seed, new FixedGoal(true));
            shift.Start();

            Assert.IsNull(shift.Grill);
            Assert.AreEqual(-1, shift.Board.MeatIngredient);
            Assert.IsFalse(shift.PlaceMeat());
            Assert.IsFalse(shift.TakeMeat());
            Assert.IsFalse(shift.DiscardBurntMeat());
            Assert.IsFalse(shift.ClearBoard(), "Смітника в конфігу немає");

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.AddIngredient(0)));
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ServeDish()));
            Assert.AreEqual(new Dish(0, 0, 5), shift.Board.LastServed);
        }

        // Рівень 1: лише хліб; рівень 3: ще соуси; рівень 4: ще салатний бар (BoardTestKit).
        [TestCase(1, Pita, true)]
        [TestCase(1, Garlic, false)]
        [TestCase(1, Hot, false)]
        [TestCase(1, Salad, false)]
        [TestCase(1, Onion, false)]
        [TestCase(2, Hot, false)]
        [TestCase(3, Pita, true)]
        [TestCase(3, Garlic, true)]
        [TestCase(3, Hot, true)]
        [TestCase(3, Salad, false)]
        [TestCase(3, Onion, false)]
        [TestCase(4, Salad, true)]
        [TestCase(4, Onion, true)]
        [TestCase(4, Hot, true)]
        public void Station_IsAvailableFromItsUnlockLevel_AndLockedBefore(int level, int ingredient, bool available)
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning(BoardTestKit.BoardParams(level: level));
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.AreEqual(available, shift.Board.CanAdd(ingredient));
            Assert.AreEqual(available, shift.AddIngredient(ingredient));

            if (available)
            {
                GrillTestKit.Tick(shift, 1);
                Assert.AreEqual(ShiftActionResult.Done, shift.LastActionResult);
                CollectionAssert.AreEqual(new[] { ingredient }, BoardTestKit.Items(shift));
            }
            else
            {
                Assert.AreEqual(before, shift.CaptureSnapshot(), "Недоступна станція нічого не змінює");
                GrillTestKit.Tick(shift, 1);
                Assert.AreEqual(0, shift.Board.ItemCount);
                Assert.AreEqual(ShiftActionResult.None, shift.LastActionResult);
            }
        }

        [Test]
        public void LockedStation_DoesNotStopTheRestOfTheKitchen()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning(BoardTestKit.BoardParams(level: 1));

            Assert.IsFalse(shift.AddIngredient(Hot));
            BoardTestKit.Build(shift, Pita, Meat);

            Assert.AreEqual(DishCheck.Correct, shift.Board.Check());
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ServeDish()));
        }

        [Test]
        public void Bin_LockedByLevel_RejectsClearBoard_AndIsAvailableFromItsLevel()
        {
            ShiftSimulation locked = BoardTestKit.CreateRunning(
                BoardTestKit.BoardParams(level: 1, stations: BoardTestKit.Stations(binLevel: 2)));
            BoardTestKit.Build(locked, Pita);
            ShiftSnapshot before = locked.CaptureSnapshot();

            Assert.IsFalse(locked.Board.IsBinAvailable);
            Assert.IsFalse(locked.ClearBoard());
            Assert.AreEqual(before, locked.CaptureSnapshot());

            ShiftSimulation unlocked = BoardTestKit.CreateRunning(
                BoardTestKit.BoardParams(level: 2, stations: BoardTestKit.Stations(binLevel: 2)));
            BoardTestKit.Build(unlocked, Pita);

            Assert.IsTrue(unlocked.Board.IsBinAvailable);
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(unlocked, unlocked.ClearBoard()));
        }

        [TestCase(-1)]
        [TestCase(BoardTestKit.IngredientCount)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void AddIngredient_ThatDoesNotExist_ReturnsFalse_AndChangesNothing(int ingredient)
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(shift.Board.CanAdd(ingredient));
            Assert.IsFalse(shift.AddIngredient(ingredient));

            Assert.AreEqual(before, shift.CaptureSnapshot());
        }

        [Test]
        public void BoardCommands_OutsideRunning_ReturnFalse_AndChangeNothing()
        {
            ShiftSimulation shift = BoardTestKit.Create();
            ShiftSnapshot intro = shift.CaptureSnapshot();
            Assert.IsFalse(shift.AddIngredient(Pita));
            Assert.IsFalse(shift.RemoveIngredient());
            Assert.IsFalse(shift.ClearBoard());
            Assert.IsFalse(shift.ServeDish());
            Assert.AreEqual(intro, shift.CaptureSnapshot());

            shift.Start();
            BoardTestKit.Build(shift, Pita);
            shift.Pause();
            ShiftSnapshot paused = shift.CaptureSnapshot();
            Assert.IsFalse(shift.AddIngredient(Pita));
            Assert.IsFalse(shift.RemoveIngredient());
            Assert.IsFalse(shift.ClearBoard());
            Assert.IsFalse(shift.ServeDish());
            Assert.AreEqual(paused, shift.CaptureSnapshot());
        }

        [Test]
        public void BoardCommands_ShareTheActionQueue_AndAreRejectedWhenItIsFull()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning(actionQueueMax: 3);

            Assert.IsTrue(shift.PlaceMeat());
            Assert.IsTrue(shift.AddIngredient(Pita));
            Assert.IsTrue(shift.RemoveIngredient());
            Assert.IsFalse(shift.AddIngredient(Pita));
            Assert.IsFalse(shift.RemoveIngredient());
            Assert.IsFalse(shift.ClearBoard());
            Assert.IsFalse(shift.ServeDish());
            Assert.IsFalse(shift.TakeMeat());

            Assert.AreEqual(3, shift.QueuedActionCount);
            Assert.AreEqual(new ShiftAction(ShiftActionType.PlaceMeat, GrillSlot.Any), shift.GetQueuedAction(0));
            Assert.AreEqual(new ShiftAction(ShiftActionType.AddIngredient, Pita), shift.GetQueuedAction(1));
            Assert.AreEqual(
                new ShiftAction(ShiftActionType.RemoveIngredient, BoardTarget.TopItem), shift.GetQueuedAction(2));

            // Дії миттєві: усі три виконуються по черзі на одному такті.
            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(0, shift.QueuedActionCount);
            Assert.AreEqual(3L, shift.ExecutedActionCount);
            Assert.AreEqual(0, shift.Board.ItemCount);
            Assert.AreEqual(MeatState.Raw, GrillTestKit.StateOf(shift, 0));
        }

        [Test]
        public void CancelQueuedAction_RemovesBoardCommandBeforeItRuns()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();

            Assert.IsTrue(shift.AddIngredient(Pita));
            Assert.IsTrue(shift.AddIngredient(Hot));
            Assert.IsTrue(shift.CancelQueuedAction(0));
            GrillTestKit.Tick(shift, 1);

            CollectionAssert.AreEqual(new[] { Hot }, BoardTestKit.Items(shift));
        }

        [Test]
        public void EachAction_TakesTheTimeOfItsStation()
        {
            // За 60 тактів на секунду: хліб 6 тактів, соус 12, салат 18, дошка 3, смітник 9.
            ShiftSimulation shift = BoardTestKit.CreateRunning(BoardTestKit.BoardParams(
                stations: BoardTestKit.Stations(breadMs: 100, sauceMs: 200, saladMs: 300, boardMs: 50, binMs: 150)));

            Assert.AreEqual(6L, shift.Board.AddActionTicks(Pita));
            Assert.AreEqual(12L, shift.Board.AddActionTicks(Hot));
            Assert.AreEqual(18L, shift.Board.AddActionTicks(Onion));
            Assert.AreEqual(3L, shift.Board.BoardActionTicks);
            Assert.AreEqual(9L, shift.Board.BinActionTicks);

            AssertTakes(shift, shift.AddIngredient(Pita), 6L);
            AssertTakes(shift, shift.AddIngredient(Hot), 12L);
            AssertTakes(shift, shift.AddIngredient(Onion), 18L);
            CollectionAssert.AreEqual(new[] { Pita, Hot, Onion }, BoardTestKit.Items(shift));

            AssertTakes(shift, shift.RemoveIngredient(), 3L);
            CollectionAssert.AreEqual(new[] { Pita, Hot }, BoardTestKit.Items(shift));

            AssertTakes(shift, shift.ServeDish(), 3L);
            Assert.AreEqual(ShiftActionResult.DishWrongOrder, shift.LastActionResult);

            AssertTakes(shift, shift.ClearBoard(), 9L);
            Assert.AreEqual(ShiftActionResult.Done, shift.LastActionResult);
            Assert.AreEqual(0, shift.Board.ItemCount);

            // Дії гриля тривають стільки, скільки задано грилю (тут 0): на найближчому такті.
            AssertTakes(shift, shift.PlaceMeat(), 1L);
        }

        [Test]
        public void QueuedActions_OfDifferentStations_RunOneAfterAnother()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning(BoardTestKit.BoardParams(
                stations: BoardTestKit.Stations(breadMs: 100, sauceMs: 200)));

            Assert.IsTrue(shift.AddIngredient(Hot));
            Assert.IsTrue(shift.AddIngredient(Pita));

            GrillTestKit.Tick(shift, 11);
            Assert.AreEqual(0, shift.Board.ItemCount);
            Assert.AreEqual(11L, shift.HeadActionProgressTicks);

            GrillTestKit.Tick(shift, 1);
            CollectionAssert.AreEqual(new[] { Hot }, BoardTestKit.Items(shift));
            Assert.AreEqual(0L, shift.HeadActionProgressTicks, "Наступна дія починає з нуля");

            GrillTestKit.Tick(shift, 5);
            Assert.AreEqual(1, shift.Board.ItemCount);

            GrillTestKit.Tick(shift, 1);
            CollectionAssert.AreEqual(new[] { Hot, Pita }, BoardTestKit.Items(shift));
        }

        [Test]
        public void Pause_FreezesBoardActionInProgress()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning(BoardTestKit.BoardParams(
                stations: BoardTestKit.Stations(breadMs: 100)));
            Assert.IsTrue(shift.AddIngredient(Pita));
            GrillTestKit.Tick(shift, 4);

            shift.Pause();
            ShiftSnapshot paused = shift.CaptureSnapshot();
            Assert.IsFalse(shift.Tick());
            Assert.AreEqual(0, shift.Advance(5d));
            Assert.AreEqual(paused, shift.CaptureSnapshot());

            shift.Resume();
            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(0, shift.Board.ItemCount);
            GrillTestKit.Tick(shift, 1);
            CollectionAssert.AreEqual(new[] { Pita }, BoardTestKit.Items(shift));
        }

        private static void AssertTakes(ShiftSimulation shift, bool accepted, long ticks)
        {
            Assert.IsTrue(accepted, "Команду не прийнято");
            long before = shift.ExecutedActionCount + shift.IgnoredActionCount;
            GrillTestKit.Tick(shift, ticks - 1);
            Assert.AreEqual(before, shift.ExecutedActionCount + shift.IgnoredActionCount, "Дія виконалась зарано");
            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(before + 1, shift.ExecutedActionCount + shift.IgnoredActionCount, "Дія не виконалась вчасно");
        }
    }
}
