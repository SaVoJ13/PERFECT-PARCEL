using KebabStreet.Core;
using KebabStreet.Game;
using KebabStreet.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Меню і вибір рівнів (Q-05f, QB-MENU-01/02/03/04, QB-LVL-02/03/04/05/06/07): математика руху в межах специфікації,
    /// «менше анімацій», розкладка шляху, дані картки рівня, нові ключі локалізації, зняття накладки при провалі
    /// завантаження сцени (P3 з QA Q-05e). Правил гри не чіпає.
    /// </summary>
    public sealed class MenuMotionTests
    {
        private const float Eps = 0.0001f;
        private float _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = 10f;
            UiMotion.Reduced = false;
            UiMotion.Clock = () => _clock;
        }

        [TearDown]
        public void TearDown()
        {
            UiMotion.Reduced = false;
            UiMotion.Clock = () => Time.realtimeSinceStartup;
        }

        [Test]
        public void Logo_SwayStaysWithin1And1_02_PeriodIn2_5To3_5()
        {
            Assert.GreaterOrEqual(MenuMotion.LogoSwayPeriodSec, 2.5f);
            Assert.LessOrEqual(MenuMotion.LogoSwayPeriodSec, 3.5f);
            for (float t = 0f; t < 7f; t += 0.05f)
            {
                float s = MenuMotion.LogoSway(t);
                Assert.GreaterOrEqual(s, 1f - Eps);
                Assert.LessOrEqual(s, 1.02f + Eps);
            }
        }

        [Test]
        public void Lights_FlickerNotFasterThan1Hz_AndNeverFullyDark()
        {
            Assert.LessOrEqual(MenuMotion.LightFlickerHz, 1f);
            for (int i = 0; i < MenuMotion.LightCount; i++)
            {
                for (float t = 0f; t < 5f; t += 0.05f)
                {
                    float o = MenuMotion.LightOpacity(i, t);
                    Assert.GreaterOrEqual(o, 0.3f);
                    Assert.LessOrEqual(o, 1f + Eps);
                }
            }

            Assert.AreNotEqual(MenuMotion.LightOpacity(0, 0.3f), MenuMotion.LightOpacity(1, 0.3f), "сусідні лампочки в протифазі");
            Assert.GreaterOrEqual(MenuMotion.SteamPerSecond, 3f);
            Assert.LessOrEqual(MenuMotion.SteamPerSecond, 8f);
        }

        [Test]
        public void PlayBreath_Is1To1_04_OnlyShortlyEachPeriod()
        {
            Assert.GreaterOrEqual(MenuMotion.PlayBreathPeriodSec, 3f);
            Assert.LessOrEqual(MenuMotion.PlayBreathPeriodSec, 4f);
            float max = 1f;
            for (float t = 0f; t < 8f; t += 0.02f)
            {
                float b = MenuMotion.PlayBreath(t);
                Assert.GreaterOrEqual(b, 1f - Eps);
                max = Mathf.Max(max, b);
            }

            Assert.AreEqual(1.04f, max, 0.002f);
            Assert.AreEqual(1f, MenuMotion.PlayBreath(MenuMotion.PlayBreathActiveSec + 0.5f), Eps, "між вдихами - спокій");
        }

        [TestCase(1)]
        [TestCase(4)]
        [TestCase(9)]
        [TestCase(14)]
        public void Cascade_GapIsAtMost80ms_AndTotalAtMost600ms(int count)
        {
            Assert.LessOrEqual(MenuMotion.CascadeTotal(count), MenuMotion.CascadeTotalSec + Eps);
            for (int i = 1; i < count; i++)
            {
                float gap = MenuMotion.CascadeDelay(i, count) - MenuMotion.CascadeDelay(i - 1, count);
                Assert.LessOrEqual(gap, 0.08f + Eps);
                Assert.Greater(gap, 0f);
            }

            Assert.AreEqual(0f, MenuMotion.CascadeDelay(0, count), Eps, "перший елемент одразу");
        }

        [Test]
        public void Cascade_NormalGapIs70ms_ForTypicalMenu()
        {
            Assert.AreEqual(0.07f, MenuMotion.CascadeDelay(1, 6), Eps);
            float real = MenuMotion.CascadeDelay(1, 9);
            Assert.GreaterOrEqual(real, 0.06f - Eps, "реальне меню (9 елементів): крок 60-80 мс");
            Assert.LessOrEqual(real, 0.08f + Eps);
            Assert.AreEqual(1f, MenuMotion.CascadeOpacity(MenuMotion.CascadeItemSec), Eps);
            Assert.AreEqual(0f, MenuMotion.CascadeOpacity(0f), Eps);
            Assert.AreEqual(0f, MenuMotion.CascadeOffset(MenuMotion.CascadeItemSec), Eps);
            Assert.Greater(MenuMotion.CascadeOffset(0f), 0f);
        }

        [Test]
        public void CurrentPulse_Is1To1_06_PeriodIn1_2To1_6()
        {
            Assert.GreaterOrEqual(MenuMotion.CurrentPulsePeriodSec, 1.2f);
            Assert.LessOrEqual(MenuMotion.CurrentPulsePeriodSec, 1.6f);
            for (float t = 0f; t < 3f; t += 0.02f)
            {
                float p = MenuMotion.CurrentPulse(t);
                Assert.GreaterOrEqual(p, 1f - Eps);
                Assert.LessOrEqual(p, 1.06f + Eps);
            }
        }

        [Test]
        public void MapOrder_LevelOneIsBottomRow_NewestOnTop()
        {
            const int count = 5;
            Assert.AreEqual(count - 1, LevelMapLayout.IndexAtRow(0, count), "верхній рядок - найновіший рівень");
            Assert.AreEqual(0, LevelMapLayout.IndexAtRow(count - 1, count), "нижній рядок - рівень 1");
            for (int row = 1; row < count; row++)
            {
                Assert.Less(LevelMapLayout.IndexAtRow(row, count), LevelMapLayout.IndexAtRow(row - 1, count));
            }
        }

        [Test]
        public void MapScroll_CentersCurrentRow_ClampedToContent()
        {
            Assert.AreEqual(400f, LevelMapLayout.ScrollTarget(500f, 100f, 200f, 1000f), Eps);
            Assert.AreEqual(0f, LevelMapLayout.ScrollTarget(10f, 100f, 200f, 1000f), Eps, "верх - не нижче нуля");
            Assert.AreEqual(800f, LevelMapLayout.ScrollTarget(950f, 100f, 200f, 1000f), Eps, "низ - не далі за вміст");
            Assert.AreEqual(0f, LevelMapLayout.ScrollTarget(50f, 100f, 2000f, 1000f), Eps, "вміст менший за вікно");
        }

        [Test]
        public void MapScroll_FromBottomToCurrent_IsMonotonicUpward()
        {
            float last = 800f;
            for (float t = 0f; t <= MenuMotion.ScrollSec; t += 0.01f)
            {
                float v = MenuMotion.ScrollOffset(800f, 100f, t);
                Assert.LessOrEqual(v, last + Eps);
                last = v;
            }

            Assert.AreEqual(100f, MenuMotion.ScrollOffset(800f, 100f, MenuMotion.ScrollSec), Eps);
        }

        [Test]
        public void Scroll_ReachesTargetWithin400ms_Monotonic()
        {
            Assert.LessOrEqual(MenuMotion.ScrollSec, 0.4f);
            Assert.AreEqual(0f, MenuMotion.ScrollOffset(0f, 500f, 0f), Eps);
            Assert.AreEqual(500f, MenuMotion.ScrollOffset(0f, 500f, MenuMotion.ScrollSec), Eps);
            float last = -1f;
            for (float t = 0f; t <= MenuMotion.ScrollSec; t += 0.01f)
            {
                float v = MenuMotion.ScrollOffset(0f, 500f, t);
                Assert.GreaterOrEqual(v, last);
                last = v;
            }
        }

        [Test]
        public void LockedShake_Is6px_3Cycles_In200To250ms_AndZeroOutside()
        {
            Assert.GreaterOrEqual(MenuMotion.ShakeSec, 0.2f);
            Assert.LessOrEqual(MenuMotion.ShakeSec, 0.25f);
            Assert.AreEqual(6f, MenuMotion.ShakePx, Eps);
            Assert.AreEqual(3, MenuMotion.ShakeCycles);
            float max = 0f;
            int zeroCrossings = 0;
            float prev = 0f;
            for (float t = 0.001f; t < MenuMotion.ShakeSec; t += 0.001f)
            {
                float v = MenuMotion.LockedShake(t);
                max = Mathf.Max(max, Mathf.Abs(v));
                if (prev != 0f && Mathf.Sign(prev) != Mathf.Sign(v) && v != 0f)
                {
                    zeroCrossings++;
                }

                prev = v;
            }

            Assert.AreEqual(6f, max, 0.1f);
            Assert.GreaterOrEqual(zeroCrossings, 4, "3 повні цикли дають 5-6 перетинів нуля");
            Assert.AreEqual(0f, MenuMotion.LockedShake(MenuMotion.ShakeSec + 0.01f), Eps);
            Assert.AreEqual(0f, MenuMotion.LockedShake(-1f), Eps);
        }

        [Test]
        public void Unlock_LockFliesAndVanishesIn300To400ms_NodePopsWithOvershoot_Total600To900ms()
        {
            Assert.GreaterOrEqual(MenuMotion.LockFlySec, 0.3f);
            Assert.LessOrEqual(MenuMotion.LockFlySec, 0.4f);
            Assert.AreEqual(1f, MenuMotion.LockScale(0f), Eps);
            Assert.AreEqual(1.3f, MenuMotion.LockScale(MenuMotion.LockFlySec), Eps);
            Assert.AreEqual(1f, MenuMotion.LockOpacity(0f), Eps);
            Assert.AreEqual(0f, MenuMotion.LockOpacity(MenuMotion.LockFlySec), Eps);
            Assert.GreaterOrEqual(MenuMotion.UnlockTotalSec, 0.6f);
            Assert.LessOrEqual(MenuMotion.UnlockTotalSec, 0.9f);

            float max = 0f;
            for (float t = MenuMotion.NodePopStartSec; t < MenuMotion.UnlockTotalSec; t += 0.005f)
            {
                max = Mathf.Max(max, MenuMotion.NodePop(t));
            }

            Assert.Greater(max, 1f, "ease-out-back перелітає 1");
            Assert.AreEqual(1f, MenuMotion.NodePop(MenuMotion.UnlockTotalSec + 0.1f), Eps);
            Assert.IsFalse(MenuMotion.NodeOpened(0f));
            Assert.IsTrue(MenuMotion.NodeOpened(MenuMotion.NodePopStartSec));
        }

        [Test]
        public void NewStars_AppearOneByOne_With120To180msGap()
        {
            float gap = MenuMotion.StarAt(1) - MenuMotion.StarAt(0);
            Assert.GreaterOrEqual(gap, 0.12f);
            Assert.LessOrEqual(gap, 0.18f);
            Assert.AreEqual(gap, MenuMotion.StarAt(2) - MenuMotion.StarAt(1), Eps);
            Assert.IsFalse(MenuMotion.StarShown(0, MenuMotion.StarAt(0) - 0.01f));
            Assert.IsTrue(MenuMotion.StarShown(0, MenuMotion.StarAt(0)));
            Assert.IsFalse(MenuMotion.StarShown(1, MenuMotion.StarAt(0)));
            Assert.Greater(MenuMotion.StarPop(0, MenuMotion.StarAt(0) + (MenuMotion.StarPopSec / 2f)), 1f);
            Assert.AreEqual(1f, MenuMotion.StarPop(0, MenuMotion.StarAt(0) + 1f), Eps);
        }

        [Test]
        public void Reduced_AllMenuMotionIsAtRest_AndInstant()
        {
            UiMotion.Reduced = true;
            Assert.AreEqual(1f, MenuMotion.LogoSway(0.7f), Eps);
            Assert.AreEqual(1f, MenuMotion.LightOpacity(1, 0.3f), Eps);
            Assert.AreEqual(1f, MenuMotion.PlayBreath(0.3f), Eps);
            Assert.AreEqual(1f, MenuMotion.CurrentPulse(0.3f), Eps);
            Assert.AreEqual(0f, MenuMotion.MarkerBob(0.3f), Eps);
            Assert.AreEqual(1f, MenuMotion.CascadeOpacity(0f), Eps);
            Assert.AreEqual(0f, MenuMotion.CascadeOffset(0f), Eps);
            Assert.AreEqual(0f, MenuMotion.LockedShake(0.05f), Eps);
            Assert.AreEqual(300f, MenuMotion.ScrollOffset(0f, 300f, 0f), Eps);
            Assert.IsTrue(MenuMotion.NodeOpened(0f));
            Assert.AreEqual(1f, MenuMotion.NodePop(MenuMotion.NodePopStartSec + 0.1f), Eps);
            Assert.AreEqual(0f, MenuMotion.LockOpacity(0f), Eps);
            Assert.IsTrue(MenuMotion.StarShown(2, 0f));
        }

        // ---------- розкладка шляху ----------

        [Test]
        public void Layout_NodesZigzag_AndNodeIsAtLeast64dp()
        {
            Assert.GreaterOrEqual(LevelMapLayout.NodeSize, 64f);
            Assert.Less(LevelMapLayout.NodeOffsetX(0), 0f);
            Assert.Greater(LevelMapLayout.NodeOffsetX(1), 0f);
            Assert.AreEqual(LevelMapLayout.NodeOffsetX(0), LevelMapLayout.NodeOffsetX(2), Eps);
        }

        [Test]
        public void Layout_DotsLieBetweenTwoNodes_InOrder()
        {
            float a = LevelMapLayout.NodeOffsetX(0);
            float b = LevelMapLayout.NodeOffsetX(1);
            float last = a;
            for (int d = 0; d < LevelMapLayout.DotsPerLink; d++)
            {
                float x = LevelMapLayout.DotOffsetX(0, d);
                Assert.Greater(x, last);
                Assert.Less(x, b);
                last = x;
            }
        }

        [Test]
        public void Layout_CurrentIsFirstUnlockedWithoutStars_ElseLastUnlocked()
        {
            var fresh = new[] { new LevelEntry(1, 0, true), new LevelEntry(2, 0, false), new LevelEntry(3, 0, false) };
            Assert.AreEqual(0, LevelMapLayout.CurrentIndex(fresh));
            var mid = new[] { new LevelEntry(1, 3, true), new LevelEntry(2, 1, true), new LevelEntry(3, 0, true), new LevelEntry(4, 0, false) };
            Assert.AreEqual(2, LevelMapLayout.CurrentIndex(mid));
            var done = new[] { new LevelEntry(1, 1, true), new LevelEntry(2, 2, true) };
            Assert.AreEqual(1, LevelMapLayout.CurrentIndex(done), "усе пройдено - останній відкритий");
            Assert.AreEqual(-1, LevelMapLayout.CurrentIndex(new LevelEntry[0]));
            Assert.AreEqual(-1, LevelMapLayout.CurrentIndex(null));
        }

        [Test]
        public void Layout_RequiredNumberIsPreviousLevel()
        {
            var e = new[] { new LevelEntry(1, 0, true), new LevelEntry(2, 0, false), new LevelEntry(3, 0, false) };
            Assert.AreEqual(0, LevelMapLayout.RequiredNumber(e, 0));
            Assert.AreEqual(1, LevelMapLayout.RequiredNumber(e, 1));
            Assert.AreEqual(2, LevelMapLayout.RequiredNumber(e, 2));
            Assert.AreEqual(0, LevelMapLayout.RequiredNumber(e, 9));
        }

        [Test]
        public void Layout_CurrentFollowsRealProgress()
        {
            LevelCatalog levels = ConfigTestKit.LoadCatalog();
            var data = new SaveData();
            Assert.AreEqual(0, LevelMapLayout.CurrentIndex(LevelProgress.Build(levels, data)));
            data.LevelStars[levels.Get(0).Id] = 2;
            Assert.AreEqual(1, LevelMapLayout.CurrentIndex(LevelProgress.Build(levels, data)));
        }

        // ---------- картка рівня ----------

        [Test]
        public void LevelCard_ShowsGoalAndStarThresholds_FromConfig_ForEveryLevel()
        {
            LevelCatalog levels = ConfigTestKit.LoadCatalog();
            for (int i = 0; i < levels.Count; i++)
            {
                LevelDefinition level = levels.Get(i);
                LevelCardLine[] lines = LevelCard.Lines(level);
                Assert.GreaterOrEqual(lines.Length, 3, level.Id);
                Assert.AreEqual(0, lines[0].Stars);
                Assert.AreEqual(level.Goal.Target, lines[0].Number, level.Id);
                Assert.AreEqual(level.Goal.Type == GoalType.Coins ? LevelCard.GoalCoinsKey : LevelCard.GoalServeKey, lines[0].Key);
                Assert.AreEqual(1, lines[1].Stars);
                Assert.AreEqual(2, lines[2].Stars);
                Assert.AreEqual(level.Stars.TwoCoins, lines[2].Number, level.Id);
                if (level.Stars.ThreeCondition != StarCondition.None)
                {
                    Assert.AreEqual(4, lines.Length, level.Id);
                    Assert.AreEqual(3, lines[3].Stars);
                }
            }
        }

        [Test]
        public void LevelCard_ThreeStarKeys_MatchEveryCondition()
        {
            var lose = new LevelLose(null);
            LevelDefinition Make(StarCondition c, int v)
            {
                LevelDefinition src = ConfigTestKit.LoadCatalog().Get(0);
                return new LevelDefinition(src.Id, src.Number, src.Location, src.NameKey, src.Draft, src.Shift, src.Goal, new LevelStars(100, c, v), lose, new string[0], null, null);
            }

            Assert.AreEqual(LevelCard.Star3NoLostKey, LevelCard.Lines(Make(StarCondition.NoLostCustomers, 0))[3].Key);
            Assert.AreEqual(LevelCard.Star3NoBurntKey, LevelCard.Lines(Make(StarCondition.NoBurnt, 0))[3].Key);
            Assert.AreEqual(LevelCard.Star3NoWrongKey, LevelCard.Lines(Make(StarCondition.NoWrongDishes, 0))[3].Key);
            LevelCardLine combo = LevelCard.Lines(Make(StarCondition.Combo, 5))[3];
            Assert.AreEqual(LevelCard.Star3ComboKey, combo.Key);
            Assert.AreEqual(5L, combo.Number);
            Assert.IsTrue(combo.HasNumber);
            Assert.AreEqual(3, LevelCard.Lines(Make(StarCondition.None, 0)).Length);
            Assert.Throws<System.ArgumentNullException>(() => LevelCard.Lines(null));
        }

        // ---------- нові зірки (QB-LVL-06) ----------

        [Test]
        public void GameContext_StarsGain_IsRememberedOnce()
        {
            var context = new GameContext(new InMemorySaveStorage(), new ManualClock(1000), new PlaceholderLocalizer());
            int level;
            int from;
            Assert.IsFalse(context.TakeStarsGain(out level, out from));
            context.MarkStarsGained(3, 1);
            Assert.IsTrue(context.TakeStarsGain(out level, out from));
            Assert.AreEqual(3, level);
            Assert.AreEqual(1, from);
            Assert.IsFalse(context.TakeStarsGain(out level, out from), "показується один раз");
        }

        // ---------- P3 з QA Q-05e ----------

        [Test]
        public void Fader_Cancel_RemovesOverlay_AndAllowsNewLeave()
        {
            var root = new VisualElement();
            var content = new VisualElement();
            root.Add(content);
            var fader = new ScreenFader(root, content);
            fader.PlayIn();
            _clock += ScreenTransition.InSec + 0.01f;
            fader.Tick();

            int loads = 0;
            fader.Leave(() => loads++);
            _clock += ScreenTransition.OutSec + 0.01f;
            fader.Tick();
            Assert.AreEqual(1, loads);
            Assert.AreEqual(PickingMode.Position, root.Q(ScreenFader.OverlayName).pickingMode, "до Cancel накладка ковтає ввід");

            fader.Cancel(); // сцену не знайдено в збірці
            Assert.IsFalse(fader.Busy);
            Assert.AreEqual(0f, fader.OverlayOpacity, Eps);
            Assert.AreEqual(0f, fader.ContentOffset, Eps);
            Assert.AreEqual(PickingMode.Ignore, root.Q(ScreenFader.OverlayName).pickingMode);

            Assert.IsTrue(fader.Leave(() => loads++), "після скасування новий перехід можливий");
        }

        // ---------- локалізація ----------

        [Test]
        public void MenuKeys_ExistInPlaceholderTable()
        {
            ILocalizer text = new PlaceholderLocalizer();
            string[] keys =
            {
                TextKeys.MenuLevelLockedHint, TextKeys.LevelStart, TextKeys.LevelGoalServe, TextKeys.LevelGoalCoins,
                TextKeys.LevelStar1, TextKeys.LevelStar2, TextKeys.LevelStar3NoLost, TextKeys.LevelStar3NoBurnt,
                TextKeys.LevelStar3NoWrong, TextKeys.LevelStar3Combo,
            };
            foreach (string key in keys)
            {
                StringAssert.DoesNotStartWith("[", text.Get(key), key);
            }
        }
    }
}
