using System;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// S1-21 (ADR-027): відгук на тап. Модель підсвічування (<see cref="TapFlash"/>), прогрес дії
    /// (<see cref="ActionProgressView"/>) і їхній показ у панелях. Затримка <c>actionTimeSec</c> і Core
    /// не змінюються: тести лише читають тривалості, які віддає Core.
    /// </summary>
    public sealed class TapFeedbackTests
    {
        private const ulong Seed = 20284958UL;
        private static readonly ILocalizer Text = new PlaceholderLocalizer();

        private static ShiftScreen Build(int levelNumber, out ShiftDriver driver)
        {
            driver = new ShiftDriver(ConfigTestKit.Level(levelNumber), Seed);
            Assert.IsTrue(driver.Begin());
            return new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });
        }

        private static void Step(ShiftDriver driver, ShiftScreen screen, int ticks)
        {
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            for (int i = 0; i < ticks; i++)
            {
                driver.Frame(tick);
                screen.Refresh();
            }
        }

        private static void Settle(ShiftDriver driver, ShiftScreen screen)
        {
            for (int guard = 0; guard < 10000 && driver.Simulation.QueuedActionCount > 0; guard++)
            {
                Step(driver, screen, 1);
            }

            Assert.AreEqual(0, driver.Simulation.QueuedActionCount, "черга дій спорожніла");
        }

        private static ShiftSimulation SimOf(ShiftDriver driver)
        {
            return driver.Simulation;
        }

        [Test]
        public void TapFlash_LightsElementForFlashTicksOnly()
        {
            var flash = new TapFlash(3, 4, 5, 2, 9L);
            Assert.IsFalse(flash.IsLit(FeedbackTarget.GrillSlot, 1, 100L));
            Assert.IsTrue(flash.Mark(FeedbackTarget.GrillSlot, 1, 100L));
            Assert.IsTrue(flash.IsLit(FeedbackTarget.GrillSlot, 1, 100L), "світиться в той самий момент (≤ 1 кадр)");
            Assert.IsTrue(flash.IsLit(FeedbackTarget.GrillSlot, 1, 108L));
            Assert.IsFalse(flash.IsLit(FeedbackTarget.GrillSlot, 1, 109L), "згасає рівно через FlashTicks");
            Assert.IsFalse(flash.IsLit(FeedbackTarget.GrillSlot, 0, 100L), "сусідній слот не світиться");
            Assert.IsFalse(flash.IsLit(FeedbackTarget.Station, 1, 100L), "інший вид елемента не світиться");
        }

        [Test]
        public void TapFlash_RepeatedTapExtendsGlow_AndManyElementsGlowTogether()
        {
            var flash = new TapFlash(3, 4, 5, 2, 10L);
            flash.Mark(FeedbackTarget.Station, 2, 0L);
            flash.Mark(FeedbackTarget.Clear, 0, 5L);
            flash.Mark(FeedbackTarget.Station, 2, 8L);
            Assert.IsTrue(flash.IsLit(FeedbackTarget.Station, 2, 17L), "новий тап подовжив світіння");
            Assert.IsTrue(flash.IsLit(FeedbackTarget.Clear, 0, 14L));
            Assert.IsFalse(flash.IsLit(FeedbackTarget.Clear, 0, 15L));
        }

        [Test]
        public void TapFlash_RejectsUnknownElementsAndBadArguments()
        {
            var flash = new TapFlash(2, 2, 2, 2, 5L);
            Assert.IsFalse(flash.Mark(FeedbackTarget.None, 0, 0L));
            Assert.IsFalse(flash.Mark(FeedbackTarget.GrillSlot, 2, 0L));
            Assert.IsFalse(flash.Mark(FeedbackTarget.Customer, -1, 0L));
            Assert.IsFalse(flash.Mark(FeedbackTarget.Clear, 1, 0L));
            Assert.IsFalse(flash.IsLit((FeedbackTarget)99, 0, 0L));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TapFlash(-1, 0, 0, 0, 5L));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TapFlash(1, 1, 1, 1, 0L));
        }

        [Test]
        public void Progress_NoActionInQueue_IsInactive()
        {
            ShiftDriver driver;
            ShiftScreen screen = Build(1, out driver);
            ActionProgressView view = ActionProgressView.FromSimulation(SimOf(driver));
            Assert.IsFalse(view.Active);
            Assert.AreEqual(FeedbackTarget.None, view.Target);
            Assert.IsFalse(view.Is(FeedbackTarget.None, 0));
            Assert.AreEqual(-1, screen.Input.ProgressOf(FeedbackTarget.GrillSlot, 0));
        }

        [Test]
        public void Progress_GrillPlaceMeat_GrowsFromZeroUntilDone_ThenDisappears()
        {
            ShiftDriver driver;
            ShiftScreen screen = Build(1, out driver);
            ShiftSimulation sim = SimOf(driver);
            long duration = sim.Grill.ActionTicks;
            Assert.Greater(duration, 1L, "станція гриля має ненульову тривалість дії (stations.json)");

            Assert.IsTrue(screen.Grill.TapSlot(0));
            screen.Refresh();
            ActionProgressView view = ActionProgressView.FromSimulation(sim);
            Assert.IsTrue(view.Is(FeedbackTarget.GrillSlot, 0));
            Assert.AreEqual(0, view.Percent, "смужка з'являється відразу після тапу");
            Assert.AreEqual(0, screen.Input.ProgressOf(FeedbackTarget.GrillSlot, 0));
            Assert.AreEqual(0, screen.Grill.GetDecor(0).Percent, "елемент показує смужку в тому ж кадрі");

            Step(driver, screen, (int)(duration / 2));
            long done = sim.HeadActionProgressTicks;
            Assert.Greater(done, 0L);
            Assert.AreEqual((int)(done * 100L / duration), screen.Input.ProgressOf(FeedbackTarget.GrillSlot, 0));
            Assert.Greater(screen.Grill.GetDecor(0).Percent, 0);
            Assert.Less(screen.Grill.GetDecor(0).Percent, 100);
            Assert.AreEqual(-1, screen.Input.ProgressOf(FeedbackTarget.GrillSlot, 1), "інший слот без смужки");

            Settle(driver, screen);
            Assert.AreEqual(0, sim.QueuedActionCount, "дію виконано");
            Assert.IsFalse(ActionProgressView.FromSimulation(sim).Active);
            Assert.AreEqual(-1, screen.Grill.GetDecor(0).Percent, "після виконання смужка зникає");
        }

        [Test]
        public void Progress_Paused_DoesNotAdvance()
        {
            ShiftDriver driver;
            ShiftScreen screen = Build(1, out driver);
            ShiftSimulation sim = SimOf(driver);
            long duration = sim.Grill.ActionTicks;
            Assert.IsTrue(screen.Grill.TapSlot(0));
            Step(driver, screen, (int)(duration / 2));
            int before = screen.Grill.GetDecor(0).Percent;
            Assert.Greater(before, 0);

            driver.SetPaused(true);
            Step(driver, screen, 50);
            Assert.AreEqual(before, screen.Grill.GetDecor(0).Percent, "на паузі смужка стоїть");
            Assert.AreEqual(1, sim.QueuedActionCount);

            driver.SetPaused(false);
            Step(driver, screen, 1);
            Settle(driver, screen);
        }

        [Test]
        public void Progress_SecondTapWaitsInQueue_BarStaysOnFirstAction()
        {
            ShiftDriver driver;
            ShiftScreen screen = Build(2, out driver);
            ShiftSimulation sim = SimOf(driver);
            long duration = sim.Grill.ActionTicks;
            Assert.IsTrue(screen.Grill.TapSlot(0));
            Assert.IsTrue(screen.Grill.TapSlot(1));
            Step(driver, screen, (int)(duration / 2));
            Assert.AreEqual(2, sim.QueuedActionCount);
            Assert.IsTrue(screen.Input.ProgressOf(FeedbackTarget.GrillSlot, 0) > 0, "смужка на першій дії");
            Assert.AreEqual(-1, screen.Input.ProgressOf(FeedbackTarget.GrillSlot, 1), "друга дія ще в черзі");

            while (sim.QueuedActionCount == 2)
            {
                Step(driver, screen, 1);
            }

            Assert.AreEqual(-1, screen.Input.ProgressOf(FeedbackTarget.GrillSlot, 0), "перша дія виконана - смужки на ній немає");
            Assert.GreaterOrEqual(screen.Input.ProgressOf(FeedbackTarget.GrillSlot, 1), 0, "смужка перейшла на другу дію");
        }

        [Test]
        public void Progress_CancelHeadAction_ResetsBar()
        {
            ShiftDriver driver;
            ShiftScreen screen = Build(1, out driver);
            ShiftSimulation sim = SimOf(driver);
            Assert.IsTrue(screen.Grill.TapSlot(0));
            Step(driver, screen, (int)(sim.Grill.ActionTicks / 2));
            Assert.IsTrue(screen.Input.TapQueuedAction(0));
            Step(driver, screen, 1);
            Assert.AreEqual(-1, screen.Grill.GetDecor(0).Percent);
            Assert.IsFalse(ActionProgressView.FromSimulation(sim).Active);
        }

        [Test]
        public void Progress_InstantAction_HasNoBar_ButStillFlashes()
        {
            ShiftDriver driver;
            ShiftScreen screen = Build(4, out driver);
            ShiftSimulation sim = SimOf(driver);
            Assert.AreEqual(0L, sim.Board.BoardActionTicks, "зняття з дошки миттєве (stations.json: board 0 с)");

            Assert.IsTrue(screen.Assembly.TapStation(0));
            Settle(driver, screen);
            Assert.AreEqual(1, sim.Board.ItemCount);

            Assert.IsTrue(screen.Assembly.TapBoardItem(0));
            screen.Refresh();
            Assert.IsFalse(ActionProgressView.FromSimulation(sim).Active, "миттєва дія не має смужки");
            Assert.AreEqual(-1, screen.Input.ProgressOf(FeedbackTarget.BoardItem, 0));
            Assert.IsTrue(screen.Input.IsLit(FeedbackTarget.BoardItem, 0), "але підсвічується");
        }

        [Test]
        public void Station_TapLightsButtonImmediately_ShowsBar_ThenGlowFades()
        {
            ShiftDriver driver;
            ShiftScreen screen = Build(4, out driver);
            ShiftSimulation sim = SimOf(driver);
            int ingredient = screen.Assembly.GetStationIngredient(0);
            long duration = sim.Board.AddActionTicks(ingredient);
            Assert.Greater(duration, 1L);

            Assert.IsFalse(screen.Assembly.GetStationDecor(0).Lit);
            Assert.IsTrue(screen.Assembly.TapStation(0));
            screen.Refresh();
            Assert.IsTrue(screen.Assembly.GetStationDecor(0).Lit, "рамка в тому ж кадрі, що й тап");
            Assert.AreEqual(0, screen.Assembly.GetStationDecor(0).Percent);

            Step(driver, screen, (int)duration / 2);
            Assert.Greater(screen.Assembly.GetStationDecor(0).Percent, 0);

            Settle(driver, screen);
            Step(driver, screen, (int)screen.Input.Flash.FlashTicks + 1);
            Assert.IsFalse(screen.Assembly.GetStationDecor(0).Lit, "рамка згасла");
            Assert.AreEqual(-1, screen.Assembly.GetStationDecor(0).Percent);
        }

        [Test]
        public void Clear_BinTapShowsBarOnClearButton()
        {
            ShiftDriver driver;
            ShiftScreen screen = Build(4, out driver);
            ShiftSimulation sim = SimOf(driver);
            Assert.IsTrue(sim.Board.IsBinAvailable);
            Assert.IsTrue(screen.Assembly.TapStation(0));
            Settle(driver, screen);

            Assert.IsTrue(screen.Assembly.TapClear());
            screen.Refresh();
            Assert.IsTrue(screen.Assembly.ClearDecor.Lit);
            Assert.AreEqual(0, screen.Assembly.ClearDecor.Percent);
            Step(driver, screen, (int)(sim.Board.BinActionTicks / 2));
            Assert.Greater(screen.Assembly.ClearDecor.Percent, 0);
            Settle(driver, screen);
            Assert.AreEqual(-1, screen.Assembly.ClearDecor.Percent);
            Assert.AreEqual(0, sim.Board.ItemCount);
        }

        [Test]
        public void Customer_TapLightsCard()
        {
            ShiftDriver driver;
            ShiftScreen screen = Build(2, out driver);
            Assert.IsFalse(screen.Counter.GetDecor(0).Lit);
            screen.Counter.TapSlot(0);
            screen.Refresh();
            Assert.IsTrue(screen.Counter.GetDecor(0).Lit);
            Assert.IsFalse(screen.Counter.GetDecor(1).Lit);
        }

        [Test]
        public void Tap_WhilePaused_DoesNotLightAnything()
        {
            ShiftDriver driver;
            ShiftScreen screen = Build(1, out driver);
            driver.SetPaused(true);
            Assert.IsFalse(screen.Grill.TapSlot(0));
            screen.Refresh();
            Assert.IsFalse(screen.Grill.GetDecor(0).Lit);
        }

        [Test]
        public void ActionTime_IsNotChangedByFeedback()
        {
            ShiftDriver driver;
            Build(1, out driver);
            Assert.AreEqual(TickTime.FromMs(500, driver.Level.Shift.TicksPerSecond), driver.Simulation.Grill.ActionTicks, "0,5 с, як у stations.json (ADR-027)");
        }

        [Test]
        public void TapFrameColor_ContrastsWithEveryBackground()
        {
            Color[] backgrounds =
            {
                UiColors.StationButton, UiColors.ItemButton, UiColors.ClearButton, UiColors.CustomerCard,
                UiColors.CustomerFreeCard, UiColors.GrillEmpty, UiColors.GrillRaw, UiColors.GrillCooking,
                UiColors.GrillDone, UiColors.GrillWarning, UiColors.GrillBurnt,
            };
            foreach (Color background in backgrounds)
            {
                Assert.GreaterOrEqual(UiColors.Contrast(UiColors.TapFrameFor(background), background), 3f);
            }

            Assert.AreEqual(UiColors.Ink, UiColors.TapFrameFor(UiColors.GrillWarning));
            Assert.AreEqual(UiColors.Saffron, UiColors.TapFrameFor(UiColors.GrillBurnt));
        }
    }
}
