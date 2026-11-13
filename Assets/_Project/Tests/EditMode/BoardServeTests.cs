using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Видача страви з дошки (GAME_DESIGN.md, розд. 2 і 5): видається лише правильна повна
    /// страва; отримувач (клієнти — S1-03) може її не забрати.
    /// </summary>
    public sealed class BoardServeTests
    {
        private const int Pita = BoardTestKit.Pita;
        private const int Meat = BoardTestKit.Meat;
        private const int Salad = BoardTestKit.Salad;
        private const int Hot = BoardTestKit.Hot;

        private static ShiftSimulation Built(FakeDishReceiver receiver, params int[] ingredients)
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning(receiver: receiver);
            BoardTestKit.Build(shift, ingredients);
            return shift;
        }

        [Test]
        public void Serve_CorrectDish_GoesToReceiver_AndBoardBecomesEmpty()
        {
            var receiver = new FakeDishReceiver();
            ShiftSimulation shift = Built(receiver, Pita, Meat, Salad, Hot);
            var expected = new Dish(
                0, 9, BoardTestKit.KebabPrice + BoardTestKit.SaladExtra + BoardTestKit.HotExtra);

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ServeDish(2)));

            Assert.AreEqual(1, receiver.Calls);
            Assert.AreEqual(expected, receiver.LastDish);
            Assert.AreEqual(2, receiver.LastTarget);
            Assert.AreEqual(0, shift.Board.ItemCount);
            Assert.AreEqual(DishCheck.Empty, shift.Board.Check());
            Assert.AreEqual(1L, shift.Board.ServedCount);
            Assert.AreEqual(expected, shift.Board.LastServed);
            Assert.AreEqual(0L, shift.Board.PenaltyCoins);
            Assert.AreEqual(0L, shift.Board.ClearedCount);
            Assert.AreEqual(new ShiftAction(ShiftActionType.ServeDish, 2), shift.LastAction);
        }

        [Test]
        public void Serve_WithoutTarget_PassesAnyCustomer()
        {
            var receiver = new FakeDishReceiver();
            ShiftSimulation shift = Built(receiver, Pita, Meat);

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ServeDish()));

            Assert.AreEqual(BoardTarget.AnyCustomer, receiver.LastTarget);
            Assert.AreEqual(new Dish(0, 0, BoardTestKit.KebabPrice), receiver.LastDish);
        }

        [Test]
        public void Serve_WithoutReceiver_DishIsCountedAsServed()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            BoardTestKit.Build(shift, Pita, Meat);

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ServeDish()));

            Assert.AreEqual(0, shift.Board.ItemCount);
            Assert.AreEqual(1L, shift.Board.ServedCount);
            Assert.AreEqual(new Dish(0, 0, BoardTestKit.KebabPrice), shift.Board.LastServed);
        }

        [Test]
        public void Serve_FromEmptyBoard_IsIgnored_AndReceiverIsNotAsked()
        {
            var receiver = new FakeDishReceiver();
            ShiftSimulation shift = Built(receiver);

            Assert.AreEqual(ShiftActionResult.BoardEmpty, BoardTestKit.Run(shift, shift.ServeDish(0)));

            Assert.AreEqual(0, receiver.Calls);
            Assert.AreEqual(default(BoardSnapshot), shift.Board.CaptureSnapshot());
        }

        [Test]
        public void Serve_IncompleteDish_IsIgnored_AndStaysOnBoard()
        {
            var receiver = new FakeDishReceiver();
            ShiftSimulation shift = Built(receiver, Pita);
            BoardSnapshot before = shift.Board.CaptureSnapshot();

            Assert.AreEqual(ShiftActionResult.DishIncomplete, BoardTestKit.Run(shift, shift.ServeDish(0)));

            Assert.AreEqual(0, receiver.Calls);
            Assert.AreEqual(before, shift.Board.CaptureSnapshot());
            Assert.AreEqual(0L, shift.Board.ServedCount);
        }

        [Test]
        public void Serve_DishInWrongOrder_IsIgnored_AndStaysOnBoard()
        {
            var receiver = new FakeDishReceiver();
            ShiftSimulation shift = Built(receiver, Meat, Pita);
            BoardSnapshot before = shift.Board.CaptureSnapshot();

            Assert.AreEqual(ShiftActionResult.DishWrongOrder, BoardTestKit.Run(shift, shift.ServeDish(0)));

            Assert.AreEqual(0, receiver.Calls);
            Assert.AreEqual(before, shift.Board.CaptureSnapshot());
        }

        [Test]
        public void Serve_DishWithExtraIngredient_IsIgnored_AndStaysOnBoard()
        {
            var receiver = new FakeDishReceiver();
            ShiftSimulation shift = Built(receiver, Pita, Meat, Hot, Hot);
            BoardSnapshot before = shift.Board.CaptureSnapshot();

            Assert.AreEqual(ShiftActionResult.DishExtraIngredient, BoardTestKit.Run(shift, shift.ServeDish(0)));

            Assert.AreEqual(0, receiver.Calls);
            Assert.AreEqual(before, shift.Board.CaptureSnapshot());
        }

        [Test]
        public void Serve_WhenReceiverRefuses_DishStaysOnBoard_AndCanBeServedLater()
        {
            var receiver = new FakeDishReceiver { Accept = false };
            ShiftSimulation shift = Built(receiver, Pita, Meat);
            BoardSnapshot before = shift.Board.CaptureSnapshot();

            Assert.AreEqual(ShiftActionResult.DishNotAccepted, BoardTestKit.Run(shift, shift.ServeDish(1)));

            Assert.AreEqual(1, receiver.Calls);
            Assert.AreEqual(before, shift.Board.CaptureSnapshot());
            Assert.AreEqual(1L, shift.IgnoredActionCount);

            receiver.Accept = true;
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ServeDish(1)));
            Assert.AreEqual(1L, shift.Board.ServedCount);
            Assert.AreEqual(0, shift.Board.ItemCount);
        }

        [Test]
        public void Serve_Twice_SecondTimeBoardIsEmpty_AndDishIsCountedOnce()
        {
            var receiver = new FakeDishReceiver();
            ShiftSimulation shift = Built(receiver, Pita, Meat);

            Assert.IsTrue(shift.ServeDish(0));
            Assert.IsTrue(shift.ServeDish(0));
            GrillTestKit.Tick(shift, 1);

            Assert.AreEqual(ShiftActionResult.BoardEmpty, shift.LastActionResult);
            Assert.AreEqual(1, receiver.Calls);
            Assert.AreEqual(1L, shift.Board.ServedCount);
        }

        [TestCase(-2)]
        [TestCase(int.MinValue)]
        public void Serve_TargetBelowAnyCustomer_ReturnsFalse_AndChangesNothing(int target)
        {
            var receiver = new FakeDishReceiver();
            ShiftSimulation shift = Built(receiver, Pita, Meat);
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(shift.ServeDish(target));

            Assert.AreEqual(before, shift.CaptureSnapshot());
            Assert.AreEqual(0, receiver.Calls);
        }

        [Test]
        public void AfterServe_NextDishIsAssembledFromScratch()
        {
            var receiver = new FakeDishReceiver();
            ShiftSimulation shift = Built(receiver, Pita, Meat, Hot);
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ServeDish(0)));

            BoardTestKit.Build(shift, Pita, Meat);
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ServeDish(1)));

            Assert.AreEqual(2, receiver.Calls);
            Assert.AreEqual(2L, shift.Board.ServedCount);
            Assert.AreEqual(new Dish(0, 0, BoardTestKit.KebabPrice), shift.Board.LastServed);
            Assert.AreEqual(5L, shift.Board.AddedCount);
            Assert.AreEqual(2L, shift.Grill.TakenCount);
        }
    }
}
