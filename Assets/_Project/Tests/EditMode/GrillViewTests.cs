using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Модель вигляду гриля (S1-09a): стан слота → показ (5 станів), напис, тап → команда,
    /// і збіг показу з живої симуляції та зі знімка на справжньому рівні.
    /// </summary>
    public sealed class GrillViewTests
    {
        private const ulong Seed = 7300191UL;

        // Умовні такти (не з конфігу): приготування 60, вікно до пригорання 100, попередження 20, дія 30.
        private const long Cook = 60L;
        private const long Burn = 100L;
        private const long Warn = 20L;
        private const long Action = 30L;

        private static GrillSlotView Compute(MeatState state, long age, ShiftActionType queued = ShiftActionType.None, long queuedProgress = 0L)
        {
            return GrillView.Compute(state, age, Cook, Burn, Warn, queued, queuedProgress, Action);
        }

        private static void AssertTap(GrillSlotView view, bool expectedSends, ShiftTap expectedTap)
        {
            ShiftTap tap;
            bool sends = GrillView.TryGetTap(view, out tap);
            Assert.AreEqual(expectedSends, sends, "чи шле тап команду: " + view);
            if (expectedSends)
            {
                Assert.AreEqual(expectedTap, tap, view.ToString());
            }
        }

        [Test]
        public void EmptySlot_ShowsEmpty_AndTapPlacesMeat()
        {
            GrillSlotView view = Compute(MeatState.Empty, 0L);
            Assert.AreEqual(new GrillSlotView(GrillSlotDisplay.Empty, 0, false, false), view);
            Assert.AreEqual(TextKeys.GrillSlotEmpty, GrillView.TextKeyOf(view));
            AssertTap(view, true, ShiftTap.PlaceMeat);
        }

        [Test]
        public void EmptySlotWithQueuedPlace_ShowsRaw_WithActionProgress_AndTapSendsNothing()
        {
            GrillSlotView view = Compute(MeatState.Empty, 0L, ShiftActionType.PlaceMeat, 15L);
            Assert.AreEqual(new GrillSlotView(GrillSlotDisplay.Raw, 50, false, true), view);
            Assert.AreEqual(TextKeys.GrillSlotRaw, GrillView.TextKeyOf(view));
            AssertTap(view, false, default(ShiftTap));
        }

        [Test]
        public void RawMeat_ShowsCooking_WithCookProgress_AndTapSendsNothing()
        {
            GrillSlotView view = Compute(MeatState.Raw, 30L);
            Assert.AreEqual(new GrillSlotView(GrillSlotDisplay.Cooking, 50, false, false), view);
            Assert.AreEqual(TextKeys.GrillSlotCooking, GrillView.TextKeyOf(view));
            AssertTap(view, false, default(ShiftTap));
        }

        [Test]
        public void DoneMeat_ShowsDone_WithBurnWindowProgress_AndTapTakesMeat()
        {
            GrillSlotView view = Compute(MeatState.Done, Cook + 10L);
            Assert.AreEqual(new GrillSlotView(GrillSlotDisplay.Done, 10, false, false), view);
            Assert.AreEqual(TextKeys.GrillSlotDone, GrillView.TextKeyOf(view));
            AssertTap(view, true, ShiftTap.TakeMeat);
        }

        [Test]
        public void DoneMeat_InWarningWindow_ShowsBurnWarning_AndTapStillTakesMeat()
        {
            // До пригорання лишилось рівно Warn тактів — межа включна, як у GrillSlot.IsBurnWarning.
            GrillSlotView atEdge = Compute(MeatState.Done, Cook + Burn - Warn);
            Assert.IsTrue(atEdge.BurnWarning);
            Assert.AreEqual(GrillSlotDisplay.Done, atEdge.Display);
            Assert.AreEqual(80, atEdge.ProgressPercent);
            Assert.AreEqual(TextKeys.GrillSlotBurning, GrillView.TextKeyOf(atEdge));
            AssertTap(atEdge, true, ShiftTap.TakeMeat);

            GrillSlotView before = Compute(MeatState.Done, Cook + Burn - Warn - 1L);
            Assert.IsFalse(before.BurnWarning, "за такт до вікна попередження ще немає");
        }

        [Test]
        public void BurntMeat_ShowsBurnt_AndTapDiscards()
        {
            GrillSlotView view = Compute(MeatState.Burnt, Cook + Burn);
            Assert.AreEqual(new GrillSlotView(GrillSlotDisplay.Burnt, 100, false, false), view);
            Assert.AreEqual(TextKeys.GrillSlotBurnt, GrillView.TextKeyOf(view));
            AssertTap(view, true, ShiftTap.DiscardBurntMeat);
        }

        [TestCase(MeatState.Done, ShiftActionType.TakeMeat)]
        [TestCase(MeatState.Burnt, ShiftActionType.DiscardBurntMeat)]
        public void QueuedAction_BlocksRepeatedTap(MeatState state, ShiftActionType queued)
        {
            GrillSlotView view = Compute(state, Cook + Burn, queued);
            Assert.IsTrue(view.ActionQueued);
            AssertTap(view, false, default(ShiftTap));
        }

        [Test]
        public void Progress_IsClampedTo0And100()
        {
            Assert.AreEqual(0, Compute(MeatState.Raw, 0L).ProgressPercent);
            Assert.AreEqual(100, Compute(MeatState.Done, Cook + Burn + 50L).ProgressPercent);
            Assert.AreEqual(0, GrillView.Compute(MeatState.Empty, 0L, Cook, Burn, Warn, ShiftActionType.PlaceMeat, 5L, 0L).ProgressPercent,
                "миттєва дія (0 тактів) не ділить на нуль");
        }

        [Test]
        public void RealLevel_SlotGoesThroughAllStates_TapsReachCoreQueue_AndSnapshotAgrees()
        {
            LevelDefinition level = ConfigTestKit.Level(1);
            var driver = new ShiftDriver(level, Seed);
            Assert.IsTrue(driver.Begin());
            ShiftSimulation sim = driver.Simulation;
            Assert.IsNotNull(sim.Grill, "у рівні 1 є гриль");
            Assert.Greater(sim.Grill.ActionTicks, 0L, "покласти м'ясо триває кілька тактів — видно стан «сире»");
            double tick = 1.0 / level.Shift.TicksPerSecond;
            const int slot = 0;

            AssertViews(driver, slot, GrillSlotDisplay.Empty);
            Assert.IsTrue(TapSlot(driver, slot, ShiftTap.PlaceMeat));
            AssertQueuedHead(sim, ShiftActionType.PlaceMeat, slot);
            Assert.IsFalse(TapSlot(driver, slot, ShiftTap.PlaceMeat), "повторний тап не дублює дію");
            Assert.AreEqual(1, sim.QueuedActionCount);

            driver.Frame(tick);
            AssertViews(driver, slot, GrillSlotDisplay.Raw);

            RunUntil(driver, slot, GrillSlotDisplay.Cooking, tick);
            Assert.AreEqual(0, sim.QueuedActionCount);
            Assert.AreEqual(MeatState.Raw, sim.Grill.GetSlot(slot).State);
            Assert.IsFalse(TapSlot(driver, slot, ShiftTap.TakeMeat), "сире не знімається: тап нічого не шле");
            Assert.AreEqual(0, sim.QueuedActionCount);

            RunUntil(driver, slot, GrillSlotDisplay.Done, tick);
            RunUntil(driver, slot, GrillSlotDisplay.Burnt, tick);
            Assert.IsTrue(TapSlot(driver, slot, ShiftTap.DiscardBurntMeat));
            AssertQueuedHead(sim, ShiftActionType.DiscardBurntMeat, slot);
            Assert.IsTrue(GrillView.FromSimulation(sim, slot).ActionQueued);

            RunUntil(driver, slot, GrillSlotDisplay.Empty, tick);
            Assert.AreEqual(1L, sim.Grill.DiscardedCount);

            // Готове знімається на дошку.
            Assert.IsTrue(TapSlot(driver, slot, ShiftTap.PlaceMeat));
            RunUntil(driver, slot, GrillSlotDisplay.Done, tick);
            Assert.IsTrue(TapSlot(driver, slot, ShiftTap.TakeMeat));
            AssertQueuedHead(sim, ShiftActionType.TakeMeat, slot);
            RunUntil(driver, slot, GrillSlotDisplay.Empty, tick);
            Assert.AreEqual(1L, sim.Grill.TakenCount);
        }

        [Test]
        public void TextKeyOf_EveryDisplay_HasItsOwnKey()
        {
            var keys = new System.Collections.Generic.HashSet<string>();
            foreach (GrillSlotDisplay display in System.Enum.GetValues(typeof(GrillSlotDisplay)))
            {
                Assert.IsTrue(keys.Add(GrillView.TextKeyOf(new GrillSlotView(display, 0, false, false))), display.ToString());
            }

            Assert.IsTrue(keys.Add(GrillView.TextKeyOf(new GrillSlotView(GrillSlotDisplay.Done, 0, true, false))));
            Assert.AreEqual(6, keys.Count);
        }

        /// <summary>Тап так, як його робить екран: команда з моделі вигляду → ShiftDriver.Tap.</summary>
        private static bool TapSlot(ShiftDriver driver, int slot, ShiftTap expected)
        {
            ShiftTap tap;
            if (!GrillView.TryGetTap(GrillView.FromSimulation(driver.Simulation, slot), out tap))
            {
                return false;
            }

            Assert.AreEqual(expected, tap);
            return driver.Tap(tap, slot);
        }

        private static void AssertQueuedHead(ShiftSimulation sim, ShiftActionType type, int slot)
        {
            Assert.GreaterOrEqual(sim.QueuedActionCount, 1);
            ShiftAction head = sim.GetQueuedAction(0);
            Assert.AreEqual(type, head.Type);
            Assert.AreEqual(slot, head.Slot, "команда адресована саме цьому слоту");
        }

        private static GrillSlotView AssertViews(ShiftDriver driver, int slot, GrillSlotDisplay expected)
        {
            GrillSlotView live = GrillView.FromSimulation(driver.Simulation, slot);
            GrillSlotView fromSnapshot = GrillView.FromSnapshot(
                driver.Simulation.CaptureSnapshot(), driver.Level.Shift.Grill, driver.Level.Shift.TicksPerSecond, slot);
            Assert.AreEqual(expected, live.Display, live.ToString());
            Assert.AreEqual(live, fromSnapshot, "показ зі знімка збігається з показом із симуляції");
            return live;
        }

        private static void RunUntil(ShiftDriver driver, int slot, GrillSlotDisplay expected, double tick)
        {
            for (int guard = 0; guard < 100000; guard++)
            {
                GrillSlotView view = GrillView.FromSimulation(driver.Simulation, slot);
                GrillSlotView fromSnapshot = GrillView.FromSnapshot(
                    driver.Simulation.CaptureSnapshot(), driver.Level.Shift.Grill, driver.Level.Shift.TicksPerSecond, slot);
                Assert.AreEqual(view, fromSnapshot, "показ зі знімка збігається з показом із симуляції");
                if (view.Display == expected)
                {
                    return;
                }

                Assert.IsFalse(driver.IsFinished, "зміна скінчилась раніше, ніж слот дійшов до " + expected);
                driver.Frame(tick);
            }

            Assert.Fail("слот не дійшов до " + expected);
        }
    }
}
