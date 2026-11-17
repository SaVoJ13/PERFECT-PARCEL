using System;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Видиме комбо (Q-11, P3_SPEC розд. 3): стан смужки вікна, «наступний бонус», причина скидання, «+Y» комбо
    /// та іскра за знімками стану Core. Правил гри не змінює: усе читається з публічних полів Core.
    /// </summary>
    public sealed class ComboViewTests
    {
        private const ulong Seed = 20284958UL;
        private static readonly ILocalizer Text = new PlaceholderLocalizer();
        private float _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = 100f;
            UiMotion.Reduced = false;
            UiMotion.Clock = () => _clock;
        }

        [TearDown]
        public void TearDown()
        {
            UiMotion.Reduced = false;
            UiMotion.Clock = () => Time.realtimeSinceStartup;
        }

        private static ShiftScreen BuildScreen(int level, out ShiftDriver driver)
        {
            driver = new ShiftDriver(ConfigTestKit.Level(level), Seed);
            Assert.IsTrue(driver.Begin());
            return new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });
        }

        /// <summary>Бот грає лише тапами, поки не буде першої вдалої видачі (комбо 1); далі бот мовчить.</summary>
        private static void PlayUntilFirstServe(ShiftDriver driver, ShiftScreen screen)
        {
            var bot = new UiBot(driver, new ScreenTaps(screen));
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            for (int guard = 0; guard < 100000 && driver.Simulation.Counter.Combo < 1L; guard++)
            {
                Assert.IsFalse(driver.IsFinished, "зміна скінчилась до першої видачі");
                bot.Step();
                driver.Frame(tick);
                screen.Refresh();
            }

            Assert.AreEqual(1L, driver.Simulation.Counter.Combo, "перша видача дала комбо 1");
        }

        private static void IdleFrame(ShiftDriver driver, ShiftScreen screen)
        {
            driver.Frame(1.0 / driver.Level.Shift.TicksPerSecond);
            screen.Refresh();
        }

        [TestCase(0L, 60L, 100)]
        [TestCase(1L, 60L, 98)]
        [TestCase(30L, 60L, 50)]
        [TestCase(59L, 60L, 2)]
        [TestCase(60L, 60L, 0)]
        [TestCase(75L, 60L, 0)]
        [TestCase(-3L, 60L, 100)]
        [TestCase(5L, 0L, 0)]
        public void BarPercent_IsRemainingShareOfWindow(long since, long window, int expected)
        {
            Assert.AreEqual(expected, ComboView.BarPercent(since, window));
        }

        [Test]
        public void NextBonus_FollowsCoreFormula_UpToTheMax()
        {
            ShiftDriver driver;
            BuildScreen(5, out driver);
            CounterConfig config = driver.Simulation.Counter.Config;
            Assert.IsTrue(ComboView.IsAvailable(config), "рівень 5: комбо за конфігом доступне");
            Assert.IsFalse(ComboView.IsAvailable(null));
            for (long combo = 1L; combo <= config.ComboMaxSteps + 3L; combo++)
            {
                long steps = Math.Min(combo, config.ComboMaxSteps);
                Assert.AreEqual(steps * config.ComboStepPercent, ComboView.NextBonusPercent(combo, config), "комбо " + combo);
                Assert.AreEqual(combo >= config.ComboMaxSteps + 1L, ComboView.IsAtMax(combo, config), "межа, комбо " + combo);
            }
        }

        [Test]
        public void LostReason_HasAKeyEach_AndNoneHasNone()
        {
            Assert.IsNull(ComboView.LostTextKey(ComboLostReason.None));
            Assert.AreEqual(TextKeys.ComboLostExpired, ComboView.LostTextKey(ComboLostReason.Expired));
            Assert.AreEqual(TextKeys.ComboLostWrongDish, ComboView.LostTextKey(ComboLostReason.WrongDish));
            Assert.AreEqual(TextKeys.ComboLostLeft, ComboView.LostTextKey(ComboLostReason.CustomerLeft));
        }

        [Test]
        public void BeforeAnyServe_NoComboIsShown()
        {
            ShiftDriver driver;
            ShiftScreen screen = BuildScreen(1, out driver);
            screen.Refresh();
            Assert.IsFalse(screen.Assembly.Combo.Visible, "ланцюга ще немає — нічого не показуємо");
            Assert.IsFalse(screen.Assembly.Combo.ReasonVisible);
            Assert.AreEqual(string.Empty, screen.Assembly.ComboText);
            Assert.AreEqual(string.Empty, screen.Assembly.Combo.NextText);
        }

        [Test]
        public void AfterFirstServe_BarIsFull_AndNextBonusIsShown()
        {
            ShiftDriver driver;
            ShiftScreen screen = BuildScreen(5, out driver);
            PlayUntilFirstServe(driver, screen);
            CounterConfig config = driver.Simulation.Counter.Config;
            ComboMeter meter = screen.Assembly.Combo;
            Assert.IsTrue(meter.Visible);
            Assert.GreaterOrEqual(meter.BarPercent, 95, "вікно щойно відкрилось");
            Assert.AreEqual(
                string.Format(Text.Get(TextKeys.ComboNext), ComboView.NextBonusPercent(1L, config)), meter.NextText);
            Assert.AreEqual(string.Empty, meter.ComboText, "«Комбо xN» — від другої видачі (AssemblyView.ComboShownFrom)");
            Assert.IsFalse(meter.WarningShown);
        }

        [Test]
        public void IdleAfterServe_BarDrains_Warns_ThenExpiresWithReason()
        {
            ShiftDriver driver;
            ShiftScreen screen = BuildScreen(5, out driver);
            PlayUntilFirstServe(driver, screen);
            Counter counter = driver.Simulation.Counter;
            long leftBefore = counter.LeftCount;
            long wrongBefore = counter.WrongDishes;
            ComboMeter meter = screen.Assembly.Combo;
            int tps = driver.Level.Shift.TicksPerSecond;
            int previous = meter.BarPercent;
            bool warned = false;
            for (int guard = 0; guard < 100 * tps && counter.Combo > 0L; guard++)
            {
                IdleFrame(driver, screen);
                if (counter.Combo == 0L)
                {
                    break;
                }

                Assert.LessOrEqual(meter.BarPercent, previous, "смужка лише спадає");
                previous = meter.BarPercent;
                warned |= meter.WarningShown;
            }

            Assert.AreEqual(0L, counter.Combo, "вікно спливло");
            Assert.AreEqual(leftBefore, counter.LeftCount, "причина — саме спливання, а не клієнт, що пішов");
            Assert.AreEqual(wrongBefore, counter.WrongDishes);
            Assert.IsTrue(warned, "в останні 2 с смужка попереджала");
            Assert.IsFalse(meter.Visible, "ланцюга немає — смужки немає");
            Assert.IsTrue(meter.ReasonVisible);
            Assert.AreEqual(Text.Get(TextKeys.ComboLostExpired), meter.ReasonText);

            for (int i = 0; i < ((ComboView.LostShowMs / 1000) + 1) * tps; i++)
            {
                IdleFrame(driver, screen);
            }

            Assert.IsFalse(meter.ReasonVisible, "причину показано недовго");
            Assert.AreEqual(string.Empty, meter.ReasonText);
        }

        [Test]
        public void WarningBlink_PulsesOpacity_ButReducedMotionKeepsItStatic()
        {
            ShiftDriver driver;
            ShiftScreen screen = BuildScreen(5, out driver);
            PlayUntilFirstServe(driver, screen);
            Counter counter = driver.Simulation.Counter;
            ComboMeter meter = screen.Assembly.Combo;
            int tps = driver.Level.Shift.TicksPerSecond;
            for (int guard = 0; guard < 100 * tps && !meter.WarningShown && counter.Combo > 0L; guard++)
            {
                IdleFrame(driver, screen);
            }

            Assert.IsTrue(meter.WarningShown, "смужка у стані «спливає»");
            _clock = 100.125f; // пік хвилі мерехтіння
            screen.Refresh();
            Assert.Less(meter.BarMotion.Opacity, 1f, "мерехтить");

            UiMotion.Reduced = true;
            screen.Refresh();
            Assert.AreEqual(1f, meter.BarMotion.Opacity, 0.0001f, "«менше анімацій»: без мерехтіння");
            Assert.IsTrue(meter.WarningShown, "тривога лишається кольором смужки");
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void ExpertBot_TrackerFollowsCore_EveryFrame_AndReasonsMatchDeltas(int level)
        {
            ShiftDriver driver;
            ShiftScreen screen = BuildScreen(level, out driver);
            var bot = new UiBot(driver, new ScreenTaps(screen));
            var tracker = new ComboTracker(driver.Simulation);
            Counter counter = driver.Simulation.Counter;
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            long prevCombo = 0L;
            long prevWrong = 0L;
            long prevLeft = 0L;
            int lostSeen = 0;
            int previousPercent = 100;
            long previousServeTick = -1L;
            for (int guard = 0; guard < 400000 && !driver.IsFinished; guard++)
            {
                bot.Step();
                driver.Frame(tick);
                screen.Refresh();
                tracker.Update(driver.Simulation);
                ComboHudView hud = tracker.Hud;

                Assert.AreEqual(counter.Combo, hud.Combo);
                Assert.AreEqual(counter.Combo >= 1L, hud.BarShown);
                Assert.That(hud.BarPercent, Is.InRange(0, 100));
                if (hud.BarShown && counter.LastServeTick == previousServeTick && counter.Combo == prevCombo)
                {
                    Assert.LessOrEqual(hud.BarPercent, previousPercent, "смужка без видачі лише спадає");
                }

                previousPercent = hud.BarPercent;
                previousServeTick = counter.LastServeTick;
                if (prevCombo > 0L && counter.Combo == 0L)
                {
                    ComboLostReason expected = counter.WrongDishes != prevWrong
                        ? ComboLostReason.WrongDish
                        : (counter.LeftCount != prevLeft ? ComboLostReason.CustomerLeft : ComboLostReason.Expired);
                    Assert.AreEqual(expected, tracker.Lost);
                    lostSeen++;
                }

                prevCombo = counter.Combo;
                prevWrong = counter.WrongDishes;
                prevLeft = counter.LeftCount;
                Assert.AreEqual(lostSeen, tracker.LostCount);
                Assert.AreEqual(hud.BarShown, screen.Assembly.Combo.Visible, "панель показує те, що каже знімок");
            }

            Assert.IsTrue(driver.IsFinished);
            TestContext.WriteLine("L0" + level + ": maxCombo=" + counter.MaxCombo + " lost=" + lostSeen);
        }

        [Test]
        public void CounterFeed_CarriesComboBonus_AndComboAfterServe()
        {
            ShiftDriver driver;
            ShiftScreen screen = BuildScreen(5, out driver);
            ShiftSimulation sim = driver.Simulation;
            Counter counter = sim.Counter;
            var feed = new CounterFeed(sim);
            var bot = new UiBot(driver, new ScreenTaps(screen));
            double tick = 1.0 / driver.Level.Shift.TicksPerSecond;
            long served = 0L;
            long comboCoins = 0L;
            bool sawBonus = false;
            for (int guard = 0; guard < 400000 && !driver.IsFinished; guard++)
            {
                bot.Step();
                driver.Frame(tick);
                feed.Update(sim);
                long servedNow = counter.ServedCount;
                long bonusDelta = counter.ComboCoins - comboCoins;
                if (servedNow - served == 1L)
                {
                    bool found = false;
                    for (int slot = 0; slot < feed.SlotCount; slot++)
                    {
                        CounterEvent e = feed.GetEvent(slot);
                        if (e.Kind == CounterEventKind.Served && e.Combo == counter.Combo && e.BonusCoins == bonusDelta)
                        {
                            found = true;
                        }
                    }

                    Assert.IsTrue(found, "подія видачі знає комбо і надбавку");
                    sawBonus |= bonusDelta > 0L;
                }

                served = servedNow;
                comboCoins = counter.ComboCoins;
            }

            Assert.IsTrue(sawBonus, "бот на L05 хоч раз отримав надбавку комбо");
        }

        [Test]
        public void Payment_ShowsComboBonusAsSeparateText_EvenWithReducedMotion()
        {
            var fx = new FxLayer();
            fx.Payment(null, 26L, 0L, 6L);
            Assert.AreEqual(2, fx.ActiveOf(FxKind.Text));
            Assert.AreEqual("+26", fx.TextOf(0));
            Assert.AreEqual("+6", fx.TextOf(1));

            UiMotion.Reduced = true;
            var calm = new FxLayer();
            Assert.AreEqual(0, calm.Payment(null, 26L, 0L, 6L), "монет-частинок немає");
            Assert.AreEqual(2, calm.ActiveOf(FxKind.Text), "числа видно і без руху");
            Assert.AreEqual(0, calm.Sparks(null), "іскри в «менше анімацій» не створюються");
        }

        [Test]
        public void Sparks_StayWithinParticleBudget()
        {
            var fx = new FxLayer();
            for (int i = 0; i < 10; i++)
            {
                fx.Sparks(null);
            }

            Assert.LessOrEqual(fx.ActiveCount, 150, "≤150 частинок (QUALITY_BAR)");
            Assert.AreEqual(3L, ComboView.SparksFrom);
        }
    }
}
