using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Модель вигляду прилавка (S1-09b): терпіння → сердечка й смужка, значки замовлення за рецептом
    /// і особливістю клієнта, тап по клієнту → «видати страву», імена й назви — ключі з конфігів.
    /// Без об'єктів Unity.
    /// </summary>
    public sealed class CounterViewTests
    {
        private const ulong Seed = 4410337UL;
        private const int Hearts = CounterView.DefaultHearts;

        // Умовне терпіння 600 тактів (не з конфігу): одне сердечко = 200, половинка = 100.
        [TestCase(600L, 6, 100, false, TestName = "Patience_Full_ThreeHearts")]
        [TestCase(599L, 6, 100, false, TestName = "Patience_JustStarted_StillThreeHearts")]
        [TestCase(500L, 5, 84, false, TestName = "Patience_FiveSixths_TwoAndHalfHearts")]
        [TestCase(300L, 3, 50, false, TestName = "Patience_Half_OneAndHalfHearts")]
        [TestCase(201L, 3, 34, false, TestName = "Patience_AboveOneHeart_NotLow")]
        [TestCase(200L, 2, 34, false, TestName = "Patience_ExactlyOneHeart_NotLow")]
        [TestCase(199L, 2, 34, true, TestName = "Patience_BelowOneHeart_Low")]
        [TestCase(1L, 1, 1, true, TestName = "Patience_LastTick_HalfHeartLow")]
        [TestCase(0L, 0, 0, false, TestName = "Patience_Zero_Empty")]
        [TestCase(-5L, 0, 0, false, TestName = "Patience_Negative_Empty")]
        [TestCase(700L, 6, 100, false, TestName = "Patience_AboveTotal_Clamped")]
        public void Patience_MapsToHalfHeartsBarAndLow(long left, int halves, int percent, bool low)
        {
            PatienceView view = CounterView.Patience(left, 600L, 0L, Hearts);
            Assert.AreEqual(Hearts, view.Hearts);
            Assert.AreEqual(halves, view.HalfHearts, view.ToString());
            Assert.AreEqual(percent, view.Percent, view.ToString());
            Assert.AreEqual(low, view.Low, view.ToString());
            Assert.IsFalse(view.Grace);
        }

        [Test]
        public void Hearts_FillLeftToRight_WithHalves()
        {
            PatienceView view = CounterView.Patience(300L, 600L, 0L, Hearts);
            Assert.AreEqual(HeartFill.Full, view.GetHeart(0));
            Assert.AreEqual(HeartFill.Half, view.GetHeart(1));
            Assert.AreEqual(HeartFill.Empty, view.GetHeart(2));

            PatienceView full = CounterView.Patience(600L, 600L, 0L, Hearts);
            PatienceView empty = CounterView.Patience(0L, 600L, 0L, Hearts);
            for (int i = 0; i < Hearts; i++)
            {
                Assert.AreEqual(HeartFill.Full, full.GetHeart(i));
                Assert.AreEqual(HeartFill.Empty, empty.GetHeart(i));
            }
        }

        [Test]
        public void Patience_OtherHeartCount_DividesScaleEqually_AndGraceIsShown()
        {
            PatienceView five = CounterView.Patience(500L, 1000L, 0L, 5);
            Assert.AreEqual(5, five.HalfHearts, "половина з 5 сердечок = 2,5");
            Assert.IsTrue(CounterView.Patience(600L, 600L, 30L, Hearts).Grace);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CounterView.Patience(1L, 1L, 0L, 0));
        }

        [Test]
        public void CustomerWhoLeft_ShowsNoHearts_AndFreesSlot()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(4), Seed);
            Assert.IsTrue(driver.Begin());
            Counter counter = driver.Simulation.Counter;
            for (int guard = 0; guard < 100000 && counter.LeftCount == 0 && !driver.IsFinished; guard++)
            {
                driver.Frame(0.1);
            }

            Assert.Greater(counter.LeftCount, 0L, "без дій гравця клієнт рано чи пізно йде");
            Customer left = Enumerable.Range(0, counter.CustomerCount)
                .Select(id => counter.GetCustomer(id))
                .First(c => c.State == CustomerState.Left);
            PatienceView patience = CounterView.Patience(left, Hearts);
            Assert.AreEqual(0, patience.HalfHearts);
            Assert.AreEqual(0, patience.Percent);
            for (int i = 0; i < Hearts; i++)
            {
                Assert.AreEqual(HeartFill.Empty, patience.GetHeart(i));
            }

            for (int slot = 0; slot < counter.Slots.Count; slot++)
            {
                Assert.AreNotEqual(left.Id, CounterView.FromSimulation(driver.Simulation, slot, Hearts).CustomerId, "клієнт, що пішов, у слоті не показується");
            }
        }

        [Test]
        public void SlotView_FollowsLiveCustomerPatience()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(4), Seed);
            Assert.IsTrue(driver.Begin());
            ShiftSimulation sim = driver.Simulation;
            bool seenOccupied = false;
            for (int guard = 0; guard < 3000 && !driver.IsFinished; guard++)
            {
                driver.Frame(1.0 / sim.Config.TicksPerSecond);
                for (int slot = 0; slot < sim.Counter.Slots.Count; slot++)
                {
                    CustomerSlotView view = CounterView.FromSimulation(sim, slot, Hearts);
                    Customer customer = sim.Counter.GetCustomerInSlot(slot);
                    if (customer == null)
                    {
                        Assert.IsFalse(view.Occupied);
                        Assert.AreEqual(CustomerSlotView.Free, view);
                        continue;
                    }

                    seenOccupied = true;
                    Assert.AreEqual(customer.Id, view.CustomerId);
                    Assert.AreEqual(customer.TypeIndex, view.TypeIndex);
                    Assert.AreEqual(
                        CounterView.Patience(customer.PatienceLeftTicks, customer.PatienceTicks, customer.GraceLeftTicks, Hearts),
                        view.Patience);
                }
            }

            Assert.IsTrue(seenOccupied);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CounterView.FromSimulation(sim, -1, Hearts));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CounterView.FromSimulation(sim, sim.Counter.Slots.Count, Hearts));
        }

        [Test]
        public void OrderIcons_MatchRecipeAndCustomerType_OnLevel4()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(4), Seed);
            ShiftSimulation sim = driver.Simulation;
            BoardConfig board = sim.Board.Config;
            Counter counter = sim.Counter;

            // Розклад L04: [0] olya — просто кебаб; [2] hanna — салат, «без цибулі» (тип);
            // [3] marko — гострий соус (тип «завжди»); [4] olya — салат і цибуля; [5] olya — часниковий.
            AssertOrder(board, counter.GetCustomer(0), "pita:Step meat:Step");
            AssertOrder(board, counter.GetCustomer(2), "pita:Step meat:Step salad:Requested onion:Excluded");
            AssertOrder(board, counter.GetCustomer(3), "pita:Step meat:Step sauce_hot:Requested");
            AssertOrder(board, counter.GetCustomer(4), "pita:Step meat:Step salad:Requested onion:Requested");
            AssertOrder(board, counter.GetCustomer(5), "pita:Step meat:Step sauce_garlic:Requested");
            AssertOrder(board, counter.GetCustomer(7), "pita:Step meat:Step salad:Requested sauce_hot:Requested");

            for (int id = 0; id < counter.CustomerCount; id++)
            {
                Assert.LessOrEqual(CounterView.OrderOf(board, counter.GetCustomer(id)).Length, 4, "бульбашка — до 4 значків (ART_STYLE_GUIDE 4.1)");
            }
        }

        [Test]
        public void OrderIcons_FromMasks_StepsThenRequestedThenExcluded()
        {
            BoardConfig board = ConfigTestKit.Level(4).Shift.Board;
            int recipe = board.IndexOfRecipe("kebab_pita");
            Recipe kebab = board.GetRecipe(recipe);
            int salad = 1 << Optional(kebab, "salad");
            int onion = 1 << Optional(kebab, "onion");
            int hot = 1 << Optional(kebab, "sauce_hot");

            AssertIcons(board, CounterView.OrderOf(board, recipe, 0, 0), "pita:Step meat:Step");
            AssertIcons(board, CounterView.OrderOf(board, recipe, hot | salad, onion), "pita:Step meat:Step salad:Requested sauce_hot:Requested onion:Excluded");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CounterView.OrderOf(board, board.RecipeCount, 0, 0));
            Assert.Throws<System.ArgumentNullException>(() => CounterView.OrderOf(null, 0, 0, 0));
        }

        [Test]
        public void Tap_OnCustomer_ServesToThatSlot_FreeOrQueuedSendsNothing()
        {
            ShiftTap tap;
            Assert.IsFalse(CounterView.TryGetTap(CustomerSlotView.Free, out tap), "порожній слот");

            PatienceView patience = CounterView.Patience(10L, 10L, 0L, Hearts);
            Assert.IsTrue(CounterView.TryGetTap(new CustomerSlotView(3, 0, patience, false), out tap));
            Assert.AreEqual(ShiftTap.ServeDish, tap);
            Assert.IsFalse(CounterView.TryGetTap(new CustomerSlotView(3, 0, patience, true), out tap), "видача цьому клієнту вже в черзі");
        }

        [Test]
        public void CustomerAndIngredientNameKeys_FromConfigs_ExistAsTextKeys()
        {
            var known = new HashSet<string>(typeof(TextKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => (string)f.GetRawConstantValue()));
            LevelCatalog catalog = ConfigTestKit.LoadCatalog();
            int checkedKeys = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                ShiftConfig shift = catalog.Get(i).Shift;
                if (shift.Counter != null)
                {
                    for (int t = 0; t < shift.Counter.TypeCount; t++)
                    {
                        CustomerType type = shift.Counter.GetCustomerType(t);
                        Assert.AreEqual("customer." + type.Id + ".name", type.NameKey, "customers.json nameKey");
                        Assert.IsTrue(known.Contains(type.NameKey), "немає TextKeys для " + type.NameKey);
                        checkedKeys++;
                    }
                }

                if (shift.Board != null)
                {
                    for (int k = 0; k < shift.Board.IngredientCount; k++)
                    {
                        Ingredient ingredient = shift.Board.GetIngredient(k);
                        Assert.IsTrue(known.Contains(ingredient.NameKey), "немає TextKeys для " + ingredient.NameKey);
                        checkedKeys++;
                    }
                }
            }

            Assert.Greater(checkedKeys, 0);
        }

        [Test]
        public void QueueLength_ComesFromCounter()
        {
            var driver = new ShiftDriver(ConfigTestKit.Level(4), Seed);
            Assert.AreEqual(0, CounterView.QueueLength(driver.Simulation));
            Assert.AreEqual(driver.Simulation.Counter.QueueLength, CounterView.QueueLength(driver.Simulation));
        }

        private static int Optional(Recipe recipe, string id)
        {
            for (int i = 0; i < recipe.OptionalCount; i++)
            {
                if (recipe.GetOptional(i) == id)
                {
                    return i;
                }
            }

            Assert.Fail("немає необов'язкового " + id);
            return -1;
        }

        private static void AssertOrder(BoardConfig board, Customer customer, string expected)
        {
            AssertIcons(board, CounterView.OrderOf(board, customer), expected);
        }

        private static void AssertIcons(BoardConfig board, OrderIcon[] icons, string expected)
        {
            string actual = string.Join(" ", icons.Select(i => board.GetIngredient(i.Ingredient).Id + ":" + i.Kind));
            Assert.AreEqual(expected, actual);
        }
    }
}
