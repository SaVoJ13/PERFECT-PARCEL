using System.Collections.Generic;
using System.Text;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Бот-тест рівнів (S1-05, ARCHITECTURE.md розд. 6): детермінований бот грає зміну публічними
    /// командами симуляції і має виконати ціль кожного рівня 1–5. Досвідчений бот — на всіх seed
    /// набору; новачок — на навчальних рівнях 1–2 за більшість seed. Метрики для балансу друкуються
    /// в лог із міткою BOTREPORT. Тести-сторожі доводять, що тест здатний упасти.
    /// </summary>
    public sealed class LevelSolvabilityTests
    {
        /// <summary>Скільки seed у наборі (картка S1-05: не менше 20).</summary>
        private const int SeedCount = 20;

        private const ulong FirstSeed = 20261201UL;
        private const ulong SeedStep = 7919UL;

        /// <summary>Скільки відсотків seed новачок має пройти на навчальних рівнях (поріг — параметр S1-05).</summary>
        private const int NoviceMinWinPercent = 70;

        private static ulong SeedAt(int index)
        {
            return FirstSeed + (ulong)index * SeedStep;
        }

        private static List<BotResult> RunAll(LevelDefinition level, BotProfile profile)
        {
            var results = new List<BotResult>(SeedCount);
            for (int i = 0; i < SeedCount; i++)
            {
                results.Add(BotRunner.Run(level, SeedAt(i), profile));
            }

            return results;
        }

        private static void Report(string text)
        {
            TestContext.Out.WriteLine(text);
            UnityEngine.Debug.Log(text);
        }

        private static string FailureList(List<BotResult> results)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < results.Count; i++)
            {
                if (!results[i].Won)
                {
                    builder.Append('\n').Append(results[i].Explain());
                }
            }

            return builder.ToString();
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void ExpertBot_SolvesLevel_OnEverySeed(int number)
        {
            LevelDefinition level = ConfigTestKit.Level(number);
            List<BotResult> results = RunAll(level, BotProfile.Expert);
            Report(BotReport.Summarize(level.Id + " expert", results));
            Assert.AreEqual(SeedCount, results.Count);
            Assert.AreEqual(string.Empty, FailureList(results).Trim(), level.Id + ": досвідчений бот має виконати ціль на всіх seed");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void NoviceBot_SolvesTrainingLevel_OnMostSeeds(int number)
        {
            LevelDefinition level = ConfigTestKit.Level(number);
            List<BotResult> results = RunAll(level, BotProfile.Novice);
            Report(BotReport.Summarize(level.Id + " novice", results));
            int wins = 0;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Won)
                {
                    wins++;
                }
            }

            Assert.GreaterOrEqual(
                wins * 100,
                NoviceMinWinPercent * SeedCount,
                level.Id + ": новачок виграв " + wins + " з " + SeedCount + ", потрібно щонайменше "
                + NoviceMinWinPercent + "%" + FailureList(results));
        }

        [Test]
        public void NoviceBot_OnHarderLevels_ReportsMetrics()
        {
            // Лише звіт для балансу: на рівнях 3–5 (не навчальних) результат новачка не вимагається.
            for (int number = 3; number <= 5; number++)
            {
                LevelDefinition level = ConfigTestKit.Level(number);
                List<BotResult> results = RunAll(level, BotProfile.Novice);
                string text = BotReport.Summarize(level.Id + " novice", results);
                Report(text);
                Assert.IsTrue(text.StartsWith(BotReport.Marker), level.Id);
            }
        }

        [TestCase(1, false)]
        [TestCase(3, false)]
        [TestCase(5, false)]
        [TestCase(2, true)]
        [TestCase(4, true)]
        public void Bot_SameSeed_GivesIdenticalResult(int number, bool novice)
        {
            LevelDefinition level = ConfigTestKit.Level(number);
            BotProfile profile = novice ? BotProfile.Novice : BotProfile.Expert;
            BotResult first = BotRunner.Run(level, SeedAt(3), profile);
            BotResult second = BotRunner.Run(level, SeedAt(3), profile);
            Assert.AreEqual(first.Fingerprint(), second.Fingerprint());
            Assert.IsTrue(first.FinalSnapshot.Equals(second.FinalSnapshot), "повний знімок симуляції має збігатися");
        }

        [Test]
        public void Bot_DifferentSeeds_ChangeTheRunOnJitteredLevel()
        {
            // L05 має запізнення приходів (jitterSec): різний seed дає різні знімки; ключі теста — що seed справді впливає.
            LevelDefinition level = ConfigTestKit.Level(5);
            BotResult a = BotRunner.Run(level, SeedAt(0), BotProfile.Expert);
            BotResult b = BotRunner.Run(level, SeedAt(1), BotProfile.Expert);
            Assert.IsFalse(a.FinalSnapshot.Equals(b.FinalSnapshot), "різні seed мають давати різні знімки");
        }

        [Test]
        public void Guard_UnrealGoal_BotFailsWithClearMessage()
        {
            // Тест-сторож: рівень 4 із ціллю в мільйон монет неможливо виконати.
            ConfigSources broken = ConfigTestKit.Mutated("L04", tree =>
                ConfigTestKit.Map(tree, "goal")["coins"] = 999999L);
            LevelDefinition level = Load(broken, 4);

            BotResult result = BotRunner.Run(level, SeedAt(0), BotProfile.Expert);

            Assert.IsFalse(result.Won, "нереальну ціль бот не виконує");
            Assert.AreEqual(BotOutcome.TimeUp, result.Outcome);
            string message = result.Explain();
            StringAssert.Contains("ПРОГРАШ", message);
            StringAssert.Contains("час вийшов", message);
            StringAssert.Contains("обслужено " + result.Served, message);
            StringAssert.Contains("999999", message);
            Assert.Greater(result.Served, 0L, "бот грає: обслужив клієнтів, але не мільйон монет");
        }

        [Test]
        public void Guard_NearZeroPatience_BotCannotWinLevelWithLostLimit()
        {
            // Тест-сторож: усі клієнти терплять півсекунди — на рівні 3 (ліміт втрат 1) втрат не уникнути.
            ConfigSources broken = ConfigTestKit.Mutated(ConfigTestKit.Customers, tree =>
            {
                List<object> types = ConfigTestKit.Arr(tree, "customers");
                for (int i = 0; i < types.Count; i++)
                {
                    ConfigTestKit.Item(types, i)["patienceSec"] = 0.5d;
                }
            });
            LevelDefinition level = Load(broken, 3);

            BotResult result = BotRunner.Run(level, SeedAt(0), BotProfile.Expert);

            Assert.IsFalse(result.Won);
            Assert.AreEqual(BotOutcome.TooManyLost, result.Outcome);
            string message = result.Explain();
            StringAssert.Contains("ПРОГРАШ", message);
            StringAssert.Contains("втрачено клієнтів", message);
            StringAssert.Contains("обслужено", message);
        }

        private static LevelDefinition Load(ConfigSources sources, int number)
        {
            ConfigResult<LevelCatalog> loaded = ConfigLoader.LoadAll(sources);
            Assert.IsTrue(loaded.IsValid, ConfigTestKit.Join(loaded.Errors));
            LevelDefinition level = loaded.Value.FindByNumber(number);
            Assert.IsNotNull(level);
            return level;
        }
    }
}
