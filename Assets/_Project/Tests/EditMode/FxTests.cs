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
    /// Гриль і дошка (Q-05d, QB-SHIFT-14/17/19/20/21/22/25, QB-ALL-16/17): математика анімацій, пул частинок з лімітом,
    /// пауза (ігровий час стоїть), «менше анімацій», стан слотів гриля, політ інгредієнта, оплата. Правил гри не чіпає.
    /// </summary>
    public sealed class FxTests
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

        private void RunUntil(ShiftDriver driver, ShiftScreen screen, Func<bool> done)
        {
            for (int guard = 0; guard < 100000 && !done(); guard++)
            {
                Assert.IsFalse(driver.IsFinished, "зміна скінчилась раніше");
                Step(driver, screen);
            }

            Assert.IsTrue(done());
        }

        // ---- чиста математика ----

        [Test]
        public void DonePop_Peaks115_ThenSettles_AndDonePulseStaysWithinSixPercent_AtMostOneHertz()
        {
            Assert.AreEqual(1f, UiMotion.DonePop(0f), Eps);
            Assert.AreEqual(UiMotion.DonePopPeak, UiMotion.DonePop(UiMotion.DonePopSec / 2f), Eps);
            Assert.AreEqual(1f, UiMotion.DonePop(UiMotion.DonePopSec), Eps);
            Assert.AreEqual(0.2f, UiMotion.DonePopSec, Eps, "200 мс");
            float min = 10f;
            float max = 0f;
            for (float t = 0f; t < 4f; t += 0.01f)
            {
                float v = UiMotion.DonePulse(t);
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }

            Assert.AreEqual(1f, min, 0.002f);
            Assert.AreEqual(1f + UiMotion.DonePulseAmplitude, max, 0.002f);
            Assert.LessOrEqual(UiMotion.DonePulseHz, 1f);
        }

        [Test]
        public void WarnPulse_IsTwoHertz_WithinThreeHertzLimit_AndShakeWithinThreePx()
        {
            Assert.AreEqual(2f, UiMotion.WarnPulseHz, Eps);
            Assert.LessOrEqual(UiMotion.WarnPulseHz, UiMotion.MaxFlashHz);
            int crossings = 0;
            float mid = 1f + (UiMotion.WarnPulseAmplitude / 2f);
            bool above = UiMotion.WarnPulse(0.0001f) > mid;
            for (float t = 0.001f; t < 1f; t += 0.001f)
            {
                bool now = UiMotion.WarnPulse(t) > mid;
                if (now != above)
                {
                    crossings++;
                    above = now;
                }
            }

            Assert.GreaterOrEqual(crossings, 3, "2 Гц: два цикли за секунду");
            Assert.LessOrEqual(crossings, 4);
            for (float t = 0f; t < 2f; t += 0.013f)
            {
                Assert.LessOrEqual(Mathf.Abs(UiMotion.WarnShake(t)), 3f);
            }
        }

        [Test]
        public void Drop_StartsThirtyPxAbove_LandsAtZero_In150ms()
        {
            Assert.AreEqual(-UiMotion.DropHeightPx, UiMotion.Drop(0f), Eps);
            Assert.AreEqual(30f, UiMotion.DropHeightPx, Eps);
            Assert.AreEqual(0.15f, UiMotion.DropSec, Eps);
            Assert.AreEqual(0f, UiMotion.Drop(UiMotion.DropSec), Eps);
            Assert.AreEqual(0f, UiMotion.Drop(-1f), Eps);
            Assert.AreEqual(1f, UiMotion.BounceOut(1f), Eps);
            Assert.AreEqual(0f, UiMotion.BounceOut(0f), Eps);
        }

        [Test]
        public void GrillMotion_ReducedMotion_ReturnsRest()
        {
            UiMotion.Reduced = true;
            Assert.AreEqual(1f, UiMotion.DonePop(0.1f), Eps);
            Assert.AreEqual(1f, UiMotion.DonePulse(0.3f), Eps);
            Assert.AreEqual(1f, UiMotion.WarnPulse(0.1f), Eps);
            Assert.AreEqual(0f, UiMotion.WarnShake(0.1f), Eps);
            Assert.AreEqual(0f, UiMotion.Drop(0.05f), Eps);
        }

        [Test]
        public void Now_StopsOnPause_ResumesWithoutJump_AndClockSwapResetsPause()
        {
            Assert.AreEqual(100f, UiMotion.Now, Eps);
            _clock = 105f;
            UiMotion.SetPaused(true);
            Assert.IsTrue(UiMotion.Paused);
            float frozen = UiMotion.Now;
            _clock = 120f;
            Assert.AreEqual(frozen, UiMotion.Now, Eps, "на паузі ігровий час стоїть");
            UiMotion.SetPaused(true);
            UiMotion.SetPaused(false);
            Assert.AreEqual(frozen, UiMotion.Now, Eps, "після паузи час продовжується без стрибка");
            _clock = 121f;
            Assert.AreEqual(frozen + 1f, UiMotion.Now, Eps);
            UiMotion.SetPaused(true);
            UiMotion.Clock = () => 5f;
            Assert.IsFalse(UiMotion.Paused);
            Assert.AreEqual(5f, UiMotion.Now, Eps);
        }

        [Test]
        public void PathAt_StartsAndEndsAtPoints_AndArcsUpward()
        {
            var from = new Vector2(10f, 100f);
            var to = new Vector2(200f, 300f);
            Assert.AreEqual(from.x, FxLayer.PathAt(from, to, 60f, 0f).x, Eps);
            Assert.AreEqual(to.y, FxLayer.PathAt(from, to, 60f, 1f).y, Eps);
            Vector2 mid = FxLayer.PathAt(from, to, 60f, 0.5f);
            Vector2 straight = FxLayer.PathAt(from, to, 0f, 0.5f);
            Assert.AreEqual(straight.y - 60f, mid.y, Eps, "дуга піднімається вгору");
        }

        // ---- пул частинок ----

        [Test]
        public void Pool_HasLimit_DropsExtra_AndFitsQualityBarMaximum()
        {
            Assert.LessOrEqual(FxLayer.MaxActive, 150);
            var fx = new FxLayer();
            for (int i = 0; i < FxLayer.SteamPool + 10; i++)
            {
                fx.Steam(null, false);
            }

            Assert.AreEqual(FxLayer.SteamPool, fx.ActiveOf(FxKind.Steam));
            Assert.AreEqual(10, fx.DroppedOf(FxKind.Steam));
            Assert.AreEqual(FxLayer.SteamPool, fx.CapacityOf(FxKind.Steam));
            Assert.LessOrEqual(fx.ActiveCount, FxLayer.MaxActive);
        }

        [Test]
        public void Particles_AreReleased_AfterTheirLife_AndPoolIsReused()
        {
            var fx = new FxLayer();
            Assert.IsTrue(fx.Steam(null, false));
            Assert.IsTrue(fx.Smoke(null));
            Assert.AreEqual(2, fx.ActiveCount);
            fx.Update();
            Assert.AreEqual(2, fx.ActiveCount, "щойно створені живуть");
            _clock += FxLayer.SmokeLifeMax + 0.1f;
            fx.Update();
            Assert.AreEqual(0, fx.ActiveCount);
            for (int i = 0; i < FxLayer.SteamPool; i++)
            {
                Assert.IsTrue(fx.Steam(null, true), "місця повернулися в пул");
            }
        }

        [Test]
        public void Steam_LivesWithinQualityBarWindow()
        {
            var fx = new FxLayer();
            fx.Steam(null, false);
            _clock += FxLayer.SteamLifeMin - 0.05f;
            fx.Update();
            Assert.AreEqual(1, fx.ActiveCount, "менше 0,8 с - ще жива");
            _clock += FxLayer.SteamLifeMax - FxLayer.SteamLifeMin + 0.1f;
            fx.Update();
            Assert.AreEqual(0, fx.ActiveCount, "понад 1,5 с - зникла");
        }

        [Test]
        public void Pause_FreezesParticles_NoStyleWrites_AndResumes()
        {
            var fx = new FxLayer();
            fx.Steam(null, false);
            _clock += 0.2f;
            fx.Update();
            int writes = fx.Writes;
            UiMotion.SetPaused(true);
            _clock += 5f;
            fx.Update();
            fx.Update();
            Assert.AreEqual(1, fx.ActiveCount, "на паузі частинка не гине");
            Assert.AreEqual(writes, fx.Writes, "на паузі стиль не пишеться");
            UiMotion.SetPaused(false);
            _clock += FxLayer.SteamLifeMax;
            fx.Update();
            Assert.AreEqual(0, fx.ActiveCount);
        }

        [Test]
        public void ReducedMotion_SpawnsNoParticles_ButPaymentTextStaysStatic()
        {
            UiMotion.Reduced = true;
            var fx = new FxLayer();
            Assert.IsFalse(fx.Steam(null, false));
            Assert.IsFalse(fx.Smoke(null));
            Assert.IsFalse(fx.Fly(null, null, ArtLibrary.Coin));
            Assert.AreEqual(0, fx.Payment(null, 40L, 0L));
            Assert.AreEqual(1, fx.ActiveOf(FxKind.Text), "число видно і без руху");
            Assert.AreEqual(0, fx.ActiveOf(FxKind.Coin));
            Assert.AreEqual("+40", fx.TextOf(0));
        }

        [Test]
        public void Payment_LaunchesThreeToEightCoins_AndPlusTexts()
        {
            var fx = new FxLayer();
            Assert.AreEqual(FxLayer.CoinsMin, fx.Payment(null, 1L, 0L), "найменша сума - мінімум монет");
            Assert.AreEqual(FxLayer.CoinsMax, fx.Payment(null, 1000L, 0L), "велика сума - максимум");
            Assert.AreEqual(2, fx.ActiveOf(FxKind.Text));
            Assert.AreEqual("+1", fx.TextOf(0));
            Assert.AreEqual("+1000", fx.TextOf(1));

            var tipped = new FxLayer();
            tipped.Payment(null, 20L, 5L);
            Assert.AreEqual(2, tipped.ActiveOf(FxKind.Text), "чайові - окремий «+N»");
            Assert.AreEqual("+5", tipped.TextOf(1));
        }

        [Test]
        public void PaymentCoins_AndText_FinishWithinQualityBarWindow()
        {
            var fx = new FxLayer();
            fx.Payment(null, 40L, 0L);
            Assert.GreaterOrEqual(FxLayer.PlusSec, 0.6f);
            Assert.LessOrEqual(FxLayer.PlusSec, 0.8f);
            _clock += FxLayer.PlusSec + 0.01f;
            fx.Update();
            Assert.AreEqual(0, fx.ActiveOf(FxKind.Text), "«+N» зникає за 0,6-0,8 с");
            _clock += FxLayer.CoinFlightMax + (FxLayer.CoinsMax * FxLayer.CoinStaggerSec) + 0.1f;
            fx.Update();
            Assert.AreEqual(0, fx.ActiveOf(FxKind.Coin), "монети долетіли і зникли");
        }

        [Test]
        public void SameSeed_GivesSameParticles()
        {
            var a = new FxLayer(7);
            var b = new FxLayer(7);
            a.Payment(null, 33L, 0L);
            b.Payment(null, 33L, 0L);
            Assert.AreEqual(a.ActiveCount, b.ActiveCount);
            _clock += 0.3f;
            a.Update();
            b.Update();
            Assert.AreEqual(a.Writes, b.Writes);
        }

        // ---- екран зміни ----

        [Test]
        public void Grill_Cooking_EmitsSteam_DropsMeat_ThenDonePops_ThenWarnsAndBurnsWithSmoke()
        {
            ShiftDriver driver = Started(ConfigTestKit.Level(4));
            ShiftScreen screen = BuildScreen(driver);
            GrillPanel grill = screen.Grill;
            MotionTarget icon = grill.GetIconMotion(0);
            Step(driver, screen);
            Assert.AreEqual(0, screen.Fx.ActiveCount, "порожній гриль - без частинок");

            Assert.IsTrue(driver.Tap(ShiftTap.PlaceMeat));
            bool dropped = false;
            RunUntil(driver, screen, () =>
            {
                dropped |= icon.TranslateY < -1f;
                return grill.GetShownView(0).Display == GrillSlotDisplay.Cooking;
            });
            Assert.IsTrue(dropped || icon.TranslateY < 0f, "шматок падає на слот (QB-SHIFT-22)");
            Assert.LessOrEqual(icon.TranslateY, 0f);
            Assert.GreaterOrEqual(icon.TranslateY, -UiMotion.DropHeightPx - Eps);

            RunUntil(driver, screen, () => screen.Fx.ActiveOf(FxKind.Steam) > 0);
            Assert.Greater(screen.Fx.ActiveOf(FxKind.Steam), 0, "пара над м'ясом (QB-SHIFT-17)");
            Assert.AreEqual(0f, icon.TranslateY, Eps, "падіння завершилось");

            RunUntil(driver, screen, () => grill.GetShownView(0).Display == GrillSlotDisplay.Done);
            float donePeak = 1f;
            for (int i = 0; i < 12; i++)
            {
                Step(driver, screen);
                donePeak = Mathf.Max(donePeak, icon.ScaleX);
            }

            Assert.Greater(donePeak, 1.05f, "готове м'ясо «вистрибує» (QB-SHIFT-19)");
            Assert.LessOrEqual(donePeak, UiMotion.DonePopPeak + Eps);

            bool warned = false;
            bool shook = false;
            RunUntil(driver, screen, () =>
            {
                GrillSlotView view = grill.GetShownView(0);
                if (view.Display == GrillSlotDisplay.Done && view.BurnWarning)
                {
                    warned = true;
                    _clock += 0.04f;
                    shook |= Mathf.Abs(icon.TranslateX) > 0.5f;
                    Assert.LessOrEqual(Mathf.Abs(icon.TranslateX), UiMotion.WarnShakePx + Eps);
                    Assert.LessOrEqual(icon.ScaleX, 1f + UiMotion.WarnPulseAmplitude + Eps);
                }

                return view.Display == GrillSlotDisplay.Burnt;
            });
            Assert.IsTrue(warned, "перед пригоранням є вікно попередження");
            Assert.IsTrue(shook, "майже пригоріле тремтить (QB-SHIFT-20)");

            RunUntil(driver, screen, () => screen.Fx.ActiveOf(FxKind.Smoke) > 0);
            Assert.Greater(screen.Fx.ActiveOf(FxKind.Smoke), 0, "дим над пригорілим (QB-SHIFT-21)");
            Assert.AreEqual(0f, icon.TranslateX, Eps);
            Assert.AreEqual(1f, icon.ScaleX, Eps, "пригоріле не пульсує");
            Assert.LessOrEqual(screen.Fx.ActiveCount, FxLayer.MaxActive);
        }

        [Test]
        public void Grill_Pause_StopsEmissionAndMotion_ThenResumes()
        {
            ShiftDriver driver = Started(ConfigTestKit.Level(4));
            ShiftScreen screen = BuildScreen(driver);
            Assert.IsTrue(driver.Tap(ShiftTap.PlaceMeat));
            RunUntil(driver, screen, () => screen.Fx.ActiveOf(FxKind.Steam) > 0);
            Assert.IsTrue(driver.SetPaused(true));
            screen.Refresh();
            Assert.IsTrue(UiMotion.Paused);
            int active = screen.Fx.ActiveCount;
            int writes = screen.Fx.Writes;
            for (int i = 0; i < 20; i++)
            {
                _clock += 0.25f;
                screen.Refresh();
            }

            Assert.AreEqual(active, screen.Fx.ActiveCount, "на паузі нових частинок немає й старі не гинуть");
            Assert.AreEqual(writes, screen.Fx.Writes, "на паузі рух не пишеться");
            Assert.IsFalse(driver.SetPaused(false));
            screen.Refresh();
            Assert.IsFalse(UiMotion.Paused);
        }

        [Test]
        public void Grill_ReducedMotion_NoParticles_NoMovement()
        {
            UiMotion.Reduced = true;
            ShiftDriver driver = Started(ConfigTestKit.Level(4));
            ShiftScreen screen = BuildScreen(driver);
            MotionTarget icon = screen.Grill.GetIconMotion(0);
            Assert.IsTrue(driver.Tap(ShiftTap.PlaceMeat));
            RunUntil(driver, screen, () => screen.Grill.GetShownView(0).Display == GrillSlotDisplay.Done);
            for (int i = 0; i < 40; i++)
            {
                Step(driver, screen);
                Assert.AreEqual(0, screen.Fx.ActiveCount);
                Assert.AreEqual(1f, icon.ScaleX, Eps);
                Assert.AreEqual(0f, icon.TranslateY, Eps);
            }
        }

        [Test]
        public void Board_IngredientFlies_FromStationToBoard_ThenLandsWithPop()
        {
            ShiftDriver driver = Started(ConfigTestKit.Level(4));
            ShiftScreen screen = BuildScreen(driver);
            ShiftSimulation sim = driver.Simulation;
            int pita = sim.Board.Config.IndexOfIngredient("pita");
            Step(driver, screen);
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Flyer));
            Assert.IsTrue(driver.Tap(ShiftTap.AddIngredient, pita));
            RunUntil(driver, screen, () => sim.Board.ItemCount == 1);
            Assert.AreEqual(1, screen.Fx.ActiveOf(FxKind.Flyer), "значок летить на дошку (QB-SHIFT-25)");
            MotionTarget item = screen.Assembly.GetItemMotion(0);
            Assert.AreEqual(0f, item.Opacity, Eps, "місце на дошці приховане до приземлення");

            _clock += FxLayer.FlightSec + 0.05f;
            screen.Refresh();
            Assert.AreEqual(1f, item.Opacity, Eps, "інгредієнт приземлився");
            Assert.Greater(item.ScaleX, 1f, "приземлення з підскоком");
            _clock += 1f;
            screen.Refresh();
            Assert.AreEqual(1f, item.ScaleX, Eps);
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Flyer), "політ завершився");
            Assert.LessOrEqual(FxLayer.FlightSec, 0.25f);
            Assert.GreaterOrEqual(FxLayer.FlightSec, 0.15f);
        }

        [Test]
        public void Board_ReducedMotion_ItemShownAtOnce_WithoutFlight()
        {
            UiMotion.Reduced = true;
            ShiftDriver driver = Started(ConfigTestKit.Level(4));
            ShiftScreen screen = BuildScreen(driver);
            int pita = driver.Simulation.Board.Config.IndexOfIngredient("pita");
            Assert.IsTrue(driver.Tap(ShiftTap.AddIngredient, pita));
            RunUntil(driver, screen, () => driver.Simulation.Board.ItemCount == 1);
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Flyer));
            Assert.AreEqual(1f, screen.Assembly.GetItemMotion(0).Opacity, Eps);
            Assert.AreEqual(1f, screen.Assembly.GetItemMotion(0).ScaleX, Eps);
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
        public void ServedCustomer_LaunchesCoinsAndPlusN_Once()
        {
            ShiftDriver driver = Started(CrowdedLevel4());
            ShiftScreen screen = BuildScreen(driver);
            ShiftSimulation sim = driver.Simulation;
            Counter core = sim.Counter;
            Step(driver, screen);
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

            Assert.GreaterOrEqual(slot, 0);
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Coin));
            Assert.IsTrue(screen.Counter.TapSlot(slot));
            RunUntil(driver, screen, () => screen.Counter.GetShownEvent(slot).Kind == CounterEventKind.Served);
            int coins = screen.Fx.ActiveOf(FxKind.Coin);
            Assert.GreaterOrEqual(coins, FxLayer.CoinsMin);
            Assert.LessOrEqual(coins, FxLayer.CoinsMax);
            Assert.GreaterOrEqual(screen.Fx.ActiveOf(FxKind.Text), 1, "«+N» над клієнтом");
            screen.Refresh();
            Assert.AreEqual(coins, screen.Fx.ActiveOf(FxKind.Coin), "без дублювання при повторному оновленні");
        }
    }
}
