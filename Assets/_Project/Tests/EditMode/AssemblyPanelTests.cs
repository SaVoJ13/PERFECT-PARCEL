using System;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Тапи бота по кнопках панелей справжнього екрана зміни (елементи UI Toolkit без сцени).</summary>
    internal sealed class ScreenTaps : IUiTaps
    {
        private readonly ShiftScreen _screen;

        public ScreenTaps(ShiftScreen screen)
        {
            _screen = screen;
        }

        public bool Grill(int slot) => _screen.Grill.TapSlot(slot);

        public bool Customer(int slot) => _screen.Counter.TapSlot(slot);

        public bool Station(int ingredient)
        {
            AssemblyPanel assembly = _screen.Assembly;
            for (int i = 0; i < assembly.StationCount; i++)
            {
                if (assembly.GetStationIngredient(i) == ingredient)
                {
                    return assembly.TapStation(i);
                }
            }

            return false;
        }

        public bool BoardItem(int position) => _screen.Assembly.TapBoardItem(position);
    }

    /// <summary>
    /// Станція збирання на екрані зміни (S1-09c) і розкладка прилавка (BUG-040): кнопки станцій,
    /// дошка в порядку й зняття тапом, черга зі скасуванням, очищення, написи і підказки; повна
    /// зміна рівнів 1–5 ботом лише тапами панелей доходить до підсумку. Елементи UI Toolkit без сцени.
    /// </summary>
    public sealed class AssemblyPanelTests
    {
        private const ulong Seed = 20284958UL;
        private static readonly ILocalizer Text = new PlaceholderLocalizer();

        private static ShiftScreen BuildScreen(LevelDefinition level, out ShiftDriver driver)
        {
            driver = new ShiftDriver(level, Seed);
            Assert.IsTrue(driver.Begin());
            return new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });
        }

        private static void RunUntil(ShiftDriver driver, ShiftScreen screen, Func<bool> done, string what)
        {
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            for (int guard = 0; guard < 100000 && !done(); guard++)
            {
                Assert.IsFalse(driver.IsFinished, "зміна скінчилась раніше, ніж: " + what);
                driver.Frame(tick);
                screen.Refresh();
            }

            Assert.IsTrue(done(), what);
        }

        private static int StationOf(AssemblyPanel assembly, BoardConfig config, string id)
        {
            for (int i = 0; i < assembly.StationCount; i++)
            {
                if (assembly.GetStationIngredient(i) == config.IndexOfIngredient(id))
                {
                    return i;
                }
            }

            Assert.Fail("немає кнопки станції " + id);
            return -1;
        }

        [TestCase(1)]
        [TestCase(4)]
        public void Stations_ButtonPerUnlockedIngredient_WithLocalizedName(int levelNumber)
        {
            ShiftDriver driver;
            ShiftScreen screen = BuildScreen(ConfigTestKit.Level(levelNumber), out driver);
            AssemblyPanel assembly = screen.Assembly;
            Board board = driver.Simulation.Board;

            CollectionAssert.AreEqual(AssemblyView.Stations(board), Stations(assembly));
            for (int i = 0; i < assembly.StationCount; i++)
            {
                Assert.AreEqual(Text.Get(board.Config.GetIngredient(assembly.GetStationIngredient(i)).NameKey), assembly.GetStationText(i));
            }

            Assert.AreEqual(board.Capacity, assembly.ItemSlotCount);
            Assert.AreEqual(Math.Min(driver.Level.Shift.ActionQueueMax, AssemblyPanel.MaxQueueShown), assembly.QueueSlotCount);
            Assert.IsTrue(assembly.ClearShown, "смітник доступний з рівня 1");
            Assert.AreEqual(Text.Get(TextKeys.AssemblyBoardEmpty), assembly.DishText);
            Assert.AreEqual(string.Empty, assembly.HintText);
            Assert.AreEqual(string.Empty, assembly.ComboText);
        }

        [Test]
        public void Board_ShowsItemsInOrder_TapRemovesThatItem_ClearEmpties()
        {
            ShiftDriver driver;
            ShiftScreen screen = BuildScreen(ConfigTestKit.Level(4), out driver);
            AssemblyPanel assembly = screen.Assembly;
            ShiftSimulation sim = driver.Simulation;
            BoardConfig config = sim.Board.Config;

            Assert.IsTrue(assembly.TapStation(StationOf(assembly, config, "pita")));
            Assert.IsTrue(assembly.TapStation(StationOf(assembly, config, "salad")));
            Assert.IsFalse(assembly.TapStation(assembly.StationCount));
            RunUntil(driver, screen, () => sim.QueuedActionCount == 0, "дві дії виконано");

            Assert.AreEqual(Text.Get(TextKeys.IngredientPita), assembly.GetItemText(0));
            Assert.AreEqual(Text.Get(TextKeys.IngredientSalad), assembly.GetItemText(1));
            Assert.IsTrue(assembly.IsItemShown(1));
            Assert.IsFalse(assembly.IsItemShown(2));
            Assert.AreEqual(Text.Get(TextKeys.AssemblyDishWrong), assembly.DishText, "салат перед м'ясом — не за рецептом");

            Assert.IsTrue(assembly.TapBoardItem(0), "тап знімає саме цей інгредієнт");
            RunUntil(driver, screen, () => sim.QueuedActionCount == 0, "зняття виконано");
            Assert.AreEqual(Text.Get(TextKeys.IngredientSalad), assembly.GetItemText(0));
            Assert.IsFalse(assembly.IsItemShown(1));

            Assert.IsTrue(assembly.TapClear());
            RunUntil(driver, screen, () => sim.QueuedActionCount == 0, "очищення виконано");
            Assert.IsFalse(assembly.IsItemShown(0));
            Assert.AreEqual(Text.Get(TextKeys.AssemblyBoardEmpty), assembly.DishText);
            Assert.AreEqual(1L, sim.Board.ClearedCount);
        }

        [Test]
        public void Queue_ShowsActionNames_AndTapCancels()
        {
            ShiftDriver driver;
            ShiftScreen screen = BuildScreen(ConfigTestKit.Level(4), out driver);
            AssemblyPanel assembly = screen.Assembly;
            ShiftSimulation sim = driver.Simulation;

            Assert.IsTrue(assembly.TapStation(StationOf(assembly, sim.Board.Config, "pita")));
            Assert.IsTrue(screen.Grill.TapSlot(0));
            screen.Refresh();
            Assert.AreEqual(Text.Get(TextKeys.IngredientPita), assembly.GetQueuedText(0));
            Assert.AreEqual(Text.Get(TextKeys.ActionPlaceMeat), assembly.GetQueuedText(1));
            Assert.IsFalse(assembly.IsQueuedShown(2));

            Assert.IsTrue(assembly.TapQueued(1));
            Assert.IsFalse(assembly.TapQueued(assembly.QueueSlotCount));
            screen.Refresh();
            Assert.AreEqual(1, sim.QueuedActionCount);
            Assert.IsFalse(assembly.IsQueuedShown(1));
            Assert.AreEqual(GrillSlotDisplay.Empty, screen.Grill.GetShownView(0).Display, "скасоване м'ясо не кладуть");
        }

        [Test]
        public void CookingMeatTap_ShowsHint_ReadyDish_ShowsReady()
        {
            ShiftDriver driver;
            ShiftScreen screen = BuildScreen(ConfigTestKit.Level(1), out driver);
            AssemblyPanel assembly = screen.Assembly;
            ShiftSimulation sim = driver.Simulation;

            Assert.IsTrue(screen.Grill.TapSlot(0));
            Assert.IsTrue(assembly.TapStation(StationOf(assembly, sim.Board.Config, "pita")));
            RunUntil(driver, screen, () => sim.QueuedActionCount == 0, "м'ясо на грилі, піта на дошці");
            Assert.AreEqual(string.Empty, assembly.DishText, "страва збирається — без напису");

            Assert.IsFalse(screen.Grill.TapSlot(0), "BUG-037: м'ясо готується");
            screen.Refresh();
            Assert.AreEqual(Text.Get(TextKeys.HintMeatNotReady), assembly.HintText);

            RunUntil(driver, screen, () => screen.Grill.GetShownView(0).Display == GrillSlotDisplay.Done, "м'ясо готове");
            Assert.AreEqual(string.Empty, assembly.HintText, "підказка зникла");
            Assert.IsTrue(screen.Grill.TapSlot(0));
            RunUntil(driver, screen, () => sim.QueuedActionCount == 0, "м'ясо на дошці");
            Assert.AreEqual(Text.Get(TextKeys.AssemblyDishReady), assembly.DishText);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void ExpertUiBot_PlaysWholeShift_OnlyThroughPanels(int levelNumber)
        {
            ShiftDriver driver;
            ShiftScreen screen = BuildScreen(ConfigTestKit.Level(levelNumber), out driver);
            var bot = new UiBot(driver, new ScreenTaps(screen));
            ShiftSimulation sim = driver.Simulation;
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            bool comboShown = false;
            for (int guard = 0; guard < 400000 && !driver.IsFinished; guard++)
            {
                bot.Step();
                driver.Frame(tick);
                screen.Refresh();

                long combo = AssemblyView.ComboShown(sim.Counter.Combo);
                comboShown |= combo > 0L;
                Assert.AreEqual(combo > 0L ? string.Format(Text.Get(TextKeys.AssemblyCombo), combo) : string.Empty, screen.Assembly.ComboText);
                string dishKey = AssemblyView.DishTextKey(sim.Board.Check());
                Assert.AreEqual(dishKey == null ? string.Empty : Text.Get(dishKey), screen.Assembly.DishText);
            }

            Assert.IsTrue(driver.IsFinished, "зміна дійшла до ShiftResult");
            Assert.IsTrue(screen.ResultPanelVisible, "підсумок показано");
            Assert.Greater(sim.ServedCount, 0L);
            TestContext.WriteLine(string.Format(
                "{0}: won={1} stars={2} served={3} coins={4} lost={5} wrong={6} maxCombo={7} comboShown={8} taps={9}",
                driver.Level.Id, driver.Result.Won, driver.Result.Stars, sim.ServedCount, sim.CoinsEarned,
                sim.LostCustomers, sim.WrongDishes, sim.MaxCombo, comboShown, bot.Taps));
            if (levelNumber == 1)
            {
                Assert.IsTrue(driver.Result.Won, "L01 бот проходить лише тапами панелей");
            }
        }

        [Test]
        public void Counter_ThreeSlotsInOneRow_TwelveSlotsInRowsOfFour()
        {
            Assert.AreEqual(1, CounterPanel.CardsPerRow(0));
            Assert.AreEqual(1, CounterPanel.CardsPerRow(1));
            Assert.AreEqual(3, CounterPanel.CardsPerRow(3));
            Assert.AreEqual(CounterPanel.MaxCardsPerRow, CounterPanel.CardsPerRow(CounterConfig.MaxSlots));

            // BUG-040: логічна ширина 1080x2340 (ScaleWithScreenSize 1080x1920, match 0.5) ≈ 978 px,
            // мінус поля зони 2×24 і рамки 2×4; на картку з проміжком 16 px лишається ≥ 144 px (48 dp).
            float zone = (1080f / Mathf.Sqrt(2340f / 1920f)) - 48f - 8f;
            Assert.GreaterOrEqual((zone / CounterPanel.CardsPerRow(3)) - 16f, 144f);
            Assert.GreaterOrEqual((zone / CounterPanel.MaxCardsPerRow) - 16f, 144f);

            ShiftDriver driver;
            CounterPanel three = BuildScreen(ConfigTestKit.Level(4), out driver).Counter;
            Assert.AreEqual(3, three.SlotCount);
            Assert.AreEqual(1, three.RowCount);
            VisualElement row = three.Root.Q("counter-slots-0");
            Assert.AreEqual(Wrap.NoWrap, row.style.flexWrap.value, "рядок без переносу");
            Assert.AreEqual(3, row.childCount);

            CounterPanel twelve = BuildScreen(CrowdedLevel4(), out driver).Counter;
            Assert.AreEqual(CounterConfig.MaxSlots, twelve.SlotCount);
            Assert.AreEqual((CounterConfig.MaxSlots + CounterPanel.MaxCardsPerRow - 1) / CounterPanel.MaxCardsPerRow, twelve.RowCount);
            for (int r = 0; r < twelve.RowCount; r++)
            {
                Assert.AreEqual(CounterPanel.MaxCardsPerRow, twelve.Root.Q("counter-slots-" + r).childCount);
            }
        }

        private static int[] Stations(AssemblyPanel assembly)
        {
            var stations = new int[assembly.StationCount];
            for (int i = 0; i < stations.Length; i++)
            {
                stations[i] = assembly.GetStationIngredient(i);
            }

            return stations;
        }

        private static LevelDefinition CrowdedLevel4()
        {
            ConfigSources sources = ConfigTestKit.Mutated("L04", t => t["counterSlots"] = (long)CounterConfig.MaxSlots);
            ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(sources);
            Assert.IsTrue(result.IsValid, ConfigTestKit.Join(result.Errors));
            return result.Value.FindByNumber(4);
        }
    }
}
