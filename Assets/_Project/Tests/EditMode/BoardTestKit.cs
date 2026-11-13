using System.Collections.Generic;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Спільні заготовки для тестів дошки (значення — лише для тестів, не баланс гри).
    /// Рецепт kebab_pita: піта, м'ясо, далі необов'язкові салат, цибуля, часниковий і гострий соус.
    /// Рецепт kebab_lavash (лаваш, м'ясо) відкривається на рівні 8. Соус — з рівня 3, салат — з 4.
    /// Гриль: 2 слоти, приготування 6 тактів, пригорання ще через 12; дії миттєві.
    /// </summary>
    internal static class BoardTestKit
    {
        public const int Pita = 0;
        public const int Meat = 1;
        public const int Salad = 2;
        public const int Onion = 3;
        public const int Garlic = 4;
        public const int Hot = 5;
        public const int Lavash = 6;
        public const int IngredientCount = 7;

        public const int KebabPrice = 20;
        public const int LavashPrice = 30;
        public const int SaladExtra = 3;
        public const int OnionExtra = 2;
        public const int GarlicExtra = 4;
        public const int HotExtra = 5;
        public const int ClearPenalty = 6;

        public const int Level = 5;
        public const int SauceLevel = 3;
        public const int SaladLevel = 4;
        public const int LavashLevel = 8;
        public const int Capacity = 6;
        public const int QueueMax = 4;

        public const long CookTicks = 6L;
        public const long BurnTicks = 12L;

        public static GrillConfig GrillParams()
        {
            return new GrillConfig(2, 100, 200, 0, 7, 0);
        }

        public static List<StationConfig> Stations(
            int breadMs = 0, int sauceMs = 0, int saladMs = 0, int boardMs = 0, int binMs = 0, int binLevel = 1)
        {
            return new List<StationConfig>
            {
                new StationConfig(StationKind.Bread, 1, breadMs),
                new StationConfig(StationKind.Sauce, SauceLevel, sauceMs),
                new StationConfig(StationKind.Salad, SaladLevel, saladMs),
                new StationConfig(StationKind.Board, 1, boardMs),
                new StationConfig(StationKind.Bin, binLevel, binMs),
            };
        }

        public static List<Ingredient> Ingredients()
        {
            return new List<Ingredient>
            {
                new Ingredient("pita", StationKind.Bread, 0),
                new Ingredient("meat", StationKind.Grill, 0),
                new Ingredient("salad", StationKind.Salad, SaladExtra),
                new Ingredient("onion", StationKind.Salad, OnionExtra),
                new Ingredient("sauce_garlic", StationKind.Sauce, GarlicExtra),
                new Ingredient("sauce_hot", StationKind.Sauce, HotExtra),
                new Ingredient("lavash", StationKind.Bread, 0),
            };
        }

        public static Recipe Kebab()
        {
            return new Recipe(
                "kebab_pita",
                new[] { "pita", "meat" },
                new[] { "salad", "onion", "sauce_garlic", "sauce_hot" },
                KebabPrice,
                1);
        }

        public static List<Recipe> Recipes()
        {
            return new List<Recipe>
            {
                Kebab(),
                new Recipe("kebab_lavash", new[] { "lavash", "meat" }, new[] { "sauce_hot" }, LavashPrice, LavashLevel),
            };
        }

        public static BoardConfig BoardParams(
            int level = Level,
            int clearPenaltyCoins = ClearPenalty,
            IEnumerable<StationConfig> stations = null,
            IEnumerable<Ingredient> ingredients = null,
            IEnumerable<Recipe> recipes = null)
        {
            return new BoardConfig(
                level,
                clearPenaltyCoins,
                stations ?? Stations(),
                ingredients ?? Ingredients(),
                recipes ?? Recipes());
        }

        public static ShiftConfig Config(BoardConfig board = null, int actionQueueMax = QueueMax)
        {
            return new ShiftConfig(
                ShiftTestKit.TicksPerSecond,
                ShiftTestKit.MaxCatchUpTicks,
                GrillTestKit.DurationSec,
                ShiftTestKit.ContinueBonusSec,
                actionQueueMax,
                GrillParams(),
                board ?? BoardParams());
        }

        /// <summary>Нова зміна з грилем і дошкою в стані Intro.</summary>
        public static ShiftSimulation Create(
            BoardConfig board = null, IDishReceiver receiver = null, int actionQueueMax = QueueMax)
        {
            return new ShiftSimulation(
                Config(board, actionQueueMax), ShiftTestKit.Seed, new FixedGoal(true), receiver);
        }

        /// <summary>Нова зміна з грилем і дошкою, вже переведена в Running.</summary>
        public static ShiftSimulation CreateRunning(
            BoardConfig board = null, IDishReceiver receiver = null, int actionQueueMax = QueueMax)
        {
            ShiftSimulation shift = Create(board, receiver, actionQueueMax);
            shift.Start();
            return shift;
        }

        /// <summary>
        /// Команду має бути прийнято; виконує один такт (дії в цих тестах миттєві)
        /// і повертає результат дії, перевіривши, що виконалась рівно одна.
        /// </summary>
        public static ShiftActionResult Run(ShiftSimulation shift, bool accepted)
        {
            Assert.IsTrue(accepted, "Команду не прийнято");
            long before = shift.ExecutedActionCount + shift.IgnoredActionCount;
            GrillTestKit.Tick(shift, 1);
            Assert.AreEqual(before + 1, shift.ExecutedActionCount + shift.IgnoredActionCount, "Дія не виконалась");
            return shift.LastActionResult;
        }

        /// <summary>Кладе м'ясо на гриль, чекає готовності і знімає його на дошку.</summary>
        public static void AddMeat(ShiftSimulation shift)
        {
            Assert.AreEqual(ShiftActionResult.Done, Run(shift, shift.PlaceMeat()), "М'ясо не покладено");
            GrillTestKit.Tick(shift, CookTicks);
            Assert.AreEqual(ShiftActionResult.Done, Run(shift, shift.TakeMeat()), "М'ясо не знято");
        }

        /// <summary>Кладе інгредієнти на дошку в заданому порядку (м'ясо — через гриль).</summary>
        public static void Build(ShiftSimulation shift, params int[] ingredients)
        {
            foreach (int ingredient in ingredients)
            {
                if (ingredient == Meat)
                {
                    AddMeat(shift);
                }
                else
                {
                    Assert.AreEqual(
                        ShiftActionResult.Done, Run(shift, shift.AddIngredient(ingredient)), "Інгредієнт не покладено");
                }
            }

            CollectionAssert.AreEqual(ingredients, Items(shift), "Підготовка тесту: вміст дошки");
        }

        /// <summary>Вміст дошки в порядку, в якому його поклали.</summary>
        public static int[] Items(ShiftSimulation shift)
        {
            var items = new int[shift.Board.ItemCount];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = shift.Board.GetItem(i);
            }

            return items;
        }
    }

    /// <summary>Отримувач страв для тестів: запам'ятовує, що йому видали, і може відмовити.</summary>
    internal sealed class FakeDishReceiver : IDishReceiver
    {
        public bool Accept { get; set; } = true;

        public int Calls { get; private set; }

        public Dish LastDish { get; private set; }

        public int LastTarget { get; private set; }

        public bool TryReceive(Dish dish, int target)
        {
            Calls++;
            LastDish = dish;
            LastTarget = target;
            return Accept;
        }
    }
}
