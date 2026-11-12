using System;
using System.Collections.Generic;
using System.Globalization;

namespace KebabStreet.Core
{
    /// <summary>
    /// Завантажувач конфігів рівнів (S1-04): розбирає JSON із рядків, суворо перевіряє схему і збирає
    /// <see cref="ShiftConfig"/> для кожного рівня. Без UnityEngine і без читання файлів: тексти
    /// приходять у <see cref="ConfigSources"/>. Помилки не кидаються, а збираються в список із
    /// шляхами до ключів («levels/L03.json: grill.cookSec=0: дозволено …»).
    /// </summary>
    public static class ConfigLoader
    {
        private const int MaxLocation = 10;
        private const int MaxGraceMs = CounterConfig.MaxPatienceMs;

        /// <summary>Завантажує всі рівні з джерел; порядок у каталозі — за номером.</summary>
        public static ConfigResult<LevelCatalog> LoadAll(ConfigSources sources)
        {
            var errors = new List<string>();
            if (sources == null)
            {
                errors.Add("конфіги: джерела не задано");
                return new ConfigResult<LevelCatalog>(null, errors);
            }

            SharedConfig shared = SharedConfig.Parse(sources, errors);
            bool sharedOk = errors.Count == 0;
            var levels = new List<LevelDefinition>();
            var failed = false;
            for (int i = 0; i < sources.LevelCount; i++)
            {
                LevelDefinition level = ParseLevel(
                    shared, sharedOk, sources.GetLevelName(i), sources.GetLevelText(i), errors);
                if (level == null)
                {
                    failed = true;
                }
                else
                {
                    levels.Add(level);
                }
            }

            if (sources.LevelCount == 0)
            {
                errors.Add("levels: немає жодного рівня");
            }

            SortByNumber(levels);
            for (int i = 1; i < levels.Count; i++)
            {
                if (levels[i].Number == levels[i - 1].Number)
                {
                    errors.Add("levels: номер рівня " + levels[i].Number.ToString(CultureInfo.InvariantCulture)
                        + " повторюється (" + levels[i - 1].Id + ", " + levels[i].Id + ")");
                }
            }

            return new ConfigResult<LevelCatalog>(failed ? null : new LevelCatalog(levels), errors);
        }

        /// <summary>Завантажує один рівень за назвою файла («L03»); спільні документи розбираються й перевіряються теж.</summary>
        public static ConfigResult<LevelDefinition> LoadLevel(ConfigSources sources, string levelName)
        {
            var errors = new List<string>();
            if (sources == null)
            {
                errors.Add("конфіги: джерела не задано");
                return new ConfigResult<LevelDefinition>(null, errors);
            }

            SharedConfig shared = SharedConfig.Parse(sources, errors);
            bool sharedOk = errors.Count == 0;
            LevelDefinition level = ParseLevel(
                shared, sharedOk, levelName, sources.FindLevelText(levelName), errors);
            return new ConfigResult<LevelDefinition>(level, errors);
        }

        /// <summary>Розбирає текст JSON у корінь документа; null — помилка вже записана.</summary>
        internal static ConfigObject OpenRoot(string text, string doc, List<string> errors)
        {
            if (text == null)
            {
                errors.Add(doc + ": файл не знайдено");
                return null;
            }

            object parsed;
            try
            {
                parsed = SaveJson.Parse(text.TrimStart('﻿'));
            }
            catch (SaveFormatException e)
            {
                errors.Add(doc + ": некоректний JSON: " + e.Message);
                return null;
            }

            return ConfigObject.Root(parsed, doc, errors);
        }

