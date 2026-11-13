using System;
using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Перевірка конфіга дошки: помилку конфіга (рецепт посилається на невідомий інгредієнт,
    /// станції немає, число поза межами) ловить тест, а не збій у грі.
    /// </summary>
    public sealed class BoardConfigTests
    {
        private static void AssertSingleError(BoardConfig config, string expectedStart, bool hasGrill = true)
        {
            IReadOnlyList<string> errors = config.GetErrors(hasGrill);

            Assert.AreEqual(1, errors.Count, string.Join("; ", errors));
            StringAssert.StartsWith(expectedStart, errors[0]);
        }

        private static List<Ingredient> IngredientsWith(int index, Ingredient replacement)
        {
            List<Ingredient> ingredients = BoardTestKit.Ingredients();
            ingredients[index] = replacement;
            return ingredients;
        }

        private static List<StationConfig> StationsWith(int index, StationConfig replacement)
        {
            List<StationConfig> stations = BoardTestKit.Stations();
            stations[index] = replacement;
            return stations;
        }

        private static BoardConfig WithRecipe(Recipe recipe)
        {
            return BoardTestKit.BoardParams(recipes: new[] { recipe });
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(8)]
        [TestCase(BoardConfig.MaxLevel)]
        public void TestKitConfig_IsValid_OnEveryLevel(int level)
        {
            CollectionAssert.IsEmpty(BoardTestKit.BoardParams(level: level).GetErrors(true));
            CollectionAssert.IsEmpty(BoardTestKit.Config(BoardTestKit.BoardParams(level: level)).GetErrors());
        }

        [Test]
        public void Config_KeepsValues_AndFindsThingsByName()
        {
            BoardConfig config = BoardTestKit.BoardParams();

            Assert.AreEqual(BoardTestKit.Level, config.Level);
            Assert.AreEqual(BoardTestKit.ClearPenalty, config.ClearPenaltyCoins);
            Assert.AreEqual(5, config.StationCount);
            Assert.AreEqual(BoardTestKit.IngredientCount, config.IngredientCount);
            Assert.AreEqual(2, config.RecipeCount);
            Assert.AreEqual(BoardTestKit.Capacity, config.Capacity);

            Assert.AreEqual(BoardTestKit.Hot, config.IndexOfIngredient("sauce_hot"));
            Assert.AreEqual(-1, config.IndexOfIngredient("ketchup"));
            Assert.AreEqual(-1, config.IndexOfIngredient("PITA"));
            Assert.AreEqual(1, config.IndexOfRecipe("kebab_lavash"));
            Assert.AreEqual(-1, config.IndexOfRecipe("burger"));

            Assert.AreEqual("sauce_garlic", config.GetIngredient(BoardTestKit.Garlic).Id);
            Assert.AreEqual(StationKind.Sauce, config.GetIngredient(BoardTestKit.Garlic).Station);
            Assert.AreEqual(BoardTestKit.GarlicExtra, config.GetIngredient(BoardTestKit.Garlic).ExtraPriceCoins);
            Assert.AreEqual(StationKind.Salad, config.GetStation(2).Kind);
            Assert.AreEqual(BoardTestKit.SaladLevel, config.FindStation(StationKind.Salad).UnlockLevel);
            Assert.IsNull(config.FindStation(StationKind.Grill));
        }

        [Test]
        public void Recipe_KeepsStepsAndOptionalInOrder()
        {
            Recipe recipe = BoardTestKit.Kebab();

            Assert.AreEqual("kebab_pita", recipe.Id);
            Assert.AreEqual(BoardTestKit.KebabPrice, recipe.PriceCoins);
            Assert.AreEqual(1, recipe.UnlockLevel);
            Assert.AreEqual(2, recipe.StepCount);
            Assert.AreEqual(4, recipe.OptionalCount);
            Assert.AreEqual(6, recipe.Length);
            Assert.AreEqual("pita", recipe.GetStep(0));
            Assert.AreEqual("meat", recipe.GetStep(1));
            Assert.AreEqual("salad", recipe.GetOptional(0));
            Assert.AreEqual("sauce_hot", recipe.GetOptional(3));
            Assert.AreEqual("meat", recipe.GetItem(1));
            Assert.AreEqual("salad", recipe.GetItem(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => recipe.GetStep(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => recipe.GetOptional(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => recipe.GetItem(6));
        }

        [Test]
        public void Config_CopiesItsLists()
        {
            List<Ingredient> ingredients = BoardTestKit.Ingredients();
            var steps = new List<string> { "pita", "meat" };
            var recipe = new Recipe("kebab_pita", steps, null, 1, 1);
            BoardConfig config = BoardTestKit.BoardParams(ingredients: ingredients, recipes: new[] { recipe });

            ingredients.Clear();
            steps.Clear();

            Assert.AreEqual(BoardTestKit.IngredientCount, config.IngredientCount);
            Assert.AreEqual(2, recipe.StepCount);
            CollectionAssert.IsEmpty(config.GetErrors(true));
        }

        [TestCase(1, false, false)]
        [TestCase(2, false, false)]
        [TestCase(3, true, false)]
        [TestCase(4, true, true)]
        [TestCase(5, true, true)]
        public void StationAvailability_DependsOnlyOnLevelAndUnlockLevel(int level, bool sauce, bool salad)
        {
            BoardConfig config = BoardTestKit.BoardParams(level: level);

            Assert.IsTrue(config.IsStationAvailable(StationKind.Bread));
            Assert.IsTrue(config.IsStationAvailable(StationKind.Board));
            Assert.IsTrue(config.IsStationAvailable(StationKind.Bin));
            Assert.AreEqual(sauce, config.IsStationAvailable(StationKind.Sauce));
            Assert.AreEqual(salad, config.IsStationAvailable(StationKind.Salad));
            Assert.IsFalse(config.IsStationAvailable(StationKind.Grill), "Гриль задається окремим конфігом");
            Assert.IsFalse(config.IsStationAvailable(StationKind.None));
        }

        [TestCase(7, false)]
        [TestCase(8, true)]
        [TestCase(9, true)]
        public void RecipeAvailability_DependsOnlyOnLevelAndUnlockLevel(int level, bool lavash)
        {
            BoardConfig config = BoardTestKit.BoardParams(level: level);

            Assert.IsTrue(config.IsRecipeAvailable(0));
            Assert.AreEqual(lavash, config.IsRecipeAvailable(1));
            Assert.IsFalse(config.IsRecipeAvailable(-1));
            Assert.IsFalse(config.IsRecipeAvailable(2));
        }

        [TestCase(100, 60, 6L)]
        [TestCase(0, 60, 0L)]
        [TestCase(8, 60, 0L)]
        [TestCase(9, 60, 1L)]
        [TestCase(250, 120, 30L)]
        public void StationActionTime_IsRoundedToNearestTick(int actionMs, int ticksPerSecond, long expectedTicks)
        {
            var station = new StationConfig(StationKind.Bread, 1, actionMs);

            Assert.AreEqual(expectedTicks, station.ActionTicks(ticksPerSecond));
            Assert.AreEqual(expectedTicks, TickTime.FromMs(actionMs, ticksPerSecond));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(BoardConfig.MaxLevel + 1)]
        public void Level_OutOfRange_IsError(int level)
        {
            // Усі unlockLevel тут 1, щоб єдиною причиною був сам номер рівня.
            var config = new BoardConfig(
                level,
                0,
                new[] { new StationConfig(StationKind.Bread, 1, 0), new StationConfig(StationKind.Board, 1, 0) },
                new[] { new Ingredient("pita", StationKind.Bread, 0) },
                new[] { new Recipe("plain_pita", new[] { "pita" }, null, 5, 1) });

            IReadOnlyList<string> errors = config.GetErrors(false);

            Assert.IsNotEmpty(errors);
            StringAssert.StartsWith("board.level=", errors[0]);
        }

        [TestCase(-1)]
        [TestCase(BoardConfig.MaxCoins + 1)]
        public void ClearPenalty_OutOfRange_IsError(int penaltyCoins)
        {
            AssertSingleError(
                BoardTestKit.BoardParams(clearPenaltyCoins: penaltyCoins), "rules.boardClearPenaltyCoins=");
        }

        [Test]
        public void Stations_WithoutBoard_IsError()
        {
            List<StationConfig> stations = BoardTestKit.Stations();
            stations.RemoveAt(3);

            AssertSingleError(BoardTestKit.BoardParams(stations: stations), "stations: немає станції Board");
        }

        [Test]
        public void Stations_BoardLockedOnThisLevel_IsError()
        {
            AssertSingleError(
                BoardTestKit.BoardParams(
                    level: 2, stations: StationsWith(3, new StationConfig(StationKind.Board, 3, 0))),
                "stations.Board.unlockLevel=3");
        }

        [Test]
        public void Stations_Repeated_IsError()
        {
            List<StationConfig> stations = BoardTestKit.Stations();
            stations.Add(new StationConfig(StationKind.Sauce, 1, 0));

            AssertSingleError(BoardTestKit.BoardParams(stations: stations), "stations[5].id=Sauce");
        }

        [TestCase(StationKind.Grill)]
        [TestCase(StationKind.None)]
        public void Stations_KindThatCannotBeListed_IsError(StationKind kind)
        {
            List<StationConfig> stations = BoardTestKit.Stations();
            stations.Add(new StationConfig(kind, 1, 0));

            AssertSingleError(BoardTestKit.BoardParams(stations: stations), "stations[5].id=" + kind);
        }

        [Test]
        public void Stations_NullEntry_IsError()
        {
            List<StationConfig> stations = BoardTestKit.Stations();
            stations.Add(null);

            AssertSingleError(BoardTestKit.BoardParams(stations: stations), "stations[5]:");
        }

        [TestCase(0)]
        [TestCase(BoardConfig.MaxLevel + 1)]
        public void Stations_UnlockLevelOutOfRange_IsError(int unlockLevel)
        {
            // Смітник: від його рівня не залежить придатність рецептів.
            AssertSingleError(
                BoardTestKit.BoardParams(stations: BoardTestKit.Stations(binLevel: unlockLevel)),
                "stations[4].unlockLevel=");
        }

        [TestCase(-1)]
        [TestCase(BoardConfig.MaxActionMs + 1)]
        public void Stations_ActionTimeOutOfRange_IsError(int actionMs)
        {
            AssertSingleError(
                BoardTestKit.BoardParams(stations: BoardTestKit.Stations(saladMs: actionMs)),
                "stations[2].actionMs=");
        }

        [Test]
        public void Ingredients_NullEntry_IsError()
        {
            List<Ingredient> ingredients = BoardTestKit.Ingredients();
            ingredients.Add(null);

            AssertSingleError(BoardTestKit.BoardParams(ingredients: ingredients), "ingredients[7]:");
        }

        [TestCase(null)]
        [TestCase("")]
        public void Ingredients_EmptyName_IsError(string id)
        {
            List<Ingredient> ingredients = BoardTestKit.Ingredients();
            ingredients.Add(new Ingredient(id, StationKind.Bread, 0));

            AssertSingleError(BoardTestKit.BoardParams(ingredients: ingredients), "ingredients[7].id:");
        }

        [Test]
        public void Ingredients_RepeatedName_IsError()
        {
            List<Ingredient> ingredients = BoardTestKit.Ingredients();
            ingredients.Add(new Ingredient("pita", StationKind.Bread, 0));

            AssertSingleError(BoardTestKit.BoardParams(ingredients: ingredients), "ingredients[7].id=pita");
        }

        [TestCase(StationKind.Board)]
        [TestCase(StationKind.Bin)]
        [TestCase(StationKind.None)]
        public void Ingredients_StationThatGivesNoIngredients_IsError(StationKind station)
        {
            List<Ingredient> ingredients = BoardTestKit.Ingredients();
            ingredients.Add(new Ingredient("napkin", station, 0));

            AssertSingleError(
                BoardTestKit.BoardParams(ingredients: ingredients), "ingredients[7].station=" + station);
        }

        [Test]
        public void Ingredients_StationMissingFromStations_IsError()
        {
            // Без салатного бару: салат і цибуля посилаються на станцію, якої немає.
            List<StationConfig> stations = BoardTestKit.Stations();
            stations.RemoveAt(2);

            IReadOnlyList<string> errors = BoardTestKit.BoardParams(stations: stations).GetErrors(true);

            Assert.AreEqual(2, errors.Count, string.Join("; ", errors));
            StringAssert.StartsWith("ingredients[2].station=Salad", errors[0]);
            StringAssert.StartsWith("ingredients[3].station=Salad", errors[1]);
        }

        [TestCase(-1)]
        [TestCase(BoardConfig.MaxCoins + 1)]
        public void Ingredients_ExtraPriceOutOfRange_IsError(int extraPriceCoins)
        {
            AssertSingleError(
                BoardTestKit.BoardParams(ingredients: IngredientsWith(
                    BoardTestKit.Salad, new Ingredient("salad", StationKind.Salad, extraPriceCoins))),
                "ingredients[2].extraPriceCoins=");
        }

        [Test]
        public void Ingredients_TwoKindsOfGrillMeat_IsError()
        {
            List<Ingredient> ingredients = BoardTestKit.Ingredients();
            ingredients.Add(new Ingredient("chicken", StationKind.Grill, 0));

            AssertSingleError(BoardTestKit.BoardParams(ingredients: ingredients), "ingredients: із грилем");
        }

        [Test]
        public void Ingredients_GrillMeatWithoutGrillInShift_IsError()
        {
            IReadOnlyList<string> errors = BoardTestKit.BoardParams().GetErrors(false);

            // Без гриля м'ясо взяти нізвідки: і сам інгредієнт, і обов'язковий крок рецепта — помилки.
            Assert.AreEqual(2, errors.Count, string.Join("; ", errors));
            StringAssert.StartsWith("ingredients: інгредієнт станції Grill", errors[0]);
            StringAssert.StartsWith("recipes[0].steps: інгредієнт «meat» недоступний", errors[1]);
        }

        [Test]
        public void Ingredients_NoGrillMeatWithGrillInShift_IsError()
        {
            var config = new BoardConfig(
                1,
                0,
                new[] { new StationConfig(StationKind.Bread, 1, 0), new StationConfig(StationKind.Board, 1, 0) },
                new[] { new Ingredient("pita", StationKind.Bread, 0) },
                new[] { new Recipe("plain_pita", new[] { "pita" }, null, 5, 1) });

            CollectionAssert.IsEmpty(config.GetErrors(false));
            AssertSingleError(config, "ingredients: із грилем");
        }

        [Test]
        public void EmptyLists_AreErrors()
        {
            var config = new BoardConfig(1, 0, null, null, null);

            IReadOnlyList<string> errors = config.GetErrors(false);

            Assert.AreEqual(3, errors.Count, string.Join("; ", errors));
            StringAssert.StartsWith("stations: немає станції Board", errors[0]);
            StringAssert.StartsWith("ingredients.count=0", errors[1]);
            StringAssert.StartsWith("recipes.count=0", errors[2]);
            Assert.AreEqual(0, config.Capacity);
        }

        [Test]
        public void Recipes_NullEntry_IsError()
        {
            AssertSingleError(
                BoardTestKit.BoardParams(recipes: new[] { BoardTestKit.Kebab(), null }), "recipes[1]:");
        }

        [TestCase(null)]
        [TestCase("")]
        public void Recipes_EmptyName_IsError(string id)
        {
            AssertSingleError(WithRecipe(new Recipe(id, new[] { "pita", "meat" }, null, 1, 1)), "recipes[0].id:");
        }

        [Test]
        public void Recipes_RepeatedName_IsError()
        {
            AssertSingleError(
                BoardTestKit.BoardParams(recipes: new[] { BoardTestKit.Kebab(), BoardTestKit.Kebab() }),
                "recipes[1].id=kebab_pita");
        }

        [Test]
        public void Recipes_WithoutSteps_IsError()
        {
            AssertSingleError(
                WithRecipe(new Recipe("only_optional", null, new[] { "sauce_hot" }, 1, 1)), "recipes[0].steps:");
        }

        [Test]
        public void Recipes_UnknownIngredientInSteps_IsError()
        {
            AssertSingleError(
                WithRecipe(new Recipe("kebab_pita", new[] { "pita", "beef" }, null, 1, 1)),
                "recipes[0]: невідомий інгредієнт «beef»");
        }

        [Test]
        public void Recipes_UnknownIngredientInOptional_IsError()
        {
            AssertSingleError(
                WithRecipe(new Recipe("kebab_pita", new[] { "pita", "meat" }, new[] { "ketchup" }, 1, 1)),
                "recipes[0]: невідомий інгредієнт «ketchup»");
        }

        [Test]
        public void Recipes_IngredientRepeated_IsError()
        {
            AssertSingleError(
                WithRecipe(new Recipe("double", new[] { "pita", "meat" }, new[] { "sauce_hot", "meat" }, 1, 1)),
                "recipes[0]: інгредієнт «meat» повторюється");
        }

        [Test]
        public void Recipes_TooLong_IsError()
        {
            var ingredients = new List<Ingredient>();
            var steps = new List<string>();
            for (int i = 0; i <= BoardConfig.MaxRecipeLength; i++)
            {
                string id = "bread" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                ingredients.Add(new Ingredient(id, StationKind.Bread, 0));
                steps.Add(id);
            }

            var config = new BoardConfig(
                1, 0, BoardTestKit.Stations(), ingredients, new[] { new Recipe("tower", steps, null, 1, 1) });

            AssertSingleError(config, "recipes[0].length=17", hasGrill: false);
        }

        [TestCase(-1)]
        [TestCase(BoardConfig.MaxCoins + 1)]
        public void Recipes_PriceOutOfRange_IsError(int priceCoins)
        {
            AssertSingleError(
                WithRecipe(new Recipe("kebab_pita", new[] { "pita", "meat" }, null, priceCoins, 1)),
                "recipes[0].price=");
        }

        [Test]
        public void Recipes_UnlockLevelOutOfRange_IsError()
        {
            AssertSingleError(
                WithRecipe(new Recipe("kebab_pita", new[] { "pita", "meat" }, null, 1, 0)),
                "recipes[0].unlockLevel=0");
        }

        [Test]
        public void Recipes_NoneAvailableOnThisLevel_IsError()
        {
            AssertSingleError(
                WithRecipe(new Recipe("kebab_pita", new[] { "pita", "meat" }, null, 1, BoardTestKit.Level + 1)),
                "recipes: жоден рецепт не доступний на рівні 5");
        }

        [Test]
        public void Recipes_RequiredStepFromLockedStation_IsError_ButOptionalFromLockedStationIsFine()
        {
            var saladFirst = new Recipe("salad_pita", new[] { "pita", "salad" }, null, 1, 1);
            var saladOptional = new Recipe("salad_pita", new[] { "pita" }, new[] { "salad" }, 1, 1);

            AssertSingleError(
                BoardTestKit.BoardParams(level: 3, recipes: new[] { saladFirst }),
                "recipes[0].steps: інгредієнт «salad» недоступний на рівні 3");
            CollectionAssert.IsEmpty(BoardTestKit.BoardParams(level: 4, recipes: new[] { saladFirst }).GetErrors(true));
            CollectionAssert.IsEmpty(
                BoardTestKit.BoardParams(level: 3, recipes: new[] { saladOptional }).GetErrors(true));
        }

        [Test]
        public void LockedRecipe_MayNeedLockedStation()
        {
            var later = new Recipe("salad_pita", new[] { "pita", "salad" }, null, 1, BoardTestKit.SaladLevel);

            CollectionAssert.IsEmpty(
                BoardTestKit.BoardParams(level: 1, recipes: new[] { BoardTestKit.Kebab(), later }).GetErrors(true));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(ShiftConfig.MaxActionQueue + 1)]
        public void ShiftWithBoard_NeedsActionQueue(int actionQueueMax)
        {
            IReadOnlyList<string> errors = BoardTestKit.Config(actionQueueMax: actionQueueMax).GetErrors();

            Assert.AreEqual(1, errors.Count, string.Join("; ", errors));
            StringAssert.StartsWith("actionQueueMax=", errors[0]);
        }

        [Test]
        public void ShiftConfig_ReportsBoardErrors_TogetherWithItsOwn()
        {
            var config = new ShiftConfig(
                60, 600, 0, 30, 4, BoardTestKit.GrillParams(), BoardTestKit.BoardParams(clearPenaltyCoins: -5));

            IReadOnlyList<string> errors = config.GetErrors();

            Assert.AreEqual(2, errors.Count, string.Join("; ", errors));
            StringAssert.StartsWith("durationSec=0", errors[0]);
            StringAssert.StartsWith("rules.boardClearPenaltyCoins=-5", errors[1]);
        }

        [Test]
        public void ShiftSimulation_RejectsInvalidBoardConfig_WithReasonInMessage()
        {
            ShiftConfig config = BoardTestKit.Config(
                WithRecipe(new Recipe("kebab_pita", new[] { "pita", "beef" }, null, 1, 1)));

            var error = Assert.Throws<ArgumentException>(
                () => new ShiftSimulation(config, ShiftTestKit.Seed, new FixedGoal(true)));

            StringAssert.Contains("recipes[0]: невідомий інгредієнт «beef»", error.Message);
        }

        [Test]
        public void OldConstructors_GiveShiftWithoutBoard()
        {
            Assert.IsNull(ShiftTestKit.Config().Board);
            Assert.IsNull(GrillTestKit.Config().Board);
            Assert.IsNull(ShiftTestKit.Create().Board);
            Assert.IsNull(GrillTestKit.Create().Board);

            ShiftSimulation shift = BoardTestKit.Create();
            Assert.IsNotNull(shift.Board);
            Assert.AreSame(shift.Config.Board, shift.Board.Config);
        }
    }
}
