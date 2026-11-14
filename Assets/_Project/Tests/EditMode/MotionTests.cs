using System;
using System.Collections.Generic;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Рух клієнтів і HUD (Q-05c, QB-SHIFT-02/03/04/09/10/13): чисті функції анімацій (тривалості, межі,
    /// вимкнення в режимі «менше анімацій»), відсутність зайвих записів стилю й дублювання, поведінка
    /// екрана з підміненим годинником. Правил гри не чіпає: події Core лише читаються.
    /// </summary>
    public sealed class MotionTests
    {
        private const ulong Seed = 4410337UL;
        private const float Eps = 0.0001f;
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

        private static ShiftDriver Started(LevelDefinition level)
        {
            var driver = new ShiftDriver(level, Seed);
            Assert.IsTrue(driver.Begin());
            return driver;
        }

        private static ShiftScreen BuildScreen(ShiftDriver driver)
        {
            return new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });
        }

        private void Step(ShiftDriver driver, ShiftScreen screen)
        {
            double dt = 1.0 / driver.Level.Shift.TicksPerSecond;
            driver.Frame(dt);
            _clock += (float)dt;
            screen.Refresh();
        }

        // ---- чисті функції ----

        [Test]
        public void Breath_StaysInRange_HasPeriodAndPhaseShift()
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            for (float t = 0f; t < UiMotion.BreathPeriodSec; t += 0.01f)
            {
                float v = UiMotion.Breath(t, 0f);
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }

            Assert.GreaterOrEqual(min, 1f - Eps);
            Assert.LessOrEqual(max, 1f + UiMotion.BreathAmplitude + Eps);
            Assert.Greater(max - min, UiMotion.BreathAmplitude * 0.9f, "дихання помітне");
            Assert.That(UiMotion.BreathPeriodSec, Is.InRange(1.5f, 2.5f));
            Assert.AreEqual(UiMotion.Breath(0.3f, 0f), UiMotion.Breath(0.3f + UiMotion.BreathPeriodSec, 0f), Eps, "період");
            Assert.AreNotEqual(UiMotion.Breath(0.3f, 0f), UiMotion.Breath(0.3f, 0.37f), "фаза різна");
        }

        [Test]
        public void TimerWarning_OnlyInLastTenSeconds_AndPulsesOncePerSecond()
        {
            Assert.IsFalse(UiMotion.IsTimerWarning(11));
            Assert.IsTrue(UiMotion.IsTimerWarning(10));
            Assert.IsTrue(UiMotion.IsTimerWarning(1));
            Assert.IsFalse(UiMotion.IsTimerWarning(0), "зміна скінчилась");

            Assert.AreEqual(1f, UiMotion.TimerPulse(0.5f, 11), Eps, "до попередження без пульсу");
            Assert.AreEqual(1f, UiMotion.TimerPulse(0f, 5), Eps, "імпульс стартує з 1");
            Assert.AreEqual(UiMotion.TimerPulsePeak, UiMotion.TimerPulse(0.5f, 5), Eps, "пік 1,12 посередині секунди");
            Assert.AreEqual(1f, UiMotion.TimerPulse(1f, 5), Eps, "наступна секунда - новий імпульс, а не затяжний");
            Assert.AreEqual(1.12f, UiMotion.TimerPulsePeak, Eps);
        }

        [Test]
        public void Pop_GoalPeaksAt125_InsideTwoHundredToTwoFiftyMs()
        {
            Assert.That(UiMotion.GoalPopSec, Is.InRange(0.2f, 0.25f));
            Assert.AreEqual(1f, UiMotion.Pop(0f, UiMotion.GoalPopSec, UiMotion.GoalPopPeak), Eps);
            Assert.AreEqual(UiMotion.GoalPopPeak, UiMotion.Pop(UiMotion.GoalPopSec / 2f, UiMotion.GoalPopSec, UiMotion.GoalPopPeak), Eps);
            Assert.AreEqual(1f, UiMotion.Pop(UiMotion.GoalPopSec, UiMotion.GoalPopSec, UiMotion.GoalPopPeak), Eps);
            Assert.AreEqual(1f, UiMotion.Pop(-1f, UiMotion.GoalPopSec, UiMotion.GoalPopPeak), Eps, "до старту - спокій");
        }

        [Test]
        public void CountUp_IsMonotonic_ReachesTarget_InThreeToFiveHundredMs()
        {
            Assert.That(UiMotion.CoinCountSec, Is.InRange(0.3f, 0.5f));
            long last = 10;
            Assert.AreEqual(10, UiMotion.CountUp(10, 110, 0f, UiMotion.CoinCountSec));
            for (float t = 0f; t < UiMotion.CoinCountSec; t += 0.01f)
            {
                long v = UiMotion.CountUp(10, 110, t, UiMotion.CoinCountSec);
                Assert.GreaterOrEqual(v, last, "число лише зростає");
                Assert.LessOrEqual(v, 110);
                last = v;
            }

            Assert.AreEqual(110, UiMotion.CountUp(10, 110, UiMotion.CoinCountSec, UiMotion.CoinCountSec));
            Assert.AreEqual(110, UiMotion.CountUp(10, 110, 5f, UiMotion.CoinCountSec));
        }

        [Test]
        public void Shake_AmplitudeAndFrequency_WithinLimits()
        {
            Assert.LessOrEqual(UiMotion.ShakeHz, 3f, "QB-ALL-13: не більше 3 спалахів/с");
            Assert.LessOrEqual(UiMotion.ShakeAmplitudePx, 3f);
            float peak = 0f;
            int crossings = 0;
            float prev = UiMotion.Shake(0.001f);
            for (float t = 0.002f; t < 1f; t += 0.001f)
            {
                float v = UiMotion.Shake(t);
                peak = Mathf.Max(peak, Mathf.Abs(v));
                if (v * prev < 0f)
                {
                    crossings++;
                }

                prev = v;
            }

            Assert.LessOrEqual(peak, UiMotion.ShakeAmplitudePx + Eps);
            Assert.LessOrEqual(crossings, 6, "<= 3 повних циклів за секунду");
        }

        [Test]
        public void Hop_GoesUpTwelvePx_AndReturns_InHundredFiftyMs()
        {
            Assert.AreEqual(0f, UiMotion.Hop(0f), Eps);
            Assert.AreEqual(-12f, UiMotion.Hop(UiMotion.HopSec / 2f), Eps);
            Assert.AreEqual(0f, UiMotion.Hop(UiMotion.HopSec), Eps);
            Assert.AreEqual(0f, UiMotion.Hop(-1f), Eps);
            Assert.AreEqual(0.15f, UiMotion.HopSec, Eps);
        }

        [Test]
        public void ArriveAndLeave_AreInThreeToFiveHundredMs_AndEndAtRest()
        {
            Assert.That(UiMotion.ArriveSec, Is.InRange(0.3f, 0.5f));
            Assert.AreEqual(-UiMotion.ArriveOffsetPx, UiMotion.SlideIn(0f), Eps, "виїзд починається збоку");
            Assert.AreEqual(0f, UiMotion.SlideIn(UiMotion.ArriveSec), Eps);
            Assert.AreEqual(0f, UiMotion.ArriveOpacity(0f), Eps);
            Assert.AreEqual(1f, UiMotion.ArriveOpacity(UiMotion.ArriveSec), Eps);
            Assert.AreEqual(0.4f, UiMotion.LeaveSec, Eps);
            Assert.AreEqual(1f, UiMotion.LeaveOpacity(0f), Eps);
            Assert.AreEqual(0.5f, UiMotion.LeaveOpacity(UiMotion.LeaveSec / 2f), Eps);
            Assert.Greater(UiMotion.LeaveOffset(UiMotion.LeaveSec * 0.99f), UiMotion.LeaveOffset(UiMotion.LeaveSec * 0.5f));
            Assert.AreEqual(0f, UiMotion.LeaveOffset(UiMotion.LeaveSec), Eps);
        }

        [Test]
        public void ReducedMotion_ReturnsRestValues_ForEveryFunction()
        {
            UiMotion.Reduced = true;
            Assert.AreEqual(1f, UiMotion.Breath(0.5f, 0.2f), Eps);
            Assert.AreEqual(1f, UiMotion.TimerPulse(0.5f, 5), Eps);
            Assert.AreEqual(1f, UiMotion.Pop(0.1f, UiMotion.GoalPopSec, UiMotion.GoalPopPeak), Eps);
            Assert.AreEqual(0f, UiMotion.Hop(0.07f), Eps);
            Assert.AreEqual(0f, UiMotion.Shake(0.1f), Eps);
            Assert.AreEqual(0f, UiMotion.SlideIn(0.1f), Eps);
            Assert.AreEqual(1f, UiMotion.ArriveOpacity(0.1f), Eps);
            Assert.AreEqual(0f, UiMotion.LeaveOffset(0.1f), Eps);
            Assert.AreEqual(1f, UiMotion.LeaveOpacity(0.1f), Eps);
            Assert.AreEqual(110, UiMotion.CountUp(10, 110, 0f, UiMotion.CoinCountSec), "число одразу кінцеве");
            Assert.IsFalse(UiMotion.Active(0.1f, 1f));
        }

        [Test]
        public void MotionTarget_WritesStyleOnlyWhenValueChanges()
        {
            var element = new VisualElement();
            var target = new MotionTarget(element);
            target.Set(1f, 1f, 0f, 0f, 1f);
            Assert.AreEqual(0, target.Writes, "стан спокою нічого не пише");

            target.Set(1.1f, 1.1f, 0f, 0f, 1f);
            Assert.AreEqual(1, target.Writes);
            target.Set(1.1f, 1.1f, 0f, 0f, 1f);
            target.Set(1.1f, 1.1f, 0f, 0f, 1f);
            Assert.AreEqual(1, target.Writes, "те саме значення не дублюється");
            Assert.AreEqual(1.1f, element.style.scale.value.value.x, Eps);

            target.Set(1.1f, 1.1f, 5f, -3f, 0.5f);
            Assert.AreEqual(2, target.Writes);
            Assert.AreEqual(0.5f, element.style.opacity.value, Eps);
            Assert.AreEqual(5f, target.TranslateX, Eps);
            Assert.AreEqual(-3f, target.TranslateY, Eps);
        }

        // ---- HUD ----

        [Test]
        public void Hud_Timer_TurnsEmberAndPulses_OnlyInLastTenSeconds()
        {
            ShiftDriver driver = Started(ConfigTestKit.Level(1));
            ShiftScreen screen = BuildScreen(driver);
            Assert.IsFalse(screen.TimerWarningShown);
            Assert.AreEqual(1f, screen.TimerMotion.ScaleX, Eps);

            // Сам драйвер крупними кроками доходить до останніх секунд; годинник анімації стоїть.
            for (int guard = 0; guard < 100000 && EmptyShift.RemainingSeconds(driver.Simulation) > UiMotion.TimerWarningSec + 1; guard++)
            {
                driver.Frame(0.5);
                screen.Refresh();
            }

            Assert.IsFalse(screen.TimerWarningShown, "до останніх 10 с без попередження");
            Assert.AreEqual(1f, screen.TimerMotion.ScaleX, Eps);

            bool pulsed = false;
            for (int guard = 0; guard < 100000 && !driver.IsFinished && !pulsed; guard++)
            {
                driver.Frame(0.05);
                _clock += 0.05f;
                screen.Refresh();
                pulsed = screen.TimerWarningShown && screen.TimerMotion.ScaleX > 1.05f;
            }

            Assert.IsTrue(pulsed, "в останні 10 с таймер червоніє й пульсує");
            Assert.LessOrEqual(screen.TimerMotion.ScaleX, UiMotion.TimerPulsePeak + Eps);
        }

        [Test]
        public void Hud_Timer_NoPulse_WhenReducedMotion()
        {
            UiMotion.Reduced = true;
            ShiftDriver driver = Started(ConfigTestKit.Level(1));
            ShiftScreen screen = BuildScreen(driver);
            for (int guard = 0; guard < 100000 && !driver.IsFinished; guard++)
            {
                driver.Frame(0.1);
                _clock += 0.1f;
                screen.Refresh();
                Assert.AreEqual(1f, screen.TimerMotion.ScaleX, Eps);
            }

            Assert.IsTrue(driver.IsFinished);
        }

        /// <summary>Рівень 4: прилавок на 12 слотів, усі клієнти приходять одразу.</summary>
        private static LevelDefinition CrowdedLevel4()
        {
            ConfigSources sources = ConfigTestKit.Mutated("L04", t =>
            {
                t["counterSlots"] = (long)CounterConfig.MaxSlots;
                foreach (object wave in ConfigTestKit.Arr(t, "waves"))
                {
                    ((Dictionary<string, object>)wave)["atSec"] = 0L;
                }
            });
            ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(sources);
            Assert.IsTrue(result.IsValid, ConfigTestKit.Join(result.Errors));
            return result.Value.FindByNumber(4);
        }

        [Test]
        public void ServedCustomer_HopsOnce_CoinsCountUp_GoalPops_WithoutDuplicates()
        {
            ShiftDriver driver = Started(CrowdedLevel4());
            ShiftScreen screen = BuildScreen(driver);
            ShiftSimulation sim = driver.Simulation;
            Counter core = sim.Counter;
            Step(driver, screen);

            // Звичайний кебаб (піта + м'ясо) лише командами драйвера - як у CounterPanelTests.
            int pita = sim.Board.Config.IndexOfIngredient("pita");
            Assert.IsTrue(driver.Tap(ShiftTap.AddIngredient, pita));
            Assert.IsTrue(driver.Tap(ShiftTap.PlaceMeat));
            RunUntil(driver, screen, () => sim.Grill.GetSlot(0).State == MeatState.Done && sim.QueuedActionCount == 0);
            Assert.IsTrue(driver.Tap(ShiftTap.TakeMeat));
            RunUntil(driver, screen, () => sim.QueuedActionCount == 0 && sim.Board.ItemCount == 2);

            int slot = -1;
            for (int s = 0; s < core.Slots.Count && slot < 0; s++)
            {
                Customer c = core.GetCustomerInSlot(s);
                if (c != null && c.RequestedMask == 0 && c.ExcludeMask == 0)
                {
                    slot = s;
                }
            }

            Assert.GreaterOrEqual(slot, 0, "є клієнт зі звичайним кебабом");
            Assert.AreEqual(0L, screen.Coins0());
            Assert.IsTrue(screen.Counter.TapSlot(slot));
            RunUntil(driver, screen, () => screen.Counter.GetShownEvent(slot).Kind == CounterEventKind.Served);

            long paid = ShiftHud.FromDriver(driver).Coins;
            Assert.Greater(paid, 0L);
            Assert.AreEqual(paid.ToString(), screen.CoinsShownText, "цільове число - з Core");
            Assert.AreNotEqual(screen.CoinsShownText, screen.CoinsAnimatedText, "у момент оплати число ще не добігло");
            Assert.Greater(screen.CoinsMotion.Writes, 0, "значок монети пульсує");

            // Той самий кадр: підскок цілі й клієнта стартують один раз.
            MotionTarget actor = screen.Counter.GetActor(slot);
            _clock += UiMotion.HopSec / 2f;
            screen.Refresh();
            Assert.AreEqual(-UiMotion.HopHeightPx, actor.TranslateY, 0.5f, "клієнт підскакує на 12 px");
            Assert.Greater(screen.GoalMotion.ScaleX, 1f, "число цілі підскакує");
            Assert.LessOrEqual(screen.GoalMotion.ScaleX, UiMotion.GoalPopPeak + Eps);
            int hopWrites = actor.Writes;
            screen.Refresh();
            Assert.AreEqual(hopWrites, actor.Writes, "без зміни часу стиль не перезаписується");

            _clock += UiMotion.CoinCountSec;
            screen.Refresh();
            Assert.AreEqual(screen.CoinsShownText, screen.CoinsAnimatedText, "число добігло");
            Assert.AreEqual(1f, screen.CoinsMotion.ScaleX, Eps);
            Assert.AreEqual(1f, screen.GoalMotion.ScaleX, Eps);
            Assert.AreEqual(0f, actor.TranslateY, Eps, "підскок завершився");
        }

        [Test]
        public void ServedCustomer_ReducedMotion_ShowsFinalCoinsImmediately_AndNoHop()
        {
            UiMotion.Reduced = true;
            ShiftDriver driver = Started(CrowdedLevel4());
            ShiftScreen screen = BuildScreen(driver);
            Step(driver, screen);
            for (int s = 0; s < screen.Counter.SlotCount; s++)
            {
                Assert.AreEqual(0f, screen.Counter.GetActor(s).TranslateY, Eps);
                Assert.AreEqual(1f, screen.Counter.GetActor(s).ScaleY, Eps, "без дихання");
            }

            Assert.AreEqual(screen.CoinsShownText, screen.CoinsAnimatedText);
        }

        // ---- клієнти ----

        [Test]
        public void Customers_Breathe_OutOfPhase()
        {
            ShiftDriver driver = Started(ConfigTestKit.Level(4));
            ShiftScreen screen = BuildScreen(driver);
            CounterPanel counter = screen.Counter;
            Counter core = driver.Simulation.Counter;
            for (int guard = 0; guard < 100000 && CountOccupied(counter) < 2; guard++)
            {
                Assert.IsFalse(driver.IsFinished);
                Step(driver, screen);
            }

            Assert.GreaterOrEqual(CountOccupied(counter), 2, "L04: двоє клієнтів одночасно");
            var scales = new List<float>();
            for (int s = 0; s < counter.SlotCount; s++)
            {
                if (counter.GetShownView(s).Occupied)
                {
                    scales.Add(counter.GetActor(s).ScaleY);
                }
            }

            Assert.AreEqual(core.Slots.Count - core.Slots.FreeCount, scales.Count);
            foreach (float v in scales)
            {
                Assert.That(v, Is.InRange(1f - Eps, 1f + UiMotion.BreathAmplitude + Eps));
            }

            Assert.AreNotEqual(scales[0], scales[1], "клієнти дихають не синхронно");
        }

        [Test]
        public void LeavingCustomer_FliesOut_ThenSlotReturnsToRest()
        {
            ShiftDriver driver = Started(ConfigTestKit.Level(4));
            ShiftScreen screen = BuildScreen(driver);
            CounterPanel counter = screen.Counter;
            bool flew = false;
            for (int guard = 0; guard < 200000 && !driver.IsFinished && !flew; guard++)
            {
                Step(driver, screen);
                for (int s = 0; s < counter.SlotCount; s++)
                {
                    MotionTarget a = counter.GetActor(s);
                    if (a.TranslateX > 1f && a.Opacity < 1f)
                    {
                        flew = true;
                    }
                }
            }

            Assert.IsTrue(flew, "клієнт, що пішов, відходить вбік і згасає");
            _clock += UiMotion.LeaveSec + 0.1f;
            screen.Refresh();
            for (int s = 0; s < counter.SlotCount; s++)
            {
                if (!counter.GetShownView(s).Occupied)
                {
                    Assert.AreEqual(1f, counter.GetActor(s).Opacity, Eps, "вільний слот у спокої");
                    Assert.AreEqual(0f, counter.GetActor(s).TranslateX, Eps);
                }
            }
        }

        [Test]
        public void ArrivingCustomer_SlidesInFromSide_AndEndsAtRest()
        {
            ShiftDriver driver = Started(ConfigTestKit.Level(4));
            ShiftScreen screen = BuildScreen(driver);
            CounterPanel counter = screen.Counter;
            bool slid = false;
            for (int guard = 0; guard < 100000 && !slid; guard++)
            {
                Assert.IsFalse(driver.IsFinished);
                Step(driver, screen);
                for (int s = 0; s < counter.SlotCount; s++)
                {
                    MotionTarget a = counter.GetActor(s);
                    if (counter.GetShownView(s).Occupied && a.TranslateX < -1f && a.Opacity < 1f)
                    {
                        slid = true;
                    }
                }
            }

            Assert.IsTrue(slid, "новий клієнт виїжджає збоку");
            _clock += UiMotion.ArriveSec + 0.1f;
            screen.Refresh();
            for (int s = 0; s < counter.SlotCount; s++)
            {
                if (counter.GetShownView(s).Occupied && !counter.GetShownView(s).Patience.Low)
                {
                    Assert.AreEqual(0f, counter.GetActor(s).TranslateX, Eps);
                    Assert.AreEqual(1f, counter.GetActor(s).Opacity, Eps);
                }
            }
        }

        [Test]
        public void ImpatientCustomer_Shakes_OnLastHeart_WithinThreePx()
        {
            ShiftDriver driver = Started(ConfigTestKit.Level(4));
            ShiftScreen screen = BuildScreen(driver);
            CounterPanel counter = screen.Counter;
            float maxShake = 0f;
            bool low = false;
            for (int guard = 0; guard < 200000 && !driver.IsFinished && !(low && maxShake > 0.5f); guard++)
            {
                Step(driver, screen);
                for (int s = 0; s < counter.SlotCount; s++)
                {
                    CustomerSlotView view = counter.GetShownView(s);
                    if (view.Occupied && view.Patience.Low)
                    {
                        low = true;
                        _clock += 0.07f; // зсув фази, щоб не потрапити в нуль синуса
                        maxShake = Mathf.Max(maxShake, Mathf.Abs(counter.GetActor(s).TranslateX));
                        Assert.LessOrEqual(Mathf.Abs(counter.GetActor(s).TranslateX), UiMotion.ShakeAmplitudePx + Eps);
                    }
                }
            }

            Assert.IsTrue(low, "на L04 клієнт доходить до останнього сердечка");
            Assert.Greater(maxShake, 0.5f, "клієнт тремтить");
        }

        private static int CountOccupied(CounterPanel counter)
        {
            int n = 0;
            for (int s = 0; s < counter.SlotCount; s++)
            {
                if (counter.GetShownView(s).Occupied)
                {
                    n++;
                }
            }

            return n;
        }

        private void RunUntil(ShiftDriver driver, ShiftScreen screen, Func<bool> done)
        {
            for (int guard = 0; guard < 100000 && !done(); guard++)
            {
                Assert.IsFalse(driver.IsFinished, "зміна скінчилась раніше");
                Step(driver, screen);
            }

            Assert.IsTrue(done());
        }
    }

    internal static class MotionTestExtensions
    {
        public static long Coins0(this ShiftScreen screen)
        {
            return long.Parse(screen.CoinsShownText);
        }

        public static MotionTarget GetActor(this CounterPanel counter, int slot)
        {
            return counter.GetActorMotion(slot);
        }
    }
}
