using KebabStreet.Core;
using KebabStreet.Game;
using KebabStreet.Services;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>Екран результату зміни (S1-10): модель для ★/★★/★★★ і кожної причини поразки, кнопки «Ще раз»/«Далі».</summary>
    public sealed class ResultViewTests
    {
        private static readonly ILocalizer Text = new PlaceholderLocalizer();

        [TearDown]
        public void ResetContext()
        {
            GameContext.ResetForTests();
        }

        private static ShiftResult Won(int stars, long coins)
        {
            return new ShiftResult(ShiftOutcome.Won, stars, null, 0L, coins, coins, 3L, 0L, 0L, 0L, 1L);
        }

        private static ShiftResult Lost(string reason, long shortfall)
        {
            return new ShiftResult(ShiftOutcome.Lost, 0, reason, shortfall, 0L, 15L, 1L, 2L, 0L, 0L, 1L);
        }

        private static GameContext Context()
        {
            var context = new GameContext(new InMemorySaveStorage(), new ManualClock(1700000000), Text);
            context.Profile.Load();
            Assert.IsTrue(context.Flow.BootCompleted());
            return context;
        }

        [Test]
        public void OneStar_ExplainsTwoStarCoins()
        {
            LevelDefinition level = ConfigTestKit.Level(1);
            ResultView view = ResultView.Build(Won(1, 39L), level, true);

            Assert.IsTrue(view.Won);
            Assert.AreEqual(1, view.Stars);
            Assert.AreEqual(39L, view.RewardCoins);
            Assert.AreEqual(TextKeys.ShiftResultWon, view.TitleKey);
            Assert.AreEqual(new ResultLine(TextKeys.ResultWhyStar2, level.Stars.TwoCoins), view.StarHint);
            Assert.IsTrue(view.Reason.IsEmpty);
            Assert.IsTrue(view.Shortfall.IsEmpty);
            StringAssert.Contains(level.Stars.TwoCoins.ToString(), ResultView.Format(Text, view.StarHint));
        }

        [TestCase(1, StarCondition.NoBurnt, TextKeys.ResultWhyStar3NoBurnt)]
        [TestCase(3, StarCondition.NoLostCustomers, TextKeys.ResultWhyStar3NoLost)]
        [TestCase(4, StarCondition.NoWrongDishes, TextKeys.ResultWhyStar3NoWrong)]
        [TestCase(5, StarCondition.Combo, TextKeys.ResultWhyStar3Combo)]
        public void TwoStars_ExplainsThirdStarCondition_PerLevel(int levelNumber, StarCondition condition, string key)
        {
            LevelDefinition level = ConfigTestKit.Level(levelNumber);
            Assert.AreEqual(condition, level.Stars.ThreeCondition);

            ResultView view = ResultView.Build(Won(2, 100L), level, true);

            Assert.AreEqual(2, view.Stars);
            Assert.AreEqual(key, view.StarHint.Key);
            if (condition == StarCondition.Combo)
            {
                Assert.AreEqual((long)level.Stars.ThreeComboValue, view.StarHint.Number);
                StringAssert.Contains(level.Stars.ThreeComboValue.ToString(), ResultView.Format(Text, view.StarHint));
            }
            else
            {
                Assert.IsFalse(view.StarHint.Number.HasValue);
            }
        }

        [Test]
        public void ThreeStars_HasNoHint_AndShowsReward()
        {
            ResultView view = ResultView.Build(Won(3, 130L), ConfigTestKit.Level(3), true);

            Assert.AreEqual(3, view.Stars);
            Assert.AreEqual(130L, view.RewardCoins);
            Assert.IsTrue(view.StarHint.IsEmpty);
            Assert.AreEqual(string.Empty, ResultView.Format(Text, view.StarHint));
        }

        [TestCase(LoseReasons.TooSlow)]
        [TestCase(LoseReasons.CustomersLeft)]
        [TestCase(LoseReasons.Burnt)]
        [TestCase(LoseReasons.WrongDishes)]
        public void Lost_ShowsReason_NoStarsNoReward_ForEveryReason(string reason)
        {
            ResultView view = ResultView.Build(Lost(reason, 2L), ConfigTestKit.Level(1), true);

            Assert.IsFalse(view.Won);
            Assert.AreEqual(0, view.Stars);
            Assert.AreEqual(0L, view.RewardCoins);
            Assert.AreEqual(TextKeys.ShiftResultLost, view.TitleKey);
            Assert.AreEqual(reason, view.Reason.Key);
            Assert.AreNotEqual("[" + reason + "]", Text.Get(reason), "ключ причини має переклад");
            Assert.IsTrue(view.StarHint.IsEmpty);
            Assert.IsTrue(view.CanRetry);
            Assert.IsFalse(view.CanNext, "«Далі» після поразки недоступна, навіть коли наступний рівень є");
        }

        [Test]
        public void Lost_Shortfall_UsesGoalTypeKey_AndIsEmptyWhenNothingMissing()
        {
            ResultView serve = ResultView.Build(Lost(LoseReasons.TooSlow, 2L), ConfigTestKit.Level(1), false);
            Assert.AreEqual(new ResultLine(TextKeys.ResultShortfallServe, 2L), serve.Shortfall);

            ResultView coins = ResultView.Build(Lost(LoseReasons.TooSlow, 50L), ConfigTestKit.Level(4), false);
            Assert.AreEqual(new ResultLine(TextKeys.ResultShortfallCoins, 50L), coins.Shortfall);

            ResultView none = ResultView.Build(Lost(LoseReasons.Burnt, 0L), ConfigTestKit.Level(1), false);
            Assert.IsTrue(none.Shortfall.IsEmpty);
        }

        [Test]
        public void Next_OnlyAfterWin_AndOnlyWhenNextLevelExists()
        {
            LevelDefinition level = ConfigTestKit.Level(2);
            Assert.IsTrue(ResultView.Build(Won(1, 10L), level, true).CanNext);
            Assert.IsFalse(ResultView.Build(Won(3, 10L), level, false).CanNext, "останній рівень");
            Assert.IsFalse(ResultView.Build(Lost(LoseReasons.TooSlow, 1L), level, true).CanNext);
            Assert.IsTrue(ResultView.Build(Won(1, 10L), level, false).CanRetry);
        }

        [Test]
        public void Retry_KeepsLevel_AndGivesNewSeed()
        {
            GameContext context = Context();
            Assert.IsTrue(context.SelectLevel(2));
            Assert.IsTrue(context.Flow.Play());
            ulong first = context.NextShiftSeed();

            Assert.IsTrue(context.RetryShift());
            ulong second = context.NextShiftSeed();

            Assert.AreEqual(2, context.SelectedLevelNumber);
            Assert.AreEqual(GameScreen.Shift, context.Flow.Current);
            Assert.AreNotEqual(first, second, "нова спроба — новий seed");
        }

        [Test]
        public void Retry_OutsideShift_IsRejected()
        {
            GameContext context = Context();
            Assert.IsTrue(context.SelectLevel(1));
            Assert.AreEqual(GameScreen.Menu, context.Flow.Current);
            Assert.IsFalse(context.RetryShift());
        }

        [Test]
        public void Advance_AfterWin_SelectsNextLevel_ButNotAfterLossOrOnLastLevel()
        {
            GameContext context = Context();
            Assert.IsTrue(context.SelectLevel(1));
            Assert.IsTrue(context.Flow.Play());

            Assert.IsFalse(context.AdvanceToNextLevel(false), "після поразки");
            Assert.AreEqual(1, context.SelectedLevelNumber);

            Assert.IsFalse(context.AdvanceToNextLevel(true), "рівень 2 ще закритий: перемогу не записано");
            Assert.AreEqual(1, context.SelectedLevelNumber);

            context.Profile.Data.LevelStars[ConfigTestKit.Level(1).Id] = 1;
            Assert.IsTrue(context.AdvanceToNextLevel(true));
            Assert.AreEqual(2, context.SelectedLevelNumber);
            Assert.AreEqual(GameScreen.Shift, context.Flow.Current);

            LevelCatalog levels = context.Levels;
            int last = levels.Get(levels.Count - 1).Number;
            for (int i = 0; i < levels.Count; i++)
            {
                context.Profile.Data.LevelStars[levels.Get(i).Id] = 1;
            }

            Assert.IsTrue(context.SelectLevel(last));
            Assert.AreEqual(0, context.NextLevelNumber);
            Assert.IsFalse(context.AdvanceToNextLevel(true), "останній рівень");
            Assert.AreEqual(last, context.SelectedLevelNumber);
        }

        [Test]
        public void Screen_LostShift_ShowsReasonShortfallAndButtons()
        {
            LevelDefinition level = ConfigTestKit.Level(3);
            var driver = new ShiftDriver(level, 20284958UL);
            Assert.IsTrue(driver.Begin());
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { }, () => { }, () => { }, true);

            for (int guard = 0; guard < 100000 && !driver.IsFinished; guard++)
            {
                driver.Frame(1.0);
            }

            Assert.IsTrue(driver.IsFinished);
            Assert.IsFalse(driver.Result.Won);
            screen.Refresh();

            Assert.IsTrue(screen.ResultPanelVisible);
            Assert.AreEqual(Text.Get(driver.Result.LoseReason), screen.ResultReasonText);
            Assert.AreEqual(
                ResultView.Format(Text, ResultView.Build(driver.Result, level, true).Shortfall), screen.ResultShortfallText);
            Assert.AreEqual(string.Empty, screen.ResultStarHintText);
            Assert.IsTrue(screen.RetryVisible);
            Assert.IsFalse(screen.NextVisible, "після поразки «Далі» прихована");
            Assert.IsFalse(screen.Result.Won);
        }

        [Test]
        public void Screen_WithoutRetryCallback_HidesRetry()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(1), 20284958UL);
            driver.Begin();
            var screen = new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });
            Assert.IsFalse(screen.RetryVisible);
            Assert.IsFalse(screen.NextVisible);
        }
    }
}
