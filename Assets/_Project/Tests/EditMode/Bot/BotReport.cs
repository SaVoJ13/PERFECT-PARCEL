using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Зведені метрики бота по набору seed для балансу (S1-05): частка перемог, монети, лишок часу,
    /// втрати, пригоріле, комбо. Текст друкується в лог тестового запуску; у репозиторій нічого не пишеться.
    /// </summary>
    internal static class BotReport
    {
        public const string Marker = "BOTREPORT";

        public static string Summarize(string title, IReadOnlyList<BotResult> results)
        {
            int n = results.Count;
            int wins = 0;
            double goalSum = 0d;
            double leftoverSum = 0d;
            double leftoverMin = double.MaxValue;
            long coinsSum = 0;
            long coinsMin = long.MaxValue;
            long coinsMax = long.MinValue;
            long servedSum = 0;
            long servedMin = long.MaxValue;
            long lostSum = 0;
            long lostMax = 0;
            long leftSum = 0;
            long wrongSum = 0;
            long burntSum = 0;
            long burntMax = 0;
            long comboSum = 0;
            long comboMax = 0;
            long mistakesSum = 0;
            int stars1 = 0;
            int stars2 = 0;
            int stars3 = 0;
            for (int i = 0; i < n; i++)
            {
                BotResult r = results[i];
                if (r.Won)
                {
                    wins++;
                    goalSum += r.GoalSeconds;
                    leftoverSum += r.LeftoverSeconds;
                    if (r.LeftoverSeconds < leftoverMin)
                    {
                        leftoverMin = r.LeftoverSeconds;
                    }
                }

                coinsSum += r.Coins;
                coinsMin = r.Coins < coinsMin ? r.Coins : coinsMin;
                coinsMax = r.Coins > coinsMax ? r.Coins : coinsMax;
                servedSum += r.Served;
                servedMin = r.Served < servedMin ? r.Served : servedMin;
                lostSum += r.Lost;
                lostMax = r.Lost > lostMax ? r.Lost : lostMax;
                leftSum += r.Left;
                wrongSum += r.WrongDishes;
                burntSum += r.Burnt;
                burntMax = r.Burnt > burntMax ? r.Burnt : burntMax;
                comboSum += r.MaxCombo;
                comboMax = r.MaxCombo > comboMax ? r.MaxCombo : comboMax;
                mistakesSum += r.Mistakes;
                stars1 += r.Result.Stars == 1 ? 1 : 0;
                stars2 += r.Result.Stars == 2 ? 1 : 0;
                stars3 += r.Result.Stars == 3 ? 1 : 0;
            }

            var b = new StringBuilder();
            b.Append(Marker).Append(' ').Append(title).Append(": ");
            b.Append("перемог ").Append(wins).Append('/').Append(n)
                .Append(" (").Append(F(100d * wins / n)).Append("%)");
            if (wins > 0)
            {
                b.Append("; ціль на ").Append(F(goalSum / wins)).Append(" с")
                    .Append(", лишок часу сер. ").Append(F(leftoverSum / wins))
                    .Append(" с (мін. ").Append(F(leftoverMin)).Append(')');
            }

            b.Append("; монети сер. ").Append(F((double)coinsSum / n))
                .Append(" (").Append(coinsMin).Append('-').Append(coinsMax).Append(')');
            b.Append("; обслужено сер. ").Append(F((double)servedSum / n)).Append(" (мін. ").Append(servedMin).Append(')');
            b.Append("; втрачено сер. ").Append(F((double)lostSum / n)).Append(" (макс. ").Append(lostMax).Append(')');
            b.Append(", пішло сер. ").Append(F((double)leftSum / n));
            b.Append("; хибних страв сер. ").Append(F((double)wrongSum / n));
            b.Append("; пригоріло сер. ").Append(F((double)burntSum / n)).Append(" (макс. ").Append(burntMax).Append(')');
            b.Append("; комбо сер. ").Append(F((double)comboSum / n)).Append(" (макс. ").Append(comboMax).Append(')');
            b.Append("; зірки 1/2/3: ").Append(stars1).Append('/').Append(stars2).Append('/').Append(stars3);
            if (mistakesSum > 0)
            {
                b.Append("; помилок бота сер. ").Append(F((double)mistakesSum / n));
            }

            return b.ToString();
        }

        private static string F(double value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
