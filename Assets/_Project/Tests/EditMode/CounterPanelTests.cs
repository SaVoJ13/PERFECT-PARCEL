using System;
using System.Collections.Generic;
using KebabStreet.Core;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Прилавок на екрані зміни (S1-09b): картки слотів за конфігом рівня, ім'я, сердечка, значки
    /// замовлення, черга, події (оплата з чайовими, скарга, відхід), порожня черга і максимум
    /// клієнтів; тап по клієнту йде лише в ShiftDriver. Елементи UI Toolkit без панелі й сцени.
    /// </summary>
    public sealed class CounterPanelTests
    {
        private const ulong Seed = 4410337UL;
        private static readonly ILocalizer Text = new PlaceholderLocalizer();

        private static ShiftDriver Started(LevelDefinition level)
        {
            var driver = new ShiftDriver(level, Seed);
            Assert.IsTrue(driver.Begin());
            return driver;
        }

        private static ShiftScreen BuildScreen(ShiftDriver driver)
        {
            return new ShiftScreen(new VisualElement(), driver, Text, () => { }, () => { }, () => { });
        }

        /// <summary>Рівень 4, де прилавок має максимум слотів і всі клієнти приходять одразу.</summary>
        private static LevelDefinition CrowdedLevel4()
        {
            ConfigSources sources = ConfigTestKit.Mutated("L04", t =>
            {
                t["counterSlots"] = (long)CounterConfig.MaxSlots;
                foreach (object wave in ConfigTestKit.Arr(t, "waves"))
                {
                    ((Dictionary<string, object>)wave)["atSec"] = 0L;
                }
            });
            ConfigResult<LevelCatalog> result = ConfigLoader.LoadAll(sources);
            Assert.IsTrue(result.IsValid, ConfigTestKit.Join(result.Errors));
            LevelDefinition level = result.Value.FindByNumber(4);
            Assert.IsNotNull(level);
            return level;
        }

        private static void Step(ShiftDriver driver, ShiftScreen screen)
        {
            driver.Frame(1.0 / driver.Level.Shift.TicksPerSecond);
            screen.Refresh();
        }

        private static void RunUntil(ShiftDriver driver, ShiftScreen screen, Func<bool> done, string what)
        {
            for (int guard = 0; guard < 100000 && !done(); guard++)
            {
                Assert.IsFalse(driver.IsFinished, "зміна скінчилась раніше, ніж: " + what);
                Step(driver, screen);
            }

            Assert.IsTrue(done(), what);
        }

        /// <summary>Перший слот, де стоїть клієнт цього типу; plainOrder — лише з замовленням «просто кебаб».</summary>
        private static int SlotOfType(Counter counter, string typeId, bool plainOrder)
        {
            int type = counter.Config.IndexOfType(typeId);
            for (int slot = 0; slot < counter.Slots.Count; slot++)
            {
                Customer customer = counter.GetCustomerInSlot(slot);
                if (customer == null || customer.TypeIndex != type)
                {
                    continue;
                }

                if (!plainOrder || (customer.RequestedMask == 0 && customer.ExcludeMask == 0))
                {
                    return slot;
                }
            }

            Assert.Fail("немає клієнта " + typeId + " у слотах");
            return -1;
        }

        [Test]
        public void EmptyCounter_AtStart_ShowsFreeSlots_AndNoQueue()
        {
            ShiftDriver driver = Started(ConfigTestKit.Level(4));
            ShiftScreen screen = BuildScreen(driver);
            CounterPanel counter = screen.Counter;

            Assert.AreEqual(driver.Level.Shift.Counter.Slots, counter.SlotCount);
            Assert.IsFalse(counter.QueueVisible);
            Assert.AreEqual(string.Empty, counter.QueueText);
            for (int slot = 0; slot < counter.SlotCount; slot++)
            {
                Assert.IsFalse(counter.GetShownView(slot).Occupied);
                Assert.AreEqual(Text.Get(TextKeys.CounterSlotFree), counter.GetNameText(slot));
                Assert.AreEqual(0, counter.GetOrderChipCount(slot));
                Assert.AreEqual(string.Empty, counter.GetEventText(slot));
                Assert.IsFalse(counter.TapSlot(slot), "тап по порожньому слоту нічого не шле");
            }

            Assert.AreEqual(0, driver.Simulation.QueuedActionCount);
            Assert.IsFalse(counter.TapSlot(-1));
            Assert.IsFalse(counter.TapSlot(counter.SlotCount));
        }

        [Test]
        public void MaxCustomers_AllSlotsFilled_QueueShown_WithoutErrors()
        {
            ShiftDriver driver = Started(CrowdedLevel4());
            ShiftScreen screen = BuildScreen(driver);
            CounterPanel counter = screen.Counter;
            Counter core = driver.Simulation.Counter;
            Assert.AreEqual(CounterConfig.MaxSlots, counter.SlotCount);

            Assert.DoesNotThrow(() => Step(driver, screen));
            Assert.AreEqual(CounterConfig.MaxSlots, core.Slots.Count - core.Slots.FreeCount, "усі слоти зайняті");
            int queued = core.CustomerCount - CounterConfig.MaxSlots;
            Assert.Greater(queued, 0, "у розкладі L04 клієнтів більше, ніж слотів");
            Assert.AreEqual(queued, core.QueueLength);
            Assert.IsTrue(counter.QueueVisible);
            Assert.AreEqual(string.Format(Text.Get(TextKeys.CounterQueue), queued), counter.QueueText);

            BoardConfig board = driver.Simulation.Board.Config;
            for (int slot = 0; slot < counter.SlotCount; slot++)
            {
                Customer customer = core.GetCustomerInSlot(slot);
                CustomerSlotView view = counter.GetShownView(slot);
                Assert.AreEqual(CounterView.FromSimulation(driver.Simulation, slot, CounterView.DefaultHearts), view);
                Assert.AreEqual(customer.Id, view.CustomerId);
                Assert.AreEqual(Text.Get(core.Config.GetCustomerType(customer.TypeIndex).NameKey), counter.GetNameText(slot));

                OrderIcon[] icons = CounterView.OrderOf(board, customer);
                Assert.AreEqual(icons.Length, counter.GetOrderChipCount(slot));
                for (int i = 0; i < icons.Length; i++)
                {
                    Assert.AreEqual(Text.Get(board.GetIngredient(icons[i].Ingredient).NameKey), counter.GetOrderChipText(slot, i));
                    Assert.AreEqual(icons[i].Kind == OrderIconKind.Excluded, counter.IsOrderChipStruck(slot, i), "«не класти» перекреслено");
                }
            }

            // Перший, хто піде, звільняє слот — його займає наступний із черги.
            RunUntil(driver, screen, () => core.QueueLength < queued, "черга рушила");
            Assert.AreEqual(core.QueueLength > 0, counter.QueueVisible);
        }

        [Test]
        public void ShownSlots_FollowSimulation_AndLeavingCustomerShowsEvent()
        {
            ShiftDriver driver = Started(ConfigTestKit.Level(4));
            ShiftScreen screen = BuildScreen(driver);
            CounterPanel counter = screen.Counter;
            Counter core = driver.Simulation.Counter;

            RunUntil(driver, screen, () =>
            {
                for (int slot = 0; slot < counter.SlotCount; slot++)
                {
                    Assert.AreEqual(CounterView.FromSimulation(driver.Simulation, slot, CounterView.DefaultHearts), counter.GetShownView(slot));
                }

                return core.LeftCount > 0;
            }, "без дій гравця клієнт іде");

            int leftSlot = -1;
            for (int slot = 0; slot < counter.SlotCount; slot++)
            {
                if (counter.GetShownEvent(slot).Kind == CounterEventKind.Left)
                {
                    leftSlot = slot;
                }
            }

            Assert.GreaterOrEqual(leftSlot, 0, "біля слота показано, що клієнт пішов");
            Assert.AreEqual(Text.Get(TextKeys.CounterLeft), counter.GetEventText(leftSlot));
            long until = counter.GetShownEvent(leftSlot).UntilTick;
            Assert.AreEqual(
                driver.Simulation.ElapsedTicks + TickTime.FromMs(CounterFeed.ShowMs, driver.Level.Shift.TicksPerSecond),
                until);

            RunUntil(driver, screen, () => driver.Simulation.ElapsedTicks >= until, "подія відпоказувалась");
            CounterEvent after = counter.GetShownEvent(leftSlot);
            Assert.IsTrue(after.Kind == CounterEventKind.None || after.UntilTick != until, "подія зникає через " + CounterFeed.ShowMs + " мс");
            if (after.Kind == CounterEventKind.None)
            {
                Assert.AreEqual(string.Empty, counter.GetEventText(leftSlot));
            }
        }

        [Test]
        public void TapOnCustomer_ServesThroughDriver_WrongDishThenPaidWithTip()
        {
            ShiftDriver driver = Started(CrowdedLevel4());
            ShiftScreen screen = BuildScreen(driver);
            CounterPanel counter = screen.Counter;
            ShiftSimulation sim = driver.Simulation;
            Counter core = sim.Counter;
            Step(driver, screen);

            int hanna = SlotOfType(core, "hanna", false);
            int olya = SlotOfType(core, "olya", true);
            Assert.AreNotEqual(0, core.GetCustomerInSlot(hanna).RequestedMask, "Ганна на L04 просить салат");
            int pita = sim.Board.Config.IndexOfIngredient("pita");

            // Звичайний кебаб (піта + м'ясо) — лише командами драйвера.
            Assert.IsTrue(driver.Tap(ShiftTap.AddIngredient, pita));
            Assert.IsTrue(driver.Tap(ShiftTap.PlaceMeat));
            RunUntil(driver, screen, () => sim.Grill.GetSlot(0).State == MeatState.Done && sim.QueuedActionCount == 0, "м'ясо готове");
            Assert.IsTrue(driver.Tap(ShiftTap.TakeMeat));
            RunUntil(driver, screen, () => sim.QueuedActionCount == 0 && sim.Board.ItemCount == 2, "кебаб на дошці");

            // Ганна просила салат: страва хибна, вона скаржиться, страва лишається на дошці.
            long hannaPatience = core.GetCustomerInSlot(hanna).PatienceLeftTicks;
            Assert.IsTrue(counter.TapSlot(hanna));
            Assert.AreEqual(new ShiftAction(ShiftActionType.ServeDish, hanna), sim.GetQueuedAction(0));
            Assert.IsFalse(counter.TapSlot(hanna), "видача Ганні вже в черзі");
            screen.Refresh();
            Assert.IsTrue(counter.GetShownView(hanna).ServeQueued);
            RunUntil(driver, screen, () => sim.QueuedActionCount == 0, "видачу виконано");
            Assert.AreEqual(DeliveryResult.WrongDish, core.LastDelivery);
            Assert.AreEqual(CounterEventKind.WrongDish, counter.GetShownEvent(hanna).Kind);
            Assert.AreEqual(Text.Get(TextKeys.CounterWrongDish), counter.GetEventText(hanna));
            long penalty = TickTime.FromMs(core.Config.WrongDishPatiencePenaltyMs, driver.Level.Shift.TicksPerSecond);
            Assert.LessOrEqual(core.GetCustomerInSlot(hanna).PatienceLeftTicks, hannaPatience - penalty, "скарга коштує терпіння (Core)");
            Assert.AreEqual(2, sim.Board.ItemCount);

            // Оля бере звичайний кебаб і платить; чайові — з лічильника Core.
            Customer olyaCustomer = core.GetCustomerInSlot(olya);
            Assert.IsTrue(counter.TapSlot(olya));
            RunUntil(driver, screen, () => sim.QueuedActionCount == 0, "видачу Олі виконано");
            Assert.AreEqual(DeliveryResult.Served, core.LastDelivery);
            Assert.AreEqual(CustomerState.Served, olyaCustomer.State);

            CounterEvent paid = counter.GetShownEvent(olya);
            Assert.AreEqual(CounterEventKind.Served, paid.Kind);
            Assert.AreEqual(olyaCustomer.PaidCoins, paid.PaidCoins);
            Assert.AreEqual(core.TipCoins, paid.TipCoins, "одна видача за зміну: чайові = чайові прилавка");
            string expected = paid.TipCoins > 0
                ? string.Format(Text.Get(TextKeys.CounterPaidTip), paid.PaidCoins, paid.TipCoins)
                : string.Format(Text.Get(TextKeys.CounterPaid), paid.PaidCoins);
            Assert.AreEqual(expected, counter.GetEventText(olya));
            Assert.AreNotEqual(olyaCustomer.Id, counter.GetShownView(olya).CustomerId, "обслужена Оля пішла зі слота");
        }

        [Test]
        public void CounterTexts_ComeFromLocalizationKeys_WhenCrowded()
        {
            ShiftDriver driver = Started(CrowdedLevel4());
            var root = new VisualElement();
            ShiftScreen screen = new ShiftScreen(root, driver, Text, () => { }, () => { }, () => { });
            Step(driver, screen);

            // PlaceholderLocalizer повертає «[ключ]» для невідомого ключа: жодного такого напису на екрані.
            root.Query<TextElement>().ForEach(e =>
                Assert.IsFalse(e.text != null && e.text.StartsWith("["), "немає перекладу: " + e.text));
        }
    }
}
