using System;
using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Перевірка конфіга клієнтів: помилку (хвиля посилається на невідомого клієнта чи рецепт,
    /// «без цибулі» для рецепта без цибулі, число поза межами) ловить тест, а не збій у грі.
    /// </summary>
    public sealed class CounterConfigTests
    {
        private static IReadOnlyList<string> Errors(CounterConfig counter, BoardConfig board = null)
        {
            return CustomerTestKit.Config(counter, board).GetErrors();
        }

        private static void AssertErrors(IReadOnlyList<string> errors, params string[] expectedStarts)
        {
            Assert.AreEqual(expectedStarts.Length, errors.Count, string.Join("; ", errors));
            for (int i = 0; i < expectedStarts.Length; i++)
            {
                StringAssert.StartsWith(expectedStarts[i], errors[i]);
            }
        }

        private static CounterConfig WithArrivals(params Arrival[] arrivals)
        {
            return CustomerTestKit.CounterParams(arrivals);
        }

        private static CounterConfig WithOrder(string customer, Order order)
        {
            return WithArrivals(new Arrival(0, customer, order));
        }

        /// <summary>Конфіг, де третій тип (hanna, у розкладі його немає) замінено на заданий.</summary>
        private static CounterConfig WithThirdType(CustomerType type)
        {
            List<CustomerType> types = CustomerTestKit.Types();
            types[2] = type;
            return CustomerTestKit.CounterParams(new[] { CustomerTestKit.At(0) }, types: types);
        }

        private static CounterConfig WithRules(
            int slots = CustomerTestKit.Slots,
            int comboWindowMs = CustomerTestKit.ComboWindowMs,
            int comboStepPercent = CustomerTestKit.ComboStepPercent,
            int comboMaxSteps = CustomerTestKit.ComboMaxSteps,
            int wrongPenaltyMs = CustomerTestKit.WrongPenaltyMs)
        {
            return CustomerTestKit.CounterParams(
                new[] { CustomerTestKit.At(0) }, slots, comboWindowMs, comboStepPercent, comboMaxSteps, wrongPenaltyMs);
        }

        // ---------- придатний конфіг ----------

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(8)]
        public void TestKitConfig_WithAllThreeTypes_IsValid(int level)
        {
            // Рівень 3: салатного бару ще немає, тож Ганна замовляє без салату.
            CounterConfig counter = WithArrivals(
                CustomerTestKit.At(0),
                CustomerTestKit.At(500, "marko"),
                CustomerTestKit.At(1000, "hanna", level >= BoardTestKit.SaladLevel ? CustomerTestKit.With("salad") : null));

            CollectionAssert.IsEmpty(Errors(counter, BoardTestKit.BoardParams(level: level)));
        }

        [Test]
        public void Config_KeepsValues_AndFindsTypesByName()
        {
            CounterConfig counter = CustomerTestKit.CounterParams(
                new[] { CustomerTestKit.At(0) }, countLostCustomers: false, extraOptionalIsWrong: false);

            Assert.AreEqual(CustomerTestKit.Slots, counter.Slots);
            Assert.AreEqual(CustomerTestKit.ComboWindowMs, counter.ComboWindowMs);
            Assert.AreEqual(CustomerTestKit.ComboStepPercent, counter.ComboStepPercent);
            Assert.AreEqual(CustomerTestKit.ComboMaxSteps, counter.ComboMaxSteps);
            Assert.AreEqual(CustomerTestKit.WrongPenaltyMs, counter.WrongDishPatiencePenaltyMs);
            Assert.IsFalse(counter.CountLostCustomers);
            Assert.IsFalse(counter.ExtraOptionalIsWrong);
            Assert.AreEqual(1, counter.Schedule.Count);
            Assert.AreEqual(3, counter.TypeCount);
            Assert.AreEqual(CustomerTestKit.Marko, counter.IndexOfType("marko"));
            Assert.AreEqual(-1, counter.IndexOfType("Marko"));
            Assert.AreEqual(-1, counter.IndexOfType(null));

            CustomerType marko = counter.GetCustomerType(CustomerTestKit.Marko);
            Assert.AreEqual("marko", marko.Id);
            Assert.AreEqual(2000, marko.PatienceMs);
            Assert.AreEqual(CustomerTestKit.MarkoPatienceTicks, marko.PatienceTicks(60));
            Assert.AreEqual(150, marko.PayPercent);
            Assert.AreEqual(50, marko.Tip.MaxPercent);
            Assert.AreEqual(0, marko.Tip.MinPatiencePercent);
            Assert.AreEqual(1, marko.AlwaysCount);
            Assert.AreEqual("sauce_hot", marko.GetAlways(0));
            Assert.AreEqual(0, marko.ExcludeCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => marko.GetAlways(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => marko.GetExclude(0));

            CustomerType hanna = counter.GetCustomerType(CustomerTestKit.Hanna);
            Assert.AreEqual(1, hanna.ExcludeCount);
            Assert.AreEqual("onion", hanna.GetExclude(0));
        }

        [Test]
        public void Order_KeepsValues_AndCopiesLists()
        {
            string[] optional = { "salad", "sauce_hot" };
            string[] exclude = { "onion" };
            var order = new Order("kebab_pita", optional, exclude);

            optional[0] = "x";
            exclude[0] = "y";

            Assert.AreEqual("kebab_pita", order.Recipe);
            Assert.AreEqual(2, order.OptionalCount);
            Assert.AreEqual("salad", order.GetOptional(0));
            Assert.AreEqual("sauce_hot", order.GetOptional(1));
            Assert.AreEqual(1, order.ExcludeCount);
            Assert.AreEqual("onion", order.GetExclude(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => order.GetOptional(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => order.GetExclude(-1));
            Assert.AreEqual(0, CustomerTestKit.Plain().OptionalCount);
            Assert.AreEqual(0, CustomerTestKit.Plain().ExcludeCount);
        }

        [Test]
        public void OldConstructors_GiveShiftWithoutCounter()
        {
            Assert.IsNull(ShiftTestKit.Config().Counter);
            Assert.IsNull(GrillTestKit.Config().Counter);
            Assert.IsNull(BoardTestKit.Config().Counter);
            Assert.IsNull(BoardTestKit.CreateRunning().Counter);
        }

        // ---------- правила прилавка ----------

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(CounterConfig.MaxSlots, true)]
        [TestCase(CounterConfig.MaxSlots + 1, false)]
        [TestCase(-1, false)]
        public void Slots_MustBeInRange(int slots, bool valid)
        {
            IReadOnlyList<string> errors = Errors(WithRules(slots: slots));

            if (valid)
            {
                CollectionAssert.IsEmpty(errors);
            }
            else
            {
                AssertErrors(errors, "counter.slots=");
            }
        }

        // field: 0 — вікно комбо, 1 — крок комбо, 2 — кроків не більше, 3 — штраф терпіння.
        [TestCase(0, -1, false)]
        [TestCase(0, 0, true)]
        [TestCase(0, CounterConfig.MaxComboWindowMs, true)]
        [TestCase(0, CounterConfig.MaxComboWindowMs + 1, false)]
        [TestCase(1, -1, false)]
        [TestCase(1, 0, true)]
        [TestCase(1, CounterConfig.MaxPercent, true)]
        [TestCase(1, CounterConfig.MaxPercent + 1, false)]
        [TestCase(2, -1, false)]
        [TestCase(2, 0, true)]
        [TestCase(2, CounterConfig.MaxComboSteps, true)]
        [TestCase(2, CounterConfig.MaxComboSteps + 1, false)]
        [TestCase(3, -1, false)]
        [TestCase(3, 0, true)]
        [TestCase(3, CounterConfig.MaxPatienceMs, true)]
        [TestCase(3, CounterConfig.MaxPatienceMs + 1, false)]
        public void Rules_MustBeInRange(int field, int value, bool valid)
        {
            CounterConfig counter;
            string name;
            switch (field)
            {
                case 0:
                    counter = WithRules(comboWindowMs: value);
                    name = "rules.comboWindowMs=";
                    break;
                case 1:
                    counter = WithRules(comboStepPercent: value);
                    name = "rules.comboStepPercent=";
                    break;
                case 2:
                    counter = WithRules(comboMaxSteps: value);
                    name = "rules.comboMaxSteps=";
                    break;
                default:
                    counter = WithRules(wrongPenaltyMs: value);
                    name = "rules.wrongDishPatiencePenaltyMs=";
                    break;
            }

            IReadOnlyList<string> errors = Errors(counter);

            if (valid)
            {
                CollectionAssert.IsEmpty(errors);
            }
            else
            {
                AssertErrors(errors, name);
            }
        }

        // ---------- типи клієнтів ----------

        [Test]
        public void Types_EmptyList_IsError()
        {
            CounterConfig counter = CustomerTestKit.CounterParams(
                new[] { CustomerTestKit.At(0) }, types: new List<CustomerType>());

            AssertErrors(Errors(counter), "customers.count=0", "waves[0].customer=olya: такого типу клієнта немає");
        }

        [Test]
        public void Types_NullEntry_EmptyOrRepeatedName_AreErrors()
        {
            AssertErrors(Errors(WithThirdType(null)), "customers[2]: порожній запис");
            AssertErrors(Errors(WithThirdType(CustomerTestKit.Type("", 1000, 100, 0, 0))), "customers[2].id: порожня назва");
            AssertErrors(Errors(WithThirdType(CustomerTestKit.Type(null, 1000, 100, 0, 0))), "customers[2].id: порожня назва");
            AssertErrors(
                Errors(WithThirdType(CustomerTestKit.Type("olya", 1000, 100, 0, 0))),
                "customers[2].id=olya: назва повторюється");
        }

        // field: 0 — терпіння, 1 — множник плати, 2 — чайові до, 3 — поріг терпіння для чайових.
        [TestCase(0, 0, false)]
        [TestCase(0, 17, true)]
        [TestCase(0, CounterConfig.MaxPatienceMs, true)]
        [TestCase(0, CounterConfig.MaxPatienceMs + 1, false)]
        [TestCase(1, -1, false)]
        [TestCase(1, 0, true)]
        [TestCase(1, CounterConfig.MaxPercent, true)]
        [TestCase(1, CounterConfig.MaxPercent + 1, false)]
        [TestCase(2, -1, false)]
        [TestCase(2, 0, true)]
        [TestCase(2, CounterConfig.MaxPercent, true)]
        [TestCase(2, CounterConfig.MaxPercent + 1, false)]
        [TestCase(3, -1, false)]
        [TestCase(3, 0, true)]
        [TestCase(3, 100, true)]
        [TestCase(3, 101, false)]
        public void TypeNumbers_MustBeInRange(int field, int value, bool valid)
        {
            CustomerType type;
            string name;
            switch (field)
            {
                case 0:
                    type = CustomerTestKit.Type("guest", value, 100, 0, 0);
                    name = "customers[2].patienceMs=";
                    break;
                case 1:
                    type = CustomerTestKit.Type("guest", 1000, value, 0, 0);
                    name = "customers[2].payPercent=";
                    break;
                case 2:
                    type = CustomerTestKit.Type("guest", 1000, 100, value, 0);
                    name = "customers[2].tipRule.maxPercent=";
                    break;
                default:
                    type = CustomerTestKit.Type("guest", 1000, 100, 0, value);
                    name = "customers[2].tipRule.minPatiencePercent=";
                    break;
            }

            IReadOnlyList<string> errors = Errors(WithThirdType(type));

            if (valid)
            {
                CollectionAssert.IsEmpty(errors);
            }
            else
            {
                AssertErrors(errors, name);
            }
        }

        [Test]
        public void TypePatience_ShorterThanOneTick_IsError()
        {
            // 8 мс за 60 тактів на секунду — менше половини такту.
            IReadOnlyList<string> errors = Errors(WithThirdType(CustomerTestKit.Type("guest", 8, 100, 0, 0)));

            AssertErrors(errors, "customers[2].patienceMs=8: менше одного такту");
        }

        [Test]
        public void Type_WithoutTipRule_OrWithEmptyIngredientName_IsError()
        {
            AssertErrors(
                Errors(WithThirdType(new CustomerType("guest", 1000, 100, null, null, null))),
                "customers[2].tipRule: немає правила чайових");
            AssertErrors(
                Errors(WithThirdType(new CustomerType("guest", 1000, 100, new TipRule(0, 0), new[] { "" }, null))),
                "customers[2].always: порожня назва інгредієнта");
            AssertErrors(
                Errors(WithThirdType(new CustomerType("guest", 1000, 100, new TipRule(0, 0), null, new string[] { null }))),
                "customers[2].exclude: порожня назва інгредієнта");
        }

        // ---------- розклад ----------

        [Test]
        public void Schedule_MissingOrEmpty_IsError()
        {
            var noSchedule = new CounterConfig(2, 1000, 10, 3, 500, true, true, CustomerTestKit.Types(), null);

            AssertErrors(Errors(noSchedule), "waves: немає розкладу приходу");
            AssertErrors(Errors(WithArrivals()), "waves.count=0");
        }

        [Test]
        public void Schedule_TooLong_IsError()
        {
            var arrivals = new Arrival[ArrivalSchedule.MaxArrivals + 1];
            for (int i = 0; i < arrivals.Length; i++)
            {
                arrivals[i] = CustomerTestKit.At(i);
            }

            AssertErrors(Errors(WithArrivals(arrivals)), "waves.count=513");

            var longest = new Arrival[ArrivalSchedule.MaxArrivals];
            Array.Copy(arrivals, longest, longest.Length);
            CollectionAssert.IsEmpty(Errors(WithArrivals(longest)));
        }

        [Test]
        public void Arrival_NullEntry_IsError()
        {
            AssertErrors(Errors(WithArrivals(CustomerTestKit.At(0), null)), "waves[1]: порожній запис");
        }

        // field: 0 — час приходу, 1 — запізнення, 2 — пільговий час.
        [TestCase(0, -1, false)]
        [TestCase(0, 0, true)]
        [TestCase(0, ArrivalSchedule.MaxAtMs, true)]
        [TestCase(0, ArrivalSchedule.MaxAtMs + 1, false)]
        [TestCase(1, -1, false)]
        [TestCase(1, ArrivalSchedule.MaxJitterMs, true)]
        [TestCase(1, ArrivalSchedule.MaxJitterMs + 1, false)]
        [TestCase(2, -1, false)]
        [TestCase(2, CounterConfig.MaxPatienceMs, true)]
        [TestCase(2, CounterConfig.MaxPatienceMs + 1, false)]
        public void ArrivalNumbers_MustBeInRange(int field, int value, bool valid)
        {
            Order plain = CustomerTestKit.Plain();
            Arrival arrival;
            string name;
            switch (field)
            {
                case 0:
                    arrival = new Arrival(value, "olya", plain, 0, 0);
                    name = "waves[0].atMs=";
                    break;
                case 1:
                    arrival = new Arrival(0, "olya", plain, value, 0);
                    name = "waves[0].jitterMs=";
                    break;
                default:
                    arrival = new Arrival(0, "olya", plain, 0, value);
                    name = "waves[0].graceMs=";
                    break;
            }

            IReadOnlyList<string> errors = Errors(WithArrivals(arrival));

            if (valid)
            {
                CollectionAssert.IsEmpty(errors);
            }
            else
            {
                AssertErrors(errors, name);
            }
        }

        [Test]
        public void Arrivals_MustNotGoBackInTime()
        {
            AssertErrors(
                Errors(WithArrivals(CustomerTestKit.At(500), CustomerTestKit.At(499))),
                "waves[1].atMs=499: раніше за попередній прихід (500)");
            CollectionAssert.IsEmpty(Errors(WithArrivals(CustomerTestKit.At(500), CustomerTestKit.At(500))));
        }

        [Test]
        public void Arrival_UnknownCustomer_IsError()
        {
            AssertErrors(
                Errors(WithOrder("petro", CustomerTestKit.Plain())),
                "waves[0].customer=petro: такого типу клієнта немає");
        }

        // ---------- замовлення ----------

        [Test]
        public void Order_MissingOrWithoutRecipe_IsError()
        {
            AssertErrors(Errors(WithArrivals(new Arrival(0, "olya", null))), "waves[0].order: немає замовлення");
            AssertErrors(
                Errors(WithOrder("olya", new Order("", null, null))), "waves[0].order.recipe: порожня назва");
            AssertErrors(
                Errors(WithOrder("olya", new Order(null, null, null))), "waves[0].order.recipe: порожня назва");
        }

        [Test]
        public void Order_UnknownOrLockedRecipe_IsError()
        {
            AssertErrors(
                Errors(WithOrder("olya", new Order("burger", null, null))),
                "waves[0].order.recipe=burger: такого рецепта немає");

            // Кебаб у лаваші відкривається на рівні 8, а тестова зміна — рівень 5.
            AssertErrors(
                Errors(WithOrder("olya", new Order("kebab_lavash", null, null))),
                "waves[0].order.recipe=kebab_lavash: рецепт недоступний на рівні 5");
        }

        [Test]
        public void Order_OptionalThatRecipeDoesNotHave_OrRepeated_IsError()
        {
            AssertErrors(
                Errors(WithOrder("olya", CustomerTestKit.With("pita"))),
                "waves[0].order.optional: «pita» немає серед необов'язкових рецепта kebab_pita");
            AssertErrors(
                Errors(WithOrder("olya", CustomerTestKit.With("ketchup"))),
                "waves[0].order.optional: «ketchup» немає серед необов'язкових рецепта kebab_pita");
            AssertErrors(
                Errors(WithOrder("olya", CustomerTestKit.With("salad", "salad"))),
                "waves[0].order.optional: «salad» повторюється");
        }

        [Test]
        public void Order_OptionalFromLockedStation_IsError()
        {
            // Рівень 3: салатного бару ще немає — таке замовлення виконати неможливо.
            AssertErrors(
                Errors(WithOrder("olya", CustomerTestKit.With("salad")), BoardTestKit.BoardParams(level: 3)),
                "waves[0].order.optional: інгредієнт «salad» недоступний на рівні 3");

            // Рівень 2: соусів ще немає, а Марко завжди просить гострий.
            AssertErrors(
                Errors(WithOrder("marko", CustomerTestKit.Plain()), BoardTestKit.BoardParams(level: 2)),
                "waves[0].customer.always: інгредієнт «sauce_hot» недоступний на рівні 2");
        }

        [Test]
        public void Exclude_OfIngredientFromLockedStation_IsAllowed()
        {
            // «Без цибулі» на рівні, де цибулі ще немає, нічому не заважає.
            CollectionAssert.IsEmpty(
                Errors(WithOrder("hanna", CustomerTestKit.Plain()), BoardTestKit.BoardParams(level: 2)));
        }

        [Test]
        public void Exclude_OfIngredientThatRecipeDoesNotHave_IsError()
        {
            // QA S1-02, З-4: «без цибулі» має сенс лише для рецепта, де цибуля є серед необов'язкових.
            AssertErrors(
                Errors(
                    WithOrder("hanna", new Order("kebab_lavash", null, null)),
                    BoardTestKit.BoardParams(level: BoardTestKit.LavashLevel)),
                "waves[0].customer.exclude: «onion» немає серед необов'язкових рецепта kebab_lavash");
            AssertErrors(
                Errors(WithOrder("olya", new Order("kebab_pita", null, new[] { "ketchup" }))),
                "waves[0].order.exclude: «ketchup» немає серед необов'язкових рецепта kebab_pita");
            AssertErrors(
                Errors(WithOrder("olya", new Order("kebab_pita", null, new[] { "onion", "onion" }))),
                "waves[0].order.exclude: «onion» повторюється");
        }

        [Test]
        public void Order_AskingForExcludedIngredient_IsError()
        {
            AssertErrors(
                Errors(WithOrder("hanna", CustomerTestKit.With("onion"))),
                "waves[0].order: той самий інгредієнт і просять, і просять не класти");
            AssertErrors(
                Errors(WithOrder("olya", new Order("kebab_pita", new[] { "salad" }, new[] { "salad" }))),
                "waves[0].order: той самий інгредієнт і просять, і просять не класти");
        }

        [Test]
        public void TypeAlways_ThatRecipeDoesNotHave_IsError()
        {
            List<CustomerType> types = CustomerTestKit.Types();
            types[2] = new CustomerType("guest", 1000, 100, new TipRule(0, 0), new[] { "pita" }, null);
            CounterConfig counter = CustomerTestKit.CounterParams(new[] { CustomerTestKit.At(0, "guest") }, types: types);

            AssertErrors(
                Errors(counter), "waves[0].customer.always: «pita» немає серед необов'язкових рецепта kebab_pita");
        }

        [Test]
        public void EveryBadArrival_IsReported_WithItsNumber()
        {
            IReadOnlyList<string> errors = Errors(WithArrivals(
                CustomerTestKit.At(0),
                CustomerTestKit.At(0, "petro"),
                CustomerTestKit.At(0, "olya", new Order("burger", null, null))));

            AssertErrors(errors, "waves[1].customer=petro", "waves[2].order.recipe=burger");
        }

        // ---------- разом зі зміною ----------

        [Test]
        public void Counter_WithoutBoard_IsError()
        {
            var config = new ShiftConfig(
                ShiftTestKit.TicksPerSecond,
                ShiftTestKit.MaxCatchUpTicks,
                60,
                0,
                4,
                BoardTestKit.GrillParams(),
                null,
                WithArrivals(CustomerTestKit.At(0)));

            AssertErrors(config.GetErrors(), "counter: клієнтам потрібна дошка");
        }

        [Test]
        public void Counter_WithBrokenBoard_ReportsBoardErrors_AndDoesNotCrashOnOrders()
        {
            BoardConfig brokenBoard = BoardTestKit.BoardParams(clearPenaltyCoins: -1);

            IReadOnlyList<string> errors = Errors(WithOrder("olya", new Order("burger", null, null)), brokenBoard);

            AssertErrors(errors, "rules.boardClearPenaltyCoins=-1");
        }

        [Test]
        public void GetErrors_OnItsOwn_NamesEveryProblem()
        {
            var counter = new CounterConfig(0, -1, -1, -1, -1, true, true, null, null);

            IReadOnlyList<string> errors = counter.GetErrors(null, false, ShiftTestKit.TicksPerSecond);

            AssertErrors(
                errors,
                "counter.slots=0",
                "rules.comboWindowMs=-1",
                "rules.comboStepPercent=-1",
                "rules.comboMaxSteps=-1",
                "rules.wrongDishPatiencePenaltyMs=-1",
                "counter: клієнтам потрібна дошка",
                "customers.count=0",
                "waves: немає розкладу приходу");
        }

        [Test]
        public void ShiftSimulation_RejectsInvalidCounterConfig_WithReason()
        {
            ShiftConfig config = CustomerTestKit.Config(WithOrder("petro", CustomerTestKit.Plain()));

            var error = Assert.Throws<ArgumentException>(
                () => new ShiftSimulation(config, ShiftTestKit.Seed, new FixedGoal(true)));

            StringAssert.Contains("waves[0].customer=petro", error.Message);
        }

        [Test]
        public void ShiftSimulation_WithCustomers_DoesNotTakeAnotherDishReceiver()
        {
            ShiftConfig config = CustomerTestKit.Config(WithArrivals(CustomerTestKit.At(0)));

            Assert.Throws<ArgumentException>(
                () => new ShiftSimulation(config, ShiftTestKit.Seed, new FixedGoal(true), new FakeDishReceiver()));
            Assert.IsNotNull(new ShiftSimulation(config, ShiftTestKit.Seed, new FixedGoal(true), null).Counter);
        }
    }
}
