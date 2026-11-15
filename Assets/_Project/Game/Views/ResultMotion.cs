using System;
using UnityEngine;

namespace KebabStreet.Game
{
    /// <summary>
    /// Ритуал результату (Q-05e, QB-RES-01...05): чиста математика часової шкали екрана результату, без стану
    /// і без виділення пам'яті. Час (<c>elapsed</c>) - секунди від появи екрана; тап «пропустити» просто
    /// підставляє кінець шкали. У режимі «менше анімацій» (<see cref="UiMotion.Reduced"/>) усе показується
    /// відразу. Правил гри тут немає: зірки й монети - вхідні числа, їх рахує Core.
    /// </summary>
    public static class ResultMotion
    {
        // QB-RES-01: банер вилітає згори ease-out-back за 300-400 мс.
        public const float BannerSec = 0.35f;
        public const float BannerOffsetPx = -240f;

        // QB-RES-02: зірки по одній, інтервал 300-400 мс, підскок 0 -> 1,3 -> 1.
        public const float StarFirstAtSec = 0.45f;
        public const float StarGapSec = 0.35f;
        public const float StarPopSec = 0.3f;
        public const float StarPopPeak = 1.3f;
        public const int SparksMin = 6;
        public const int SparksMax = 10;

        // QB-RES-03: монети набігають 0,6-1,2 с.
        public const float CoinsStartSec = BannerSec;
        public const float CoinsSec = 0.9f;

        // QB-RES-04: конфеті 40-80 частинок.
        public const int ConfettiCount = 60;

        // QB-RES-05: кнопки з'являються після 0,6-0,9 с затримки (від останньої події шкали) за 0,2 с.
        public const float ButtonsDelaySec = 0.6f;
        public const float ButtonsFadeSec = 0.2f;

        // QB-RES-08: підсвітка нового рівня в меню пульсує 1 Гц (не більше 3 Гц, QB-ALL-13).
        public const float UnlockPulseHz = 1f;
        public const float UnlockMinOpacity = 0.35f;
        public const float UnlockFadeInSec = 0.4f;

        private const float OvershootC1 = 1.70158f;
        private const float OvershootC3 = OvershootC1 + 1f;
        private const float PopRiseShare = 0.6f;
        private const float FullTurn = 2f * (float)Math.PI;
        private const float Half = 0.5f;

        /// <summary>Коли (с) з'являється зірка з номером <paramref name="index"/> (0...).</summary>
        public static float StarAt(int index)
        {
            return StarFirstAtSec + (index * StarGapSec);
        }

        /// <summary>Остання подія шкали: остання зароблена зірка або кінець банера, якщо зірок немає.</summary>
        public static float LastEventAt(int stars)
        {
            return stars > 0 ? StarAt(stars - 1) : BannerSec;
        }

        /// <summary>Коли з'являються кнопки.</summary>
        public static float ButtonsAt(int stars)
        {
            return LastEventAt(stars) + ButtonsDelaySec;
        }

        /// <summary>Повна тривалість шкали (з появою кнопок): після неї пропускати нічого.</summary>
        public static float TotalSec(int stars)
        {
            float coinsEnd = CoinsStartSec + CoinsSec;
            return Mathf.Max(coinsEnd, ButtonsAt(stars) + ButtonsFadeSec);
        }

        /// <summary>Коли пускати конфеті: з останньою зіркою (3★) або з банером, якщо зірок немає.</summary>
        public static float ConfettiAt(int stars)
        {
            return stars >= 3 ? StarAt(stars - 1) : BannerSec;
        }

        /// <summary>Зсув банера по вертикалі: від -240 px до 0 з відскоком; 0 поза проміжком.</summary>
        public static float BannerOffset(float elapsedSec)
        {
            if (UiMotion.Reduced || elapsedSec >= BannerSec)
            {
                return 0f;
            }

            float x = Mathf.Clamp01(elapsedSec / BannerSec);
            float back = 1f + (OvershootC3 * Mathf.Pow(x - 1f, 3f)) + (OvershootC1 * Mathf.Pow(x - 1f, 2f));
            return BannerOffsetPx * (1f - back);
        }

        /// <summary>Прозорість банера 0..1.</summary>
        public static float BannerOpacity(float elapsedSec)
        {
            return UiMotion.Reduced || elapsedSec >= BannerSec ? 1f : Mathf.Clamp01(elapsedSec / BannerSec);
        }

        /// <summary>Чи зірка <paramref name="index"/> уже «засвітилась» (замінила порожній контур).</summary>
        public static bool StarReached(int index, float elapsedSec)
        {
            return UiMotion.Reduced || elapsedSec >= StarAt(index);
        }

        /// <summary>Масштаб зірки: 0 -> 1,3 -> 1 за 0,3 с від її моменту; 1 до і після (до моменту показано контур).</summary>
        public static float StarScale(int index, float elapsedSec)
        {
            float e = elapsedSec - StarAt(index);
            if (UiMotion.Reduced || e < 0f || e >= StarPopSec)
            {
                return 1f;
            }

            float x = e / StarPopSec;
            if (x < PopRiseShare)
            {
                float rise = x / PopRiseShare;
                return StarPopPeak * (1f - ((1f - rise) * (1f - rise)));
            }

            float fall = (x - PopRiseShare) / (1f - PopRiseShare);
            return Mathf.Lerp(StarPopPeak, 1f, fall);
        }

        /// <summary>Скільки монет показати: набігання від 0 до суми.</summary>
        public static long CoinsShown(long reward, float elapsedSec)
        {
            return UiMotion.CountUp(0L, reward, elapsedSec - CoinsStartSec, CoinsSec);
        }

        /// <summary>Прозорість кнопок 0..1; кнопки доступні для тапу лише від <see cref="ButtonsAt"/>.</summary>
        public static float ButtonsOpacity(int stars, float elapsedSec)
        {
            if (UiMotion.Reduced)
            {
                return 1f;
            }

            return Mathf.Clamp01((elapsedSec - ButtonsAt(stars)) / ButtonsFadeSec);
        }

        /// <summary>Чи кнопки вже можна тиснути.</summary>
        public static bool ButtonsReady(int stars, float elapsedSec)
        {
            return UiMotion.Reduced || elapsedSec >= ButtonsAt(stars);
        }

        /// <summary>Прозорість рамки-підсвітки щойно відкритого рівня: з'являється, далі пульсує; у «менше анімацій» - стала.</summary>
        public static float UnlockOpacity(float sinceSec)
        {
            if (UiMotion.Reduced)
            {
                return 1f;
            }

            float fade = Mathf.Clamp01(sinceSec / UnlockFadeInSec);
            float wave = Half + (Half * Mathf.Sin(FullTurn * UnlockPulseHz * sinceSec));
            return fade * (UnlockMinOpacity + ((1f - UnlockMinOpacity) * wave));
        }
    }
}
