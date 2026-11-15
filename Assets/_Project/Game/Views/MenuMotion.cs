using UnityEngine;

namespace KebabStreet.Game
{
    /// <summary>
    /// Рух меню і вибору рівнів (Q-05f, QB-MENU-01/03/04, QB-LVL-03/04/05/06): чиста математика без стану і без виділення
    /// пам'яті. Усе підпорядковане <see cref="UiMotion.Reduced"/> (стан спокою). Лише scale/translate/opacity (QB-ALL-16).
    /// </summary>
    public static class MenuMotion
    {
        // QB-MENU-01: погойдування логотипа scale 1,00..1,02, період 2,5-3,5 с.
        public const float LogoSwayAmplitude = 0.02f;
        public const float LogoSwayPeriodSec = 3f;

        // QB-MENU-02: мерехтіння лампочок не частіше 1 Гц; пара над логотипом 3-8 частинок/с.
        public const float LightFlickerHz = 0.8f;
        public const int LightCount = 7;
        public const float SteamPerSecond = 5f;

        // QB-MENU-03: дихання кнопки «Грати» scale 1,00..1,04 раз на 3-4 с, не постійно (є пауза).
        public const float PlayBreathAmplitude = 0.04f;
        public const float PlayBreathPeriodSec = 3.5f;
        public const float PlayBreathActiveSec = 0.9f;

        // QB-MENU-04: каскад: затримка 60-80 мс між елементами, загалом <= 600 мс.
        public const float CascadeGapSec = 0.07f;
        public const float CascadeItemSec = 0.1f;
        public const float CascadeTotalSec = 0.6f;
        public const float CascadeRisePx = 24f;

        // QB-LVL-03: пульс поточного 1,00..1,06, період 1,2-1,6 с; автопрокрутка <= 400 мс.
        public const float CurrentPulseAmplitude = 0.06f;
        public const float CurrentPulsePeriodSec = 1.4f;
        public const float ScrollSec = 0.35f;
        public const float MarkerBobPx = 8f;

        // QB-LVL-04: тряска замкненого вузла +-6 px, 3 цикли за 200-250 мс.
        public const float ShakePx = 6f;
        public const int ShakeCycles = 3;
        public const float ShakeSec = 0.225f;

        // QB-LVL-05: замок відлітає 1 -> 1,3 + зникає 300-400 мс, вузол вистрибує; загалом 600-900 мс.
        public const float LockFlySec = 0.35f;
        public const float LockScalePeak = 1.3f;
        public const float NodePopStartSec = 0.35f;
        public const float NodePopSec = 0.4f;
        public const float NodePopFrom = 0.6f;

        // QB-LVL-06: нові зірки по одній, інтервал 120-180 мс.
        public const float StarGapSec = 0.15f;
        public const float StarPopSec = 0.2f;
        public const float StarPopPeak = 1.3f;
        public const float StarsStartSec = 0.4f;

        private const float FullTurn = 2f * (float)System.Math.PI;
        private const float OvershootC1 = 1.70158f;
        private const float OvershootC3 = OvershootC1 + 1f;
        private const float Half = 0.5f;

        /// <summary>Тривалість усієї послідовності відкриття рівня.</summary>
        public static float UnlockTotalSec => NodePopStartSec + NodePopSec;

        /// <summary>Погойдування логотипа (scale).</summary>
        public static float LogoSway(float timeSec)
        {
            return UiMotion.Reduced ? 1f : 1f + (LogoSwayAmplitude * Wave(timeSec, 1f / LogoSwayPeriodSec));
        }

        /// <summary>Яскравість лампочки <paramref name="index"/> 0,35..1: сусідні в протифазі, частота не більше 1 Гц.</summary>
        public static float LightOpacity(int index, float timeSec)
        {
            if (UiMotion.Reduced)
            {
                return 1f;
            }

            float phase = (index % 2) * Half;
            return 0.35f + (0.65f * Wave(timeSec + phase, LightFlickerHz));
        }

