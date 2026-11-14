using System.Collections.Generic;
using System.IO;
using System.Linq;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Завантажувач і валідація tutorial.json (S1-11): справжній файл читається, усі ключі текстів є в усіх мовах,
    /// ключі навчання рівнів збігаються з tutorial.json (BUG-036), хибні конфіги дають зрозумілі повідомлення.
    /// </summary>
    public sealed class TutorialConfigTests
    {
        private static string RealText()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Resources", "Config", "tutorial.json");
            Assert.IsTrue(File.Exists(path), "немає файлу " + path);
            return File.ReadAllText(path);
        }

        private static TutorialConfig Real()
        {
            TutorialLoadResult result = ResourceConfigSource.LoadTutorial();
            Assert.IsTrue(result.IsValid, "tutorial.json має бути валідний:\n" + string.Join("\n", result.Errors.ToArray()));
            return result.Config;
        }

        private static LocalizationTable Table(string code)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Resources", "Localization", code + ".json");
            return LocalizationTable.Parse(File.ReadAllText(path));
        }

        /// <summary>Замінює рівно одне входження; тест падає, якщо шматка немає (файл змінився, а тест ні).</summary>
        private static string Replace(string text, string from, string to)
        {
            Assert.IsTrue(text.Contains(from), "у tutorial.json немає шматка: " + from);
            return text.Replace(from, to);
        }

        [Test]
        public void RealFile_Loads_WithExpectedShape()
        {
            TutorialConfig config = Real();

            Assert.AreEqual("uncle_ali", config.Speaker);
            Assert.IsTrue(config.AllowSkipStep);
            Assert.IsTrue(config.AllowSkipAll);
            Assert.IsTrue(config.RememberSkipAll);
            Assert.IsTrue(config.OncePerProfile);
            Assert.AreEqual(2000, config.MinGapMs);
            Assert.AreEqual("ui.tutorial.next", config.SkipStepButtonKey);
            Assert.AreEqual("ui.tutorial.skip", config.SkipAllButtonKey);
            Assert.AreEqual(5, config.LevelCount);
            Assert.AreEqual(5, config.StepsFor("L01").Count, "L01: чотири власні кроки і крок l02.burnt");
            Assert.AreEqual("l02.burnt", config.StepsFor("L01").Last().Id, "додатковий крок L02 діє й на L01");
            Assert.AreEqual(1, config.StepsFor("L02").Count);
            Assert.AreEqual(0, config.StepsFor("L05").Count);
            Assert.AreEqual(0, config.StepsFor("L99").Count);
        }

        [Test]
        public void RealFile_EveryStep_HasKnownAnchorAndZone()
        {
            TutorialConfig config = Real();
            for (int l = 0; l < config.LevelCount; l++)
            {
                for (int s = 0; s < config.GetLevel(l).StepCount; s++)
                {
                    TutorialStep step = config.GetLevel(l).GetStep(s);
                    Assert.AreNotEqual(TutorialZone.None, TutorialAnchors.ZoneOf(step.Anchor), step.Id);
                    Assert.IsTrue(step.Skippable, step.Id + ": усі кроки мають пропускатися");
                }
            }
        }

        [TestCase("en")]
        [TestCase("uk")]
        [TestCase("pl")]
        public void RealFile_EveryTextAndButtonKey_ExistsInLanguage(string code)
        {
            TutorialConfig config = Real();
            LocalizationTable table = Table(code);
            var keys = new List<string> { config.SkipStepButtonKey, config.SkipAllButtonKey };
            for (int l = 0; l < config.LevelCount; l++)
            {
                for (int s = 0; s < config.GetLevel(l).StepCount; s++)
                {
                    keys.Add(config.GetLevel(l).GetStep(s).TextKey);
                }
            }

            Assert.Greater(keys.Count, 10);
            foreach (string key in keys)
            {
                string value;
                Assert.IsTrue(table.TryGet(key, out value), code + ": немає ключа " + key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(value), code + ": порожній текст " + key);
            }
        }

        [Test]
        public void LevelTutorialKeys_MatchTutorialJson_ForEveryLevel()
        {
            TutorialConfig config = Real();
            LevelCatalog levels = ConfigTestKit.LoadCatalog();
            for (int i = 0; i < levels.Count; i++)
            {
                LevelDefinition level = levels.Get(i);
                var fromLevel = new List<string>();
                for (int k = 0; k < level.TutorialKeyCount; k++)
                {
                    fromLevel.Add(level.GetTutorialKey(k));
                }

                List<string> fromTutorial = config.TextKeysFor(level.Id);
                CollectionAssert.AreEquivalent(fromTutorial, fromLevel, level.Id + ": tutorialKeys рівня і кроки tutorial.json розійшлися (BUG-036)");
            }
        }

        [Test]
        public void EveryStepTextKey_IsReferencedByItsLevel_OrIsAnExtraStepOfAnotherLevel()
        {
            TutorialConfig config = Real();
            LevelCatalog levels = ConfigTestKit.LoadCatalog();
            for (int l = 0; l < config.LevelCount; l++)
            {
                TutorialLevel level = config.GetLevel(l);
                LevelDefinition definition = levels.FindByNumber(int.Parse(level.LevelId.Substring(1)));
                Assert.IsNotNull(definition, "рівень " + level.LevelId + " з tutorial.json є в конфігах рівнів");
                for (int s = 0; s < level.StepCount; s++)
                {
                    bool found = false;
                    for (int k = 0; k < definition.TutorialKeyCount; k++)
                    {
                        found |= definition.GetTutorialKey(k) == level.GetStep(s).TextKey;
                    }

                    Assert.IsTrue(found, level.LevelId + ": ключ " + level.GetStep(s).TextKey + " відсутній у tutorialKeys");
                }
            }
        }

        [Test]
        public void RealFile_MatchesRecipeIngredientsAndCustomers()
        {
            TutorialConfig config = Real();
            BoardConfig board = ConfigTestKit.Level(4).Shift.Board;
            CounterConfig counter = ConfigTestKit.Level(4).Shift.Counter;
            for (int l = 0; l < config.LevelCount; l++)
            {
                for (int s = 0; s < config.GetLevel(l).StepCount; s++)
                {
                    TutorialStep step = config.GetLevel(l).GetStep(s);
                    foreach (TutorialTrigger trigger in new[] { step.ShowWhen, step.CompleteWhen })
                    {
                        if (trigger == null)
                        {
                            continue;
                        }

                        foreach (string ingredient in new[] { trigger.Ingredient, trigger.OrderOptional })
                        {
                            if (ingredient != null)
                            {
                                Assert.GreaterOrEqual(board.IndexOfIngredient(ingredient), 0, step.Id + ": невідомий інгредієнт " + ingredient);
                            }
                        }

                        if (trigger.Customer != null)
                        {
                            Assert.GreaterOrEqual(counter.IndexOfType(trigger.Customer), 0, step.Id + ": невідомий клієнт " + trigger.Customer);
                        }
                    }
                }
            }
        }

        [Test]
        public void NullText_ReportsFileNotFound()
        {
            TutorialLoadResult result = TutorialConfigLoader.Parse(null);

            Assert.IsFalse(result.IsValid);
            Assert.IsNull(result.Config);
            StringAssert.Contains("файл не знайдено", result.Errors[0]);
        }

        [Test]
        public void BrokenJson_ReportsMessage()
        {
            TutorialLoadResult result = TutorialConfigLoader.Parse("{ \"version\": ");

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("некоректний JSON", result.Errors[0]);
        }

        [Test]
        public void InvalidResult_ValueOrThrow_ListsErrors()
        {
            TutorialLoadResult result = TutorialConfigLoader.Parse("[]");

            var error = Assert.Throws<System.InvalidOperationException>(() => result.ValueOrThrow());
            StringAssert.Contains("tutorial.json", error.Message);
        }

        private static IEnumerable<TestCaseData> InvalidCases()
        {
            yield return new TestCaseData("\"event\": \"ShiftStarted\"", "\"event\": \"Fly\"", "невідома подія").SetName("Invalid_UnknownEvent");
            yield return new TestCaseData("\"anchor\": \"board.pita\"", "\"anchor\": \"moon\"", "невідомий якір").SetName("Invalid_UnknownAnchor");
            yield return new TestCaseData("\"textKey\": \"tutorial.l01.step1\"", "\"textKey\": \"Hello there\"", "ключ локалізації").SetName("Invalid_TextKeyIsText");
            yield return new TestCaseData("\"id\": \"l01.step2\"", "\"id\": \"l01.step1\"", "уже є").SetName("Invalid_DuplicateStepId");
            yield return new TestCaseData("\"afterStep\": \"l01.step1\"", "\"afterStep\": \"l01.nope\"", "попереднього кроку").SetName("Invalid_AfterStepUnknown");
            yield return new TestCaseData("\"pauseSimulation\": false", "\"pauseSimulation\": true", "не може зупиняти").SetName("Invalid_PauseSimulation");
            yield return new TestCaseData("\"maxConcurrent\": 1", "\"maxConcurrent\": 2", "лише 1").SetName("Invalid_MaxConcurrent");
            yield return new TestCaseData("\"speaker\": \"uncle_ali\",", "\"speaker\": \"uncle_ali\", \"bogus\": 1,", "невідомий ключ").SetName("Invalid_UnknownKey");
            yield return new TestCaseData("[\"L01\"]", "[\"L09\"]", "такого рівня немає").SetName("Invalid_AlsoOnLevelsMissingLevel");
            yield return new TestCaseData("\"minGapSec\": 2", "\"minGapSec\": -1", "minGapSec").SetName("Invalid_NegativeGap");
            yield return new TestCaseData("\"penaltyFree\": true", "\"penaltyFree\": 5", "true або false").SetName("Invalid_PenaltyFreeNotBool");
            yield return new TestCaseData("\"ingredient\": \"pita\"", "\"ingredient\": \"Pita bread\"", "ідентифікатор").SetName("Invalid_IngredientIsText");
            yield return new TestCaseData("\"completeWhen\": { \"event\": \"PlaceMeat\" }", "\"completeWhen\": { \"event\": \"PlaceMeat\", \"orderOptional\": \"x\" }", "невідомий ключ").SetName("Invalid_CompleteWhenForeignKey");
            yield return new TestCaseData("\"level\": \"L03\"", "\"level\": \"third\"", "L01").SetName("Invalid_LevelIdFormat");
            yield return new TestCaseData("\"level\": \"L05\"", "\"level\": \"L04\"", "рівень повторюється").SetName("Invalid_DuplicateLevel");
        }

        [TestCaseSource(nameof(InvalidCases))]
        public void BrokenConfig_GivesClearMessage(string from, string to, string expected)
        {
            string text = RealText().Replace("\r\n", "\n");

            TutorialLoadResult result = TutorialConfigLoader.Parse(Replace(text, from, to));

            Assert.IsFalse(result.IsValid, "хибний конфіг не має проходити");
            Assert.IsNull(result.Config);
            string all = string.Join("\n", result.Errors.ToArray());
            StringAssert.Contains(expected, all);
            StringAssert.Contains("tutorial.json", all, "повідомлення називає документ");
        }

        [Test]
        public void StepWithoutCompleteWhenAndWithoutAutoHide_IsRejected()
        {
            const string json =
                "{ \"version\": 1, \"speaker\": \"uncle_ali\","
                + " \"skip\": { \"allowSkipStep\": true, \"allowSkipAll\": true, \"skipStepButtonKey\": \"ui.tutorial.next\","
                + " \"skipAllButtonKey\": \"ui.tutorial.skip\", \"rememberSkipAll\": true },"
                + " \"display\": { \"maxConcurrent\": 1, \"minGapSec\": 2, \"pauseSimulation\": false, \"oncePerProfile\": true },"
                + " \"levels\": [ { \"level\": \"L01\", \"steps\": ["
                + " { \"id\": \"a\", \"textKey\": \"tutorial.l01.step1\", \"anchor\": \"grill.slot1\","
                + " \"showWhen\": { \"event\": \"ShiftStarted\" }, \"completeWhen\": null, \"autoHideSec\": null, \"skippable\": true } ] } ] }";

            TutorialLoadResult result = TutorialConfigLoader.Parse(json);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("autoHideSec", string.Join("\n", result.Errors.ToArray()));
        }

        [Test]
        public void MissingRequiredBlocks_AreAllReported()
        {
            TutorialLoadResult result = TutorialConfigLoader.Parse("{ \"version\": 1 }");

            string all = string.Join("\n", result.Errors.ToArray());
            StringAssert.Contains("speaker", all);
            StringAssert.Contains("skip", all);
            StringAssert.Contains("display", all);
            StringAssert.Contains("levels", all);
        }
    }
}
