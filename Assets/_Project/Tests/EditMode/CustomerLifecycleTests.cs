using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Життєвий цикл клієнта (GAME_DESIGN.md, розд. 6): Arriving → Waiting → Served | Left,
    /// терпіння спадає лише в слоті, межі терпіння — з точністю до такту.
    /// </summary>
    public sealed class CustomerLifecycleTests
    {
        private const long Patience = CustomerTestKit.OlyaPatienceTicks;

        [Test]
        public void Customer_IsNotThere_UntilHisArrivalTick()
        {
            // 1000 мс = такт 60.
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(1000));
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);

            Assert.AreEqual(60L, customer.ArrivalTick);
            CustomerTestKit.TickTo(shift, 59L);
            Assert.AreEqual(CustomerState.None, customer.State);
            Assert.AreEqual(0, shift.Counter.ArrivedCount);
            Assert.AreEqual(CounterSlots.None, customer.Slot);
            Assert.AreEqual(2, shift.Counter.Slots.FreeCount);

            CustomerTestKit.TickTo(shift, 60L);
            Assert.AreEqual(CustomerState.Waiting, customer.State);
            Assert.AreEqual(1, shift.Counter.ArrivedCount);
            Assert.AreEqual(0, customer.Slot);
            Assert.AreEqual(Patience, customer.PatienceLeftTicks);
            Assert.AreSame(customer, shift.Counter.GetCustomerInSlot(0));
            Assert.IsNull(shift.Counter.GetCustomerInSlot(1));
        }

        [Test]
        public void Customer_ScheduledAtZero_TakesSlotOnFirstTick_WithFullPatience()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0));
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);

            Assert.AreEqual(CustomerState.None, customer.State, "До першого такту клієнта ще немає");
            Assert.AreEqual(Patience, customer.PatienceTicks);

            GrillTestKit.Tick(shift, 1);

            Assert.AreEqual(CustomerState.Waiting, customer.State);
            Assert.AreEqual(0, customer.Slot);
            Assert.AreEqual(Patience, customer.PatienceLeftTicks);
            Assert.AreEqual(CustomerTestKit.Olya, customer.TypeIndex);
            Assert.AreEqual(0, customer.Id);
        }

        [Test]
        public void Patience_DropsByOneTickPerTick_WhileWaiting()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0));
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);

            for (long tick = 1L; tick <= Patience; tick++)
            {
                GrillTestKit.Tick(shift, 1);
                Assert.AreEqual(CustomerState.Waiting, customer.State, "Такт " + tick);
                Assert.AreEqual(Patience + 1L - tick, customer.PatienceLeftTicks, "Такт " + tick);
            }
        }

        [Test]
        public void Customer_Leaves_ExactlyWhenPatienceRunsOut()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0));
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);

            CustomerTestKit.TickTo(shift, Patience);
            Assert.AreEqual(CustomerState.Waiting, customer.State);
            Assert.AreEqual(1L, customer.PatienceLeftTicks);
            Assert.AreEqual(0L, shift.Counter.LeftCount);

            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(CustomerState.Left, customer.State);
            Assert.AreEqual(0L, customer.PatienceLeftTicks);
            Assert.AreEqual(CounterSlots.None, customer.Slot);
            Assert.AreEqual(CounterSlots.None, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(1L, shift.Counter.LeftCount);
            Assert.AreEqual(1L, shift.Counter.LostCustomers);
            Assert.AreEqual(1L, shift.LostCustomers);
            Assert.AreEqual(0L, shift.Counter.ServedCount);
        }

        [Test]
        public void Left_Customer_StaysLeft_AndIsCountedOnce()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0));
            CustomerTestKit.TickTo(shift, Patience + 1L);
            CounterSnapshot left = shift.Counter.CaptureSnapshot();

            GrillTestKit.Tick(shift, 500);

            Assert.AreEqual(left, shift.Counter.CaptureSnapshot());
            Assert.AreEqual(1L, shift.Counter.LeftCount);
        }

        [Test]
        public void Serve_OnLastTickOfPatience_Succeeds()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0));
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);
            CustomerTestKit.Prepare(shift);

            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.ServeAt(shift, Patience, 0));

            Assert.AreEqual(CustomerState.Served, customer.State);
            Assert.AreEqual(1L, customer.PatienceLeftTicks);
            Assert.AreEqual(CounterSlots.None, customer.Slot);
            Assert.AreEqual(BoardTestKit.KebabPrice, customer.PaidCoins);
            Assert.AreEqual(1L, shift.Counter.ServedCount);
            Assert.AreEqual(1L, shift.ServedCount);
            Assert.AreEqual(0L, shift.Counter.LeftCount);
            Assert.AreEqual(DeliveryResult.Served, shift.Counter.LastDelivery);
            Assert.AreEqual(0, shift.Board.ItemCount);
        }

        [Test]
        public void Serve_OnTickCustomerLeaves_IsTooLate_AndDishStaysOnBoard()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0));
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);
            CustomerTestKit.Prepare(shift);

            Assert.AreEqual(
                ShiftActionResult.DishNotAccepted, CustomerTestKit.ServeAt(shift, Patience + 1L, 0));

            Assert.AreEqual(CustomerState.Left, customer.State);
            Assert.AreEqual(0L, customer.PaidCoins);
            Assert.AreEqual(DeliveryResult.NoCustomer, shift.Counter.LastDelivery);
            Assert.AreEqual(0L, shift.Counter.ServedCount);
            Assert.AreEqual(0L, shift.Counter.WrongDishes);
            Assert.AreEqual(0L, shift.IncomeCoins);
            Assert.AreEqual(2, shift.Board.ItemCount);
            Assert.AreEqual(0L, shift.Board.ServedCount);
        }

        [Test]
        public void Served_Customer_FreesSlot_AndNeverLeaves()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0));
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);
            CustomerTestKit.Prepare(shift);
            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.Serve(shift, 0));
            long patienceWhenServed = customer.PatienceLeftTicks;

            Assert.AreEqual(Patience + 1L - 10L, patienceWhenServed);
            Assert.AreEqual(2, shift.Counter.Slots.FreeCount);

            GrillTestKit.Tick(shift, Patience + 100L);

            Assert.AreEqual(CustomerState.Served, customer.State);
            Assert.AreEqual(patienceWhenServed, customer.PatienceLeftTicks);
            Assert.AreEqual(0L, shift.Counter.LeftCount);
            Assert.AreEqual(0L, shift.Counter.LostCustomers);
        }

        [Test]
        public void Grace_DelaysPatienceLoss_ByExactlyItsLength()
        {
            // Пільга 1000 мс = 60 тактів: у слоті з такту 1, терпіння спадає з такту 62.
            ShiftSimulation shift = CustomerTestKit.Running(
                new Arrival(0, "olya", CustomerTestKit.Plain(), 0, 1000));
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);
            Assert.AreEqual(60L, customer.GraceTicks);

            CustomerTestKit.TickTo(shift, 1L);
            Assert.AreEqual(60L, customer.GraceLeftTicks);
            Assert.AreEqual(Patience, customer.PatienceLeftTicks);

            CustomerTestKit.TickTo(shift, 61L);
            Assert.AreEqual(0L, customer.GraceLeftTicks);
            Assert.AreEqual(Patience, customer.PatienceLeftTicks);

            CustomerTestKit.TickTo(shift, 62L);
            Assert.AreEqual(Patience - 1L, customer.PatienceLeftTicks);

            CustomerTestKit.TickTo(shift, 60L + Patience);
            Assert.AreEqual(CustomerState.Waiting, customer.State);
            Assert.AreEqual(1L, customer.PatienceLeftTicks);

            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(CustomerState.Left, customer.State);
        }

        [Test]
        public void Pause_FreezesPatienceAndArrivals()
        {
            ShiftSimulation shift = CustomerTestKit.Running(CustomerTestKit.At(0), CustomerTestKit.At(500));
            CustomerTestKit.TickTo(shift, 10L);
            Assert.IsTrue(shift.Pause());
            ShiftSnapshot paused = shift.CaptureSnapshot();

            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(shift.Tick());
            }

            Assert.AreEqual(0, shift.Advance(5d));
            Assert.AreEqual(paused, shift.CaptureSnapshot());
            Assert.AreEqual(Patience - 9L, CustomerTestKit.CustomerOf(shift, 0).PatienceLeftTicks);
            Assert.AreEqual(CustomerState.None, CustomerTestKit.StateOf(shift, 1));

            Assert.IsTrue(shift.Resume());
            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(Patience - 10L, CustomerTestKit.CustomerOf(shift, 0).PatienceLeftTicks);

            // Другий клієнт приходить на такті 30 зміни, а не «за годинником».
            CustomerTestKit.TickTo(shift, 29L);
            Assert.AreEqual(CustomerState.None, CustomerTestKit.StateOf(shift, 1));
            CustomerTestKit.TickTo(shift, 30L);
            Assert.AreEqual(CustomerState.Waiting, CustomerTestKit.StateOf(shift, 1));
        }

        [Test]
        public void LostShift_FreezesCustomers_AndContinueResumesThem()
        {
            // Зміна на 1 секунду (60 тактів), ціль не виконано → Lost.
            ShiftSimulation shift = CustomerTestKit.RunningWith(
                CustomerTestKit.CounterParams(new[] { CustomerTestKit.At(0) }), durationSec: 1, goalAchieved: false);
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);

            ShiftTestKit.TickUntilNotRunning(shift);
            Assert.AreEqual(ShiftState.Lost, shift.State);
            Assert.AreEqual(60L, shift.ElapsedTicks);
            Assert.AreEqual(Patience - 59L, customer.PatienceLeftTicks);

            Assert.IsFalse(shift.Tick());
            Assert.IsFalse(shift.ServeDish(0), "Після поразки команди не приймаються");
            Assert.AreEqual(Patience - 59L, customer.PatienceLeftTicks);

            Assert.IsTrue(shift.Continue());
            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(Patience - 60L, customer.PatienceLeftTicks);
            Assert.AreEqual(CustomerState.Waiting, customer.State);
        }

        [Test]
        public void Customers_AreReadOnlyFromOutside_ShiftWithoutCounterHasNone()
        {
            ShiftSimulation withoutCounter = BoardTestKit.CreateRunning();

            Assert.IsNull(withoutCounter.Counter);
            Assert.AreEqual(default(CounterSnapshot), withoutCounter.CaptureSnapshot().Counter);
            Assert.AreEqual(0L, withoutCounter.ServedCount);
            Assert.AreEqual(0L, withoutCounter.LostCustomers);
            Assert.AreEqual(0L, withoutCounter.WrongDishes);
            Assert.AreEqual(0L, withoutCounter.MaxCombo);
            Assert.AreEqual(0L, withoutCounter.IncomeCoins);
            Assert.AreEqual(0L, withoutCounter.CoinsEarned);
        }
    }
}
