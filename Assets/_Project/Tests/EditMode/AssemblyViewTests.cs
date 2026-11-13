using System;
using System.Collections.Generic;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Модель вигляду станції збирання і шар тапів (S1-09c), без елементів UI: станції за рівнем,
    /// написи про страву, підказки (BUG-037), назви дій черги, комбо; тапи збирання/видачі йдуть
    /// у Core і змінюють знімок; бот-експерт проходить L01 лише тапами екрана.
    /// </summary>
    public sealed class AssemblyViewTests
    {
        private const ulong Seed = 20284958UL;

        private static ShiftDriver Started(int levelNumber)
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(levelNumber), Seed);
            Assert.IsTrue(driver.Begin());
            return driver;
        }

        private static void RunUntil(ShiftDriver driver, ShiftInput input, Func<bool> done, string what)
        {
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            for (int guard = 0; guard < 100000 && !done(); guard++)
            {
                Assert.IsFalse(driver.IsFinished, "зміна скінчилась раніше, ніж: " + what);
                driver.Frame(tick);
                input.Update();
            }

            Assert.IsTrue(done(), what);
        }

        private static int[] BoardItems(ShiftSimulation sim)
        {
            BoardSnapshot board = sim.CaptureSnapshot().Board;
            var items = new int[board.ItemCount];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = board.GetItem(i);
            }

            return items;
        }

        [TestCase(1, new[] { "pita" })]
        [TestCase(2, new[] { "pita" })]
        [TestCase(3, new[] { "pita", "sauce_garlic", "sauce_hot" })]
        [TestCase(4, new[] { "pita", "salad", "onion", "sauce_garlic", "sauce_hot" })]
        public void Stations_FollowLevelUnlocks_WithoutMeat(int levelNumber, string[] ids)
        {
            Board board = Started(levelNumber).Simulation.Board;
            int[] stations = AssemblyView.Stations(board);
            var shown = new List<string>();
            foreach (int ingredient in stations)
            {
                shown.Add(board.Config.GetIngredient(ingredient).Id);
            }

            CollectionAssert.AreEqual(ids, shown, "кнопки станцій у порядку конфіга; м'ясо — лише з гриля");
        }

        [Test]
        public void DishText_SaysReadyOrWrong_AndNothingWhileBuilding()
        {
            Assert.AreEqual(TextKeys.AssemblyBoardEmpty, AssemblyView.DishTextKey(DishCheck.Empty));
            Assert.IsNull(AssemblyView.DishTextKey(DishCheck.Incomplete));
            Assert.AreEqual(TextKeys.AssemblyDishReady, AssemblyView.DishTextKey(DishCheck.Correct));
            Assert.AreEqual(TextKeys.AssemblyDishWrong, AssemblyView.DishTextKey(DishCheck.WrongOrder));
            Assert.AreEqual(TextKeys.AssemblyDishWrong, AssemblyView.DishTextKey(DishCheck.ExtraIngredient));
        }

        [Test]
        public void Hint_ForEveryIgnoredResult_IsKnownOrNone()
        {
            Assert.AreEqual(TextKeys.HintMeatNotReady, AssemblyView.HintKeyOf(ShiftActionResult.MeatRaw));
            Assert.AreEqual(TextKeys.HintMeatNotReady, AssemblyView.HintKeyOf(ShiftActionResult.NoDoneMeat));
            Assert.AreEqual(TextKeys.HintGrillFull, AssemblyView.HintKeyOf(ShiftActionResult.GrillFull));
            Assert.AreEqual(TextKeys.HintGrillFull, AssemblyView.HintKeyOf(ShiftActionResult.SlotOccupied));
            Assert.AreEqual(TextKeys.HintBoardFull, AssemblyView.HintKeyOf(ShiftActionResult.BoardFull));
            Assert.AreEqual(TextKeys.HintDishIncomplete, AssemblyView.HintKeyOf(ShiftActionResult.BoardEmpty));
            Assert.AreEqual(TextKeys.HintDishIncomplete, AssemblyView.HintKeyOf(ShiftActionResult.DishIncomplete));
            Assert.AreEqual(TextKeys.HintDishWrong, AssemblyView.HintKeyOf(ShiftActionResult.DishWrongOrder));
            Assert.AreEqual(TextKeys.HintDishWrong, AssemblyView.HintKeyOf(ShiftActionResult.DishExtraIngredient));
            Assert.IsNull(AssemblyView.HintKeyOf(ShiftActionResult.None));
            Assert.IsNull(AssemblyView.HintKeyOf(ShiftActionResult.Done));
            Assert.IsNull(AssemblyView.HintKeyOf(ShiftActionResult.DishNotAccepted), "про скаргу клієнта каже прилавок");
        }

        [Test]
        public void QueuedActionNames_AreDistinctKeys_IngredientUsesItsName()
        {
            var keys = new HashSet<string>();
            foreach (ShiftActionType type in Enum.GetValues(typeof(ShiftActionType)))
            {
                string key = AssemblyView.ActionTextKey(type);
                if (type == ShiftActionType.None || type == ShiftActionType.AddIngredient)
                {
                    Assert.IsNull(key, type.ToString());
                    continue;
                }

                Assert.IsNotNull(key, type.ToString());
                Assert.IsTrue(keys.Add(key), "повтор ключа " + key);
            }
        }

        [Test]
        public void Combo_ShownFromSecondServeInARow()
        {
            Assert.AreEqual(0L, AssemblyView.ComboShown(0L));
            Assert.AreEqual(0L, AssemblyView.ComboShown(1L));
            Assert.AreEqual(2L, AssemblyView.ComboShown(2L));
            Assert.AreEqual(5L, AssemblyView.ComboShown(5L));
        }

        [Test]
        public void AssemblyTaps_GoToCore_AndChangeBoardSnapshot()
        {
            ShiftDriver driver = Started(4);
            var input = new ShiftInput(driver);
            ShiftSimulation sim = driver.Simulation;
            BoardConfig config = sim.Board.Config;
            int pita = config.IndexOfIngredient("pita");
            int salad = config.IndexOfIngredient("salad");
            int meat = config.IndexOfIngredient("meat");

            Assert.IsTrue(input.TapStation(pita));
            Assert.AreEqual(new ShiftAction(ShiftActionType.AddIngredient, pita), sim.GetQueuedAction(0));
            Assert.IsFalse(input.TapStation(meat), "м'ясо — лише з гриля");
            Assert.IsTrue(input.TapStation(salad));
            RunUntil(driver, input, () => sim.QueuedActionCount == 0, "дві дії виконано");
            CollectionAssert.AreEqual(new[] { pita, salad }, BoardItems(sim), "дошка — у порядку тапів");

            // PROVISIONAL 17: будь-який інгредієнт знімається без штрафу.
            Assert.IsFalse(input.TapBoardItem(2), "на місці 2 нічого немає");
            Assert.IsFalse(input.TapBoardItem(-1));
            Assert.IsTrue(input.TapBoardItem(0));
            Assert.AreEqual(new ShiftAction(ShiftActionType.RemoveIngredient, 0), sim.GetQueuedAction(0));
            RunUntil(driver, input, () => sim.QueuedActionCount == 0, "зняття виконано");
            CollectionAssert.AreEqual(new[] { salad }, BoardItems(sim));
            Assert.AreEqual(1L, sim.CaptureSnapshot().Board.RemovedCount);
            Assert.AreEqual(0L, sim.CaptureSnapshot().Board.PenaltyCoins);

            Assert.IsTrue(input.TapClearBoard());
            RunUntil(driver, input, () => sim.QueuedActionCount == 0, "очищення виконано");
            CollectionAssert.IsEmpty(BoardItems(sim));
            Assert.AreEqual(1L, sim.CaptureSnapshot().Board.ClearedCount);
            Assert.AreEqual((long)config.ClearPenaltyCoins, sim.CaptureSnapshot().Board.PenaltyCoins, "штраф — з конфіга");
            Assert.IsFalse(input.TapClearBoard(), "порожню дошку не очищають");

            // Скасування дії з черги (PROVISIONAL 14).
            Assert.IsTrue(input.TapStation(pita));
            Assert.IsTrue(input.TapGrillSlot(0));
            Assert.AreEqual(2, sim.QueuedActionCount);
            Assert.IsFalse(input.TapQueuedAction(5));
            Assert.IsTrue(input.TapQueuedAction(1));
            Assert.AreEqual(1, sim.QueuedActionCount);
            Assert.AreEqual(ShiftActionType.AddIngredient, sim.GetQueuedAction(0).Type);
        }

        [Test]
        public void TapOnCookingMeat_SendsNothing_ShowsHint_ThatExpires()
        {
            ShiftDriver driver = Started(1);
            var input = new ShiftInput(driver);
            ShiftSimulation sim = driver.Simulation;

            Assert.IsTrue(input.TapGrillSlot(0));
            Assert.IsFalse(input.TapGrillSlot(0), "м'ясо вже кладуть");
            Assert.IsNull(input.HintKey, "дія в черзі — без підказки");
            RunUntil(driver, input, () => sim.QueuedActionCount == 0, "м'ясо на грилі");
            Assert.AreEqual(GrillSlotDisplay.Cooking, GrillView.FromSimulation(sim, 0).Display);

            // BUG-037: тап по м'ясу, що готується, не шле команди, але пояснює чому.
            Assert.IsFalse(input.TapGrillSlot(0));
            Assert.AreEqual(0, sim.QueuedActionCount);
            Assert.AreEqual(TextKeys.HintMeatNotReady, input.HintKey);

            long until = sim.ElapsedTicks + TickTime.FromMs(ShiftInput.HintMs, driver.Level.Shift.TicksPerSecond);
            RunUntil(driver, input, () => sim.ElapsedTicks >= until - 1, "майже весь час підказки");
            Assert.AreEqual(TextKeys.HintMeatNotReady, input.HintKey);
            RunUntil(driver, input, () => input.HintKey == null, "підказка зникає");
            Assert.AreEqual(until, sim.ElapsedTicks);
        }

        [Test]
        public void ServeOnEmptyBoard_CoreIgnores_HintSaysDishNotReady()
        {
            ShiftDriver driver = Started(1);
            var input = new ShiftInput(driver);
            ShiftSimulation sim = driver.Simulation;
            RunUntil(driver, input, () => sim.Counter.GetCustomerInSlot(0) != null, "клієнт у слоті");

            Assert.IsTrue(input.TapCustomer(0));
            Assert.AreEqual(new ShiftAction(ShiftActionType.ServeDish, 0), sim.GetQueuedAction(0));
            Assert.IsFalse(input.TapCustomer(0), "видача вже в черзі");
            Assert.IsFalse(input.TapCustomer(1), "слота 1 на L01 немає");
            RunUntil(driver, input, () => sim.QueuedActionCount == 0, "видачу виконано");
            Assert.AreEqual(ShiftActionResult.BoardEmpty, sim.LastActionResult);
            Assert.AreEqual(TextKeys.HintDishIncomplete, input.HintKey);
        }

        [Test]
        public void Paused_TapsDoNothing_AndNoHint()
        {
            ShiftDriver driver = Started(1);
            var input = new ShiftInput(driver);
            Assert.IsTrue(input.TapGrillSlot(0));
            RunUntil(driver, input, () => driver.Simulation.QueuedActionCount == 0, "м'ясо на грилі");

            driver.SetPaused(true);
            Assert.IsFalse(input.TapGrillSlot(0));
            Assert.IsNull(input.HintKey, "на паузі підказок немає");
            Assert.IsFalse(input.TapStation(driver.Simulation.Board.Config.IndexOfIngredient("pita")));
            Assert.AreEqual(0, driver.Simulation.QueuedActionCount);
        }

        [Test]
        public void ExpertUiBot_Level1_OnlyScreenTaps_WinsLikeCoreBot()
        {
            ShiftDriver driver = Started(1);
            var input = new ShiftInput(driver);
            var bot = new UiBot(driver, new InputTaps(input));
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            for (int guard = 0; guard < 200000 && !driver.IsFinished; guard++)
            {
                bot.Step();
                driver.Frame(tick);
                input.Update();
            }

            Assert.IsTrue(driver.IsFinished, "зміна дійшла до ShiftResult");
            Assert.IsTrue(driver.Result.Won, "бот лише тапами екрана проходить L01");

            // Той самий рівень і seed: бот симуляції (S1-05) дає той самий результат.
            BotResult core = BotRunner.Run(ConfigTestKit.Level(1), Seed, BotProfile.Expert);
            Assert.AreEqual(core.Served, driver.Simulation.ServedCount);
            Assert.AreEqual(core.Coins, driver.Simulation.CoinsEarned);
            Assert.AreEqual(core.Result.Stars, driver.Result.Stars);
            Assert.AreEqual(core.ExecutedActions, driver.Simulation.ExecutedActionCount);
            Assert.AreEqual(0L, driver.Simulation.IgnoredActionCount);
            Assert.AreEqual(core.ExecutedActions, (long)bot.Taps, "кожен тап бота виконано");
        }
    }
}
