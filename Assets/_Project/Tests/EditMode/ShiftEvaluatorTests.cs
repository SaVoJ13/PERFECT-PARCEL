using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Результат зміни і зірки (S1-06): ціль, зірки з межами порогів, кожна умова ★★★, причини
    /// поразки і їх пріоритет, ADR-020, дострокове завершення, детермінізм, інтеграція з ботом.
    /// Правила — GAME_DESIGN.md розд. 1, 7, 9. Чисті перевірки йдуть через <see cref="ShiftCounters"/>.
    /// </summary>
    public sealed class ShiftEvaluatorTests
    {
        private const ulong Seed = 20261201UL;

        private static ShiftEvaluator Serve(
            int target, int twoCoins, StarCondition three, int combo = 0, int? maxLost = null,
            int? maxBurnt = null, int? maxWrong = null)
        {
            return new ShiftEvaluator(
                new LevelGoal(GoalType.Serve, target),
                new LevelStars(twoCoins, three, combo),
                new LevelLose(maxLost, maxBurnt, maxWrong));
        }

        private static ShiftEvaluator Earn(int target, int twoCoins, StarCondition three)
        {
            return new ShiftEvaluator(
                new LevelGoal(GoalType.Coins, target),
                new LevelStars(twoCoins, three, 0),
                new LevelLose(null));
        }

        private static ShiftCounters C(
            long coins = 0, long served = 0, long lost = 0, long wrong = 0, long burnt = 0, long combo = 0)
        {
            return new ShiftCounters(coins, served, lost, wrong, burnt, combo);
        }

        private static LevelDefinition Load(ConfigSources sources, int number)
        {
            ConfigResult<LevelCatalog> loaded = ConfigLoader.LoadAll(sources);
            Assert.IsTrue(loaded.IsValid, ConfigTestKit.Join(loaded.Errors));
            LevelDefinition level = loaded.Value.FindByNumber(number);
            Assert.IsNotNull(level);
            return level;
        }

        // ---- ціль ----

        [Test]
        public void ServeGoal_ExactlyTarget_Wins_OneLess_LosesWithShortfallOne()
        {
            ShiftEvaluator e = Serve(8, 100, StarCondition.NoBurnt);
            Assert.IsTrue(e.Evaluate(C(served: 8)).Won);
            ShiftResult miss = e.Evaluate(C(served: 7));
            Assert.AreEqual(ShiftOutcome.Lost, miss.Outcome);
            Assert.AreEqual(1L, miss.Shortfall);
            Assert.AreEqual(0, miss.Stars);
        }

        [Test]
        public void CoinsGoal_ExactlyTarget_Wins_OneLess_LosesWithShortfallOne()
        {
            ShiftEvaluator e = Earn(150, 190, StarCondition.NoWrongDishes);
            Assert.IsTrue(e.Evaluate(C(coins: 150)).Won);
            ShiftResult miss = e.Evaluate(C(coins: 149, served: 99));
            Assert.AreEqual(ShiftOutcome.Lost, miss.Outcome);
            Assert.AreEqual(1L, miss.Shortfall);
            Assert.AreEqual(LoseReasons.TooSlow, miss.LoseReason);
        }

        [Test]
        public void Win_HasNoLoseReasonAndZeroShortfall()
        {
            ShiftResult r = Serve(3, 40, StarCondition.NoBurnt).Evaluate(C(served: 5));
            Assert.IsNull(r.LoseReason);
            Assert.AreEqual(0L, r.Shortfall);
        }

        // ---- зірки ----

        [Test]
        public void OneStar_WhenGoalReachedAndCoinsBelowTwoThreshold()
        {
            ShiftResult r = Serve(3, 40, StarCondition.NoBurnt).Evaluate(C(coins: 39, served: 3));
            Assert.AreEqual(1, r.Stars);
        }

        [Test]
        public void TwoStars_ExactlyAtThreshold_AndOneBelowGivesOne()
        {
            ShiftEvaluator e = Serve(3, 40, StarCondition.NoBurnt);
            Assert.AreEqual(1, e.Evaluate(C(coins: 39, served: 3)).Stars);
            Assert.AreEqual(3, e.Evaluate(C(coins: 40, served: 3)).Stars, "поріг і умова ★★★ виконані");
            Assert.AreEqual(2, e.Evaluate(C(coins: 40, served: 3, burnt: 1)).Stars, "поріг є, умови ★★★ немає");
        }

        [Test]
        public void ThreeStars_RequireTwoStarThreshold()
        {
            // Умова ★★★ виконана, але монет менше за поріг ★★: зірки накопичуються по порядку, тож лише одна.
            ShiftResult r = Serve(3, 40, StarCondition.NoBurnt).Evaluate(C(coins: 39, served: 3, burnt: 0));
            Assert.AreEqual(1, r.Stars);
        }

        [Test]
        public void ThreeStars_NoLostCustomers_ZeroLostOnly()
        {
            ShiftEvaluator e = Serve(8, 110, StarCondition.NoLostCustomers, maxLost: 1);
            Assert.AreEqual(3, e.Evaluate(C(coins: 110, served: 8, lost: 0)).Stars);
            Assert.AreEqual(2, e.Evaluate(C(coins: 110, served: 8, lost: 1)).Stars);
        }

        [Test]
        public void ThreeStars_NoBurnt_ZeroBurntOnly()
        {
            ShiftEvaluator e = Serve(6, 85, StarCondition.NoBurnt);
            Assert.AreEqual(3, e.Evaluate(C(coins: 85, served: 6, burnt: 0)).Stars);
            Assert.AreEqual(2, e.Evaluate(C(coins: 85, served: 6, burnt: 1)).Stars);
        }

        [Test]
        public void ThreeStars_NoWrongDishes_ZeroWrongOnly()
        {
            ShiftEvaluator e = Earn(150, 190, StarCondition.NoWrongDishes);
            Assert.AreEqual(3, e.Evaluate(C(coins: 190, wrong: 0)).Stars);
            Assert.AreEqual(2, e.Evaluate(C(coins: 190, wrong: 1)).Stars);
        }

        [Test]
        public void ThreeStars_Combo_AtValueYes_OneBelowNo()
        {
            var e = new ShiftEvaluator(
                new LevelGoal(GoalType.Coins, 200),
                new LevelStars(250, StarCondition.Combo, 3),
                new LevelLose(null));
            Assert.AreEqual(3, e.Evaluate(C(coins: 250, combo: 3)).Stars);
            Assert.AreEqual(3, e.Evaluate(C(coins: 250, combo: 4)).Stars);
            Assert.AreEqual(2, e.Evaluate(C(coins: 250, combo: 2)).Stars);
        }

        [Test]
        public void ThreeStars_NoCondition_IsNeverGiven()
        {
            ShiftResult r = Serve(3, 40, StarCondition.None).Evaluate(C(coins: 1000, served: 3));
            Assert.AreEqual(2, r.Stars);
        }

        // ---- причини поразки ----

        [Test]
        public void LoseReason_TooSlow_WhenNoLimitBreached()
        {
            ShiftResult r = Serve(3, 40, StarCondition.NoBurnt, maxLost: 1).Evaluate(C(served: 2, lost: 1));
            Assert.AreEqual(LoseReasons.TooSlow, r.LoseReason);
            Assert.AreEqual("lost.reason.tooSlow", r.LoseReason);
            Assert.AreEqual(1L, r.Shortfall);
        }

        [Test]
        public void LoseReason_CustomersLeft_OnlyAboveLimit()
        {
            ShiftEvaluator e = Serve(8, 110, StarCondition.NoLostCustomers, maxLost: 1);
            Assert.AreEqual(LoseReasons.TooSlow, e.Evaluate(C(served: 3, lost: 1)).LoseReason, "рівно ліміт — ще не причина");
            ShiftResult r = e.Evaluate(C(served: 3, lost: 2));
            Assert.AreEqual(LoseReasons.CustomersLeft, r.LoseReason);
            Assert.AreEqual("lost.reason.customersLeft", r.LoseReason);
            Assert.AreEqual(5L, r.Shortfall);
        }

        [Test]
        public void LoseReason_Burnt_AboveLimit()
        {
            ShiftEvaluator e = Serve(8, 110, StarCondition.NoBurnt, maxBurnt: 2);
            Assert.AreEqual(LoseReasons.TooSlow, e.Evaluate(C(served: 3, burnt: 2)).LoseReason);
            Assert.AreEqual("lost.reason.burnt", e.Evaluate(C(served: 3, burnt: 3)).LoseReason);
        }

        [Test]
        public void LoseReason_WrongDishes_AboveLimit()
        {
            ShiftEvaluator e = Serve(8, 110, StarCondition.NoBurnt, maxWrong: 2);
            Assert.AreEqual(LoseReasons.TooSlow, e.Evaluate(C(served: 3, wrong: 2)).LoseReason);
            Assert.AreEqual("lost.reason.wrongDishes", e.Evaluate(C(served: 3, wrong: 3)).LoseReason);
        }

        [Test]
        public void LoseReason_Priority_CustomersLeft_ThenBurnt_ThenWrongDishes_ThenTooSlow()
        {
            ShiftEvaluator e = Serve(8, 110, StarCondition.NoBurnt, maxLost: 0, maxBurnt: 0, maxWrong: 0);
            Assert.AreEqual(LoseReasons.CustomersLeft, e.Evaluate(C(lost: 1, burnt: 1, wrong: 1)).LoseReason);
            Assert.AreEqual(LoseReasons.Burnt, e.Evaluate(C(burnt: 1, wrong: 1)).LoseReason);
            Assert.AreEqual(LoseReasons.WrongDishes, e.Evaluate(C(wrong: 1)).LoseReason);
            Assert.AreEqual(LoseReasons.TooSlow, e.Evaluate(C()).LoseReason);
        }

        [Test]
        public void LimitBreach_LosesEvenWhenGoalReached()
        {
            ShiftResult r = Serve(8, 110, StarCondition.NoLostCustomers, maxLost: 1)
                .Evaluate(C(coins: 500, served: 8, lost: 2));
            Assert.AreEqual(ShiftOutcome.Lost, r.Outcome);
            Assert.AreEqual(LoseReasons.CustomersLeft, r.LoseReason);
            Assert.AreEqual(0L, r.Shortfall, "ціль досягнуто, програш через межу");
            Assert.AreEqual(0, r.Stars);
        }

        [Test]
        public void Reward_IsCoinsEarnedOnWin_ZeroOnLoss()
        {
            ShiftEvaluator e = Serve(3, 40, StarCondition.NoBurnt);
            Assert.AreEqual(52L, e.Evaluate(C(coins: 52, served: 3)).RewardCoins);
            ShiftResult lost = e.Evaluate(C(coins: 52, served: 2));
            Assert.AreEqual(0L, lost.RewardCoins);
            Assert.AreEqual(52L, lost.CoinsEarned, "зароблене лишається в лічильниках");
        }

        [Test]
        public void Result_CopiesCounters()
        {
            ShiftResult r = Serve(3, 1, StarCondition.NoBurnt).Evaluate(C(coins: 7, served: 3, lost: 4, wrong: 5, burnt: 6, combo: 9));
            Assert.AreEqual(7L, r.CoinsEarned);
            Assert.AreEqual(3L, r.ServedCount);
            Assert.AreEqual(4L, r.LostCustomers);
            Assert.AreEqual(5L, r.WrongDishes);
            Assert.AreEqual(6L, r.BurntCount);
            Assert.AreEqual(9L, r.MaxCombo);
        }

        [Test]
        public void Evaluator_RejectsMissingData()
        {
            Assert.Throws<System.ArgumentNullException>(() => new ShiftEvaluator(null, new LevelStars(1, StarCondition.None, 0), new LevelLose(null)));
            Assert.Throws<System.ArgumentNullException>(() => ShiftEvaluator.ForLevel(null));
        }

        // ---- ADR-020 (рівні 1–2: втрата клієнтів не веде до поразки) ----

        [TestCase(1)]
        [TestCase(2)]
        public void Adr020_TrainingLevels_LostCustomersNeverLose(int number)
        {
            ShiftEvaluator e = ShiftEvaluator.ForLevel(ConfigTestKit.Level(number));
            int target = ConfigTestKit.Level(number).Goal.Target;
            ShiftResult r = e.Evaluate(C(coins: 10, served: target, lost: 99));
            Assert.AreEqual(ShiftOutcome.Won, r.Outcome);
            Assert.IsFalse(e.ShouldEndEarly(C(served: 0, lost: 99)), "втрата клієнтів не завершує зміну на навчальних рівнях");
        }

        [Test]
        public void Level3_LostLimitFromConfig_IsOne()
        {
            ShiftEvaluator e = ShiftEvaluator.ForLevel(ConfigTestKit.Level(3));
            Assert.IsFalse(e.ShouldEndEarly(C(served: 2, lost: 1)));
            Assert.IsTrue(e.ShouldEndEarly(C(served: 2, lost: 2)));
        }

        // ---- дострокове завершення ----

        [Test]
        public void EndOnGoal_DefaultTrueForServe_FalseForCoins()
        {
            Assert.IsTrue(new LevelGoal(GoalType.Serve, 3).EndOnGoal);
            Assert.IsFalse(new LevelGoal(GoalType.Coins, 100).EndOnGoal);
            Assert.IsTrue(ConfigTestKit.Level(1).Goal.EndOnGoal);
            Assert.IsFalse(ConfigTestKit.Level(4).Goal.EndOnGoal);
        }

        [Test]
        public void ShouldEndEarly_ServeGoal_AtNthServedOnly()
        {
            ShiftEvaluator e = Serve(3, 40, StarCondition.NoBurnt);
            Assert.IsFalse(e.ShouldEndEarly(C(served: 2)));
            Assert.IsTrue(e.ShouldEndEarly(C(served: 3)));
        }

        [Test]
        public void ShouldEndEarly_CoinsGoal_RunsToTimerEnd_UnlessEndOnGoalSet()
        {
            var stars = new LevelStars(250, StarCondition.NoBurnt, 0);
            var lose = new LevelLose(null);
            var byDefault = new ShiftEvaluator(new LevelGoal(GoalType.Coins, 100), stars, lose);
            var explicitEnd = new ShiftEvaluator(new LevelGoal(GoalType.Coins, 100, true), stars, lose);
            Assert.IsFalse(byDefault.ShouldEndEarly(C(coins: 500)));
            Assert.IsFalse(explicitEnd.ShouldEndEarly(C(coins: 99)));
            Assert.IsTrue(explicitEnd.ShouldEndEarly(C(coins: 100)));
        }

        [Test]
        public void ShouldEndEarly_ServeGoalWithEndOnGoalOff_KeepsRunning()
        {
            var e = new ShiftEvaluator(
                new LevelGoal(GoalType.Serve, 3, false), new LevelStars(40, StarCondition.NoBurnt, 0), new LevelLose(null));
            Assert.IsFalse(e.ShouldEndEarly(C(served: 3)));
        }

        [Test]
        public void Simulation_Level1_EndsOnThirdServed_BeforeTimerEnd()
        {
            LevelDefinition level = ConfigTestKit.Level(1);
            ShiftEvaluator e = ShiftEvaluator.ForLevel(level);
            var sim = new ShiftSimulation(level.Shift, Seed, e);
            var bot = new BotPlayer(sim, BotProfile.Expert, Seed);
            sim.Start();
            while (sim.State == ShiftState.Running)
            {
                bot.Step();
                sim.Tick();
            }

            Assert.AreEqual(ShiftState.Won, sim.State);
            Assert.AreEqual(3L, sim.ServedCount);
            Assert.Less(sim.ElapsedTicks, level.Shift.DurationTicks, "зміна завершилась достроково");
            Assert.Greater(sim.RemainingTicks, 0L);
            Assert.IsTrue(e.Evaluate(sim).Won, "оцінювач і стан симуляції збігаються");
        }

        [Test]
        public void Simulation_EndOnGoalFalse_PlaysToTimerEnd_AndStillWins()
        {
            ConfigSources changed = ConfigTestKit.Mutated("L01", tree =>
                ConfigTestKit.Map(tree, "goal")["endOnGoal"] = false);
            LevelDefinition level = Load(changed, 1);
            Assert.IsFalse(level.Goal.EndOnGoal);

            BotResult r = BotRunner.Run(level, Seed, BotProfile.Expert);

            Assert.AreEqual(BotOutcome.Won, r.Outcome);
            Assert.AreEqual(level.Shift.DurationTicks, r.EndTick, "без endOnGoal зміна триває до кінця таймера");
            Assert.AreEqual(4L, r.Served);
        }

        [Test]
        public void Simulation_LostLimit_EndsImmediatelyAsLost()
        {
            ConfigSources broken = ConfigTestKit.Mutated(ConfigTestKit.Customers, tree =>
            {
                List<object> types = ConfigTestKit.Arr(tree, "customers");
                for (int i = 0; i < types.Count; i++)
                {
                    ConfigTestKit.Item(types, i)["patienceSec"] = 0.5d;
                }
            });
            LevelDefinition level = Load(broken, 3);
            ShiftEvaluator e = ShiftEvaluator.ForLevel(level);
            var sim = new ShiftSimulation(level.Shift, Seed, e);
            var bot = new BotPlayer(sim, BotProfile.Expert, Seed);
            sim.Start();
            while (sim.State == ShiftState.Running)
            {
                bot.Step();
                sim.Tick();
            }

            Assert.AreEqual(ShiftState.Lost, sim.State);
            Assert.Less(sim.ElapsedTicks, level.Shift.DurationTicks, "поразка з ліміту втрат — одразу, не в кінці таймера");
            Assert.GreaterOrEqual(sim.LostCustomers, 2L, "зміна зупинилась, щойно втрачено більше за ліміт 1");
            ShiftResult r = e.Evaluate(sim);
            Assert.AreEqual(LoseReasons.CustomersLeft, r.LoseReason);
            Assert.AreEqual(0, r.Stars);
        }

        [Test]
        public void Simulation_TimeUp_UnrealGoal_LostTooSlowWithShortfall()
        {
            ConfigSources broken = ConfigTestKit.Mutated("L04", tree =>
                ConfigTestKit.Map(tree, "goal")["coins"] = 999999L);
            LevelDefinition level = Load(broken, 4);

            BotResult r = BotRunner.Run(level, Seed, BotProfile.Expert);

            Assert.AreEqual(level.Shift.DurationTicks, r.EndTick);
            Assert.AreEqual(ShiftOutcome.Lost, r.Result.Outcome);
            Assert.AreEqual(LoseReasons.TooSlow, r.Result.LoseReason);
            Assert.AreEqual(999999L - r.Coins, r.Result.Shortfall);
        }

        [Test]
        public void Simulation_WithoutEarlyEndInterface_BehavesAsBefore()
        {
            // Умова цілі без IShiftEarlyEnd (як у старих тестах): зміна триває до кінця таймера.
            LevelDefinition level = ConfigTestKit.Level(1);
            var sim = new ShiftSimulation(level.Shift, Seed, new FixedGoal(true));
            sim.Start();
            for (long i = 0; i < level.Shift.DurationTicks - 1; i++)
            {
                sim.Tick();
            }

            Assert.AreEqual(ShiftState.Running, sim.State);
            sim.Tick();
            Assert.AreEqual(ShiftState.Won, sim.State);
        }

        // ---- детермінізм і боти ----

        [Test]
        public void Evaluate_SameCounters_SameResult()
        {
            ShiftEvaluator e = Serve(8, 110, StarCondition.NoLostCustomers, maxLost: 1);
            ShiftResult a = e.Evaluate(C(coins: 120, served: 8, lost: 1, combo: 2));
            ShiftResult b = e.Evaluate(C(coins: 120, served: 8, lost: 1, combo: 2));
            Assert.AreEqual(Dump(a), Dump(b));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void BotRun_SameSeed_SameShiftResult(int number)
        {
            LevelDefinition level = ConfigTestKit.Level(number);
            BotResult a = BotRunner.Run(level, Seed, BotProfile.Expert);
            BotResult b = BotRunner.Run(level, Seed, BotProfile.Expert);
            Assert.AreEqual(Dump(a.Result), Dump(b.Result));
            Assert.IsTrue(a.FinalSnapshot.Equals(b.FinalSnapshot));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void ExpertBot_Levels1To5_StaysSolvable_WithSharedRules(int number)
        {
            LevelDefinition level = ConfigTestKit.Level(number);
            for (int i = 0; i < 5; i++)
            {
                BotResult r = BotRunner.Run(level, Seed + (ulong)i * 7919UL, BotProfile.Expert);
                Assert.IsTrue(r.Result.Won, r.Explain());
                Assert.GreaterOrEqual(r.Result.Stars, 1);
                Assert.LessOrEqual(r.Result.Stars, 3);
                Assert.IsNull(r.Result.LoseReason);
            }
        }

        [Test]
        public void ConfigLoader_EndOnGoal_ReadsOptionalKeyAndRejectsNonBoolean()
        {
            ConfigSources on = ConfigTestKit.Mutated("L04", tree =>
                ConfigTestKit.Map(tree, "goal")["endOnGoal"] = true);
            Assert.IsTrue(Load(on, 4).Goal.EndOnGoal);

            ConfigSources bad = ConfigTestKit.Mutated("L01", tree =>
                ConfigTestKit.Map(tree, "goal")["endOnGoal"] = "yes");
            ConfigResult<LevelCatalog> loaded = ConfigLoader.LoadAll(bad);
            Assert.IsFalse(loaded.IsValid);
            StringAssert.Contains("endOnGoal", ConfigTestKit.Join(loaded.Errors));
        }

        private static string Dump(ShiftResult r)
        {
            return string.Join(
                "|",
                r.Outcome, r.Stars, r.LoseReason, r.Shortfall, r.RewardCoins, r.CoinsEarned,
                r.ServedCount, r.LostCustomers, r.WrongDishes, r.BurntCount, r.MaxCombo);
        }
    }
}
