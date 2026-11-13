using System;
using System.Collections.Generic;
using System.Globalization;

namespace KebabStreet.Core
{
    /// <summary>Значення гриля, що можуть бути в stations.json (типові) і в рівні (перевизначення); null — не задано.</summary>
    internal sealed class GrillValues
    {
        public int? Slots;
        public int? CookMs;
        public int? BurnAfterDoneMs;
        public int? WarnBeforeBurnMs;
        public int? BurntPenaltyCoins;

        public static GrillValues Read(ConfigObject node)
        {
            var values = new GrillValues();
            int value;
            if (node.TryInt("slots", 1, GrillConfig.MaxSlots, out value))
            {
                values.Slots = value;
            }

            if (node.TryMillis("cookSec", 1, GrillConfig.MaxDurationMs, out value))
            {
                values.CookMs = value;
            }

            if (node.TryMillis("burnAfterDoneSec", 1, GrillConfig.MaxDurationMs, out value))
            {
                values.BurnAfterDoneMs = value;
            }

            if (node.TryMillis("warnBeforeBurnSec", 0, GrillConfig.MaxDurationMs, out value))
            {
                values.WarnBeforeBurnMs = value;
            }

            if (node.TryInt("burntPenaltyCoins", 0, GrillConfig.MaxBurntPenaltyCoins, out value))
            {
                values.BurntPenaltyCoins = value;
            }

            node.Done();
            return values;
        }
    }

    /// <summary>
    /// Спільні для всіх рівнів конфіги: rules.json, stations.json, recipes.json, customers.json.
    /// Розбираються один раз; помилки додаються в спільний список із шляхами до ключів.
    /// </summary>
    internal sealed class SharedConfig
    {
        private const int MaxStations = 16;

        public const string RulesDoc = "rules.json";
        public const string StationsDoc = "stations.json";
        public const string RecipesDoc = "recipes.json";
        public const string CustomersDoc = "customers.json";

        public int TicksPerSecond;
        public int MaxCatchUpTicks;
        public int ContinueBonusSec;
        public int ActionQueueMax;
        public int BoardClearPenaltyCoins;
        public int ComboWindowMs;
        public int ComboStepPercent;
        public int ComboMaxSteps;
        public int WrongDishPatienceMs;
        public bool ExtraOptionalIsWrong;

        public readonly List<StationConfig> BoardStations = new List<StationConfig>();
        public int GrillUnlockLevel;
        public int GrillActionMs;
        public int CounterUnlockLevel;
        public GrillValues GrillDefaults = new GrillValues();

        public readonly List<Ingredient> Ingredients = new List<Ingredient>();
        public readonly List<Recipe> Recipes = new List<Recipe>();

        public readonly List<CustomerType> Customers = new List<CustomerType>();
        public readonly List<int> CustomerFirstLevels = new List<int>();

        /// <summary>Розбирає чотири спільні документи; помилки — у errors.</summary>
        public static SharedConfig Parse(ConfigSources sources, List<string> errors)
        {
            var shared = new SharedConfig();
            shared.ParseRules(ConfigLoader.OpenRoot(sources.Rules, RulesDoc, errors));
            shared.ParseStations(ConfigLoader.OpenRoot(sources.Stations, StationsDoc, errors));
            shared.ParseRecipes(ConfigLoader.OpenRoot(sources.Recipes, RecipesDoc, errors));
            shared.ParseCustomers(ConfigLoader.OpenRoot(sources.Customers, CustomersDoc, errors));
            return shared;
        }

