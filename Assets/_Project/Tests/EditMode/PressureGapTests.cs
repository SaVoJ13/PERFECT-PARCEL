using System.Collections.Generic;
using System.Globalization;
using System.Text;
using KebabStreet.Core;
using NUnit.Framework;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Метрика Q-07 (ADR-033, П1 крок 1): розрив між «жадібним» ботом (найменше терпіння першим,
    /// <see cref="BotProfile.Expert"/>) і «розумним» (<see cref="BotProfile.SmartExpert"/>) на рівнях 3–5, а також
    /// зірки новачка. Кандидати розкладу хвиль підставляються в пам'ять (конфіги на диску не чіпаються);
    /// результат друкується рядками з міткою PRESSURE. Тест лише вимірює і перевіряє детермінізм: пороги
    /// розриву він не вимагає (спершу треба побачити числа).
    /// </summary>
    public sealed class PressureGapTests
    {
        private const int SeedCount = 20;
        private const ulong FirstSeed = 20261201UL;
        private const ulong SeedStep = 7919UL;

        private static readonly string[] Salad = { "salad" };
        private static readonly string[] SaladOnion = { "salad", "onion" };
        private static readonly string[] Garlic = { "sauce_garlic" };
        private static readonly string[] Full = { "salad", "onion", "sauce_garlic" };

        private static ulong SeedAt(int index)
        {
            return FirstSeed + (ulong)index * SeedStep;
        }

        // Кандидати параметрів клієнтів (Q-08, ADR-034): marko = нетерплячий і дешевий, hanna = терплячий і дорогий.
        // Поля: ім'я, marko(терпіння с, оплата, чайові макс %, чайові мін %), hanna(те саме).
        private static readonly double[][] CustomerSets =
        {
            null, // base: customers.json з диска
            new double[] { 20, 0.70, 0, 0, 60, 1.60, 15, 0 },   // C1
            new double[] { 15, 0.60, 0, 0, 70, 1.80, 20, 0 },   // C2
            new double[] { 18, 0.80, 10, 50, 55, 1.50, 10, 0 }, // C3
            new double[] { 20, 0.80, 0, 0, 50, 1.40, 10, 0 },   // C4 (м'якший)
        };

        private static readonly string[] CustomerSetNames = { "cust-base", "C1", "C2", "C3", "C4" };

        [Test]
        public void Report_GreedyVsSmartGap_OnLevels3To5()
        {
            for (int number = 3; number <= 5; number++)
            {
                List<KeyValuePair<string, string>> variants = Variants(number);
                for (int cs = 0; cs < CustomerSets.Length; cs++)
                for (int v = 0; v < variants.Count; v++)
                {
                    if (variants[v].Key != "base" && variants[v].Key != "P-peaks")
                    {
                        continue;
                    }

                    string label = "L0" + number + " " + CustomerSetNames[cs] + " " + variants[v].Key;
                    string error;
                    LevelDefinition level = LoadLevel(number, variants[v].Value, CustomerSets[cs], out error);
                    if (level == null)
                    {
                        Report("PRESSURE " + label + ": НЕВАЛІДНИЙ розклад: " + error);
                        continue;
                    }

                    List<BotResult> greedy = RunAll(level, BotProfile.Expert);
                    List<BotResult> smart = RunAll(level, BotProfile.SmartExpert);
                    List<BotResult> novice = RunAll(level, BotProfile.Novice);
                    List<BotResult> smartNovice = RunAll(level, BotProfile.SmartNovice);
                    Report(Stat(label + " greedy-expert", greedy));
                    Report(Stat(label + " smart-expert", smart));
                    Report(Stat(label + " greedy-novice", novice));
                    Report(Stat(label + " smart-novice", smartNovice));
                    Report("PRESSURE " + label + " GAP expert: coins " + F(Mean(smart, CoinsOf) - Mean(greedy, CoinsOf))
                        + ", stars " + F2(Mean(smart, StarsOf) - Mean(greedy, StarsOf))
                        + "; novice: coins " + F(Mean(smartNovice, CoinsOf) - Mean(novice, CoinsOf))
                        + ", stars " + F2(Mean(smartNovice, StarsOf) - Mean(novice, StarsOf)));
                }
            }

            Assert.Pass();
        }

        [Test]
        public void SmartBot_SameSeed_GivesIdenticalResult()
        {
            LevelDefinition level = ConfigTestKit.Level(4);
            BotResult first = BotRunner.Run(level, SeedAt(3), BotProfile.SmartExpert);
            BotResult second = BotRunner.Run(level, SeedAt(3), BotProfile.SmartExpert);
            Assert.AreEqual(first.Fingerprint(), second.Fingerprint());
            Assert.IsTrue(first.FinalSnapshot.Equals(second.FinalSnapshot));
        }

        private static List<BotResult> RunAll(LevelDefinition level, BotProfile profile)
        {
            var results = new List<BotResult>(SeedCount);
            for (int i = 0; i < SeedCount; i++)
            {
                results.Add(BotRunner.Run(level, SeedAt(i), profile));
            }

            return results;
        }

        private static void Report(string text)
        {
            TestContext.Out.WriteLine(text);
            UnityEngine.Debug.Log(text);
        }

        private static LevelDefinition LoadLevel(int number, string wavesJson, double[] customerSet, out string error)
        {
            error = null;
            if (wavesJson == null && customerSet == null)
            {
                return ConfigTestKit.Level(number);
            }

            ConfigSources sources = ConfigTestKit.Real();
            if (customerSet != null)
            {
                sources = ConfigTestKit.Mutated(
                    ConfigTestKit.Customers,
                    tree =>
                    {
                        List<object> list = ConfigTestKit.Arr(tree, "customers");
                        Apply(ConfigTestKit.ById(list, "marko"), customerSet, 0);
                        Apply(ConfigTestKit.ById(list, "hanna"), customerSet, 4);
                    });
            }

            if (wavesJson != null)
            {
                string levelText = ConfigTestKit.TextOf(sources, "L0" + number);
                var levelTree = (Dictionary<string, object>)SaveJson.Parse(levelText);
                levelTree["waves"] = SaveJson.Parse(wavesJson);
                sources = ConfigTestKit.Replace(sources, "L0" + number, ConfigTestKit.Write(levelTree));
            }
            ConfigResult<LevelCatalog> loaded = ConfigLoader.LoadAll(sources);
            if (!loaded.IsValid)
            {
                error = ConfigTestKit.Join(loaded.Errors);
                return null;
            }

            return loaded.Value.FindByNumber(number);
        }

        private static void Apply(Dictionary<string, object> customer, double[] set, int offset)
        {
            customer["patienceSec"] = (long)set[offset];
            customer["payMultiplier"] = set[offset + 1];
            var tip = (Dictionary<string, object>)customer["tipRule"];
            tip["maxPercent"] = (long)set[offset + 2];
            tip["minPatiencePercent"] = (long)set[offset + 3];
        }

        private delegate double Metric(BotResult r);

        private static double CoinsOf(BotResult r)
        {
            return r.Coins;
        }

        private static double StarsOf(BotResult r)
        {
            return r.Won ? r.Result.Stars : 0;
        }

        private static double Mean(List<BotResult> results, Metric metric)
        {
            double sum = 0d;
            for (int i = 0; i < results.Count; i++)
            {
                sum += metric(results[i]);
            }

            return sum / results.Count;
        }

        private static string Stat(string label, List<BotResult> results)
        {
            int wins = 0;
            var dist = new int[4];
            double lost = 0d;
            double burnt = 0d;
            double combo = 0d;
            double served = 0d;
            for (int i = 0; i < results.Count; i++)
            {
                BotResult r = results[i];
                wins += r.Won ? 1 : 0;
                dist[r.Won ? r.Result.Stars : 0]++;
                lost += r.Lost;
                burnt += r.Burnt;
                combo += r.MaxCombo;
                served += r.Served;
            }

            int n = results.Count;
            var b = new StringBuilder();
            b.Append("PRESSURE ").Append(label).Append(": win=").Append(wins).Append('/').Append(n)
                .Append(" coins=").Append(F(Mean(results, CoinsOf)))
                .Append(" stars=").Append(F2(Mean(results, StarsOf)))
                .Append(" dist0/1/2/3=").Append(dist[0]).Append('/').Append(dist[1]).Append('/').Append(dist[2]).Append('/').Append(dist[3])
                .Append(" served=").Append(F(served / n))
                .Append(" lost=").Append(F(lost / n))
                .Append(" burnt=").Append(F(burnt / n))
                .Append(" combo=").Append(F(combo / n));
            return b.ToString();
        }

        private static string F(double v)
        {
            return v.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static string F2(double v)
        {
            return v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        // ---- Кандидати розкладу хвиль (лише waves; goal, stars, lose беруться з диска) ----

        private static string W(int at, string customer, string[] optional = null, int grace = 0, int jitter = 0)
        {
            var b = new StringBuilder();
            b.Append("{\"atSec\": ").Append(at).Append(", \"customer\": \"").Append(customer).Append('"');
            if (grace > 0)
            {
                b.Append(", \"graceSec\": ").Append(grace);
            }

            if (jitter > 0)
            {
                b.Append(", \"jitterSec\": ").Append(jitter);
            }

            b.Append(", \"order\": {\"recipe\": \"kebab_pita\"");
            if (optional != null && optional.Length > 0)
            {
                b.Append(", \"optional\": [");
                for (int i = 0; i < optional.Length; i++)
                {
                    b.Append(i > 0 ? ", " : string.Empty).Append('"').Append(optional[i]).Append('"');
                }

                b.Append(']');
            }

            b.Append("}}");
            return b.ToString();
        }

        private static string Arr(params string[] waves)
        {
            return "[" + string.Join(",\n", waves) + "]";
        }

        private static List<KeyValuePair<string, string>> Variants(int number)
        {
            var list = new List<KeyValuePair<string, string>>();
            list.Add(new KeyValuePair<string, string>("base", null));
            switch (number)
            {
                case 3:
                    list.Add(new KeyValuePair<string, string>("P-peaks", Arr(
                        W(4, "olya"), W(4, "olya", Garlic),
                        W(22, "marko", null, 20), W(22, "olya"), W(22, "olya", Garlic),
                        W(44, "olya"), W(44, "marko", null, 20), W(44, "olya", Garlic),
                        W(66, "olya"), W(66, "olya"))));
                    list.Add(new KeyValuePair<string, string>("Q-marko", Arr(
                        W(4, "olya"),
                        W(14, "marko", null, 10), W(14, "olya", Garlic),
                        W(30, "marko", null, 10), W(30, "marko", null, 10),
                        W(48, "olya", Garlic), W(48, "marko", null, 10), W(48, "olya"),
                        W(66, "olya"), W(66, "olya"))));
                    break;
                case 4:
                    list.Add(new KeyValuePair<string, string>("P-peaks", Arr(
                        W(4, "olya", SaladOnion), W(4, "hanna", Salad), W(4, "marko"), W(4, "olya", Garlic),
                        W(40, "hanna", Salad), W(40, "olya", SaladOnion), W(40, "marko"), W(40, "olya"),
                        W(80, "hanna", Salad), W(80, "olya", SaladOnion), W(80, "marko"), W(80, "hanna", Salad),
                        W(120, "olya"), W(120, "hanna", Salad))));
                    list.Add(new KeyValuePair<string, string>("Q-heavy", Arr(
                        W(4, "olya", Full), W(4, "hanna", Salad), W(4, "marko", Garlic),
                        W(30, "hanna", Salad), W(30, "olya", Full), W(30, "marko", Salad),
                        W(56, "olya", SaladOnion), W(56, "marko", Garlic), W(56, "hanna", Salad),
                        W(82, "olya", Full), W(82, "hanna", Salad), W(82, "marko", Salad),
                        W(108, "olya", Garlic), W(108, "marko"))));
                    break;
                default:
                    list.Add(new KeyValuePair<string, string>("P-peaks", Arr(
                        W(2, "olya"), W(2, "marko"), W(2, "hanna", Salad), W(2, "olya", SaladOnion), W(2, "olya", Garlic),
                        W(36, "marko", Salad, 0, 2), W(36, "hanna", Salad, 0, 2), W(36, "olya", null, 0, 2),
                        W(36, "olya", SaladOnion, 0, 2), W(36, "marko", null, 0, 2),
                        W(72, "olya", null, 0, 2), W(72, "marko", null, 0, 2), W(72, "hanna", Salad, 0, 2),
                        W(72, "olya", Salad, 0, 2), W(72, "olya", Garlic, 0, 2),
                        W(108, "marko", Salad), W(108, "hanna", Salad), W(108, "olya", SaladOnion),
                        W(134, "olya"), W(134, "hanna", Salad))));
                    list.Add(new KeyValuePair<string, string>("Q-heavy", Arr(
                        W(3, "olya", Full), W(3, "marko", Salad), W(3, "hanna", Salad), W(3, "marko", Garlic), W(3, "olya", SaladOnion),
                        W(30, "marko", Salad), W(30, "olya", Full), W(30, "hanna", Salad), W(30, "marko"), W(30, "olya", Garlic),
                        W(58, "olya", SaladOnion), W(58, "marko", Garlic), W(58, "hanna", Salad), W(58, "marko", Salad), W(58, "olya", Full),
                        W(88, "hanna", Salad), W(88, "olya", Full), W(88, "marko"), W(88, "olya", Garlic), W(88, "marko", Salad))));
                    break;
            }

            return list;
        }
    }
}