        /// <summary>Дихання «Грати»: короткий вдих-видих на початку кожного періоду, далі спокій.</summary>
        public static float PlayBreath(float timeSec)
        {
            if (UiMotion.Reduced || timeSec < 0f)
            {
                return 1f;
            }

            float inPeriod = timeSec % PlayBreathPeriodSec;
            if (inPeriod >= PlayBreathActiveSec)
            {
                return 1f;
            }

            return 1f + (PlayBreathAmplitude * Mathf.Sin(Mathf.PI * (inPeriod / PlayBreathActiveSec)));
        }

        /// <summary>Затримка входу елемента каскаду: 70 мс між елементами, стискається, щоб останній закінчився до 600 мс.</summary>
        public static float CascadeDelay(int index, int count)
        {
            if (index <= 0 || count <= 1)
            {
                return 0f;
            }

            float gap = Mathf.Min(CascadeGapSec, (CascadeTotalSec - CascadeItemSec) / (count - 1));
            return index * gap;
        }

        /// <summary>Загальна тривалість каскаду з <paramref name="count"/> елементів.</summary>
        public static float CascadeTotal(int count)
        {
            return count <= 0 ? 0f : CascadeDelay(count - 1, count) + CascadeItemSec;
        }

        /// <summary>Прозорість елемента каскаду 0..1.</summary>
        public static float CascadeOpacity(float sinceStartSec)
        {
            return UiMotion.Reduced || sinceStartSec >= CascadeItemSec ? 1f : Mathf.Clamp01(sinceStartSec / CascadeItemSec);
        }

        /// <summary>Зсув елемента каскаду по вертикалі, px (знизу до 0).</summary>
        public static float CascadeOffset(float sinceStartSec)
        {
            if (UiMotion.Reduced || sinceStartSec >= CascadeItemSec)
            {
                return 0f;
            }

            float k = 1f - Mathf.Clamp01(sinceStartSec / CascadeItemSec);
            return CascadeRisePx * k * k;
        }

        /// <summary>Пульс поточного рівня (scale).</summary>
        public static float CurrentPulse(float timeSec)
        {
            return UiMotion.Reduced ? 1f : 1f + (CurrentPulseAmplitude * Wave(timeSec, 1f / CurrentPulsePeriodSec));
        }

        /// <summary>Погойдування маркера над поточним рівнем, px (від'ємне - вгору).</summary>
        public static float MarkerBob(float timeSec)
        {
            return UiMotion.Reduced ? 0f : -MarkerBobPx * Wave(timeSec, 1f / CurrentPulsePeriodSec);
        }

        /// <summary>Позиція прокрутки під час автоприлоту до поточного рівня: ease-out за <see cref="ScrollSec"/>; Reduced - одразу.</summary>
        public static float ScrollOffset(float from, float to, float elapsedSec)
        {
            if (UiMotion.Reduced || elapsedSec >= ScrollSec)
            {
                return to;
            }

            float k = Mathf.Clamp01(elapsedSec / ScrollSec);
            return Mathf.Lerp(from, to, 1f - ((1f - k) * (1f - k)));
        }

        /// <summary>Тряска замкненого вузла по горизонталі, px; 0 поза проміжком.</summary>
        public static float LockedShake(float elapsedSec)
        {
            if (UiMotion.Reduced || elapsedSec < 0f || elapsedSec >= ShakeSec)
            {
                return 0f;
            }

            return ShakePx * Mathf.Sin(FullTurn * ShakeCycles * (elapsedSec / ShakeSec));
        }

        /// <summary>Масштаб замка, що відлітає: 1 -> 1,3 за <see cref="LockFlySec"/>.</summary>
        public static float LockScale(float elapsedSec)
        {
            return UiMotion.Reduced ? 1f : Mathf.Lerp(1f, LockScalePeak, Mathf.Clamp01(elapsedSec / LockFlySec));
        }