        private static LevelDefinition ParseLevel(
            SharedConfig shared, bool sharedOk, string name, string text, List<string> errors)
        {
            string doc = "levels/" + name + ".json";
            ConfigObject root = OpenRoot(text, doc, errors);
            if (root == null)
            {
                return null;
            }

            int errorsBefore = errors.Count;
            string id = root.Str("id");
            int number = NumberOf(name);
            if (id.Length > 0 && (number < 1 || !string.Equals(id, name, StringComparison.Ordinal)))
            {
                root.Report("id", "=" + id + ": має збігатися з назвою файла (" + name + ") у форматі L01");
            }

            int location = root.Int("location", 1, MaxLocation);
            string nameKey = root.Key("nameKey");
            bool draft = root.OptBool("draft", false);
            int durationSec = root.Int("durationSec", 1, ShiftConfig.MaxDurationSec);
            int counterSlots = root.Int("counterSlots", 1, CounterConfig.MaxSlots);
            LevelGoal goal = ParseGoal(root.Obj("goal"));
            LevelStars stars = ParseStars(root.Obj("stars"));

            ConfigObject loseNode = root.Obj("lose");
            int maxLost;
            bool hasMaxLost = loseNode.TryNullableInt("maxLostCustomers", 0, ArrivalSchedule.MaxArrivals, out maxLost);
            bool countLost = loseNode.Bool("countLostCustomers");
            loseNode.Done();
            if (!countLost && hasMaxLost)
            {
                root.Report("lose.maxLostCustomers", ": ліміт втрат без countLostCustomers=true не має сенсу");
            }

            if (!countLost && stars.ThreeCondition == StarCondition.NoLostCustomers)
            {
                root.Report("stars.three.condition", "=noLostCustomers: потрібен lose.countLostCustomers=true");
            }

            GrillValues levelGrill = GrillValues.Read(root.OptObj("grill"));
            var arrivals = ParseWaves(root, shared, number, doc, errors);
            if (goal.Type == GoalType.Serve && goal.Target > arrivals.Count)
            {
                root.Report("goal.count", "=" + goal.Target.ToString(CultureInfo.InvariantCulture)
                    + ": більше за кількість приходів у waves (" + arrivals.Count.ToString(CultureInfo.InvariantCulture)
                    + "), ціль недосяжна");
            }

            List<string> tutorialKeys = root.StrList("tutorialKeys", false, 32, true);
            ConfigObject story = root.Obj("storyKeys");
            string intro = story.Key("intro");
            string outro = story.Key("outro");
            story.Done();
            root.Done();

            if (number >= 1 && shared.GrillUnlockLevel > number)
            {
                root.Report("grill", ": станція grill доступна з рівня "
                    + shared.GrillUnlockLevel.ToString(CultureInfo.InvariantCulture) + " (stations.json), а це рівень "
                    + number.ToString(CultureInfo.InvariantCulture));
            }

            if (number >= 1 && shared.CounterUnlockLevel > number)
            {
                root.Report("counterSlots", ": станція counter доступна з рівня "
                    + shared.CounterUnlockLevel.ToString(CultureInfo.InvariantCulture) + " (stations.json), а це рівень "
                    + number.ToString(CultureInfo.InvariantCulture));
            }

            GrillConfig grill = MergeGrill(shared, levelGrill, root);
            if (!sharedOk || errors.Count != errorsBefore || grill == null)
            {
                return null;
            }

            var board = new BoardConfig(
                number,
                shared.BoardClearPenaltyCoins,
                shared.BoardStations,
                shared.Ingredients,
                shared.Recipes);
            var counter = new CounterConfig(
                counterSlots,
                shared.ComboWindowMs,
                shared.ComboStepPercent,
                shared.ComboMaxSteps,
                shared.WrongDishPatienceMs,
                countLost,
                shared.ExtraOptionalIsWrong,
                shared.Customers,
                new ArrivalSchedule(arrivals));
            var shift = new ShiftConfig(
                shared.TicksPerSecond,
                shared.MaxCatchUpTicks,
                durationSec,
                shared.ContinueBonusSec,
                shared.ActionQueueMax,
                grill,
                board,
                counter);

            IReadOnlyList<string> shiftErrors = shift.GetErrors();
            for (int i = 0; i < shiftErrors.Count; i++)
            {
                errors.Add(doc + ": " + shiftErrors[i]);
            }

            if (shiftErrors.Count > 0)
            {
                return null;
            }

            return new LevelDefinition(
                id,
                number,
                location,
                nameKey,
                draft,
                shift,
                goal,
                stars,
                new LevelLose(hasMaxLost ? (int?)maxLost : null),
                tutorialKeys,
                intro,
                outro);
        }

        private static GrillConfig MergeGrill(SharedConfig shared, GrillValues level, ConfigObject root)
        {
            GrillValues d = shared.GrillDefaults;
            int? slots = level.Slots ?? d.Slots;
            int? cook = level.CookMs ?? d.CookMs;
            int? burn = level.BurnAfterDoneMs ?? d.BurnAfterDoneMs;
            int? warn = level.WarnBeforeBurnMs ?? d.WarnBeforeBurnMs;
            int? penalty = level.BurntPenaltyCoins ?? d.BurntPenaltyCoins;
            bool ok = true;
            ok &= Require(root, "slots", slots);
            ok &= Require(root, "cookSec", cook);
            ok &= Require(root, "burnAfterDoneSec", burn);
            ok &= Require(root, "warnBeforeBurnSec", warn);
            ok &= Require(root, "burntPenaltyCoins", penalty);
            if (!ok)
            {
                return null;
            }

            return new GrillConfig(slots.Value, cook.Value, burn.Value, warn.Value, penalty.Value, shared.GrillActionMs);
        }

        private static bool Require(ConfigObject root, string key, int? value)
        {
            if (value.HasValue)
            {
                return true;
            }

            root.Report("grill." + key, ": не задано ні в рівні, ні в stations.json (grill)");
            return false;
        }