        /// <summary>Номер customers[id] за назвою; -1 — немає.</summary>
        public int IndexOfCustomer(string id)
        {
            for (int i = 0; i < Customers.Count; i++)
            {
                if (string.Equals(Customers[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool HasIngredient(string id)
        {
            for (int i = 0; i < Ingredients.Count; i++)
            {
                if (string.Equals(Ingredients[i].Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void ParseRules(ConfigObject root)
        {
            if (root == null)
            {
                return;
            }

            root.OptBool("draft", false);
            TicksPerSecond = root.Int("ticksPerSecond", ShiftConfig.MinTicksPerSecond, ShiftConfig.MaxTicksPerSecond);
            MaxCatchUpTicks = root.Int("maxCatchUpTicks", 1, ShiftConfig.MaxCatchUpTicksLimit);
            ContinueBonusSec = root.Int("continueBonusSec", 0, ShiftConfig.MaxContinueBonusSec);
            ActionQueueMax = root.Int("actionQueueMax", 1, ShiftConfig.MaxActionQueue);
            BoardClearPenaltyCoins = root.Int("boardClearPenaltyCoins", 0, BoardConfig.MaxCoins);
            ComboWindowMs = root.Millis("comboWindowSec", 0, CounterConfig.MaxComboWindowMs);
            ComboStepPercent = root.Int("comboStepPercent", 0, CounterConfig.MaxPercent);
            ComboMaxSteps = root.Int("comboMaxSteps", 0, CounterConfig.MaxComboSteps);
            WrongDishPatienceMs = root.Millis("wrongDishPatienceSec", 0, CounterConfig.MaxPatienceMs);
            ExtraOptionalIsWrong = root.Bool("extraOptionalIsWrong");
            root.Done();
        }

        private void ParseStations(ConfigObject root)
        {
            if (root == null)
            {
                return;
            }

            root.OptBool("draft", false);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ConfigObject node in root.ObjList("stations", 1, MaxStations))
            {
                string id = node.Str("id");
                int unlockLevel = node.Int("unlockLevel", 1, BoardConfig.MaxLevel);
                if (id.Length > 0 && !seen.Add(id))
                {
                    node.Report("id", "=" + id + ": станція повторюється");
                }

                switch (id)
                {
                    case "grill":
                        GrillUnlockLevel = unlockLevel;
                        GrillActionMs = node.Millis("actionTimeSec", 0, GrillConfig.MaxActionMs);
                        GrillDefaults = GrillValues.Read(node.OptObj("grill"));
                        break;
                    case "counter":
                        CounterUnlockLevel = unlockLevel;
                        break;
                    case "bread":
                        AddBoardStation(node, StationKind.Bread, unlockLevel);
                        break;
                    case "sauce":
                        AddBoardStation(node, StationKind.Sauce, unlockLevel);
                        break;
                    case "salad":
                        AddBoardStation(node, StationKind.Salad, unlockLevel);
                        break;
                    case "board":
                        AddBoardStation(node, StationKind.Board, unlockLevel);
                        break;
                    case "bin":
                        AddBoardStation(node, StationKind.Bin, unlockLevel);
                        break;
                    case "":
                        break;
                    default:
                        node.Report("id", "=" + id + ": невідома станція (дозволено grill, bread, sauce, salad, counter, board, bin)");
                        break;
                }

                node.Done();
            }

            foreach (string required in new[] { "grill", "counter", "board" })
            {
                if (!seen.Contains(required))
                {
                    root.Report("stations", ": немає станції " + required);
                }
            }

            root.Done();
        }

        private void AddBoardStation(ConfigObject node, StationKind kind, int unlockLevel)
        {
            int actionMs = node.Millis("actionTimeSec", 0, BoardConfig.MaxActionMs);
            BoardStations.Add(new StationConfig(kind, unlockLevel, actionMs));
        }

        private void ParseRecipes(ConfigObject root)
        {
            if (root == null)
            {
                return;
            }

            root.OptBool("draft", false);
            foreach (ConfigObject node in root.ObjList("ingredients", 1, BoardConfig.MaxIngredients))
            {
                string id = node.Str("id");
                node.Key("nameKey");
                string station = node.Str("station");
                int extra = node.Int("extraPriceCoins", 0, BoardConfig.MaxCoins);
                StationKind kind = StationKind.None;
                switch (station)
                {
                    case "grill": kind = StationKind.Grill; break;
                    case "bread": kind = StationKind.Bread; break;
                    case "sauce": kind = StationKind.Sauce; break;
                    case "salad": kind = StationKind.Salad; break;
                    case "":
                        break;
                    default:
                        node.Report("station", "=" + station + ": невідома станція інгредієнта (дозволено grill, bread, sauce, salad)");
                        break;
                }

                Ingredients.Add(new Ingredient(id, kind, extra));
                node.Done();
            }

            foreach (ConfigObject node in root.ObjList("recipes", 1, BoardConfig.MaxRecipes))
            {
                string id = node.Str("id");
                node.Key("nameKey");
                List<string> steps = node.StrList("steps", true, BoardConfig.MaxRecipeLength, false);
                List<string> optional = node.StrList("optional", false, BoardConfig.MaxRecipeLength, false);
                int price = node.Int("price", 1, BoardConfig.MaxCoins);
                int unlockLevel = node.Int("unlockLevel", 1, BoardConfig.MaxLevel);
                if (steps.Count == 0 && node.Has("steps"))
                {
                    node.Report("steps", ": потрібен хоча б один крок");
                }

                CheckIngredients(node, "steps", steps);
                CheckIngredients(node, "optional", optional);
                Recipes.Add(new Recipe(id, steps, optional, price, unlockLevel));
                node.Done();
            }

            root.Done();
        }

        private void CheckIngredients(ConfigObject node, string key, List<string> ids)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (!HasIngredient(ids[i]))
                {
                    node.Report(
                        key + "[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        "=" + ids[i] + ": невідомий інгредієнт (його немає в ingredients)");
                }
            }
        }

        private void ParseCustomers(ConfigObject root)
        {
            if (root == null)
            {
                return;
            }

            root.OptBool("draft", false);
            foreach (ConfigObject node in root.ObjList("customers", 1, CounterConfig.MaxTypes))
            {
                string id = node.Str("id");
                node.Key("nameKey");
                int patienceMs = node.Millis("patienceSec", 1, CounterConfig.MaxPatienceMs);
                int payPercent = node.Percent("payMultiplier", 1, CounterConfig.MaxPercent);
                ConfigObject tip = node.Obj("tipRule");
                int maxPercent = tip.Int("maxPercent", 0, CounterConfig.MaxPercent);
                int minPatience = tip.Int("minPatiencePercent", 0, CoinMath.PercentBase);
                tip.Done();
                List<string> always = node.StrList("always", false, BoardConfig.MaxRecipeLength, false);
                List<string> exclude = node.StrList("exclude", false, BoardConfig.MaxRecipeLength, false);
                int firstLevel = node.Int("firstLevel", 1, BoardConfig.MaxLevel);
                CheckIngredients(node, "always", always);
                CheckIngredients(node, "exclude", exclude);
                Customers.Add(new CustomerType(
                    id, patienceMs, payPercent, new TipRule(maxPercent, minPatience), always, exclude));
                CustomerFirstLevels.Add(firstLevel);
                node.Done();
            }

            root.Done();
        }
    }
}
