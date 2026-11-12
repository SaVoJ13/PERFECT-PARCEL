using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Слоти прилавка і черга (GAME_DESIGN.md, розд. 6): клієнти займають вільні слоти в порядку
    /// приходу; хто не вмістився — чекає в черзі й не втрачає терпіння, доки не стане в слот.
    /// </summary>
    public sealed class CounterQueueTests
    {
        private static Arrival Olya(int atMs = 0)
        {
            return CustomerTestKit.At(atMs);
        }

        private static Arrival Marko(int atMs = 0)
        {
            return CustomerTestKit.At(atMs, "marko");
        }

        [Test]
        public void Customers_TakeLowestFreeSlots_InScheduleOrder()
        {
            ShiftSimulation shift = CustomerTestKit.Running(Olya(), Olya());

            GrillTestKit.Tick(shift, 1);

            Assert.AreEqual(2, shift.Counter.Slots.Count);
            Assert.AreEqual(0, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(1, shift.Counter.Slots.GetCustomer(1));
            Assert.AreEqual(0, CustomerTestKit.CustomerOf(shift, 0).Slot);
            Assert.AreEqual(1, CustomerTestKit.CustomerOf(shift, 1).Slot);
            Assert.AreEqual(0, shift.Counter.Slots.FreeCount);
            Assert.AreEqual(0, shift.Counter.QueueLength);
            Assert.AreEqual(2, shift.Counter.CustomerCount);
        }

        [Test]
        public void Queue_BeyondSlots_Waits_WithoutLosingPatience()
        {
            ShiftSimulation shift = CustomerTestKit.Running(Olya(), Olya(), Olya());
            Customer third = CustomerTestKit.CustomerOf(shift, 2);

            CustomerTestKit.TickTo(shift, 100L);

            Assert.AreEqual(CustomerState.Arriving, third.State);
            Assert.AreEqual(CounterSlots.None, third.Slot);
            Assert.AreEqual(CustomerTestKit.OlyaPatienceTicks, third.PatienceLeftTicks);
            Assert.AreEqual(1, shift.Counter.QueueLength);
            Assert.AreSame(third, shift.Counter.GetQueued(0));
            Assert.AreEqual(3, shift.Counter.ArrivedCount);
            Assert.AreEqual(CustomerTestKit.OlyaPatienceTicks - 99L, CustomerTestKit.CustomerOf(shift, 0).PatienceLeftTicks);
            Assert.AreEqual(CustomerTestKit.OlyaPatienceTicks - 99L, CustomerTestKit.CustomerOf(shift, 1).PatienceLeftTicks);
        }

        [Test]
        public void Queue_TakesSlot_OnTheSameTickSomeoneLeaves_WithFullPatience()
        {
            // Марко (120 тактів терпіння) у слоті 0 з такту 1 → іде на такті 121.
            ShiftSimulation shift = CustomerTestKit.Running(Marko(), Olya(), Olya());
            Customer third = CustomerTestKit.CustomerOf(shift, 2);

            CustomerTestKit.TickTo(shift, CustomerTestKit.MarkoPatienceTicks);
            Assert.AreEqual(CustomerState.Waiting, CustomerTestKit.StateOf(shift, 0));
            Assert.AreEqual(CustomerState.Arriving, third.State);

            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(CustomerState.Left, CustomerTestKit.StateOf(shift, 0));
            Assert.AreEqual(CustomerState.Waiting, third.State);
            Assert.AreEqual(0, third.Slot);
            Assert.AreEqual(2, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(CustomerTestKit.OlyaPatienceTicks, third.PatienceLeftTicks);
            Assert.AreEqual(0, shift.Counter.QueueLength);

            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(CustomerTestKit.OlyaPatienceTicks - 1L, third.PatienceLeftTicks);
        }

        [Test]
        public void Queue_TakesSlot_OnTheTickAfterServe()
        {
            ShiftSimulation shift = CustomerTestKit.Running(Olya(), Olya(), Olya());
            Customer third = CustomerTestKit.CustomerOf(shift, 2);
            CustomerTestKit.Prepare(shift);

            // Такт 10: видача клієнтові в слоті 0. Слот вільний, черга займе його на такті 11.
            Assert.AreEqual(ShiftActionResult.Done, CustomerTestKit.ServeAt(shift, 10L, 0));
            Assert.AreEqual(CustomerState.Served, CustomerTestKit.StateOf(shift, 0));
            Assert.AreEqual(CounterSlots.None, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(CustomerState.Arriving, third.State);
            Assert.AreEqual(1, shift.Counter.Slots.FreeCount);

            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(CustomerState.Waiting, third.State);
            Assert.AreEqual(0, third.Slot);
            Assert.AreEqual(CustomerTestKit.OlyaPatienceTicks, third.PatienceLeftTicks);
        }

        [Test]
        public void Queue_IsFirstInFirstOut()
        {
            // Один слот, чотири Марки: кожен наступний стає в слот, коли попередній пішов.
            ShiftSimulation shift = CustomerTestKit.RunningWith(
                CustomerTestKit.CounterParams(new[] { Marko(), Marko(), Marko(), Marko() }, slots: 1));
            long patience = CustomerTestKit.MarkoPatienceTicks;

            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(3, shift.Counter.QueueLength);
            Assert.AreEqual(1, shift.Counter.GetQueued(0).Id);
            Assert.AreEqual(2, shift.Counter.GetQueued(1).Id);
            Assert.AreEqual(3, shift.Counter.GetQueued(2).Id);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => shift.Counter.GetQueued(3));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => shift.Counter.GetQueued(-1));

            CustomerTestKit.TickTo(shift, 1L + patience);
            Assert.AreEqual(1, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(2, shift.Counter.GetQueued(0).Id);

            CustomerTestKit.TickTo(shift, 1L + 2L * patience);
            Assert.AreEqual(2, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(CustomerState.Arriving, CustomerTestKit.StateOf(shift, 3));

            CustomerTestKit.TickTo(shift, 1L + 3L * patience);
            Assert.AreEqual(3, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(0, shift.Counter.QueueLength);

            CustomerTestKit.TickTo(shift, 1L + 4L * patience);
            Assert.AreEqual(4L, shift.Counter.LeftCount);
            Assert.AreEqual(1, shift.Counter.Slots.FreeCount);
        }

        [Test]
        public void LaterArrival_TakesTheFreedLowestSlot()
        {
            // Три слоти; Марко зі слота 0 йде на такті 121, новий клієнт приходить на такті 180.
            ShiftSimulation shift = CustomerTestKit.RunningWith(
                CustomerTestKit.CounterParams(new[] { Marko(), Olya(), Olya(), Olya(3000) }, slots: 3));

            CustomerTestKit.TickTo(shift, 179L);
            Assert.AreEqual(CounterSlots.None, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(1, shift.Counter.Slots.GetCustomer(1));
            Assert.AreEqual(2, shift.Counter.Slots.GetCustomer(2));
            Assert.AreEqual(CustomerState.None, CustomerTestKit.StateOf(shift, 3));

            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(3, shift.Counter.Slots.GetCustomer(0));
            Assert.AreEqual(CustomerState.Waiting, CustomerTestKit.StateOf(shift, 3));
            Assert.AreEqual(4, shift.Counter.ArrivedCount);
        }

        [Test]
        public void Serve_ToEmptyOrMissingSlot_IsNotAccepted_AndNobodyIsPunished()
        {
            ShiftSimulation shift = CustomerTestKit.Running(Olya());
            Customer customer = CustomerTestKit.CustomerOf(shift, 0);
            CustomerTestKit.Prepare(shift);

            // Такт 10: слот 1 порожній. Такт 11: слота 5 немає.
            Assert.AreEqual(ShiftActionResult.DishNotAccepted, CustomerTestKit.Serve(shift, 1));
            Assert.AreEqual(DeliveryResult.NoCustomer, shift.Counter.LastDelivery);
            Assert.AreEqual(ShiftActionResult.DishNotAccepted, CustomerTestKit.Serve(shift, 5));
            Assert.AreEqual(DeliveryResult.NoCustomer, shift.Counter.LastDelivery);

            Assert.AreEqual(0L, shift.Counter.WrongDishes);
            Assert.AreEqual(0L, customer.WrongDishCount);
            Assert.AreEqual(CustomerTestKit.OlyaPatienceTicks - 10L, customer.PatienceLeftTicks);
            Assert.AreEqual(CustomerState.Waiting, customer.State);
            Assert.AreEqual(2, shift.Board.ItemCount);
            Assert.AreEqual(0L, shift.Counter.LastPaidCoins);
        }

        [Test]
        public void Slots_AnswerOnlyForExistingNumbers()
        {
            ShiftSimulation shift = CustomerTestKit.Running(Olya());
            CounterSlots slots = shift.Counter.Slots;

            Assert.IsTrue(slots.IsValidSlot(0));
            Assert.IsTrue(slots.IsValidSlot(1));
            Assert.IsFalse(slots.IsValidSlot(2));
            Assert.IsFalse(slots.IsValidSlot(-1));
            Assert.AreEqual(CounterSlots.None, slots.GetCustomer(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => slots.GetCustomer(2));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => slots.GetCustomer(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => shift.Counter.GetCustomer(1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => shift.Counter.GetCustomer(-1));
        }
    }
}
