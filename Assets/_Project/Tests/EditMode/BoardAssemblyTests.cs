using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Збирання страви на дошці (GAME_DESIGN.md, розд. 5): перевірка за рецептом,
    /// зняття хибного інгредієнта, очищення дошки зі штрафом.
    /// </summary>
    public sealed class BoardAssemblyTests
    {
        private const int Pita = BoardTestKit.Pita;
        private const int Meat = BoardTestKit.Meat;
        private const int Salad = BoardTestKit.Salad;
        private const int Onion = BoardTestKit.Onion;
        private const int Garlic = BoardTestKit.Garlic;
        private const int Hot = BoardTestKit.Hot;
        private const int Lavash = BoardTestKit.Lavash;

        private static ShiftSimulation Built(params int[] ingredients)
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            BoardTestKit.Build(shift, ingredients);
            return shift;
        }

        [Test]
        public void NewBoard_IsEmpty_AndHasCapacityOfLongestRecipe()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();

            Assert.AreEqual(0, shift.Board.ItemCount);
            Assert.AreEqual(BoardTestKit.Capacity, shift.Board.Capacity);
            Assert.IsFalse(shift.Board.IsFull);
            Assert.AreEqual(DishCheck.Empty, shift.Board.Check());
            Assert.AreEqual(Meat, shift.Board.MeatIngredient);
            Assert.AreEqual(default(BoardSnapshot), shift.Board.CaptureSnapshot());
        }

        // Очікувана ціна: ціна рецепта + надбавки доданих необов'язкових (BoardTestKit).
        [TestCase(new[] { Pita, Meat }, 0, 20)]
        [TestCase(new[] { Pita, Meat, Salad }, 1, 23)]
        [TestCase(new[] { Pita, Meat, Onion }, 2, 22)]
        [TestCase(new[] { Pita, Meat, Garlic }, 4, 24)]
        [TestCase(new[] { Pita, Meat, Hot }, 8, 25)]
        [TestCase(new[] { Pita, Meat, Salad, Hot }, 9, 28)]
        [TestCase(new[] { Pita, Meat, Garlic, Hot }, 12, 29)]
        [TestCase(new[] { Pita, Meat, Salad, Onion, Garlic, Hot }, 15, 34)]
        public void Check_CorrectDish_WithAndWithoutOptional(int[] ingredients, int optionalMask, int priceCoins)
        {
            ShiftSimulation shift = Built(ingredients);

            Dish dish;
            Assert.AreEqual(DishCheck.Correct, shift.Board.Check(out dish));
            Assert.AreEqual(new Dish(0, optionalMask, priceCoins), dish);
            Assert.AreEqual(DishCheck.Correct, shift.Board.Check());
        }

        [Test]
        public void Dish_TellsWhichOptionalIngredientsWereAdded()
        {
            ShiftSimulation shift = Built(Pita, Meat, Onion, Hot);

            Dish dish;
            shift.Board.Check(out dish);

            // Порядок optional у рецепті: салат, цибуля, часниковий соус, гострий соус.
            Assert.IsFalse(dish.HasOptional(0));
            Assert.IsTrue(dish.HasOptional(1));
            Assert.IsFalse(dish.HasOptional(2));
            Assert.IsTrue(dish.HasOptional(3));
            Assert.IsFalse(dish.HasOptional(4));
            Assert.IsFalse(dish.HasOptional(-1));
            Assert.AreEqual(
                BoardTestKit.KebabPrice + BoardTestKit.OnionExtra + BoardTestKit.HotExtra, dish.PriceCoins);
        }

        [Test]
        public void Check_OnlyFirstSteps_IsIncomplete()
        {
            ShiftSimulation shift = Built(Pita);

            Dish dish;
            Assert.AreEqual(DishCheck.Incomplete, shift.Board.Check(out dish));
            Assert.AreEqual(default(Dish), dish);
        }

        [TestCase(new[] { Meat })]
        [TestCase(new[] { Meat, Pita })]
        [TestCase(new[] { Salad })]
        [TestCase(new[] { Pita, Salad })]
        [TestCase(new[] { Pita, Salad, Meat })]
        [TestCase(new[] { Pita, Meat, Hot, Salad })]
        [TestCase(new[] { Pita, Meat, Garlic, Onion })]
        [TestCase(new[] { Pita, Meat, Hot, Garlic })]
        public void Check_IngredientsOfRecipeInWrongOrder_IsWrongOrder(int[] ingredients)
        {
            ShiftSimulation shift = Built(ingredients);

            Dish dish;
            Assert.AreEqual(DishCheck.WrongOrder, shift.Board.Check(out dish));
            Assert.AreEqual(default(Dish), dish);
        }

        [TestCase(new[] { Pita, Pita })]
        [TestCase(new[] { Pita, Meat, Meat })]
        [TestCase(new[] { Pita, Meat, Salad, Salad })]
        [TestCase(new[] { Pita, Meat, Hot, Hot })]
        [TestCase(new[] { Lavash })]
        [TestCase(new[] { Pita, Meat, Lavash })]
        [TestCase(new[] { Lavash, Pita, Meat })]
        public void Check_DuplicateOrForeignIngredient_IsExtraIngredient(int[] ingredients)
        {
            ShiftSimulation shift = Built(ingredients);

            Dish dish;
            Assert.AreEqual(DishCheck.ExtraIngredient, shift.Board.Check(out dish));
            Assert.AreEqual(default(Dish), dish);
        }

        [Test]
        public void Check_ExtraIngredientWinsOverWrongOrder()
        {
            ShiftSimulation shift = Built(Meat, Pita, Pita);

            Assert.AreEqual(DishCheck.ExtraIngredient, shift.Board.Check());
        }

        [Test]
        public void Recipe_LockedByLevel_IsNotRecognised_UntilItsLevel()
        {
            ShiftSimulation locked = Built(Lavash);
            Assert.AreEqual(DishCheck.ExtraIngredient, locked.Board.Check());

            ShiftSimulation unlocked = BoardTestKit.CreateRunning(
                BoardTestKit.BoardParams(level: BoardTestKit.LavashLevel));
            BoardTestKit.Build(unlocked, Lavash);
            Assert.AreEqual(DishCheck.Incomplete, unlocked.Board.Check());

            BoardTestKit.AddMeat(unlocked);
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(unlocked, unlocked.AddIngredient(Hot)));
            Dish dish;
            Assert.AreEqual(DishCheck.Correct, unlocked.Board.Check(out dish));
            Assert.AreEqual(new Dish(1, 1, BoardTestKit.LavashPrice + BoardTestKit.HotExtra), dish);
        }

        [Test]
        public void TwoRecipes_EachIsRecognisedByItsOwnIngredients()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning(
                BoardTestKit.BoardParams(level: BoardTestKit.LavashLevel));
            BoardTestKit.Build(shift, Pita, Meat, Salad);

            Dish dish;
            Assert.AreEqual(DishCheck.Correct, shift.Board.Check(out dish));
            Assert.AreEqual(0, dish.RecipeIndex);

            // Салату в рецепті з лавашем немає, піти теж: для обох рецептів це зайвий інгредієнт.
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.AddIngredient(Lavash)));
            Assert.AreEqual(DishCheck.ExtraIngredient, shift.Board.Check());
        }

        [Test]
        public void Add_AnyAvailableIngredient_IsAccepted_EvenWhenItIsWrong()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.AddIngredient(Hot)));
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.AddIngredient(Hot)));

            CollectionAssert.AreEqual(new[] { Hot, Hot }, BoardTestKit.Items(shift));
            Assert.AreEqual(2L, shift.Board.AddedCount);
            Assert.AreEqual(new ShiftAction(ShiftActionType.AddIngredient, Hot), shift.LastAction);
        }

        [Test]
        public void Add_OnFullBoard_IsIgnored()
        {
            ShiftSimulation shift = Built(Pita, Pita, Pita, Pita, Pita, Pita);
            Assert.IsTrue(shift.Board.IsFull, "Підготовка тесту");
            BoardSnapshot before = shift.Board.CaptureSnapshot();

            Assert.AreEqual(ShiftActionResult.BoardFull, BoardTestKit.Run(shift, shift.AddIngredient(Salad)));

            Assert.AreEqual(before, shift.Board.CaptureSnapshot());
            Assert.AreEqual(1L, shift.IgnoredActionCount);
        }

        [Test]
        public void Remove_WrongIngredient_FixesTheDish_WithoutPenalty()
        {
            ShiftSimulation shift = Built(Pita, Onion, Meat);
            Assert.AreEqual(DishCheck.WrongOrder, shift.Board.Check(), "Підготовка тесту");

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.RemoveIngredient(1)));

            CollectionAssert.AreEqual(new[] { Pita, Meat }, BoardTestKit.Items(shift));
            Assert.AreEqual(DishCheck.Correct, shift.Board.Check());
            Assert.AreEqual(1L, shift.Board.RemovedCount);
            Assert.AreEqual(0L, shift.Board.PenaltyCoins);
            Assert.AreEqual(0L, shift.Board.ClearedCount);
            Assert.AreEqual(0L, shift.Grill.PenaltyCoins);
        }

        [Test]
        public void Remove_WithoutPosition_TakesTheLastAddedIngredient()
        {
            ShiftSimulation shift = Built(Pita, Meat, Salad, Salad);
            Assert.AreEqual(DishCheck.ExtraIngredient, shift.Board.Check(), "Підготовка тесту");

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.RemoveIngredient()));

            CollectionAssert.AreEqual(new[] { Pita, Meat, Salad }, BoardTestKit.Items(shift));
            Assert.AreEqual(DishCheck.Correct, shift.Board.Check());
            Assert.AreEqual(new ShiftAction(ShiftActionType.RemoveIngredient, BoardTarget.TopItem), shift.LastAction);
        }

        [Test]
        public void Remove_FirstIngredient_ShiftsTheRest()
        {
            ShiftSimulation shift = Built(Pita, Meat, Hot);

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.RemoveIngredient(0)));

            CollectionAssert.AreEqual(new[] { Meat, Hot }, BoardTestKit.Items(shift));
            Assert.AreEqual(DishCheck.WrongOrder, shift.Board.Check());
        }

        [Test]
        public void Remove_UntilEmpty_ThenFromEmptyBoard_IsIgnored()
        {
            ShiftSimulation shift = Built(Pita, Salad);

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.RemoveIngredient()));
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.RemoveIngredient()));
            Assert.AreEqual(0, shift.Board.ItemCount);
            BoardSnapshot before = shift.Board.CaptureSnapshot();

            Assert.AreEqual(ShiftActionResult.BoardEmpty, BoardTestKit.Run(shift, shift.RemoveIngredient()));
            Assert.AreEqual(ShiftActionResult.BoardEmpty, BoardTestKit.Run(shift, shift.RemoveIngredient(0)));

            Assert.AreEqual(before, shift.Board.CaptureSnapshot());
            Assert.AreEqual(2L, shift.Board.RemovedCount);
        }

        [Test]
        public void Remove_FromPlaceWithNothingOnIt_IsIgnored()
        {
            ShiftSimulation shift = Built(Pita, Meat);
            BoardSnapshot before = shift.Board.CaptureSnapshot();

            Assert.AreEqual(ShiftActionResult.NoSuchItem, BoardTestKit.Run(shift, shift.RemoveIngredient(2)));
            Assert.AreEqual(
                ShiftActionResult.NoSuchItem,
                BoardTestKit.Run(shift, shift.RemoveIngredient(BoardTestKit.Capacity - 1)));

            Assert.AreEqual(before, shift.Board.CaptureSnapshot());
        }

        [TestCase(-2)]
        [TestCase(BoardTestKit.Capacity)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void Remove_PlaceThatDoesNotExist_ReturnsFalse_AndChangesNothing(int position)
        {
            ShiftSimulation shift = Built(Pita, Meat);
            ShiftSnapshot before = shift.CaptureSnapshot();

            Assert.IsFalse(shift.RemoveIngredient(position));

            Assert.AreEqual(before, shift.CaptureSnapshot());
        }

        [Test]
        public void Clear_ThrowsEverythingAway_AndChargesPenalty()
        {
            ShiftSimulation shift = Built(Pita, Meat, Salad);

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ClearBoard()));

            Assert.AreEqual(0, shift.Board.ItemCount);
            Assert.AreEqual(DishCheck.Empty, shift.Board.Check());
            Assert.AreEqual(1L, shift.Board.ClearedCount);
            Assert.AreEqual((long)BoardTestKit.ClearPenalty, shift.Board.PenaltyCoins);
            Assert.AreEqual(0L, shift.Board.RemovedCount);
            Assert.AreEqual(0L, shift.Board.ServedCount);
            Assert.AreEqual(0L, shift.Grill.PenaltyCoins, "Штраф гриля рахується окремо");
        }

        [Test]
        public void Clear_EveryTime_AddsOnePenalty()
        {
            ShiftSimulation shift = Built(Pita);
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ClearBoard()));
            BoardTestKit.Build(shift, Hot, Hot);
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ClearBoard()));

            Assert.AreEqual(2L, shift.Board.ClearedCount);
            Assert.AreEqual(2L * BoardTestKit.ClearPenalty, shift.Board.PenaltyCoins);
        }

        [Test]
        public void Clear_EmptyBoard_IsIgnored_WithoutPenalty()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();

            Assert.AreEqual(ShiftActionResult.BoardEmpty, BoardTestKit.Run(shift, shift.ClearBoard()));

            Assert.AreEqual(default(BoardSnapshot), shift.Board.CaptureSnapshot());
            Assert.AreEqual(0L, shift.Board.PenaltyCoins);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(BoardConfig.MaxCoins)]
        public void ClearPenalty_IsOnlyAParameter(int penaltyCoins)
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning(
                BoardTestKit.BoardParams(clearPenaltyCoins: penaltyCoins));
            BoardTestKit.Build(shift, Pita, Salad);

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ClearBoard()));

            Assert.AreEqual((long)penaltyCoins, shift.Board.PenaltyCoins);
            Assert.AreEqual(1L, shift.Board.ClearedCount);
            Assert.AreEqual(0, shift.Board.ItemCount);
        }

        [Test]
        public void AfterClear_NewDishCanBeAssembled()
        {
            ShiftSimulation shift = Built(Meat, Pita);
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ClearBoard()));

            BoardTestKit.Build(shift, Pita, Meat);

            Assert.AreEqual(DishCheck.Correct, shift.Board.Check());
            Assert.AreEqual(4L, shift.Board.AddedCount);
        }
    }
}
