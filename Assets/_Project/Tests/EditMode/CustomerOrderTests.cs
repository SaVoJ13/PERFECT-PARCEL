using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Приймання страви клієнтом (GAME_DESIGN.md, розд. 5–6): страва має відповідати замовленню
    /// з урахуванням необов'язкових інгредієнтів і прохання «без цибулі»; хибна страва —
    /// скарга, втрата терпіння, страва лишається на дошці.
    /// </summary>
    public sealed class CustomerOrderTests
    {
        private const int Salad = BoardTestKit.Salad;
        private const int Onion = BoardTestKit.Onion;
        private const int Garlic = BoardTestKit.Garlic;
        private const int Hot = BoardTestKit.Hot;

        private static ShiftSimulation OneCustomer(string customer, Order order, bool extraOptionalIsWrong = true)
        {
            return CustomerTestKit.RunningWith(CustomerTestKit.CounterParams(
                new[] { CustomerTestKit.At(0, customer, order) }, extraOptionalIsWrong: extraOptionalIsWrong));
        }

        private static void AssertServed(ShiftSimulation shift, int target = 0)
        {
            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.Serve(shift, target));
            Assert.AreEqual(DeliveryResult.Served, shift.Counter.LastDelivery);
            Assert.AreEqual(CustomerState.Served, CustomerTestKit.StateOf(shift, 0));
            Assert.AreEqual(0, shift.Board.ItemCount);
            Assert.AreEqual(1L, shift.Counter.ServedCount);
            Assert.AreEqual(0L, shift.Counter.WrongDishes);
        }

        private static void AssertWrong(ShiftSimulation shift, int itemsOnBoard, int target = 0)
        {
            long patienceBefore = CustomerTestKit.CustomerOf(shift, 0).PatienceLeftTicks;

            Assert.AreEqual(ShiftActionResult.DishNotAccepted, CustomerTestKit.Serve(shift, target));

            Assert.AreEqual(DeliveryResult.WrongDish, shift.Counter.LastDelivery);
            Assert.AreEqual(CustomerState.Waiting, CustomerTestKit.StateOf(shift, 0));
            Assert.AreEqual(itemsOnBoard, shift.Board.ItemCount, "Хибна страва лишається на дошці");
            Assert.AreEqual(0L, shift.Counter.ServedCount);
            Assert.AreEqual(1L, shift.Counter.WrongDishes);
            Assert.AreEqual(1L, shift.WrongDishes);

            // Такт видачі забирає 1 такт терпіння, скарга — ще штраф.
            Assert.AreEqual(
                patienceBefore - 1L - CustomerTestKit.WrongPenaltyTicks,
                CustomerTestKit.CustomerOf(shift, 0).PatienceLeftTicks);
        }

        [Test]
        public void PlainOrder_PlainDish_IsServed()
        {
            ShiftSimulation shift = OneCustomer("olya", CustomerTestKit.Plain());
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);
            Assert.AreEqual(0, customer.RecipeIndex);
            Assert.AreEqual(0, customer.RequestedMask);
            Assert.AreEqual(0, customer.ExcludeMask);

            CustomerTestKit.Prepare(shift);
            AssertServed(shift);

            Assert.AreEqual(BoardTestKit.KebabPrice, customer.PaidCoins);
            Assert.AreEqual(new Dish(0, 0, BoardTestKit.KebabPrice), shift.Board.LastServed);
            Assert.AreEqual(1L, shift.Board.ServedCount);
        }

        [Test]
        public void OrderWithOptional_DishWithIt_IsServed_AndExtraPriceIsPaid()
        {
            ShiftSimulation shift = OneCustomer("olya", CustomerTestKit.With("salad", "sauce_garlic"));
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);
            Assert.AreEqual(CustomerTestKit.SaladBit | CustomerTestKit.GarlicBit, customer.RequestedMask);

            CustomerTestKit.Prepare(shift, Salad, Garlic);
            AssertServed(shift);

            Assert.AreEqual(
                BoardTestKit.KebabPrice + BoardTestKit.SaladExtra + BoardTestKit.GarlicExtra, customer.PaidCoins);
        }

        [Test]
        public void OrderWithOptional_DishWithoutIt_IsWrong()
        {
            ShiftSimulation shift = OneCustomer("olya", CustomerTestKit.With("salad"));

            CustomerTestKit.Prepare(shift);

            AssertWrong(shift, 2);
        }

        [Test]
        public void OrderWithOptional_DishWithAnotherOptional_IsWrong()
        {
            ShiftSimulation shift = OneCustomer("olya", CustomerTestKit.With("salad"));

            CustomerTestKit.Prepare(shift, Garlic);

            AssertWrong(shift, 3);
        }

        [Test]
        public void ExtraOptional_NobodyAskedFor_IsWrongDish_WhenRuleIsOn()
        {
            ShiftSimulation shift = OneCustomer("olya", CustomerTestKit.Plain());

            CustomerTestKit.Prepare(shift, Salad);

            AssertWrong(shift, 3);
        }

        [Test]
        public void ExtraOptional_IsAccepted_AndPaid_WhenRuleIsOff()
        {
            ShiftSimulation shift = OneCustomer("olya", CustomerTestKit.Plain(), extraOptionalIsWrong: false);

            CustomerTestKit.Prepare(shift, Salad, Hot);
            AssertServed(shift);

            Assert.AreEqual(
                BoardTestKit.KebabPrice + BoardTestKit.SaladExtra + BoardTestKit.HotExtra,
                CustomerTestKit.CustomerOf(shift, 0).PaidCoins);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingRequestedOptional_IsWrong_UnderBothRules(bool extraOptionalIsWrong)
        {
            ShiftSimulation shift = OneCustomer("olya", CustomerTestKit.With("salad"), extraOptionalIsWrong);

            CustomerTestKit.Prepare(shift);

            AssertWrong(shift, 2);
        }

        [Test]
        public void TypeThatAlwaysAsksForIngredient_HasItInOrder()
        {
            // Марко завжди просить гострий соус, хоч у замовленні хвилі його немає.
            ShiftSimulation shift = OneCustomer("marko", CustomerTestKit.Plain());
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);
            Assert.AreEqual(CustomerTestKit.HotBit, customer.RequestedMask);
            Assert.AreEqual(CustomerTestKit.Marko, customer.TypeIndex);

            CustomerTestKit.Prepare(shift);
            AssertWrong(shift, 2);

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.AddIngredient(Hot)));
            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.Serve(shift, 0));
            Assert.AreEqual(CustomerState.Served, customer.State);
            Assert.AreEqual(1L, customer.WrongDishCount);
        }

        [Test]
        public void TypeAlways_IsCombinedWithOrderOptional_AndMayRepeatIt()
        {
            ShiftSimulation both = OneCustomer("marko", CustomerTestKit.With("salad"));
            Assert.AreEqual(
                CustomerTestKit.SaladBit | CustomerTestKit.HotBit, CustomerTestKit.CustomerOf(both, 0).RequestedMask);
            CustomerTestKit.Prepare(both, Salad, Hot);
            AssertServed(both);

            ShiftSimulation repeated = OneCustomer("marko", CustomerTestKit.With("sauce_hot"));
            Assert.AreEqual(CustomerTestKit.HotBit, CustomerTestKit.CustomerOf(repeated, 0).RequestedMask);
            CustomerTestKit.Prepare(repeated, Hot);
            AssertServed(repeated);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NoOnion_DishWithOnion_IsWrong_UnderBothRules(bool extraOptionalIsWrong)
        {
            // Бабуся Ганна: салат, але без цибулі.
            ShiftSimulation shift = OneCustomer("hanna", CustomerTestKit.With("salad"), extraOptionalIsWrong);
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);
            Assert.AreEqual(CustomerTestKit.SaladBit, customer.RequestedMask);
            Assert.AreEqual(CustomerTestKit.OnionBit, customer.ExcludeMask);

            CustomerTestKit.Prepare(shift, Salad, Onion);

            AssertWrong(shift, 4);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NoOnion_DishWithoutOnion_IsServed_UnderBothRules(bool extraOptionalIsWrong)
        {
            ShiftSimulation shift = OneCustomer("hanna", CustomerTestKit.With("salad"), extraOptionalIsWrong);

            CustomerTestKit.Prepare(shift, Salad);
            AssertServed(shift);

            Assert.AreEqual(
                BoardTestKit.KebabPrice + BoardTestKit.SaladExtra, CustomerTestKit.CustomerOf(shift, 0).PaidCoins);
        }

        [Test]
        public void NoOnion_WrongDish_CanBeFixedByRemovingOnion()
        {
            ShiftSimulation shift = OneCustomer("hanna", CustomerTestKit.With("salad"));
            CustomerTestKit.Prepare(shift, Salad, Onion);
            AssertWrong(shift, 4);

            Assert.AreEqual(ShiftActionResult.Done, BoardTestKit.Run(shift, shift.RemoveIngredient()));
            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.Serve(shift, 0));

            Assert.AreEqual(CustomerState.Served, CustomerTestKit.StateOf(shift, 0));
            Assert.AreEqual(1L, shift.Counter.WrongDishes);
            Assert.AreEqual(1L, shift.Counter.ServedCount);
            Assert.AreEqual(0L, shift.Board.PenaltyCoins, "Зняти інгредієнт — без штрафу");
        }

        [Test]
        public void ExcludeInOrder_WorksLikeExcludeOfType()
        {
            // «Без цибулі» можна задати і в самому замовленні хвилі (request.exclude).
            var order = new Order("kebab_pita", new[] { "salad" }, new[] { "onion" });
            ShiftSimulation shift = OneCustomer("olya", order, extraOptionalIsWrong: false);
            Assert.AreEqual(CustomerTestKit.OnionBit, CustomerTestKit.CustomerOf(shift, 0).ExcludeMask);

            CustomerTestKit.Prepare(shift, Salad, Onion);

            AssertWrong(shift, 4);
        }

        [Test]
        public void WrongDish_Repeated_ExhaustsPatience_AndCustomerLeaves()
        {
            // Марко: 120 тактів. Простий кебаб йому хибний (потрібен гострий соус).
            // Такти 10, 11, 12, 13: залишок 111→81, 80→50, 49→19, 18→0.
            ShiftSimulation shift = OneCustomer("marko", CustomerTestKit.Plain());
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);
            CustomerTestKit.Prepare(shift);

            CustomerTestKit.Serve(shift, 0);
            Assert.AreEqual(81L, customer.PatienceLeftTicks);
            CustomerTestKit.Serve(shift, 0);
            Assert.AreEqual(50L, customer.PatienceLeftTicks);
            CustomerTestKit.Serve(shift, 0);
            Assert.AreEqual(19L, customer.PatienceLeftTicks);
            Assert.AreEqual(CustomerState.Waiting, customer.State);

            Assert.AreEqual(ShiftActionResult.DishNotAccepted, CustomerTestKit.Serve(shift, 0));

            Assert.AreEqual(CustomerState.Left, customer.State);
            Assert.AreEqual(0L, customer.PatienceLeftTicks);
            Assert.AreEqual(4L, customer.WrongDishCount);
            Assert.AreEqual(4L, shift.Counter.WrongDishes);
            Assert.AreEqual(1L, shift.Counter.LeftCount);
            Assert.AreEqual(1L, shift.Counter.LostCustomers);
            Assert.AreEqual(DeliveryResult.WrongDish, shift.Counter.LastDelivery);
            Assert.AreEqual(CounterSlots.None, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(2, shift.Board.ItemCount);
        }

        [Test]
        public void WrongRecipe_IsWrongDish_RightRecipeIsServed()
        {
            // Рівень 8: доступний і кебаб у лаваші.
            BoardConfig board = BoardTestKit.BoardParams(level: BoardTestKit.LavashLevel);
            CounterConfig counter = CustomerTestKit.CounterParams(new[]
            {
                CustomerTestKit.At(0),
                CustomerTestKit.At(0, "olya", new Order("kebab_lavash", null, null)),
            });
            ShiftSimulation shift = CustomerTestKit.RunningWith(counter, board);
            Assert.AreEqual(1, CustomerTestKit.CustomerOf(shift, 1).RecipeIndex);
            BoardTestKit.Build(shift, BoardTestKit.Lavash, BoardTestKit.Meat);

            Assert.AreEqual(ShiftActionResult.DishNotAccepted, CustomerTestKit.Serve(shift, 0));
            Assert.AreEqual(DeliveryResult.WrongDish, shift.Counter.LastDelivery);
            Assert.AreEqual(1L, shift.Counter.WrongDishes);

            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.Serve(shift, 1));
            Assert.AreEqual(CustomerState.Served, CustomerTestKit.StateOf(shift, 1));
            Assert.AreEqual(BoardTestKit.LavashPrice, CustomerTestKit.CustomerOf(shift, 1).PaidCoins);
            Assert.AreEqual(CustomerState.Waiting, CustomerTestKit.StateOf(shift, 0));
        }

        [Test]
        public void ServeWithoutTarget_GoesToCustomerWhoseOrderMatches()
        {
            // Слот 0 — Марко (гострий соус), слот 1 — Оля (простий кебаб).
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0, "marko"), CustomerTestKit.At(0));
            CustomerTestKit.Prepare(shift);

            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.Serve(shift));

            Assert.AreEqual(CustomerState.Waiting, CustomerTestKit.StateOf(shift, 0));
            Assert.AreEqual(CustomerState.Served, CustomerTestKit.StateOf(shift, 1));
            Assert.AreEqual(0L, shift.Counter.WrongDishes);
            Assert.AreEqual(0L, CustomerTestKit.CustomerOf(shift, 0).WrongDishCount);
        }

        [Test]
        public void ServeWithoutTarget_PrefersCustomerWithLeastPatience_ThenLowerSlot()
        {
            // Оля (600 тактів) у слоті 0, Ганна (300) у слоті 1: простий кебаб підходить обом.
            ShiftSimulation shift = CustomerTestKit.RunningWith(CustomerTestKit.CounterParams(
                new[] { CustomerTestKit.At(0), CustomerTestKit.At(0, "hanna"), CustomerTestKit.At(0), CustomerTestKit.At(0) },
                slots: 4));

            CustomerTestKit.PrepareAndServe(shift);
            Assert.AreEqual(CustomerState.Served, CustomerTestKit.StateOf(shift, 1), "Ганні лишилось найменше");

            // Далі три Олі з однаковим терпінням: спершу слот 0, потім слот 2.
            CustomerTestKit.PrepareAndServe(shift);
            Assert.AreEqual(CustomerState.Served, CustomerTestKit.StateOf(shift, 0));
            Assert.AreEqual(CustomerState.Waiting, CustomerTestKit.StateOf(shift, 2));

            CustomerTestKit.PrepareAndServe(shift);
            Assert.AreEqual(CustomerState.Served, CustomerTestKit.StateOf(shift, 2));
            Assert.AreEqual(CustomerState.Waiting, CustomerTestKit.StateOf(shift, 3));
        }

        [Test]
        public void ServeWithoutTarget_WhenNobodyWantsTheDish_IsNotAWrongDish()
        {
            ShiftSimulation shift = OneCustomer("marko", CustomerTestKit.Plain());
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);
            CustomerTestKit.Prepare(shift);

            Assert.AreEqual(ShiftActionResult.DishNotAccepted, CustomerTestKit.Serve(shift));

            Assert.AreEqual(DeliveryResult.NoMatch, shift.Counter.LastDelivery);
            Assert.AreEqual(0L, shift.Counter.WrongDishes);
            Assert.AreEqual(0L, customer.WrongDishCount);
            Assert.AreEqual(CustomerTestKit.MarkoPatienceTicks - 9L, customer.PatienceLeftTicks);
            Assert.AreEqual(2, shift.Board.ItemCount);
        }

        [Test]
        public void ServeWithoutTarget_WhenNobodyIsAtTheCounter_IsNoCustomer()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(5000));
            CustomerTestKit.Prepare(shift);

            Assert.AreEqual(ShiftActionResult.DishNotAccepted, CustomerTestKit.Serve(shift));

            Assert.AreEqual(DeliveryResult.NoCustomer, shift.Counter.LastDelivery);
            Assert.AreEqual(0L, shift.Counter.WrongDishes);
            Assert.AreEqual(2, shift.Board.ItemCount);
        }

        [Test]
        public void CustomerInQueue_CannotBeServed()
        {
            // Один слот: друга Оля в черзі. Видача «будь-кому» дістається лише тій, що в слоті.
            ShiftSimulation shift = CustomerTestKit.RunningWith(CustomerTestKit.CounterParams(
                new[] { CustomerTestKit.At(0, "marko"), CustomerTestKit.At(0) }, slots: 1));
            CustomerTestKit.Prepare(shift);

            Assert.AreEqual(ShiftActionResult.DishNotAccepted, CustomerTestKit.Serve(shift));

            Assert.AreEqual(DeliveryResult.NoMatch, shift.Counter.LastDelivery);
            Assert.AreEqual(CustomerState.Arriving, CustomerTestKit.StateOf(shift, 1));
        }

        [Test]
        public void Accepts_ComparesRecipeRequestedAndForbidden()
        {
            ShiftSimulation strict = OneCustomer("hanna", CustomerTestKit.With("salad"));
            Customer customer = CustomerTestKit.CustomerOf(strict, 0);

            Assert.IsTrue(customer.Accepts(new Dish(0, CustomerTestKit.SaladBit, 0)));
            Assert.IsFalse(customer.Accepts(new Dish(1, CustomerTestKit.SaladBit, 0)));
            Assert.IsFalse(customer.Accepts(new Dish(0, 0, 0)));
            Assert.IsFalse(customer.Accepts(new Dish(0, CustomerTestKit.SaladBit | CustomerTestKit.OnionBit, 0)));
            Assert.IsFalse(customer.Accepts(new Dish(0, CustomerTestKit.SaladBit | CustomerTestKit.HotBit, 0)));

            ShiftSimulation relaxed = OneCustomer("hanna", CustomerTestKit.With("salad"), extraOptionalIsWrong: false);
            customer = CustomerTestKit.CustomerOf(relaxed, 0);

            Assert.IsTrue(customer.Accepts(new Dish(0, CustomerTestKit.SaladBit, 0)));
            Assert.IsTrue(customer.Accepts(new Dish(0, CustomerTestKit.SaladBit | CustomerTestKit.HotBit, 0)));
            Assert.IsFalse(customer.Accepts(new Dish(0, CustomerTestKit.SaladBit | CustomerTestKit.OnionBit, 0)));
            Assert.IsFalse(customer.Accepts(new Dish(0, CustomerTestKit.HotBit, 0)));
        }
    }
}
