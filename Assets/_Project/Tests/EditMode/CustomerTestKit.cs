using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Спільні заготовки для тестів клієнтів (значення — лише для тестів, не баланс гри).
    /// Кухня — з BoardTestKit (рівень 5, дії миттєві, м'ясо готується 6 тактів). За 60 тактів
    /// на секунду: Оля терпить 600 тактів, Марко 120 (завжди гострий соус, плата ×1,5, чайові
    /// до 50%), Ганна 300 (без цибулі). Прилавок: 2 слоти, вікно комбо 60 тактів, крок комбо
    /// 10% (не більше 3 кроків), хибна страва забирає 30 тактів терпіння.
    /// Клієнт, що приходить на час 0, стає в слот на такті 1 з повним терпінням, тож наприкінці
    /// такту T в Олі лишається 601 − T тактів, і йде вона на такті 601.
    /// </summary>
    internal static class CustomerTestKit
    {
        public const int Olya = 0;
        public const int Marko = 1;
        public const int Hanna = 2;

        public const int Slots = 2;
        public const int ComboWindowMs = 1000;
        public const int ComboStepPercent = 10;
        public const int ComboMaxSteps = 3;
        public const int WrongPenaltyMs = 500;
        public const int DurationSec = 60;

        public const long ComboWindowTicks = 60L;
        public const long WrongPenaltyTicks = 30L;
        public const long OlyaPatienceTicks = 600L;
        public const long MarkoPatienceTicks = 120L;
        public const long HannaPatienceTicks = 300L;

        // Біти необов'язкових інгредієнтів рецепта kebab_pita (як у Dish.OptionalMask).
        public const int SaladBit = 1;
        public const int OnionBit = 2;
        public const int GarlicBit = 4;
        public const int HotBit = 8;

        /// <summary>Тип клієнта без особливостей.</summary>
        public static CustomerType Type(string id, int patienceMs, int payPercent, int tipMaxPercent, int tipMinPatiencePercent)
        {
            return new CustomerType(
                id, patienceMs, payPercent, new TipRule(tipMaxPercent, tipMinPatiencePercent), null, null);
        }

        public static List<CustomerType> Types()
        {
            return new List<CustomerType>
            {
                Type("olya", 10000, 100, 0, 0),
                new CustomerType("marko", 2000, 150, new TipRule(50, 0), new[] { "sauce_hot" }, null),
                new CustomerType("hanna", 5000, 100, new TipRule(0, 0), null, new[] { "onion" }),
            };
        }

        /// <summary>Замовлення: простий кебаб у піті.</summary>
        public static Order Plain()
        {
            return new Order("kebab_pita", null, null);
        }

        /// <summary>Замовлення: кебаб у піті з названими необов'язковими інгредієнтами.</summary>
        public static Order With(params string[] optional)
        {
            return new Order("kebab_pita", optional, null);
        }

        /// <summary>Прихід за розкладом без випадковості; order = null — простий кебаб.</summary>
        public static Arrival At(int atMs, string customer = "olya", Order order = null)
        {
            return new Arrival(atMs, customer, order ?? Plain());
        }

        public static CounterConfig CounterParams(
            IEnumerable<Arrival> arrivals,
            int slots = Slots,
            int comboWindowMs = ComboWindowMs,
            int comboStepPercent = ComboStepPercent,
            int comboMaxSteps = ComboMaxSteps,
            int wrongPenaltyMs = WrongPenaltyMs,
            bool countLostCustomers = true,
            bool extraOptionalIsWrong = true,
            IEnumerable<CustomerType> types = null)
        {
            return new CounterConfig(
                slots,
                comboWindowMs,
                comboStepPercent,
                comboMaxSteps,
                wrongPenaltyMs,
                countLostCustomers,
                extraOptionalIsWrong,
                types ?? Types(),
                new ArrivalSchedule(arrivals));
        }

        public static ShiftConfig Config(
            CounterConfig counter, BoardConfig board = null, GrillConfig grill = null, int durationSec = DurationSec)
        {
            return new ShiftConfig(
                ShiftTestKit.TicksPerSecond,
                ShiftTestKit.MaxCatchUpTicks,
                durationSec,
                ShiftTestKit.ContinueBonusSec,
                BoardTestKit.QueueMax,
                grill ?? BoardTestKit.GrillParams(),
                board ?? BoardTestKit.BoardParams(),
                counter);
        }

        /// <summary>Нова зміна з грилем, дошкою і клієнтами, вже в Running.</summary>
        public static ShiftSimulation RunningWith(
            CounterConfig counter,
            BoardConfig board = null,
            GrillConfig grill = null,
            int durationSec = DurationSec,
            bool goalAchieved = true,
            ulong seed = ShiftTestKit.Seed)
        {
            var shift = new ShiftSimulation(
                Config(counter, board, grill, durationSec), seed, new FixedGoal(goalAchieved));
            shift.Start();
            return shift;
        }

        /// <summary>Нова зміна зі звичайними параметрами прилавка і заданими приходами, вже в Running.</summary>
        public static ShiftSimulation Running(params Arrival[] arrivals)
        {
            return RunningWith(CounterParams(arrivals));
        }

        /// <summary>Доводить зміну до заданого такту (ElapsedTicks = tick).</summary>
        public static void TickTo(ShiftSimulation shift, long tick)
        {
            Assert.LessOrEqual(shift.ElapsedTicks, tick, "Підготовка тесту: такт уже минув");
            GrillTestKit.Tick(shift, tick - shift.ElapsedTicks);
        }

        /// <summary>
        /// Збирає на порожній дошці кебаб у піті з названими необов'язковими інгредієнтами.
        /// Триває 9 тактів плюс по такту на кожен необов'язковий.
        /// </summary>
        public static void Prepare(ShiftSimulation shift, params int[] optional)
        {
            var items = new List<int> { BoardTestKit.Pita, BoardTestKit.Meat };
            items.AddRange(optional);
            BoardTestKit.Build(shift, items.ToArray());
        }

        /// <summary>Видає страву з дошки на наступному такті; повертає результат дії.</summary>
        public static ShiftActionResult Serve(ShiftSimulation shift, int target = BoardTarget.AnyCustomer)
        {
            return BoardTestKit.Run(shift, shift.ServeDish(target));
        }

        /// <summary>Видає страву з дошки так, щоб дія виконалась рівно на заданому такті.</summary>
        public static ShiftActionResult ServeAt(ShiftSimulation shift, long tick, int target = BoardTarget.AnyCustomer)
        {
            TickTo(shift, tick - 1L);
            ShiftActionResult result = Serve(shift, target);
            Assert.AreEqual(tick, shift.ElapsedTicks, "Підготовка тесту: такт видачі");
            return result;
        }

        /// <summary>Збирає простий кебаб і одразу видає його; повертає, скільки заплатили.</summary>
        public static long PrepareAndServe(ShiftSimulation shift, int target = BoardTarget.AnyCustomer)
        {
            Prepare(shift);
            Assert.AreEqual(ShiftActionResult.Done, Serve(shift, target), "Страву не видано");
            return shift.Counter.LastPaidCoins;
        }

        public static Customer CustomerOf(ShiftSimulation shift, int id)
        {
            return shift.Counter.GetCustomer(id);
        }

        public static CustomerState StateOf(ShiftSimulation shift, int id)
        {
            return shift.Counter.GetCustomer(id).State;
        }
    }
}
