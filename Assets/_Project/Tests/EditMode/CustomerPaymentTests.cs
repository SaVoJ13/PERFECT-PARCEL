using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Оплата (GAME_DESIGN.md, розд. 6–7): ціна страви × множник типу + чайові за залишком
    /// терпіння + надбавка комбо; комбо росте у вікні й скидається помилкою чи втратою клієнта;
    /// монети зміни = дохід − штрафи гриля і дошки; параметри навчальних рівнів (ADR-020).
    /// Очікувані суми пораховано вручну.
    /// </summary>
    public sealed class CustomerPaymentTests
    {
        private const int Price = BoardTestKit.KebabPrice;

        /// <summary>Один клієнт власного типу «guest» (600 тактів терпіння), що замовив кебаб із гострим соусом (ціна 25).</summary>
        private static ShiftSimulation GuestWithHotKebab(int payPercent, int tipMaxPercent, int tipMinPatiencePercent)
        {
            var types = new List<CustomerType>
            {
                CustomerTestKit.Type("guest", 10000, payPercent, tipMaxPercent, tipMinPatiencePercent),
            };
            ShiftSimulation shift = CustomerTestKit.RunningWith(CustomerTestKit.CounterParams(
                new[] { CustomerTestKit.At(0, "guest", CustomerTestKit.With("sauce_hot")) }, types: types));
            CustomerTestKit.Prepare(shift, BoardTestKit.Hot);
            return shift;
        }

        /// <summary>Один клієнт власного типу з простим кебабом (ціна 20) і заданими чайовими.</summary>
        private static ShiftSimulation GuestWithTips(int tipMaxPercent, int tipMinPatiencePercent, int atMs = 0)
        {
            var types = new List<CustomerType>
            {
                CustomerTestKit.Type("guest", 10000, 100, tipMaxPercent, tipMinPatiencePercent),
            };
            ShiftSimulation shift = CustomerTestKit.RunningWith(CustomerTestKit.CounterParams(
                new[] { CustomerTestKit.At(atMs, "guest") }, types: types));
            CustomerTestKit.Prepare(shift);
            return shift;
        }

        private static ShiftSimulation Olyas(
            int count,
            int comboWindowMs = CustomerTestKit.ComboWindowMs,
            int comboStepPercent = CustomerTestKit.ComboStepPercent,
            int comboMaxSteps = CustomerTestKit.ComboMaxSteps)
        {
            var arrivals = new List<Arrival>();
            for (int i = 0; i < count; i++)
            {
                arrivals.Add(CustomerTestKit.At(0));
            }

            return CustomerTestKit.RunningWith(CustomerTestKit.CounterParams(
                arrivals,
                slots: count,
                comboWindowMs: comboWindowMs,
                comboStepPercent: comboStepPercent,
                comboMaxSteps: comboMaxSteps));
        }

        // ---------- плата за страву ----------

        // Ціна 25: 25 × відсоток / 100, половина — вгору.
        [TestCase(100, 25)]
        [TestCase(150, 38)]
        [TestCase(110, 28)]
        [TestCase(101, 25)]
        [TestCase(250, 63)]
        [TestCase(1000, 250)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(0, 0)]
        public void Pay_IsDishPriceTimesPayMultiplier_RoundedHalfUp(int payPercent, int expected)
        {
            ShiftSimulation shift = GuestWithHotKebab(payPercent, 0, 0);

            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.Serve(shift, 0));

            Assert.AreEqual(expected, CustomerTestKit.CustomerOf(shift, 0).PaidCoins);
            Assert.AreEqual(expected, shift.Counter.LastPaidCoins);
            Assert.AreEqual(expected, shift.Counter.IncomeCoins);
            Assert.AreEqual(expected, shift.IncomeCoins);
            Assert.AreEqual(expected, shift.CoinsEarned);
            Assert.AreEqual(0L, shift.Counter.TipCoins);
            Assert.AreEqual(0L, shift.Counter.ComboCoins);
            Assert.AreEqual(new Dish(0, CustomerTestKit.HotBit, 25), shift.Board.LastServed);
        }

        // ---------- чайові ----------

        // Терпіння 600 тактів, чайові до 50% від плати 20. Наприкінці такту T лишається 601 − T.
        // Чайові = 20 × 50% × залишок / 600, половина — вгору.
        [TestCase(10L, 10)]
        [TestCase(301L, 5)]
        [TestCase(571L, 1)]
        [TestCase(572L, 0)]
        [TestCase(600L, 0)]
        public void Tip_IsProportionalToPatienceLeft(long serveTick, int expectedTip)
        {
            ShiftSimulation shift = GuestWithTips(50, 0);

            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.ServeAt(shift, serveTick, 0));

            Assert.AreEqual(601L - serveTick, CustomerTestKit.CustomerOf(shift, 0).PatienceLeftTicks);
            Assert.AreEqual(expectedTip, shift.Counter.TipCoins);
            Assert.AreEqual(Price + expectedTip, CustomerTestKit.CustomerOf(shift, 0).PaidCoins);
            Assert.AreEqual(Price + expectedTip, shift.CoinsEarned);
        }

        [Test]
        public void Tip_AtFullPatience_IsTheMaximum()
        {
            // Клієнт приходить на такті 60, страва вже готова: видача на тому ж такті.
            ShiftSimulation shift = GuestWithTips(50, 0, atMs: 1000);

            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.ServeAt(shift, 60L, 0));

            Assert.AreEqual(600L, CustomerTestKit.CustomerOf(shift, 0).PatienceLeftTicks);
            Assert.AreEqual(10L, shift.Counter.TipCoins);
            Assert.AreEqual(30L, shift.Counter.IncomeCoins);
        }

        // Поріг 50%: чайові лише якщо лишилось не менше 300 тактів із 600.
        [TestCase(301L, 5)]
        [TestCase(302L, 0)]
        [TestCase(10L, 10)]
        public void Tip_BelowPatienceThreshold_IsNotPaid(long serveTick, int expectedTip)
        {
            ShiftSimulation shift = GuestWithTips(50, 50);

            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.ServeAt(shift, serveTick, 0));

            Assert.AreEqual(expectedTip, shift.Counter.TipCoins);
            Assert.AreEqual(Price + expectedTip, shift.Counter.IncomeCoins);
        }

        [Test]
        public void Tip_IsCountedFromPayAfterMultiplier()
        {
            // Марко: плата ×1,5, чайові до 50%, терпіння 120. Кебаб із гострим соусом: 25 → 38.
            // Видача на такті 11: лишилось 110 → чайові 38 × 50% × 110 / 120 = 17,4 → 17.
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0, "marko"));
            CustomerTestKit.Prepare(shift, BoardTestKit.Hot);

            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.ServeAt(shift, 11L, 0));

            Assert.AreEqual(110L, CustomerTestKit.CustomerOf(shift, 0).PatienceLeftTicks);
            Assert.AreEqual(17L, shift.Counter.TipCoins);
            Assert.AreEqual(55L, CustomerTestKit.CustomerOf(shift, 0).PaidCoins);
            Assert.AreEqual(55L, shift.CoinsEarned);
        }

        [Test]
        public void TipRule_Formula_OnItsOwn()
        {
            var rule = new TipRule(50, 50);

            Assert.AreEqual(10L, rule.TipCoins(20L, 600L, 600L));
            Assert.AreEqual(10L, rule.TipCoins(20L, 900L, 600L), "Залишок не може бути більшим за все терпіння");
            Assert.AreEqual(5L, rule.TipCoins(20L, 300L, 600L));
            Assert.AreEqual(0L, rule.TipCoins(20L, 299L, 600L));
            Assert.AreEqual(0L, rule.TipCoins(20L, 0L, 600L));
            Assert.AreEqual(0L, rule.TipCoins(0L, 600L, 600L));
            Assert.AreEqual(0L, new TipRule(0, 0).TipCoins(20L, 600L, 600L));
            Assert.AreEqual(200L, new TipRule(1000, 0).TipCoins(20L, 600L, 600L));
        }

        // ---------- комбо ----------

        [Test]
        public void Combo_GrowsWithEveryServeInsideWindow_AndBonusIsCapped()
        {
            // Видачі на тактах 10, 20, 30, 40, 50 (вікно 60). Крок 10%, не більше 3 кроків:
            // 20, 20+2, 20+4, 20+6, 20+6.
            ShiftSimulation shift = Olyas(5);

            Assert.AreEqual(20L, CustomerTestKit.PrepareAndServe(shift));
            Assert.AreEqual(1L, shift.Counter.Combo);
            Assert.AreEqual(10L, shift.Counter.LastServeTick);
            Assert.AreEqual(22L, CustomerTestKit.PrepareAndServe(shift));
            Assert.AreEqual(2L, shift.Counter.Combo);
            Assert.AreEqual(24L, CustomerTestKit.PrepareAndServe(shift));
            Assert.AreEqual(26L, CustomerTestKit.PrepareAndServe(shift));
            Assert.AreEqual(4L, shift.Counter.Combo);
            Assert.AreEqual(26L, CustomerTestKit.PrepareAndServe(shift));

            Assert.AreEqual(50L, shift.ElapsedTicks);
            Assert.AreEqual(5L, shift.Counter.Combo);
            Assert.AreEqual(5L, shift.Counter.MaxCombo);
            Assert.AreEqual(5L, shift.MaxCombo);
            Assert.AreEqual(118L, shift.Counter.IncomeCoins);
            Assert.AreEqual(18L, shift.Counter.ComboCoins);
            Assert.AreEqual(0L, shift.Counter.TipCoins);
            Assert.AreEqual(5L, shift.ServedCount);
            Assert.AreEqual(118L, shift.CoinsEarned);
        }

        [Test]
        public void Combo_ServeExactlyAtWindowEnd_StillCounts()
        {
            ShiftSimulation shift = Olyas(2);
            CustomerTestKit.PrepareAndServe(shift);
            CustomerTestKit.Prepare(shift);

            CustomerTestKit.TickTo(shift, 10L + CustomerTestKit.ComboWindowTicks - 1L);
            Assert.AreEqual(1L, shift.Counter.Combo);
            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.Serve(shift));

            Assert.AreEqual(70L, shift.ElapsedTicks);
            Assert.AreEqual(2L, shift.Counter.Combo);
            Assert.AreEqual(22L, shift.Counter.LastPaidCoins);
        }

        [Test]
        public void Combo_ServeOneTickAfterWindow_StartsAgain()
        {
            ShiftSimulation shift = Olyas(2);
            CustomerTestKit.PrepareAndServe(shift);
            CustomerTestKit.Prepare(shift);

            CustomerTestKit.TickTo(shift, 10L + CustomerTestKit.ComboWindowTicks);
            Assert.AreEqual(1L, shift.Counter.Combo, "На останньому такті вікна комбо ще живе");
            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.Serve(shift));

            Assert.AreEqual(71L, shift.ElapsedTicks);
            Assert.AreEqual(1L, shift.Counter.Combo);
            Assert.AreEqual(20L, shift.Counter.LastPaidCoins);
            Assert.AreEqual(1L, shift.Counter.MaxCombo);
            Assert.AreEqual(40L, shift.Counter.IncomeCoins);
        }

        [Test]
        public void Combo_Expires_WhenWindowPassesWithoutServe()
        {
            ShiftSimulation shift = Olyas(2);
            CustomerTestKit.PrepareAndServe(shift);

            CustomerTestKit.TickTo(shift, 70L);
            Assert.AreEqual(1L, shift.Counter.Combo);
            CustomerTestKit.TickTo(shift, 71L);
            Assert.AreEqual(0L, shift.Counter.Combo);
            Assert.AreEqual(1L, shift.Counter.MaxCombo);
        }

        [Test]
        public void Combo_IsResetByWrongDish()
        {
            ShiftSimulation shift = Olyas(3);
            CustomerTestKit.PrepareAndServe(shift);
            Assert.AreEqual(1L, shift.Counter.Combo);

            // Такт 21: кебаб із салатом Олі в слоті 1 — хибна страва.
            CustomerTestKit.Prepare(shift, BoardTestKit.Salad);
            Assert.AreEqual(ShiftActionResult.DishNotAccepted, CustomerTestKit.Serve(shift, 1));
            Assert.AreEqual(0L, shift.Counter.Combo);

            // Такти 22–23: зняти салат і видати — комбо починається з 1, хоча вікно ще не минуло.
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.RemoveIngredient()));
            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.Serve(shift, 1));

            Assert.AreEqual(23L, shift.ElapsedTicks);
            Assert.AreEqual(1L, shift.Counter.Combo);
            Assert.AreEqual(20L, shift.Counter.LastPaidCoins);
            Assert.AreEqual(1L, shift.Counter.MaxCombo);
            Assert.AreEqual(0L, shift.Counter.ComboCoins);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Combo_IsResetByCustomerWhoLeft(bool countLostCustomers)
        {
            // Вікно комбо 300 тактів. Марко йде на такті 121 — комбо скидається,
            // навіть якщо втрата на навчальному рівні не рахується.
            ShiftSimulation shift = CustomerTestKit.RunningWith(CustomerTestKit.CounterParams(
                new[] { CustomerTestKit.At(0), CustomerTestKit.At(0), CustomerTestKit.At(0, "marko") },
                slots: 3,
                comboWindowMs: 5000,
                countLostCustomers: countLostCustomers));
            CustomerTestKit.PrepareAndServe(shift);

            CustomerTestKit.TickTo(shift, 120L);
            Assert.AreEqual(1L, shift.Counter.Combo);
            CustomerTestKit.TickTo(shift, 121L);
            Assert.AreEqual(CustomerState.Left, CustomerTestKit.StateOf(shift, 2));
            Assert.AreEqual(0L, shift.Counter.Combo);

            Assert.AreEqual(20L, CustomerTestKit.PrepareAndServe(shift));
            Assert.AreEqual(1L, shift.Counter.Combo);
            Assert.AreEqual(1L, shift.Counter.MaxCombo);
        }

        [Test]
        public void Combo_MaxCombo_KeepsTheBestRun()
        {
            ShiftSimulation shift = Olyas(4);
            CustomerTestKit.PrepareAndServe(shift);
            CustomerTestKit.PrepareAndServe(shift);
            CustomerTestKit.PrepareAndServe(shift);
            Assert.AreEqual(3L, shift.Counter.MaxCombo);

            CustomerTestKit.TickTo(shift, 200L);
            Assert.AreEqual(20L, CustomerTestKit.PrepareAndServe(shift));

            Assert.AreEqual(1L, shift.Counter.Combo);
            Assert.AreEqual(3L, shift.Counter.MaxCombo);
            Assert.AreEqual(3L, shift.MaxCombo);
        }

        // Крок комбо і його межа — лише параметри: (крок %, кроків не більше) → плата за 1-шу, 2-гу, 3-тю видачу.
        [TestCase(10, 1, 20, 22, 22)]
        [TestCase(10, 0, 20, 20, 20)]
        [TestCase(0, 3, 20, 20, 20)]
        [TestCase(50, 3, 20, 30, 40)]
        [TestCase(25, 2, 20, 25, 30)]
        public void Combo_StepAndCap_AreOnlyParameters(int stepPercent, int maxSteps, int first, int second, int third)
        {
            ShiftSimulation shift = Olyas(3, comboStepPercent: stepPercent, comboMaxSteps: maxSteps);

            Assert.AreEqual(first, CustomerTestKit.PrepareAndServe(shift));
            Assert.AreEqual(second, CustomerTestKit.PrepareAndServe(shift));
            Assert.AreEqual(third, CustomerTestKit.PrepareAndServe(shift));

            Assert.AreEqual(3L, shift.Counter.Combo, "Комбо рахується незалежно від надбавки");
            Assert.AreEqual(first + second + third, shift.Counter.IncomeCoins);
        }

        [Test]
        public void Combo_WindowZero_MeansEveryServeStartsAgain()
        {
            ShiftSimulation shift = Olyas(2, comboWindowMs: 0);

            Assert.AreEqual(20L, CustomerTestKit.PrepareAndServe(shift));
            Assert.AreEqual(20L, CustomerTestKit.PrepareAndServe(shift));

            Assert.AreEqual(1L, shift.Counter.Combo);
            Assert.AreEqual(1L, shift.Counter.MaxCombo);
        }

        // ---------- монети зміни і штрафи ----------

        [Test]
        public void CoinsEarned_IsIncomeMinusGrillAndBoardPenalties()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0));
            Assert.AreEqual(20L, CustomerTestKit.PrepareAndServe(shift));
            Assert.AreEqual(20L, shift.CoinsEarned);

            // Очистити дошку у смітник: штраф 6.
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.AddIngredient(BoardTestKit.Pita)));
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ClearBoard()));
            Assert.AreEqual(BoardTestKit.ClearPenalty, shift.PenaltyCoins);
            Assert.AreEqual(14L, shift.CoinsEarned);

            // Спалити м'ясо і викинути: штраф 7.
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.PlaceMeat()));
            GrillTestKit.Tick(shift, BoardTestKit.CookTicks + BoardTestKit.BurnTicks);
            Assert.AreEqual(1L, shift.BurntCount);
            Assert.AreEqual(14L, shift.CoinsEarned, "Штраф береться, коли пригоріле викинули");
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.DiscardBurntMeat()));

            Assert.AreEqual(20L, shift.IncomeCoins);
            Assert.AreEqual(13L, shift.PenaltyCoins);
            Assert.AreEqual(7L, shift.CoinsEarned);
            Assert.AreEqual(7L, shift.Grill.PenaltyCoins);
            Assert.AreEqual(6L, shift.Board.PenaltyCoins);
        }

        [Test]
        public void CoinsEarned_IsNeverNegative_AndPenaltiesAreNotForgotten()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0));

            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.AddIngredient(BoardTestKit.Pita)));
                Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ClearBoard()));
            }

            Assert.AreEqual(12L, shift.PenaltyCoins);
            Assert.AreEqual(0L, shift.IncomeCoins);
            Assert.AreEqual(0L, shift.CoinsEarned);

            Assert.AreEqual(20L, CustomerTestKit.PrepareAndServe(shift));

            Assert.AreEqual(8L, shift.CoinsEarned);
        }

        [Test]
        public void ShiftWithoutCustomers_CountsOnlyPenalties()
        {
            ShiftSimulation shift = BoardTestKit.CreateRunning();
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.AddIngredient(BoardTestKit.Pita)));
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.ClearBoard()));

            Assert.AreEqual(BoardTestKit.ClearPenalty, shift.PenaltyCoins);
            Assert.AreEqual(0L, shift.CoinsEarned);
            Assert.AreEqual(0L, GrillTestKit.CreateRunning().PenaltyCoins);
            Assert.AreEqual(0L, ShiftTestKit.CreateRunning().PenaltyCoins);
            Assert.AreEqual(0L, ShiftTestKit.CreateRunning().BurntCount);
        }

        // ---------- параметри навчальних рівнів (ADR-020) ----------

        [Test]
        public void LostCustomers_AreNotCounted_WhenParameterIsOff_EverythingElseIsTheSame()
        {
            ShiftSimulation counted = CustomerTestKit.RunningWith(CustomerTestKit.CounterParams(
                new[] { CustomerTestKit.At(0, "marko"), CustomerTestKit.At(0) }, countLostCustomers: true));
            ShiftSimulation tutorial = CustomerTestKit.RunningWith(CustomerTestKit.CounterParams(
                new[] { CustomerTestKit.At(0, "marko"), CustomerTestKit.At(0) }, countLostCustomers: false));

            CustomerTestKit.TickTo(counted, 700L);
            CustomerTestKit.TickTo(tutorial, 700L);

            Assert.AreEqual(2L, counted.Counter.LeftCount);
            Assert.AreEqual(2L, counted.Counter.LostCustomers);
            Assert.AreEqual(2L, counted.LostCustomers);

            Assert.AreEqual(2L, tutorial.Counter.LeftCount, "Клієнти так само йдуть");
            Assert.AreEqual(0L, tutorial.Counter.LostCustomers, "Але втрата не рахується");
            Assert.AreEqual(0L, tutorial.LostCustomers);
            Assert.AreEqual(CustomerState.Left, CustomerTestKit.StateOf(tutorial, 0));
            Assert.AreEqual(CustomerState.Left, CustomerTestKit.StateOf(tutorial, 1));

            // Усе, крім лічильника втрат, однакове.
            CounterSnapshot a = counted.Counter.CaptureSnapshot();
            CounterSnapshot b = tutorial.Counter.CaptureSnapshot();
            Assert.AreEqual(a.GetCustomer(0), b.GetCustomer(0));
            Assert.AreEqual(a.GetCustomer(1), b.GetCustomer(1));
            Assert.AreEqual(a.LeftCount, b.LeftCount);
            Assert.AreEqual(counted.Counter.Slots.FreeCount, tutorial.Counter.Slots.FreeCount);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void BurntMeat_WithoutPenalty_IsOnlyAGrillParameter()
        {
            // Навчальний рівень: grill.burntPenaltyCoins = 0.
            var tutorialGrill = new GrillConfig(2, 100, 200, 0, 0, 0);
            ShiftSimulation shift = CustomerTestKit.RunningWith(
                CustomerTestKit.CounterParams(new[] { CustomerTestKit.At(0) }, countLostCustomers: false),
                grill: tutorialGrill);
            Assert.AreEqual(20L, CustomerTestKit.PrepareAndServe(shift));

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.PlaceMeat()));
            GrillTestKit.Tick(shift, BoardTestKit.CookTicks + BoardTestKit.BurnTicks);
            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.DiscardBurntMeat()));

            Assert.AreEqual(1L, shift.BurntCount, "Пригоріле рахується і без штрафу");
            Assert.AreEqual(0L, shift.PenaltyCoins);
            Assert.AreEqual(20L, shift.CoinsEarned);
        }
    }
}
