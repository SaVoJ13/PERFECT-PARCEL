using KebabStreet.Core;
using KebabStreet.Game;
using KebabStreet.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Ритуал результату (Q-05e, QB-RES-01...05, QB-RES-08, QB-ALL-17): шкала банера, зірок, монет, конфеті й кнопок,
    /// тап-пропуск, «менше анімацій», підсвітка нового рівня. Правил гри не чіпає: результат лише читається.
    /// </summary>
    public sealed class ResultSequenceTests
    {
        private const ulong Seed = 20284958UL;
        private const float Eps = 0.0001f;
        private const float FrameSec = 1f / 60f;
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
            GameContext.ResetForTests();
        }

        private static ShiftScreen WonScreen(out ShiftDriver driver)
        {
            LevelDefinition level = ConfigTestKit.Level(1);
            driver = new ShiftDriver(level, Seed);
            Assert.IsTrue(driver.Begin());
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { }, () => { }, () => { }, true);
            var bot = new UiBot(driver, new ScreenTaps(screen));
            double tick = 1.0 / level.Shift.TicksPerSecond;
            for (int guard = 0; guard < 400000 && !driver.IsFinished; guard++)
            {
                bot.Step();
                driver.Frame(tick);
                screen.Refresh();
            }

            Assert.IsTrue(driver.IsFinished);
            Assert.IsTrue(driver.Result.Won, "L01 бот виграє");
            screen.Refresh();
            Assert.IsTrue(screen.ResultPanelVisible);
            return screen;
        }

        private static ShiftScreen LostScreen()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(3), Seed);
            Assert.IsTrue(driver.Begin());
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { }, () => { }, () => { }, true);
            for (int guard = 0; guard < 100000 && !driver.IsFinished; guard++)
            {
                driver.Frame(1.0);
            }

            Assert.IsFalse(driver.Result.Won);
            screen.Refresh();
            Assert.IsTrue(screen.ResultPanelVisible);
            return screen;
        }

        private void Advance(ShiftScreen screen, float seconds)
        {
            int frames = Mathf.CeilToInt(seconds / FrameSec);
            for (int i = 0; i < frames; i++)
            {
                _clock += FrameSec;
                screen.Refresh();
            }
        }

        // ---- чиста шкала ----

        [Test]
        public void Banner_FliesFromTop_WithOvershootEase_In300To400ms()
        {
            Assert.GreaterOrEqual(ResultMotion.BannerSec, 0.3f);
            Assert.LessOrEqual(ResultMotion.BannerSec, 0.4f);
            Assert.AreEqual(ResultMotion.BannerOffsetPx, ResultMotion.BannerOffset(0f), 0.01f, "стартує згори");
            Assert.AreEqual(0f, ResultMotion.BannerOffset(ResultMotion.BannerSec), Eps);
            Assert.AreEqual(0f, ResultMotion.BannerOpacity(0f), Eps);
            Assert.AreEqual(1f, ResultMotion.BannerOpacity(ResultMotion.BannerSec), Eps);

            float overshoot = 0f;
            for (float t = 0f; t < ResultMotion.BannerSec; t += 0.005f)
            {
                overshoot = Mathf.Max(overshoot, ResultMotion.BannerOffset(t));
            }

            Assert.Greater(overshoot, 0f, "ease-out-back: банер трохи перелітає вниз і вертається");
        }

        [Test]
        public void Stars_AppearOneByOne_Every300To400ms_AfterBanner()
        {
            Assert.GreaterOrEqual(ResultMotion.StarGapSec, 0.3f);
            Assert.LessOrEqual(ResultMotion.StarGapSec, 0.4f);
            Assert.AreEqual(ResultMotion.StarGapSec, ResultMotion.StarAt(1) - ResultMotion.StarAt(0), Eps);
            Assert.AreEqual(ResultMotion.StarGapSec, ResultMotion.StarAt(2) - ResultMotion.StarAt(1), Eps);
            Assert.GreaterOrEqual(ResultMotion.StarAt(0), ResultMotion.BannerSec, "перша зірка після банера");

            Assert.IsFalse(ResultMotion.StarReached(0, ResultMotion.StarAt(0) - 0.01f));
            Assert.IsTrue(ResultMotion.StarReached(0, ResultMotion.StarAt(0)));
            Assert.IsFalse(ResultMotion.StarReached(1, ResultMotion.StarAt(1) - 0.01f));
            Assert.IsTrue(ResultMotion.StarReached(2, ResultMotion.StarAt(2)));
        }

        [Test]
        public void StarScale_PopsFromZeroThroughPeakAndSettlesAtOne()
        {
            float at = ResultMotion.StarAt(1);
            Assert.AreEqual(0f, ResultMotion.StarScale(1, at), Eps);
            float peak = 0f;
            for (float t = 0f; t < ResultMotion.StarPopSec; t += 0.005f)
            {
                peak = Mathf.Max(peak, ResultMotion.StarScale(1, at + t));
            }

            Assert.AreEqual(ResultMotion.StarPopPeak, peak, 0.02f, "0 -> 1,3 -> 1");
            Assert.AreEqual(1f, ResultMotion.StarScale(1, at + ResultMotion.StarPopSec), Eps);
            Assert.AreEqual(1f, ResultMotion.StarScale(1, at - 0.1f), Eps, "до моменту - контур у спокої");
        }

        [Test]
        public void Coins_RunUpToSum_In0_6To1_2s_Monotonic()
        {
            Assert.GreaterOrEqual(ResultMotion.CoinsSec, 0.6f);
            Assert.LessOrEqual(ResultMotion.CoinsSec, 1.2f);
            Assert.AreEqual(0L, ResultMotion.CoinsShown(250L, 0f));
            long last = 0L;
            for (float t = 0f; t <= ResultMotion.CoinsStartSec + ResultMotion.CoinsSec + 0.1f; t += 0.01f)
            {
                long shown = ResultMotion.CoinsShown(250L, t);
                Assert.GreaterOrEqual(shown, last, "число не повертається назад");
                Assert.LessOrEqual(shown, 250L);
                last = shown;
            }

            Assert.AreEqual(250L, last);
        }

        [Test]
        public void Buttons_AppearAfter0_6To0_9sFromLastEvent_ThenFade()
        {
            Assert.GreaterOrEqual(ResultMotion.ButtonsDelaySec, 0.6f);
            Assert.LessOrEqual(ResultMotion.ButtonsDelaySec, 0.9f);
            for (int stars = 0; stars <= 3; stars++)
            {
                float at = ResultMotion.ButtonsAt(stars);
                Assert.AreEqual(ResultMotion.LastEventAt(stars) + ResultMotion.ButtonsDelaySec, at, Eps);
                Assert.AreEqual(0f, ResultMotion.ButtonsOpacity(stars, at - 0.01f), Eps);
                Assert.IsFalse(ResultMotion.ButtonsReady(stars, at - 0.01f));
                Assert.IsTrue(ResultMotion.ButtonsReady(stars, at));
                Assert.AreEqual(1f, ResultMotion.ButtonsOpacity(stars, at + ResultMotion.ButtonsFadeSec), Eps);
                Assert.GreaterOrEqual(ResultMotion.TotalSec(stars), at + ResultMotion.ButtonsFadeSec - Eps, "шкала охоплює кнопки");
                Assert.GreaterOrEqual(ResultMotion.TotalSec(stars), ResultMotion.CoinsStartSec + ResultMotion.CoinsSec - Eps, "і монети");
            }
        }

        [Test]
        public void Confetti_Count40To80_OnlyForThreeStars_TimedWithLastStar()
        {
            Assert.GreaterOrEqual(ResultMotion.ConfettiCount, 40);
            Assert.LessOrEqual(ResultMotion.ConfettiCount, 80);
            Assert.AreEqual(ResultMotion.StarAt(2), ResultMotion.ConfettiAt(3), Eps);
            Assert.AreEqual(ResultMotion.BannerSec, ResultMotion.ConfettiAt(1), Eps, "перша перемога без 3 зірок: з банером");
        }

        [Test]
        public void Reduced_EverythingShownAtOnce()
        {
            UiMotion.Reduced = true;
            Assert.AreEqual(0f, ResultMotion.BannerOffset(0f), Eps);
            Assert.AreEqual(1f, ResultMotion.BannerOpacity(0f), Eps);
            Assert.IsTrue(ResultMotion.StarReached(2, 0f));
            Assert.AreEqual(1f, ResultMotion.StarScale(2, ResultMotion.StarAt(2)), Eps);
            Assert.AreEqual(250L, ResultMotion.CoinsShown(250L, 0f));
            Assert.IsTrue(ResultMotion.ButtonsReady(3, 0f));
            Assert.AreEqual(1f, ResultMotion.ButtonsOpacity(3, 0f), Eps);
            Assert.AreEqual(1f, ResultMotion.UnlockOpacity(0.3f), Eps, "підсвітка нового рівня лишається, але не пульсує");
        }

        [Test]
        public void UnlockHighlight_FadesIn_ThenPulsesAtMost3Hz()
        {
            Assert.LessOrEqual(ResultMotion.UnlockPulseHz, UiMotion.MaxFlashHz);
            Assert.AreEqual(0f, ResultMotion.UnlockOpacity(0f), Eps);
            float min = 1f;
            float max = 0f;
            for (float t = 1f; t < 3f; t += 0.01f)
            {
                float o = ResultMotion.UnlockOpacity(t);
                min = Mathf.Min(min, o);
                max = Mathf.Max(max, o);
            }

            Assert.AreEqual(ResultMotion.UnlockMinOpacity, min, 0.02f);
            Assert.AreEqual(1f, max, 0.02f);
        }

        // ---- екран ----

        [Test]
        public void Screen_Win_StarsLightUpOneByOne_WithSparks_AndButtonsAfterwards()
        {
            ShiftDriver driver;
            ShiftScreen screen = WonScreen(out driver);
            int stars = driver.Result.Stars;
            Assert.GreaterOrEqual(stars, 1);
            Assert.AreEqual(0, screen.StarsRevealed, "на старті всі зірки - порожні контури");
            Assert.IsFalse(screen.ResultButtonsReady);
            Assert.AreEqual(0f, screen.ResultButtonMotion(0).Opacity, Eps);

            int previous = 0;
            bool sparksSeen = false;
            float elapsed = 0f;
            while (elapsed < ResultMotion.ButtonsAt(stars) - 0.05f)
            {
                Advance(screen, FrameSec);
                elapsed += FrameSec;
                Assert.LessOrEqual(screen.StarsRevealed - previous, 1, "зірки по одній, не пачкою");
                previous = screen.StarsRevealed;
                sparksSeen |= screen.Fx.ActiveOf(FxKind.Spark) > 0;
                Assert.IsFalse(screen.ResultButtonsReady, "кнопок ще немає");
            }

            Assert.AreEqual(stars, screen.StarsRevealed);
            Assert.IsTrue(sparksSeen, "кожна зірка з іскрами");
            Advance(screen, 0.2f);
            Assert.IsTrue(screen.ResultButtonsReady);
            Advance(screen, ResultMotion.ButtonsFadeSec);
            Assert.AreEqual(1f, screen.ResultButtonMotion(1).Opacity, Eps);
            Assert.AreEqual(screen.ResultCoinsText, screen.ResultCoinsShownText);
        }

        [Test]
        public void Screen_Win_CoinsRunUp_ButFullTextIsAlwaysTheFinalSum()
        {
            ShiftDriver driver;
            ShiftScreen screen = WonScreen(out driver);
            string full = string.Format(Text.Get(TextKeys.ShiftResultCoins), driver.Result.RewardCoins);
            Assert.AreEqual(full, screen.ResultCoinsText);
            if (driver.Result.RewardCoins > 3L)
            {
                Assert.AreNotEqual(full, screen.ResultCoinsShownText, "на старті число ще не набігло");
            }

            Advance(screen, ResultMotion.CoinsStartSec + (ResultMotion.CoinsSec / 2f));
            Assert.AreEqual(full, screen.ResultCoinsText);
            Advance(screen, ResultMotion.CoinsSec);
            Assert.AreEqual(full, screen.ResultCoinsShownText);
        }

        [Test]
        public void Screen_Skip_JumpsToEnd_WithoutSparksOrConfetti_AndIsIdempotent()
        {
            ShiftDriver driver;
            ShiftScreen screen = WonScreen(out driver);
            screen.MarkFirstWin(true);
            Assert.IsFalse(screen.ResultSkipped);

            screen.SkipResult();
            Assert.IsTrue(screen.ResultSkipped);
            Assert.AreEqual(driver.Result.Stars, screen.StarsRevealed);
            Assert.AreEqual(screen.ResultCoinsText, screen.ResultCoinsShownText);
            Assert.IsTrue(screen.ResultButtonsReady, "кнопки доступні одразу після пропуску");
            Assert.AreEqual(1f, screen.ResultButtonMotion(2).Opacity, Eps);
            Assert.AreEqual(0f, screen.BannerMotion.TranslateY, Eps);
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Spark));
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Confetti));

            int writes = screen.BannerMotion.Writes;
            screen.SkipResult();
            Advance(screen, 3f);
            Assert.AreEqual(driver.Result.Stars, screen.StarsRevealed);
            Assert.AreEqual(writes, screen.BannerMotion.Writes, "повторний пропуск і час нічого не переписують");
        }

        [Test]
        public void Screen_Skip_BeforeResult_DoesNothing()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(1), Seed);
            driver.Begin();
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });
            screen.SkipResult();
            Assert.IsFalse(screen.ResultSkipped);
        }

        [Test]
        public void Screen_Confetti_OnThreeStarsOrFirstWin_AndNotOtherwise()
        {
            foreach (bool firstWin in new[] { false, true })
            {
                _clock = 100f;
                ShiftDriver driver;
                ShiftScreen screen = WonScreen(out driver);
                screen.MarkFirstWin(firstWin);
                bool expected = driver.Result.Stars >= 3 || firstWin;
                int peak = 0;
                float total = ResultMotion.TotalSec(driver.Result.Stars) + 0.2f;
                for (float t = 0f; t < total; t += FrameSec)
                {
                    Advance(screen, FrameSec);
                    peak = Mathf.Max(peak, screen.Fx.ActiveOf(FxKind.Confetti));
                }

                if (expected)
                {
                    Assert.GreaterOrEqual(peak, 40, "40-80 частинок конфеті");
                    Assert.LessOrEqual(peak, 80);
                }
                else
                {
                    Assert.AreEqual(0, peak, "без 3★ і без першої перемоги конфеті немає");
                }
            }
        }

        [Test]
        public void Screen_Lost_NoStarsNoConfetti_ButtonsStillAppear()
        {
            ShiftScreen screen = LostScreen();
            Assert.AreEqual(0, screen.StarsRevealed);
            Advance(screen, ResultMotion.TotalSec(0) + 0.1f);
            Assert.AreEqual(0, screen.StarsRevealed);
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Confetti));
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Spark));
            Assert.IsTrue(screen.ResultButtonsReady);
            Assert.IsTrue(screen.RetryVisible);
        }

        [Test]
        public void Screen_ReducedMotion_ShowsFinalResultImmediately_WithoutParticles()
        {
            UiMotion.Reduced = true;
            ShiftDriver driver;
            ShiftScreen screen = WonScreen(out driver);
            screen.MarkFirstWin(true);
            screen.Refresh();
            Assert.AreEqual(driver.Result.Stars, screen.StarsRevealed);
            Assert.IsTrue(screen.ResultButtonsReady);
            Assert.AreEqual(screen.ResultCoinsText, screen.ResultCoinsShownText);
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Confetti));
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Spark));
            Assert.AreEqual(1f, screen.BannerMotion.Opacity, Eps);
        }

        [Test]
        public void Screen_FxLayerRaisedAbovePanel_AndGameParticlesCleared()
        {
            ShiftDriver driver;
            ShiftScreen screen = WonScreen(out driver);
            VisualElement parent = screen.Fx.Root.parent;
            Assert.IsNotNull(parent);
            Assert.AreEqual(parent.childCount - 1, parent.IndexOf(screen.Fx.Root), "шар частинок поверх плашки результату");
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Steam));
            Assert.AreEqual(0, screen.Fx.ActiveOf(FxKind.Coin));
        }

        [Test]
        public void Screen_SettledResult_NoRedundantStyleWrites()
        {
            ShiftDriver driver;
            ShiftScreen screen = WonScreen(out driver);
            Advance(screen, ResultMotion.TotalSec(driver.Result.Stars) + 0.5f);
            int writes = screen.BannerMotion.Writes;
            int starWrites = screen.StarMotion(0) == null ? 0 : screen.StarMotion(0).Writes;
            Advance(screen, 1f);
            Assert.AreEqual(writes, screen.BannerMotion.Writes, "без зайвих записів стилю (QB-ALL-16)");
            if (screen.StarMotion(0) != null)
            {
                Assert.AreEqual(starWrites, screen.StarMotion(0).Writes);
            }
        }

        // ---- шар частинок ----

        [Test]
        public void FxLayer_Confetti_PoolAndLimits()
        {
            var fx = new FxLayer(7);
            Assert.GreaterOrEqual(FxLayer.ConfettiPool, 40);
            Assert.AreEqual(FxLayer.ConfettiPool, fx.CapacityOf(FxKind.Confetti));
            Assert.LessOrEqual(FxLayer.MaxActive, 150, "ігрові частинки в межах 150");
            Assert.LessOrEqual(FxLayer.ResultMaxActive, 150, "ефекти результату теж");

            int started = fx.Confetti(ResultMotion.ConfettiCount);
            Assert.AreEqual(ResultMotion.ConfettiCount, started);
            Assert.AreEqual(started, fx.ActiveOf(FxKind.Confetti));
            Assert.AreEqual(0, fx.Confetti(FxLayer.ConfettiPool), "пул заповнено: нові відкинуто");
            Assert.Greater(fx.DroppedOf(FxKind.Confetti), 0);

            _clock += FxLayer.ConfettiStaggerSec + FxLayer.ConfettiLifeMax + 0.1f;
            fx.Update();
            Assert.AreEqual(0, fx.ActiveOf(FxKind.Confetti), "конфеті живе 1,5-2,5 с і повертається в пул");
            Assert.AreEqual(started, fx.Confetti(started), "пул використано повторно");
        }

        [Test]
        public void FxLayer_Sparks_6To10_AndReducedCreatesNone()
        {
            var fx = new FxLayer(7);
            int n = fx.Sparks(null);
            Assert.GreaterOrEqual(n, ResultMotion.SparksMin);
            Assert.LessOrEqual(n, ResultMotion.SparksMax);
            _clock += FxLayer.SparkLifeSec + 0.01f;
            fx.Update();
            Assert.AreEqual(0, fx.ActiveOf(FxKind.Spark));

            UiMotion.Reduced = true;
            Assert.AreEqual(0, fx.Sparks(null));
            Assert.AreEqual(0, fx.Confetti(60));
        }

        // ---- підсвітка нового рівня ----

        [Test]
        public void Context_UnlockHighlight_IsTakenOnce()
        {
            var context = new GameContext(new InMemorySaveStorage(), new ManualClock(1700000000), Text);
            Assert.AreEqual(0, context.UnlockHighlightLevel);
            context.MarkUnlocked(2);
            Assert.AreEqual(2, context.UnlockHighlightLevel);
            Assert.AreEqual(2, context.TakeUnlockHighlight());
            Assert.AreEqual(0, context.TakeUnlockHighlight(), "підсвітка показується один раз");
        }
    }
}
