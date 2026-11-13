using System.IO;
using System.Text.RegularExpressions;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Екран зміни (S1-09a): верхня смуга (таймер, ціль, монети) з драйвера, підказка ★★,
    /// плашки паузи й підсумку, гриль зі слотами за конфігом рівня; тапи йдуть лише в ShiftDriver.
    /// Елементи UI Toolkit створюються без панелі й без об'єктів сцени.
    /// </summary>
    public sealed class ShiftScreenTests
    {
        // Той самий seed, що в ShiftDriverTests: на ньому бот-експерт рівня 1 гарантовано проходить через підказку ★★.
        private const ulong Seed = 20284958UL;
        private static readonly ILocalizer Text = new PlaceholderLocalizer();
        private const ulong BotMask = 0xB07B07B07B07B07BUL;

        private sealed class Taps
        {
            public int Pause;
            public int Resume;
            public int Exit;
        }

        private static ShiftDriver Started(int levelNumber)
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(levelNumber), Seed);
            Assert.IsTrue(driver.Begin());
            return driver;
        }

        private static ShiftScreen BuildScreen(ShiftDriver driver, Taps taps)
        {
            return new ShiftScreen(
                new VisualElement(),
                driver,
                Text,
                () => taps.Pause++,
                () => taps.Resume++,
                () => taps.Exit++);
        }

        [Test]
        public void Hud_ShowsTimerGoalAndCoins_FromDriver()
        {
            ShiftDriver driver = Started(1);
            ShiftScreen screen = BuildScreen(driver, new Taps());
            LevelDefinition level = driver.Level;

            ShiftHudView hud = ShiftHud.FromDriver(driver);
            Assert.AreEqual((long)level.Shift.DurationSec, hud.RemainingSeconds);
            Assert.AreEqual(GoalType.Serve, hud.GoalType);
            Assert.AreEqual(0L, hud.GoalProgress);
            Assert.AreEqual((long)level.Goal.Target, hud.GoalTarget);
            Assert.AreEqual(0L, hud.Coins);
            Assert.IsFalse(hud.Paused);
            Assert.IsFalse(hud.Finished);

            Assert.AreEqual(EmptyShift.FormatClock(level.Shift.DurationSec), screen.TimerText);
            Assert.AreEqual(string.Format(Text.Get(TextKeys.ShiftHudGoalServe), 0, level.Goal.Target), screen.GoalText);
            Assert.AreEqual(string.Format(Text.Get(TextKeys.ShiftHudCoins), 0), screen.CoinsText);
            Assert.AreEqual(string.Empty, screen.HintText);
            Assert.IsFalse(screen.PausePanelVisible);
            Assert.IsFalse(screen.ResultPanelVisible);
            Assert.IsNotNull(screen.CounterZone, "зона прилавка");
            Assert.IsNotNull(screen.AssemblyZone, "порожня зона збирання (S1-09c)");
            Assert.AreEqual(1, screen.CounterZone.childCount, "S1-09b: у зоні прилавка — панель клієнтів");
            Assert.AreSame(screen.Counter.Root, screen.CounterZone[0]);
            Assert.AreEqual(0, screen.AssemblyZone.childCount);

            driver.Frame(1.0);
            screen.Refresh();
            Assert.AreEqual(EmptyShift.FormatClock(EmptyShift.RemainingSeconds(driver.Simulation)), screen.TimerText);
            Assert.AreNotEqual(EmptyShift.FormatClock(level.Shift.DurationSec), screen.TimerText, "таймер іде");
        }

        [Test]
        public void GoalText_UsesCoinsTemplate_OnCoinsLevel()
        {
            Assert.AreEqual(TextKeys.ShiftHudGoalServe, ShiftHud.GoalTextKey(GoalType.Serve));
            Assert.AreEqual(TextKeys.ShiftHudGoalCoins, ShiftHud.GoalTextKey(GoalType.Coins));

            ShiftDriver driver = Started(4);
            Assert.AreEqual(GoalType.Coins, driver.Level.Goal.Type, "рівень 4 — ціль у монетах");
            ShiftScreen screen = BuildScreen(driver, new Taps());
            Assert.AreEqual(string.Format(Text.Get(TextKeys.ShiftHudGoalCoins), 0, driver.Level.Goal.Target), screen.GoalText);
        }

        [Test]
        public void Grill_HasSlotPerConfig_AndTapGoesToDriverQueue()
        {
            ShiftDriver driver = Started(1);
            ShiftScreen screen = BuildScreen(driver, new Taps());
            GrillPanel grill = screen.Grill;

            Assert.AreEqual(driver.Level.Shift.Grill.Slots, grill.SlotCount);
            Assert.AreEqual(driver.Simulation.Grill.SlotCount, grill.SlotCount);
            Assert.AreEqual(Text.Get(TextKeys.GrillSlotEmpty), grill.GetSlotText(0));

            Assert.IsTrue(grill.TapSlot(0));
            Assert.AreEqual(1, driver.Simulation.QueuedActionCount);
            Assert.AreEqual(new ShiftAction(ShiftActionType.PlaceMeat, 0), driver.Simulation.GetQueuedAction(0));
            Assert.IsFalse(grill.TapSlot(0), "дія для слота вже в черзі");
            Assert.IsFalse(grill.TapSlot(-1));
            Assert.IsFalse(grill.TapSlot(grill.SlotCount));
            Assert.AreEqual(1, driver.Simulation.QueuedActionCount);

            screen.Refresh();
            Assert.AreEqual(GrillSlotDisplay.Raw, grill.GetShownView(0).Display);
            Assert.AreEqual(Text.Get(TextKeys.GrillSlotRaw), grill.GetSlotText(0));
        }

        [Test]
        public void Grill_ShowsEveryStateText_AsSlotChanges()
        {
            ShiftDriver driver = Started(1);
            ShiftScreen screen = BuildScreen(driver, new Taps());
            GrillPanel grill = screen.Grill;
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            var seen = new System.Collections.Generic.HashSet<string>();

            Assert.IsTrue(grill.TapSlot(0));
            for (int guard = 0; guard < 100000 && grill.GetShownView(0).Display != GrillSlotDisplay.Burnt; guard++)
            {
                driver.Frame(tick);
                screen.Refresh();
                seen.Add(grill.GetSlotText(0));
                Assert.AreEqual(GrillView.FromSimulation(driver.Simulation, 0), grill.GetShownView(0));
            }

            CollectionAssert.IsSubsetOf(
                new[]
                {
                    Text.Get(TextKeys.GrillSlotRaw),
                    Text.Get(TextKeys.GrillSlotCooking),
                    Text.Get(TextKeys.GrillSlotDone),
                    Text.Get(TextKeys.GrillSlotBurning),
                    Text.Get(TextKeys.GrillSlotBurnt),
                }, seen);

            Assert.IsTrue(grill.TapSlot(0), "пригоріле — тап викидає");
            Assert.AreEqual(ShiftActionType.DiscardBurntMeat, driver.Simulation.GetQueuedAction(0).Type);
        }

        [Test]
        public void Pause_ShowsPanel_BlocksTaps_AndResumeHidesIt()
        {
            ShiftDriver driver = Started(1);
            var taps = new Taps();
            ShiftScreen screen = BuildScreen(driver, taps);

            driver.SetPaused(true);
            screen.Refresh();
            Assert.IsTrue(screen.PausePanelVisible);
            Assert.IsTrue(ShiftHud.FromDriver(driver).Paused);
            Assert.IsFalse(screen.Grill.TapSlot(0), "на паузі тап не доходить до черги");
            Assert.AreEqual(0, driver.Simulation.QueuedActionCount);

            driver.SetPaused(false);
            screen.Refresh();
            Assert.IsFalse(screen.PausePanelVisible);
            Assert.IsTrue(screen.Grill.TapSlot(0));
            Assert.AreEqual(0, taps.Pause + taps.Resume + taps.Exit, "екран сам нічого не натискає");
        }

        [Test]
        public void ExpertShift_ShowsHintBetweenGoalAndTwoStars_ThenResult()
        {
            ShiftDriver driver = Started(1);
            ShiftScreen screen = BuildScreen(driver, new Taps());
            var bot = new BotPlayer(driver.Simulation, BotProfile.Expert, Seed ^ BotMask);
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            bool hintShown = false;

            for (int guard = 0; guard < 200000 && !driver.IsFinished; guard++)
            {
                bot.Step();
                driver.Frame(tick);
                screen.Refresh();

                ShiftHudView hud = ShiftHud.FromDriver(driver);
                Assert.AreEqual(driver.Simulation.CoinsEarned, hud.Coins);
                Assert.AreEqual(driver.Simulation.ServedCount, hud.GoalProgress);
                Assert.AreEqual(string.Format(Text.Get(TextKeys.ShiftHudCoins), hud.Coins), screen.CoinsText);
                Assert.AreEqual(driver.GoalDoneStarHint, hud.GoalDoneStarHint);
                if (driver.GoalDoneStarHint)
                {
                    hintShown = true;
                    Assert.AreEqual(Text.Get(TextKeys.ShiftGoalDoneStar2), screen.HintText);
                }
                else
                {
                    Assert.AreEqual(string.Empty, screen.HintText);
                }
            }

            Assert.IsTrue(driver.IsFinished);
            Assert.IsTrue(hintShown, "на рівні 1 (endOnGoal=false) підказка ★★ з'являється");
            Assert.IsTrue(screen.ResultPanelVisible);
            Assert.IsFalse(screen.PausePanelVisible);
            Assert.AreEqual(Text.Get(driver.Result.Won ? TextKeys.ShiftResultWon : TextKeys.ShiftResultLost), screen.ResultTitleText);
            Assert.AreEqual(string.Format(Text.Get(TextKeys.ShiftResultStars), driver.Result.Stars), screen.ResultStarsText);
            Assert.AreEqual(string.Format(Text.Get(TextKeys.ShiftResultCoins), driver.Result.RewardCoins), screen.ResultCoinsText);
        }

        [Test]
        public void ScreenTexts_ComeFromLocalizationKeys()
        {
            ShiftDriver driver = Started(1);
            var root = new VisualElement();
            new ShiftScreen(root, driver, Text, () => { }, () => { }, () => { });

            // PlaceholderLocalizer повертає «[ключ]» для невідомого ключа: жодного такого напису на екрані.
            root.Query<TextElement>().ForEach(e =>
                Assert.IsFalse(e.text != null && e.text.StartsWith("["), "немає перекладу: " + e.text));
        }

        [Test]
        public void Views_SendTapsOnlyThroughShiftDriver()
        {
            string folder = Path.Combine(Application.dataPath, "_Project", "Game", "Views");
            var direct = new Regex(
                @"\.(PlaceMeat|TakeMeat|DiscardBurntMeat|AddIngredient|RemoveIngredient|ClearBoard|ServeDish|CancelQueuedAction|Tick|Advance)\s*\(");
            string[] files = Directory.GetFiles(folder, "*.cs");
            Assert.Greater(files.Length, 0);
            foreach (string file in files)
            {
                string source = File.ReadAllText(file);
                Assert.IsFalse(direct.IsMatch(source), Path.GetFileName(file) + ": команди Core лише через ShiftDriver.Tap");
            }
        }
    }
}
