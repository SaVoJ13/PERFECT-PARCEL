using System;
using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;
using static KebabStreet.Tests.EditMode.ConfigTestKit;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Суворе завантаження конфігів (S1-04): кожна категорія помилок має вадний JSON, який
    /// відхиляється з шляхом до ключа. Вадний JSON — зміна справжнього файла через дерево.
    /// </summary>
    public sealed class ConfigValidationTests
    {
        public sealed class BadCase
        {
            public BadCase(string name, string doc, Action<Dictionary<string, object>> mutate, string[] expect)
            {
                Name = name;
                Doc = doc;
                Mutate = mutate;
                Expect = expect;
            }

            public string Name { get; }

            public string Doc { get; }

            public Action<Dictionary<string, object>> Mutate { get; }

            /// <summary>Усі ці фрагменти мають бути в одному рядку помилки.</summary>
            public string[] Expect { get; }

            public override string ToString()
            {
                return Name;
            }
        }

        private static BadCase C(string name, string doc, Action<Dictionary<string, object>> mutate, params string[] expect)
        {
            return new BadCase(name, doc, mutate, expect);
        }

        private static string Prefix(string doc)
        {
            switch (doc)
            {
                case Stations: return "stations.json";
                case Recipes: return "recipes.json";
                case Customers: return "customers.json";
                case Rules: return "rules.json";
                default: return "levels/" + doc + ".json";
            }
        }

        private static Dictionary<string, object> Wave(Dictionary<string, object> level, int index)
        {
            return Item(Arr(level, "waves"), index);
        }

        private static Dictionary<string, object> Station(Dictionary<string, object> tree, string id)
        {
            return ById(Arr(tree, "stations"), id);
        }

        private static Dictionary<string, object> Customer(Dictionary<string, object> tree, string id)
        {
            return ById(Arr(tree, "customers"), id);
        }

        private static IEnumerable<BadCase> Cases()
        {
            // --- рівні: відсутні поля, невідомі ключі, хибні типи
            yield return C("Level_MissingDurationSec", "L04", t => t.Remove("durationSec"), "levels/L04.json: durationSec", "обов'язкове");
            yield return C("Level_MissingGoalCount", "L03", t => Map(t, "goal").Remove("count"), "levels/L03.json: goal.count", "обов'язкове");
            yield return C("Level_MissingId", "L02", t => t.Remove("id"), "levels/L02.json: id", "обов'язкове");
            yield return C("Level_UnknownKey", "L04", t => t["bogus"] = 1L, "levels/L04.json: bogus", "невідомий ключ");
            yield return C("Level_UnknownKeyInGrill", "L01", t => Map(t, "grill")["foo"] = 1L, "levels/L01.json: grill.foo", "невідомий ключ");
            yield return C("Level_UnknownKeyInWave", "L02", t => Wave(t, 0)["foo"] = 1L, "levels/L02.json: waves[0].foo", "невідомий ключ");
            yield return C("Level_UnknownKeyInOrder", "L02", t => Map(Wave(t, 1), "order")["foo"] = 1L, "levels/L02.json: waves[1].order.foo", "невідомий ключ");
            yield return C("Level_DurationIsString", "L01", t => t["durationSec"] = "90", "levels/L01.json: durationSec", "очікувалось ціле число");
            yield return C("Level_WavesNotArray", "L01", t => t["waves"] = "x", "levels/L01.json: waves", "очікувався масив");
            yield return C("Level_GoalNotObject", "L01", t => t["goal"] = 3L, "levels/L01.json: goal", "очікувався об'єкт");
            yield return C("Level_BoolIsString", "L01", t => Map(t, "lose")["countLostCustomers"] = "no", "lose.countLostCustomers", "true або false");

            // --- рівні: нульові, від'ємні й завеликі числа
            yield return C("Level_DurationZero", "L01", t => t["durationSec"] = 0L, "levels/L01.json: durationSec=0", "дозволено 1..");
            yield return C("Level_DurationNegative", "L01", t => t["durationSec"] = -5L, "levels/L01.json: durationSec=-5");
            yield return C("Level_CounterSlotsZero", "L02", t => t["counterSlots"] = 0L, "levels/L02.json: counterSlots=0");
            yield return C("Level_GoalCountZero", "L01", t => Map(t, "goal")["count"] = 0L, "levels/L01.json: goal.count=0");
            yield return C("Level_GoalCoinsZero", "L04", t => Map(t, "goal")["coins"] = 0L, "levels/L04.json: goal.coins=0");
            yield return C("Level_GrillCookZero", "L01", t => Map(t, "grill")["cookSec"] = 0L, "levels/L01.json: grill.cookSec=0");
            yield return C("Level_GrillCookNegative", "L01", t => Map(t, "grill")["cookSec"] = -1L, "levels/L01.json: grill.cookSec=-1");
            yield return C("Level_GrillSlotsZero", "L02", t => Map(t, "grill")["slots"] = 0L, "levels/L02.json: grill.slots=0");
            yield return C("Level_GrillSlotsTooMany", "L02", t => Map(t, "grill")["slots"] = 100L, "levels/L02.json: grill.slots=100");
            yield return C("Level_GrillPenaltyNegative", "L02", t => Map(t, "grill")["burntPenaltyCoins"] = -1L, "grill.burntPenaltyCoins=-1");
            yield return C("Level_StarsTwoZero", "L01", t => Map(Map(t, "stars"), "two")["value"] = 0L, "levels/L01.json: stars.two.value=0");
            yield return C("Level_WaveTimeNegative", "L01", t => Wave(t, 0)["atSec"] = -1L, "levels/L01.json: waves[0].atSec=-1");
            yield return C("Level_WaveTimeTooPrecise", "L01", t => Wave(t, 0)["atSec"] = 0.0004, "levels/L01.json: waves[0].atSec", "занадто точне");
            yield return C("Level_WaveGraceTooLong", "L01", t => Wave(t, 0)["graceSec"] = 700L, "levels/L01.json: waves[0].graceSec=700");
            yield return C("Level_WaveJitterNegative", "L05", t => Wave(t, 8)["jitterSec"] = -2L, "levels/L05.json: waves[8].jitterSec=-2");
            yield return C("Level_WaveListEmpty", "L01", t => t["waves"] = new List<object>(), "levels/L01.json: waves.count=0");
            yield return C("Level_ComboValueTooSmall", "L05", t => Map(Map(t, "stars"), "three")["value"] = 1L, "stars.three.value=1");

            // --- рівні: посилання на неіснуюче
            yield return C("Level_UnknownCustomer", "L02", t => Wave(t, 0)["customer"] = "ghost", "levels/L02.json: shift: waves[0].customer=ghost", "такого типу клієнта немає");
            yield return C("Level_UnknownRecipe", "L02", t => Map(Wave(t, 2), "order")["recipe"] = "pizza", "levels/L02.json: shift: waves[2].order.recipe=pizza", "такого рецепта немає");
            yield return C("Level_UnknownOrderIngredient", "L04", t => Map(Wave(t, 1), "order")["optional"] = new List<object> { "cheese" }, "levels/L04.json: shift: waves[1].order.optional", "cheese");
            yield return C("Level_WavesOutOfOrder", "L01", t => Wave(t, 1)["atSec"] = 1L, "levels/L01.json: shift: waves[1].atMs=1000", "раніше за попередній");

            // --- рівні: unlockLevel вище рівня
            yield return C("Level_CustomerBeforeFirstLevel", "L02", t => Wave(t, 0)["customer"] = "marko", "levels/L02.json: waves[0].customer=marko", "з'являється з рівня 3");
            yield return C("Level_HannaOnLevelThree", "L03", t => Wave(t, 1)["customer"] = "hanna", "levels/L03.json: waves[1].customer=hanna", "з'являється з рівня 4");
            yield return C("Level_SaladBeforeItsStation", "L01", t => Map(Wave(t, 0), "order")["optional"] = new List<object> { "salad" }, "levels/L01.json: shift: waves[0].order.optional", "недоступний на рівні 1");
            yield return C("Level_SauceBeforeItsStation", "L02", t => Map(Wave(t, 0), "order")["optional"] = new List<object> { "sauce_garlic" }, "levels/L02.json: shift: waves[0].order.optional", "недоступний на рівні 2");

            // --- рівні: ідентифікатор, ціль, зірки, поразка
            yield return C("Level_IdDiffersFromFileName", "L02", t => t["id"] = "L07", "levels/L02.json: id=L07", "має збігатися");
            yield return C("Level_GoalUnknownType", "L01", t => Map(t, "goal")["type"] = "kill", "levels/L01.json: goal.type=kill", "дозволено serve, coins");
            yield return C("Level_GoalForeignKey", "L01", t => Map(t, "goal")["coins"] = 5L, "levels/L01.json: goal.coins", "невідомий ключ");
            yield return C("Level_GoalAboveArrivals", "L01", t => Map(t, "goal")["count"] = 99L, "levels/L01.json: goal.count=99", "недосяжна");
            yield return C("Level_StarsUnknownCondition", "L03", t => Map(Map(t, "stars"), "three")["condition"] = "fancy", "levels/L03.json: stars.three.condition=fancy");
            yield return C("Level_StarsComboWithoutValue", "L05", t => Map(Map(t, "stars"), "three").Remove("value"), "levels/L05.json: stars.three.value", "обов'язкове");
            yield return C("Level_StarsValueOnNonCombo", "L03", t => Map(Map(t, "stars"), "three")["value"] = 3L, "levels/L03.json: stars.three.value", "невідомий ключ");
            yield return C("Level_StarsTwoWrongType", "L03", t => Map(Map(t, "stars"), "two")["type"] = "serve", "levels/L03.json: stars.two.type=serve");
            yield return C("Level_MaxLostWithoutCounting", "L01", t => Map(t, "lose")["maxLostCustomers"] = 1L, "levels/L01.json: lose.maxLostCustomers", "не має сенсу");
            yield return C("Level_NoLostStarWithoutCounting", "L01", t => Map(Map(t, "stars"), "three")["condition"] = "noLostCustomers", "levels/L01.json: stars.three.condition", "countLostCustomers=true");
            yield return C("Level_MaxLostNegative", "L03", t => Map(t, "lose")["maxLostCustomers"] = -1L, "levels/L03.json: lose.maxLostCustomers=-1");

            // --- рівні: тексти замість ключів
            yield return C("Level_NameKeyIsText", "L01", t => t["nameKey"] = "Перший кебаб", "levels/L01.json: nameKey", "ключ локалізації");
            yield return C("Level_TutorialKeyIsText", "L01", t => Arr(t, "tutorialKeys")[0] = "Візьми м'ясо", "levels/L01.json: tutorialKeys[0]", "ключ локалізації");
            yield return C("Level_StoryKeyIsText", "L01", t => Map(t, "storyKeys")["intro"] = "Дядько Алі", "levels/L01.json: storyKeys.intro", "ключ локалізації");

            // --- станції
            yield return C("Stations_UnknownId", Stations, t => Station(t, "bin")["id"] = "fryer", "stations.json: stations[6].id=fryer", "невідома станція");
            yield return C("Stations_Duplicate", Stations, t => Station(t, "salad")["id"] = "bread", "stations.json: stations[3].id=bread", "станція повторюється");
            yield return C("Stations_NoGrill", Stations, t => Arr(t, "stations").RemoveAt(0), "stations.json: stations", "немає станції grill");
            yield return C("Stations_UnlockZero", Stations, t => Station(t, "bread")["unlockLevel"] = 0L, "stations.json: stations[1].unlockLevel=0");
            yield return C("Stations_ActionNegative", Stations, t => Station(t, "bread")["actionTimeSec"] = -1L, "stations.json: stations[1].actionTimeSec=-1");
            yield return C("Stations_CounterHasAction", Stations, t => Station(t, "counter")["actionTimeSec"] = 1L, "stations.json: stations[4].actionTimeSec", "невідомий ключ");
            yield return C("Stations_GrillDefaultCookZero", Stations, t => Map(Station(t, "grill"), "grill")["cookSec"] = 0L, "stations.json: stations[0].grill.cookSec=0");
            yield return C("Stations_GrillUnknownKey", Stations, t => Map(Station(t, "grill"), "grill")["foo"] = 1L, "stations.json: stations[0].grill.foo", "невідомий ключ");
            yield return C("Stations_GrillMissingEverywhere", Stations, t => Map(Station(t, "grill"), "grill").Remove("cookSec"), "levels/L03.json: grill.cookSec", "не задано");
            yield return C("Stations_GrillUnlockAboveLevel", Stations, t => Station(t, "grill")["unlockLevel"] = 3L, "levels/L01.json: grill", "доступна з рівня 3");
            yield return C("Stations_CounterUnlockAboveLevel", Stations, t => Station(t, "counter")["unlockLevel"] = 2L, "levels/L01.json: counterSlots", "counter доступна з рівня 2");
            yield return C("Stations_SauceUnlockedTooLate", Stations, t => Station(t, "sauce")["unlockLevel"] = 5L, "levels/L03.json: shift:", "sauce_hot", "недоступний на рівні 3");
            yield return C("Stations_BoardUnlockedTooLate", Stations, t => Station(t, "board")["unlockLevel"] = 2L, "levels/L01.json: shift:", "дошка має бути доступна на рівні 1");

            // --- рецепти й інгредієнти
            yield return C("Recipes_UnknownStepIngredient", Recipes, t => Item(Arr(t, "recipes"), 0)["steps"] = new List<object> { "pita", "pork" }, "recipes.json: recipes[0].steps[1]=pork", "невідомий інгредієнт");
            yield return C("Recipes_UnknownOptionalIngredient", Recipes, t => Arr(Item(Arr(t, "recipes"), 0), "optional")[1] = "ghost", "recipes.json: recipes[0].optional[1]=ghost", "невідомий інгредієнт");
            yield return C("Recipes_EmptySteps", Recipes, t => Item(Arr(t, "recipes"), 0)["steps"] = new List<object>(), "recipes.json: recipes[0].steps", "хоча б один крок");
            yield return C("Recipes_PriceZero", Recipes, t => Item(Arr(t, "recipes"), 0)["price"] = 0L, "recipes.json: recipes[0].price=0");
            yield return C("Recipes_PriceNegative", Recipes, t => Item(Arr(t, "recipes"), 0)["price"] = -3L, "recipes.json: recipes[0].price=-3");
            yield return C("Recipes_UnlockZero", Recipes, t => Item(Arr(t, "recipes"), 0)["unlockLevel"] = 0L, "recipes.json: recipes[0].unlockLevel=0");
            yield return C("Recipes_UnlockAboveAllLevels", Recipes, t => Item(Arr(t, "recipes"), 0)["unlockLevel"] = 6L, "levels/L01.json: shift:", "жоден рецепт не доступний на рівні 1");
            yield return C("Recipes_UnknownKey", Recipes, t => Item(Arr(t, "recipes"), 0)["special"] = "x", "recipes.json: recipes[0].special", "невідомий ключ");
            yield return C("Recipes_MissingNameKey", Recipes, t => Item(Arr(t, "recipes"), 0).Remove("nameKey"), "recipes.json: recipes[0].nameKey", "обов'язкове");
            yield return C("Recipes_NameKeyIsText", Recipes, t => Item(Arr(t, "recipes"), 0)["nameKey"] = "Кебаб у піті", "recipes.json: recipes[0].nameKey", "ключ локалізації");
            yield return C("Ingredients_UnknownStation", Recipes, t => Item(Arr(t, "ingredients"), 0)["station"] = "fryer", "recipes.json: ingredients[0].station=fryer", "невідома станція");
            yield return C("Ingredients_ExtraPriceNegative", Recipes, t => Item(Arr(t, "ingredients"), 2)["extraPriceCoins"] = -1L, "recipes.json: ingredients[2].extraPriceCoins=-1");
            yield return C("Ingredients_NameKeyIsText", Recipes, t => Item(Arr(t, "ingredients"), 1)["nameKey"] = "М'ясо", "recipes.json: ingredients[1].nameKey", "ключ локалізації");
            yield return C("Ingredients_Duplicate", Recipes, t => Arr(t, "ingredients").Add(new Dictionary<string, object>(Item(Arr(t, "ingredients"), 0))), "shift: ingredients[6].id=pita", "назва повторюється");

            // --- клієнти
            yield return C("Customers_UnknownAlwaysIngredient", Customers, t => Arr(Customer(t, "marko"), "always")[0] = "ghost", "customers.json: customers[1].always[0]=ghost", "невідомий інгредієнт");
            yield return C("Customers_UnknownExcludeIngredient", Customers, t => Arr(Customer(t, "hanna"), "exclude")[0] = "ghost", "customers.json: customers[2].exclude[0]=ghost");
            yield return C("Customers_PatienceZero", Customers, t => Customer(t, "olya")["patienceSec"] = 0L, "customers.json: customers[0].patienceSec=0");
            yield return C("Customers_PatienceNegative", Customers, t => Customer(t, "marko")["patienceSec"] = -20L, "customers.json: customers[1].patienceSec=-20");
            yield return C("Customers_PayMultiplierZero", Customers, t => Customer(t, "olya")["payMultiplier"] = 0L, "customers.json: customers[0].payMultiplier=0");
            yield return C("Customers_PayMultiplierTooPrecise", Customers, t => Customer(t, "olya")["payMultiplier"] = 1.005, "customers.json: customers[0].payMultiplier", "занадто точне");
            yield return C("Customers_MissingTipRule", Customers, t => Customer(t, "olya").Remove("tipRule"), "customers.json: customers[0].tipRule", "обов'язкове");
            yield return C("Customers_TipPercentNegative", Customers, t => Map(Customer(t, "marko"), "tipRule")["maxPercent"] = -1L, "customers.json: customers[1].tipRule.maxPercent=-1");
            yield return C("Customers_MinPatienceAboveHundred", Customers, t => Map(Customer(t, "marko"), "tipRule")["minPatiencePercent"] = 101L, "customers[1].tipRule.minPatiencePercent=101");
            yield return C("Customers_FirstLevelZero", Customers, t => Customer(t, "hanna")["firstLevel"] = 0L, "customers.json: customers[2].firstLevel=0");
            yield return C("Customers_UnknownKey", Customers, t => Customer(t, "marko")["special"] = "alwaysHotSauce", "customers.json: customers[1].special", "невідомий ключ");
            yield return C("Customers_DuplicateId", Customers, t => Customer(t, "hanna")["id"] = "olya", "shift: customers[2].id=olya", "назва повторюється");
            yield return C("Customers_NameKeyIsText", Customers, t => Customer(t, "olya")["nameKey"] = "Студентка Оля", "customers.json: customers[0].nameKey", "ключ локалізації");

            // --- правила
            yield return C("Rules_TicksZero", Rules, t => t["ticksPerSecond"] = 0L, "rules.json: ticksPerSecond=0");
            yield return C("Rules_QueueZero", Rules, t => t["actionQueueMax"] = 0L, "rules.json: actionQueueMax=0");
            yield return C("Rules_ComboWindowNegative", Rules, t => t["comboWindowSec"] = -1L, "rules.json: comboWindowSec=-1");
            yield return C("Rules_PenaltyNegative", Rules, t => t["boardClearPenaltyCoins"] = -1L, "rules.json: boardClearPenaltyCoins=-1");
            yield return C("Rules_MissingKey", Rules, t => t.Remove("comboStepPercent"), "rules.json: comboStepPercent", "обов'язкове");
            yield return C("Rules_UnknownKey", Rules, t => t["bogus"] = true, "rules.json: bogus", "невідомий ключ");
            yield return C("Rules_FlagIsString", Rules, t => t["extraOptionalIsWrong"] = "yes", "rules.json: extraOptionalIsWrong", "true або false");
        }

        public static IEnumerable<TestCaseData> BadConfigs()
        {
            foreach (BadCase badCase in Cases())
            {
                yield return new TestCaseData(badCase).SetName("BadConfig_" + badCase.Name);
            }
        }

        [TestCaseSource(nameof(BadConfigs))]
        public void BadConfig_IsRejected_WithKeyPath(BadCase badCase)
        {
            ConfigSources sources = Mutated(badCase.Doc, badCase.Mutate);
            ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(sources);

            Assert.IsFalse(result.IsValid, badCase.Name + ": конфіг не відхилено");
            Assert.IsNull(result.Value);
            Assert.Greater(result.Errors.Count, 0);
            Assert.IsTrue(
                AnyErrorContainsAll(result.Errors, badCase.Expect),
                badCase.Name + ": немає помилки з [" + string.Join(" | ", badCase.Expect) + "]. Усі помилки:\n" + Join(result.Errors));
        }

        [Test]
        public void BadCases_CoverEveryDocument()
        {
            var docs = new HashSet<string>();
            int total = 0;
            foreach (BadCase badCase in Cases())
            {
                docs.Add(badCase.Doc);
                total++;
                Assert.IsNotEmpty(badCase.Expect, badCase.Name);
                Assert.IsTrue(badCase.Expect[0].Contains(".json") || badCase.Expect[0].Contains("shift") || badCase.Expect[0].Contains("["), badCase.Name + ": у помилці має бути шлях");
            }

            foreach (string doc in new[] { Stations, Recipes, Customers, Rules, "L01", "L02", "L03", "L04", "L05" })
            {
                Assert.IsTrue(docs.Contains(doc), "Немає вадного JSON для документа " + doc);
            }

            Assert.GreaterOrEqual(total, 60);
        }

        [Test]
        public void EveryBadCase_ReallyChangesTheFile()
        {
            // Запобіжник: правка не має бути порожньою (інакше тест «відхилено» тримався б на чомусь іншому).
            foreach (BadCase badCase in Cases())
            {
                ConfigSources mutated = Mutated(badCase.Doc, badCase.Mutate);
                string baseline = Write(SaveJson.Parse(TextOf(Real(), badCase.Doc)));
                Assert.AreNotEqual(baseline, TextOf(mutated, badCase.Doc), badCase.Name);
            }
        }

        [Test]
        public void RoundTripWithoutMutation_StaysValid()
        {
            // Доводить, що вадність у BadConfig_* — від правки, а не від запису дерева назад.
            foreach (string doc in new[] { Stations, Recipes, Customers, Rules, "L01", "L02", "L03", "L04", "L05" })
            {
                ConfigSources sources = Mutated(doc, t => { });
                ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(sources);
                Assert.IsTrue(result.IsValid, doc + ":\n" + Join(result.Errors));
            }
        }

        [Test]
        public void MalformedJson_InEveryDocument_ReportsPath()
        {
            foreach (string doc in new[] { Stations, Recipes, Customers, Rules, "L01", "L05" })
            {
                ConfigSources sources = Replace(Real(), doc, "{ \"id\": ");
                ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(sources);
                Assert.IsFalse(result.IsValid, doc);
                Assert.IsTrue(
                    AnyErrorContainsAll(result.Errors, Prefix(doc) + ": некоректний JSON"), doc + ":\n" + Join(result.Errors));
            }
        }

        [Test]
        public void MissingFile_InEveryDocument_ReportsPath()
        {
            foreach (string doc in new[] { Stations, Recipes, Customers, Rules, "L02" })
            {
                ConfigSources sources = Replace(Real(), doc, null);
                ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(sources);
                Assert.IsFalse(result.IsValid, doc);
                Assert.IsTrue(
                    AnyErrorContainsAll(result.Errors, Prefix(doc) + ": файл не знайдено"), doc + ":\n" + Join(result.Errors));
            }
        }

        [Test]
        public void WrongRootType_IsReported_NotThrown()
        {
            foreach (string garbage in new[] { "[]", "null", "123", "\"text\"", "{}", string.Empty })
            {
                foreach (string doc in new[] { Stations, Recipes, Customers, Rules, "L03" })
                {
                    ConfigSources sources = Replace(Real(), doc, garbage);
                    ConfigResult<LevelCatalog> result = null;
                    Assert.DoesNotThrow(() => result = ConfigLoader.LoadAll(sources), doc + " <- " + garbage);
                    Assert.IsFalse(result.IsValid, doc + " <- " + garbage);
                    Assert.IsNull(result.Value);
                }
            }
        }

        [Test]
        public void Bom_AtFileStart_IsTolerated()
        {
            ConfigSources sources = Replace(Real(), "L01", "﻿" + Real().FindLevelText("L01"));
            ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(sources);
            Assert.IsTrue(result.IsValid, Join(result.Errors));
        }

        [Test]
        public void NullSources_ReportError()
        {
            ConfigResult<LevelCatalog> all = ConfigLoader.LoadAll(null);
            Assert.IsFalse(all.IsValid);
            Assert.AreEqual(1, all.Errors.Count);
            ConfigResult<LevelDefinition> one = ConfigLoader.LoadLevel(null, "L01");
            Assert.IsFalse(one.IsValid);
        }

        [Test]
        public void NoLevels_IsAnError()
        {
            var empty = new ConfigSources(
                Real().Stations, Real().Recipes, Real().Customers, Real().Rules, new List<KeyValuePair<string, string>>());
            ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(empty);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(AnyErrorContainsAll(result.Errors, "levels: немає жодного рівня"));
        }

        [Test]
        public void LoadLevel_UnknownLevelName_ReportsMissingFile()
        {
            ConfigResult<LevelDefinition> result = ConfigLoader.LoadLevel(Real(), "L99");
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(AnyErrorContainsAll(result.Errors, "levels/L99.json: файл не знайдено"));
        }

        [Test]
        public void LoadLevel_ReportsErrorsOfSharedFiles()
        {
            ConfigSources sources = Mutated(Customers, t => Customer(t, "olya")["patienceSec"] = 0L);
            ConfigResult<LevelDefinition> result = ConfigLoader.LoadLevel(sources, "L01");
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(AnyErrorContainsAll(result.Errors, "customers.json: customers[0].patienceSec=0"));
        }

        [Test]
        public void ValueOrThrow_ThrowsWithErrorList_WhenInvalid()
        {
            ConfigResult<LevelDefinition> result = ConfigLoader.LoadLevel(Real(), "L99");
            var thrown = Assert.Throws<InvalidOperationException>(() => result.ValueOrThrow());
            StringAssert.Contains("L99", thrown.Message);
        }

        [Test]
        public void ValidConfig_HasNoErrorsAndValueOrThrowReturnsIt()
        {
            ConfigResult<LevelDefinition> result = ConfigLoader.LoadLevel(Real(), "L01");
            Assert.AreSame(result.Value, result.ValueOrThrow());
        }

        [Test]
        public void MultipleProblems_AreAllReported_NotOnlyTheFirst()
        {
            ConfigSources sources = Mutated("L01", t =>
            {
                t["durationSec"] = 0L;
                t["counterSlots"] = 0L;
                t["bogus"] = 1L;
            });
            ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(sources);
            Assert.IsTrue(AnyErrorContainsAll(result.Errors, "durationSec=0"));
            Assert.IsTrue(AnyErrorContainsAll(result.Errors, "counterSlots=0"));
            Assert.IsTrue(AnyErrorContainsAll(result.Errors, "bogus", "невідомий ключ"));
        }

        [Test]
        public void ErrorsOfOneLevel_DoNotHideOtherLevels()
        {
            ConfigSources sources = Mutated("L02", t => t["durationSec"] = 0L);
            ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(sources);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Errors.Count, Join(result.Errors));
        }

        private static bool AnyErrorContainsAll(IReadOnlyList<string> errors, params string[] parts)
        {
            foreach (string error in errors)
            {
                bool all = true;
                foreach (string part in parts)
                {
                    all &= error.Contains(part);
                }

                if (all)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
