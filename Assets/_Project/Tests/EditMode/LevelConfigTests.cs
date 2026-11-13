using System.Collections.Generic;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Справжні конфіги рівнів 1–5 (S1-04): завантаження, відповідність таблиці зведення
    /// LEVEL_DESIGN.md розд. 1, параметри ADR-020, детермінований розклад.
    /// </summary>
    public sealed class LevelConfigTests
    {
        private const ulong Seed = 20261112UL;

        [Test]
        public void ResourceSource_FindsAllConfigFiles()
        {
            ConfigSources sources = ResourceConfigSource.Load();
            Assert.IsNotNull(sources.Stations, "stations.json");
            Assert.IsNotNull(sources.Recipes, "recipes.json");
            Assert.IsNotNull(sources.Customers, "customers.json");
            Assert.IsNotNull(sources.Rules, "rules.json");
            Assert.AreEqual(5, sources.LevelCount, "levels/L01..L05.json");
            Assert.AreEqual("L01", sources.GetLevelName(0));
            Assert.AreEqual("L05", sources.GetLevelName(4));
        }

        [Test]
        public void AllFiveLevels_LoadWithoutErrors()
        {
            ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(ConfigTestKit.Real());
            Assert.IsTrue(result.IsValid, ConfigTestKit.Join(result.Errors));
            Assert.AreEqual(0, result.Errors.Count);
            Assert.AreEqual(5, result.Value.Count);
            for (int i = 0; i < 5; i++)
            {
                LevelDefinition level = result.Value.Get(i);
                Assert.AreEqual(i + 1, level.Number);
                Assert.AreEqual("L0" + (i + 1), level.Id);
            }
        }

        [Test]
        public void EveryLevel_BuildsShiftConfigWithoutCoreErrors()
        {
            LevelCatalog catalog = ConfigTestKit.LoadCatalog();
            for (int i = 0; i < catalog.Count; i++)
            {
                LevelDefinition level = catalog.Get(i);
                Assert.IsNotNull(level.Shift.Grill, level.Id + ": гриль");
                Assert.IsNotNull(level.Shift.Board, level.Id + ": дошка");
                Assert.IsNotNull(level.Shift.Counter, level.Id + ": прилавок");
                Assert.AreEqual(0, level.Shift.GetErrors().Count, level.Id + ": " + ConfigTestKit.Join(level.Shift.GetErrors()));
                Assert.AreEqual(level.Number, level.Shift.Board.Level, level.Id + ": рівень дошки");
            }
        }

        [Test]
        public void LoadLevel_LoadsOneLevelByName()
        {
            ConfigResult<LevelDefinition> result = ConfigLoader.LoadLevel(ConfigTestKit.Real(), "L03");
            Assert.IsTrue(result.IsValid, ConfigTestKit.Join(result.Errors));
            Assert.AreEqual(3, result.Value.Number);
        }

        [Test]
        public void Levels_AreMarkedDraft_UntilBotTest()
        {
            LevelCatalog catalog = ConfigTestKit.LoadCatalog();
            for (int i = 0; i < catalog.Count; i++)
            {
                Assert.IsTrue(catalog.Get(i).Draft, catalog.Get(i).Id + ": числа чернеткові до S1-05");
            }
        }

        [Test]
        public void Level1_TableRow()
        {
            LevelDefinition level = ConfigTestKit.Level(1);
            Assert.AreEqual(GoalType.Serve, level.Goal.Type);
            Assert.AreEqual(3, level.Goal.Target);
            CollectionAssert.AreEqual(new[] { "olya" }, ConfigTestKit.UsedCustomers(level));
            AssertStations(level, sauce: false, salad: false);
            Assert.IsNull(level.Lose.MaxLostCustomers);
            Assert.AreEqual(1, level.Shift.Counter.Slots, "один клієнт за раз");
            Assert.AreEqual(1, level.Shift.Grill.Slots);
        }

        [Test]
        public void Level2_TableRow()
        {
            LevelDefinition level = ConfigTestKit.Level(2);
            Assert.AreEqual(GoalType.Serve, level.Goal.Type);
            Assert.AreEqual(6, level.Goal.Target);
            CollectionAssert.AreEqual(new[] { "olya" }, ConfigTestKit.UsedCustomers(level));
            AssertStations(level, sauce: false, salad: false);
            Assert.AreEqual(2, level.Shift.Grill.Slots, "два шматки м'яса в роботі");
            Assert.AreEqual(2, level.Shift.Counter.Slots);
            Assert.IsNull(level.Lose.MaxLostCustomers);
        }

        [Test]
        public void Level2_HasTwoSimultaneousPairs()
        {
            ArrivalSchedule schedule = ConfigTestKit.Level(2).Shift.Counter.Schedule;
            int pairs = 0;
            for (int i = 1; i < schedule.Count; i++)
            {
                if (schedule.Get(i).AtMs == schedule.Get(i - 1).AtMs)
                {
                    pairs++;
                }
            }

            Assert.AreEqual(2, pairs, "двічі за зміну приходять двоє одночасно");
        }

        [Test]
        public void Level3_TableRow()
        {
            LevelDefinition level = ConfigTestKit.Level(3);
            Assert.AreEqual(GoalType.Serve, level.Goal.Type);
            Assert.AreEqual(8, level.Goal.Target);
            Assert.AreEqual(1, level.Lose.MaxLostCustomers);
            CollectionAssert.AreEqual(new[] { "marko", "olya" }, ConfigTestKit.UsedCustomers(level));
            AssertStations(level, sauce: true, salad: false);
            Assert.AreEqual(StarCondition.NoLostCustomers, level.Stars.ThreeCondition);
        }

        [Test]
        public void Level3_MarkoComesMidShift_WithGraceOfTwentySeconds()
        {
            LevelDefinition level = ConfigTestKit.Level(3);
            ArrivalSchedule schedule = level.Shift.Counter.Schedule;
            int firstMarkoMs = -1;
            for (int i = 0; i < schedule.Count; i++)
            {
                Arrival arrival = schedule.Get(i);
                if (arrival.Customer == "marko")
                {
                    Assert.AreEqual(20000, arrival.GraceMs, "терпіння Марка не спадає перші 20 с");
                    if (firstMarkoMs < 0)
                    {
                        firstMarkoMs = arrival.AtMs;
                    }
                }
            }

            Assert.Greater(firstMarkoMs, 0);
            Assert.Greater(firstMarkoMs, level.Shift.DurationSec * 1000 / 4, "не на початку");
            Assert.Less(firstMarkoMs, level.Shift.DurationSec * 1000 * 3 / 4, "не наприкінці");
        }

        [Test]
        public void Level4_TableRow()
        {
            LevelDefinition level = ConfigTestKit.Level(4);
            Assert.AreEqual(GoalType.Coins, level.Goal.Type);
            Assert.AreEqual(150, level.Goal.Target, "концепція: 150");
            CollectionAssert.AreEqual(new[] { "hanna", "marko", "olya" }, ConfigTestKit.UsedCustomers(level));
            AssertStations(level, sauce: true, salad: true);
            Assert.AreEqual(StarCondition.NoWrongDishes, level.Stars.ThreeCondition);
        }

        [Test]
        public void Level4_HannaDoesNotWantOnion_AsDataNotCode()
        {
            CounterConfig counter = ConfigTestKit.Level(4).Shift.Counter;
            CustomerType hanna = counter.GetCustomerType(counter.IndexOfType("hanna"));
            Assert.AreEqual(1, hanna.ExcludeCount);
            Assert.AreEqual("onion", hanna.GetExclude(0));
            BoardConfig board = ConfigTestKit.Level(4).Shift.Board;
            Recipe kebab = board.GetRecipe(board.IndexOfRecipe("kebab_pita"));
            bool onionIsOptional = false;
            for (int i = 0; i < kebab.OptionalCount; i++)
            {
                onionIsOptional |= kebab.GetOptional(i) == "onion";
            }

            Assert.IsTrue(onionIsOptional, "цибуля має бути серед optional рецепта (QA S1-02, З-4)");
        }

        [Test]
        public void Level5_TableRow()
        {
            LevelDefinition level = ConfigTestKit.Level(5);
            Assert.AreEqual(GoalType.Coins, level.Goal.Type);
            Assert.AreEqual(200, level.Goal.Target, "концепція: 200");
            CollectionAssert.AreEqual(new[] { "hanna", "marko", "olya" }, ConfigTestKit.UsedCustomers(level));
            AssertStations(level, sauce: true, salad: true);
            Assert.AreEqual(StarCondition.Combo, level.Stars.ThreeCondition);
            Assert.GreaterOrEqual(level.Stars.ThreeComboValue, 2);
        }

        [Test]
        public void Level5_HasWaveOfThreeAtOnce()
        {
            ArrivalSchedule schedule = ConfigTestKit.Level(5).Shift.Counter.Schedule;
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < schedule.Count; i++)
            {
                int at = schedule.Get(i).AtMs;
                counts[at] = counts.ContainsKey(at) ? counts[at] + 1 : 1;
            }

            bool hasThree = false;
            foreach (KeyValuePair<int, int> pair in counts)
            {
                hasThree |= pair.Value >= 3;
            }

            Assert.IsTrue(hasThree, "хвиля 3 клієнтів одночасно");
        }

        [Test]
        public void NewMechanicsAppearOnlyOnTheirLevels()
        {
            // Нове не частіше ніж кожен другий рівень окрім навчальних: соус — 3, салат — 4.
            for (int n = 1; n <= 5; n++)
            {
                LevelDefinition level = ConfigTestKit.Level(n);
                Assert.AreEqual(n >= 3, level.Shift.Board.IsStationAvailable(StationKind.Sauce), "соус, рівень " + n);
                Assert.AreEqual(n >= 4, level.Shift.Board.IsStationAvailable(StationKind.Salad), "салат, рівень " + n);
                Assert.IsTrue(level.Shift.Board.IsStationAvailable(StationKind.Bread), "піта, рівень " + n);
                Assert.IsTrue(level.Shift.Board.IsStationAvailable(StationKind.Board), "дошка, рівень " + n);
                Assert.IsTrue(level.Shift.Board.IsStationAvailable(StationKind.Bin), "смітник, рівень " + n);
            }
        }

        [Test]
        public void CustomersAppearNotBeforeTheirFirstLevel()
        {
            Assert.AreEqual(1, ConfigTestKit.UsedCustomers(ConfigTestKit.Level(1)).Count);
            Assert.AreEqual(1, ConfigTestKit.UsedCustomers(ConfigTestKit.Level(2)).Count);
            Assert.AreEqual(2, ConfigTestKit.UsedCustomers(ConfigTestKit.Level(3)).Count);
            Assert.AreEqual(3, ConfigTestKit.UsedCustomers(ConfigTestKit.Level(4)).Count);
            Assert.AreEqual(3, ConfigTestKit.UsedCustomers(ConfigTestKit.Level(5)).Count);
        }

        [Test]
        public void Levels1And2_Adr020_AreOnlyParameters()
        {
            for (int n = 1; n <= 2; n++)
            {
                LevelDefinition level = ConfigTestKit.Level(n);
                Assert.AreEqual(0, level.Shift.Grill.BurntPenaltyCoins, "L0" + n + ": пригоріле без штрафу");
                Assert.IsFalse(level.Shift.Counter.CountLostCustomers, "L0" + n + ": втрата клієнтів не рахується");
                Assert.IsNull(level.Lose.MaxLostCustomers, "L0" + n + ": поразка лише за часом");
            }
        }

        [Test]
        public void Levels3To5_CountLostCustomersAndPunishBurnt()
        {
            for (int n = 3; n <= 5; n++)
            {
                LevelDefinition level = ConfigTestKit.Level(n);
                Assert.Greater(level.Shift.Grill.BurntPenaltyCoins, 0, "L0" + n);
                Assert.IsTrue(level.Shift.Counter.CountLostCustomers, "L0" + n);
            }
        }

        [Test]
        public void Level1_GoalStaysReachable_EvenIfCustomerWouldLeave()
        {
            // OWNER_INBOX №24: на рівні 1 клієнт не йде (довга пільга) і є запасний прихід.
            LevelDefinition level = ConfigTestKit.Level(1);
            ArrivalSchedule schedule = level.Shift.Counter.Schedule;
            Assert.Greater(schedule.Count, level.Goal.Target, "запасний клієнт");
            for (int i = 0; i < schedule.Count; i++)
            {
                Assert.GreaterOrEqual(
                    schedule.Get(i).GraceMs, level.Shift.DurationSec * 1000, "пільга терпіння покриває всю зміну");
            }
        }

        [Test]
        public void ServeGoals_AreReachableByTheScheduleSize()
        {
            for (int n = 1; n <= 3; n++)
            {
                LevelDefinition level = ConfigTestKit.Level(n);
                Assert.LessOrEqual(level.Goal.Target, level.Shift.Counter.Schedule.Count, "L0" + n);
            }
        }

        [Test]
        public void LoseAndStars_AreStoredAsData()
        {
            Assert.AreEqual(StarCondition.NoBurnt, ConfigTestKit.Level(1).Stars.ThreeCondition);
            Assert.AreEqual(StarCondition.NoBurnt, ConfigTestKit.Level(2).Stars.ThreeCondition);
            for (int n = 1; n <= 5; n++)
            {
                LevelDefinition level = ConfigTestKit.Level(n);
                Assert.Greater(level.Stars.TwoCoins, 0, "L0" + n);
                if (level.Goal.Type == GoalType.Coins)
                {
                    Assert.Greater(level.Stars.TwoCoins, level.Goal.Target, "L0" + n + ": ★★ вище за ціль");
                }
            }
        }

        [Test]
        public void TutorialAndStoryKeys_FollowLevelDesign()
        {
            Assert.AreEqual(5, ConfigTestKit.Level(1).TutorialKeyCount);
            Assert.AreEqual("tutorial.l01.step1", ConfigTestKit.Level(1).GetTutorialKey(0));
            Assert.AreEqual("tutorial.l02.burnt", ConfigTestKit.Level(2).GetTutorialKey(0));
            Assert.AreEqual("tutorial.l03.patience", ConfigTestKit.Level(3).GetTutorialKey(0));
            Assert.AreEqual("tutorial.l03.sauce", ConfigTestKit.Level(3).GetTutorialKey(1));
            Assert.AreEqual("tutorial.l04.salad", ConfigTestKit.Level(4).GetTutorialKey(0));
            Assert.AreEqual("tutorial.l04.request", ConfigTestKit.Level(4).GetTutorialKey(1));
            Assert.AreEqual(0, ConfigTestKit.Level(5).TutorialKeyCount);
            for (int n = 1; n <= 5; n++)
            {
                LevelDefinition level = ConfigTestKit.Level(n);
                Assert.AreEqual("story.l0" + n + ".intro", level.StoryIntroKey);
                Assert.AreEqual("story.l0" + n + ".outro", level.StoryOutroKey);
                Assert.AreEqual("level.l0" + n + ".name", level.NameKey);
            }
        }

        [Test]
        public void Schedule_FromConfig_IsDeterministicForSameSeed()
        {
            for (int n = 1; n <= 5; n++)
            {
                LevelDefinition level = ConfigTestKit.Level(n);
                int tps = level.Shift.TicksPerSecond;
                long[] first = level.Shift.Counter.Schedule.ResolveTicks(tps, new SplitMix64Random(Seed));
                long[] second = level.Shift.Counter.Schedule.ResolveTicks(tps, new SplitMix64Random(Seed));
                CollectionAssert.AreEqual(first, second, "L0" + n);
            }
        }

        [Test]
        public void Schedule_WithoutJitter_IsExactlyAtConfiguredSeconds()
        {
            LevelDefinition level = ConfigTestKit.Level(1);
            long[] ticks = level.Shift.Counter.Schedule.ResolveTicks(level.Shift.TicksPerSecond, new SplitMix64Random(Seed));
            CollectionAssert.AreEqual(new long[] { 5 * 60, 25 * 60, 45 * 60, 65 * 60 }, ticks);
        }

        [Test]
        public void Schedule_WithJitter_StaysInsideTheWindow_AndDependsOnSeed()
        {
            LevelDefinition level = ConfigTestKit.Level(5);
            ArrivalSchedule schedule = level.Shift.Counter.Schedule;
            int tps = level.Shift.TicksPerSecond;
            long[] a = schedule.ResolveTicks(tps, new SplitMix64Random(1UL));
            long[] b = schedule.ResolveTicks(tps, new SplitMix64Random(2UL));
            bool anyJitter = false;
            bool anyDifference = false;
            for (int i = 0; i < schedule.Count; i++)
            {
                Arrival arrival = schedule.Get(i);
                long min = TickTime.FromMs(arrival.AtMs, tps);
                long max = min + TickTime.FromMs(arrival.JitterMs, tps);
                Assert.GreaterOrEqual(a[i], min, "прихід " + i);
                Assert.LessOrEqual(a[i], max, "прихід " + i);
                anyJitter |= arrival.JitterMs > 0;
                anyDifference |= a[i] != b[i];
            }

            Assert.IsTrue(anyJitter, "у L05 є запізнення приходів");
            Assert.IsTrue(anyDifference, "різний seed — різні запізнення");
        }

        [Test]
        public void EveryLevel_RunsInSimulation_AndSpawnsFirstCustomer()
        {
            for (int n = 1; n <= 5; n++)
            {
                LevelDefinition level = ConfigTestKit.Level(n);
                var shift = new ShiftSimulation(level.Shift, Seed, new FixedGoal(true));
                shift.Start();
                long firstArrival = level.Shift.Counter.Schedule.Get(0).AtMs;
                long ticks = (firstArrival + 2000) * level.Shift.TicksPerSecond / 1000;
                GrillTestKit.Tick(shift, ticks);
                Assert.AreEqual(ShiftState.Running, shift.State, "L0" + n);
                Assert.AreEqual(ticks, shift.ElapsedTicks, "L0" + n);
            }
        }

        [Test]
        public void SameSeed_GivesSameShiftConfigBehaviour_ForLevel5()
        {
            LevelDefinition level = ConfigTestKit.Level(5);
            Assert.AreEqual(Snapshot(level, Seed), Snapshot(level, Seed));
        }

        private static ShiftSnapshot Snapshot(LevelDefinition level, ulong seed)
        {
            var shift = new ShiftSimulation(level.Shift, seed, new FixedGoal(true));
            shift.Start();
            GrillTestKit.Tick(shift, 60L * 80L);
            return shift.CaptureSnapshot();
        }

        private static void AssertStations(LevelDefinition level, bool sauce, bool salad)
        {
            BoardConfig board = level.Shift.Board;
            Assert.AreEqual(sauce, board.IsStationAvailable(StationKind.Sauce), level.Id + ": соус");
            Assert.AreEqual(salad, board.IsStationAvailable(StationKind.Salad), level.Id + ": салатний бар");
            Assert.IsTrue(board.IsStationAvailable(StationKind.Bread), level.Id + ": піта");
        }
    }
}