        private static LevelGoal ParseGoal(ConfigObject node)
        {
            string type = node.Str("type");
            GoalType goalType = GoalType.Serve;
            int target = 0;
            switch (type)
            {
                case "serve":
                    target = node.Int("count", 1, ArrivalSchedule.MaxArrivals);
                    break;
                case "coins":
                    goalType = GoalType.Coins;
                    target = node.Int("coins", 1, BoardConfig.MaxCoins);
                    break;
                case "":
                    break;
                default:
                    node.Report("type", "=" + type + ": дозволено serve, coins");
                    break;
            }

            bool endOnGoal = node.OptBool("endOnGoal", goalType == GoalType.Serve);
            node.Done();
            return new LevelGoal(goalType, target, endOnGoal);
        }

        private static LevelStars ParseStars(ConfigObject node)
        {
            ConfigObject two = node.Obj("two");
            string twoType = two.Str("type");
            if (twoType.Length > 0 && twoType != "coins")
            {
                two.Report("type", "=" + twoType + ": дозволено coins");
            }

            int twoCoins = two.Int("value", 1, BoardConfig.MaxCoins);
            two.Done();

            ConfigObject three = node.Obj("three");
            string condition = three.Str("condition");
            StarCondition parsed = StarCondition.None;
            int comboValue = 0;
            switch (condition)
            {
                case "noLostCustomers":
                    parsed = StarCondition.NoLostCustomers;
                    break;
                case "noBurnt":
                    parsed = StarCondition.NoBurnt;
                    break;
                case "noWrongDishes":
                    parsed = StarCondition.NoWrongDishes;
                    break;
                case "combo":
                    parsed = StarCondition.Combo;
                    comboValue = three.Int("value", 2, ArrivalSchedule.MaxArrivals);
                    break;
                case "":
                    break;
                default:
                    three.Report("condition", "=" + condition + ": дозволено noLostCustomers, noBurnt, noWrongDishes, combo");
                    break;
            }

            three.Done();
            node.Done();
            return new LevelStars(twoCoins, parsed, comboValue);
        }

        private static List<Arrival> ParseWaves(
            ConfigObject root, SharedConfig shared, int number, string doc, List<string> errors)
        {
            var arrivals = new List<Arrival>();
            foreach (ConfigObject node in root.ObjList("waves", 1, ArrivalSchedule.MaxArrivals))
            {
                int atMs = node.Millis("atSec", 0, ArrivalSchedule.MaxAtMs);
                int jitterMs;
                node.TryMillis("jitterSec", 0, ArrivalSchedule.MaxJitterMs, out jitterMs);
                int graceMs;
                node.TryMillis("graceSec", 0, MaxGraceMs, out graceMs);
                string customer = node.Str("customer");
                ConfigObject orderNode = node.Obj("order");
                string recipe = orderNode.Str("recipe");
                List<string> optional = orderNode.StrList("optional", false, BoardConfig.MaxRecipeLength, false);
                List<string> exclude = orderNode.StrList("exclude", false, BoardConfig.MaxRecipeLength, false);
                orderNode.Done();
                node.Done();

                int typeIndex = shared.IndexOfCustomer(customer);
                if (typeIndex >= 0 && number >= 1 && shared.CustomerFirstLevels[typeIndex] > number)
                {
                    node.Report("customer", "=" + customer + ": тип з'являється з рівня "
                        + shared.CustomerFirstLevels[typeIndex].ToString(CultureInfo.InvariantCulture)
                        + " (customers.json, firstLevel), а це рівень " + number.ToString(CultureInfo.InvariantCulture));
                }

                arrivals.Add(new Arrival(atMs, customer, new Order(recipe, optional, exclude), jitterMs, graceMs));
            }

            return arrivals;
        }

        /// <summary>Номер рівня з назви «L01»; 0 — формат хибний.</summary>
        private static int NumberOf(string name)
        {
            if (name == null || name.Length < 3 || name.Length > 4 || name[0] != 'L')
            {
                return 0;
            }

            int value = 0;
            for (int i = 1; i < name.Length; i++)
            {
                if (name[i] < '0' || name[i] > '9')
                {
                    return 0;
                }

                value = value * 10 + (name[i] - '0');
            }

            return value;
        }

        private static void SortByNumber(List<LevelDefinition> levels)
        {
            for (int i = 1; i < levels.Count; i++)
            {
                LevelDefinition item = levels[i];
                int place = i;
                while (place > 0 && levels[place - 1].Number > item.Number)
                {
                    levels[place] = levels[place - 1];
                    place--;
                }

                levels[place] = item;
            }
        }
    }
}