        /// <summary>Прозорість замка, що відлітає: 1 -> 0 (у Reduced замок вже зник).</summary>
        public static float LockOpacity(float elapsedSec)
        {
            return UiMotion.Reduced ? 0f : 1f - Mathf.Clamp01(elapsedSec / LockFlySec);
        }

        /// <summary>Чи вузол вже «вистрибнув» (замок зник, вузол має відкритий вигляд).</summary>
        public static bool NodeOpened(float elapsedSec)
        {
            return UiMotion.Reduced || elapsedSec >= NodePopStartSec;
        }

        /// <summary>Масштаб вузла, що відкривається: від 0,6 до 1 з перельотом (ease-out-back); до початку і після - 1.</summary>
        public static float NodePop(float elapsedSec)
        {
            if (UiMotion.Reduced || elapsedSec < NodePopStartSec || elapsedSec >= NodePopStartSec + NodePopSec)
            {
                return 1f;
            }

            float x = (elapsedSec - NodePopStartSec) / NodePopSec;
            float back = 1f + (OvershootC3 * Mathf.Pow(x - 1f, 3f)) + (OvershootC1 * Mathf.Pow(x - 1f, 2f));
            return Mathf.LerpUnclamped(NodePopFrom, 1f, back);
        }

        /// <summary>Коли з'являється зірка <paramref name="index"/> (0..2), що щойно отримана.</summary>
        public static float StarAt(int index)
        {
            return StarsStartSec + (index * StarGapSec);
        }

        /// <summary>Підскок нової зірки 1 -> 1,3 -> 1; до появи і після - 1.</summary>
        public static float StarPop(int index, float elapsedSec)
        {
            return UiMotion.Pop(elapsedSec - StarAt(index), StarPopSec, StarPopPeak);
        }

        /// <summary>Чи нова зірка вже має бути показана як повна.</summary>
        public static bool StarShown(int index, float elapsedSec)
        {
            return UiMotion.Reduced || elapsedSec >= StarAt(index);
        }

        private static float Wave(float timeSec, float hz)
        {
            return Half + (Half * Mathf.Sin(FullTurn * hz * timeSec));
        }
    }

    /// <summary>Розкладка шляху рівнів (Q-05f, QB-LVL-02): зигзаг вузлів і крапки доріжки між ними. Без UnityEngine.</summary>
    public static class LevelMapLayout
    {
        public const float NodeSize = 150f;
        public const float ZigzagPx = 150f;
        public const int DotsPerLink = 3;
        public const float DotSize = 18f;
        public const float DotGap = 22f;

        /// <summary>Зсув центра вузла від осі, px: -, +, -, +...</summary>
        public static float NodeOffsetX(int index)
        {
            return (index % 2 == 0 ? -1f : 1f) * ZigzagPx * 0.5f;
        }

        /// <summary>Зсув центра <paramref name="dot"/>-ї крапки (0..DotsPerLink-1) з'єднання між вузлами index і index+1.</summary>
        public static float DotOffsetX(int index, int dot)
        {
            float t = (dot + 1f) / (DotsPerLink + 1f);
            float a = NodeOffsetX(index);
            float b = NodeOffsetX(index + 1);
            return a + ((b - a) * t);
        }

        /// <summary>Індекс поточного рівня: перший відкритий без зірок; усе пройдено - останній відкритий; -1, якщо порожньо.</summary>
        public static int CurrentIndex(LevelEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return -1;
            }

            int lastUnlocked = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!entries[i].Unlocked)
                {
                    break;
                }

                lastUnlocked = i;
                if (entries[i].BestStars == 0)
                {
                    return i;
                }
            }

            return lastUnlocked;
        }

        /// <summary>Номер рівня, який треба пройти, щоб відкрити <paramref name="index"/>-й (попередній у каталозі); 0 для першого.</summary>
        public static int RequiredNumber(LevelEntry[] entries, int index)
        {
            return entries == null || index <= 0 || index >= entries.Length ? 0 : entries[index - 1].Number;
        }
    }
}
