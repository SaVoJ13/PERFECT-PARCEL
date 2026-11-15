using UnityEngine;

namespace KebabStreet.Game
{
    /// <summary>
    /// Q-05h: чиста математика пауз-панелі і навчання (QB-PAUSE-*, QB-TUT-*), без стану й без виділення пам'яті.
    /// У режимі «менше анімацій» (<see cref="UiMotion.Reduced"/>) усе показується одразу, рух відсутній.
    /// Час приходить зі справжнього годинника (<see cref="UiMotion.Clock"/>): ігровий <see cref="UiMotion.Now"/> на паузі стоїть.
    /// </summary>
    public static class OverlayMotion
    {
        // QB-PAUSE-01: панель з'являється швидко, стан паузи фіксується одразу (анімація лише вигляду).
        public const float PausePopSec = 0.15f;
        public const float PausePopFrom = 0.92f;

        // QB-PAUSE-02: гру за панеллю затемнено на 55-65%.
        public const float PauseDimAlpha = 0.60f;

        // QB-TUT-01: затемнення 55-70%, виріз з'являється за 200-300 мс із запасом 12-16 px.
        public const float DimAlpha = 0.62f;
        public const float SpotlightSec = 0.25f;
        public const float CutoutMarginPx = 14f;
        public const float CutoutExpandPx = 160f;

        // QB-TUT-02: рука «тапає»: translate 10-16 px, scale 1 -> 0,9, цикл 0,8-1,2 с.
        public const float HandCycleSec = 1f;
        public const float HandTravelPx = 14f;
        public const float HandScaleMin = 0.9f;

        // QB-TUT-03: текст друкується 30-45 символів/с.
        public const float CharsPerSec = 38f;

        // Поява плашки наставника і пульс рамки (тап поза вирізом, QB-TUT-05).
        public const float PlaqueSec = 0.25f;
        public const float PlaqueRisePx = 40f;
        public const float FramePulseAmplitude = 0.03f;
        public const float FramePulseHz = 1f;
        public const float BlockedPulsePeak = 1.08f;
        public const float BlockedPulseSec = 0.2f;

        private const float HalfTurn = (float)System.Math.PI;
        private const float FullTurn = 2f * (float)System.Math.PI;

        /// <summary>Поява картки паузи: масштаб 0,92 -> 1; 1 у «менше анімацій» і після появи.</summary>
        public static float PauseScale(float elapsedSec)
        {
            return Mathf.Lerp(PausePopFrom, 1f, Progress(elapsedSec, PausePopSec));
        }

        /// <summary>Прозорість картки паузи 0..1 за час появи.</summary>
        public static float PauseOpacity(float elapsedSec)
        {
            return Progress(elapsedSec, PausePopSec);
        }

        /// <summary>Скільки символів репліки показано (0..length); у «менше анімацій» - усі.</summary>
        public static int TypedChars(float elapsedSec, int length)
        {
            if (length <= 0)
            {
                return 0;
            }

            if (UiMotion.Reduced || elapsedSec < 0f)
            {
                return UiMotion.Reduced ? length : 0;
            }

            return Mathf.Min(length, Mathf.FloorToInt(elapsedSec * CharsPerSec));
        }

        /// <summary>Хід відкриття прожектора 0..1 (ease-out).</summary>
        public static float SpotlightProgress(float elapsedSec)
        {
            float p = Progress(elapsedSec, SpotlightSec);
            float inv = 1f - p;
            return 1f - (inv * inv * inv);
        }

        /// <summary>Прозорість плашки наставника 0..1.</summary>
        public static float PlaqueOpacity(float elapsedSec)
        {
            return Progress(elapsedSec, PlaqueSec);
        }

        /// <summary>Зсув плашки вниз у px: від <see cref="PlaqueRisePx"/> до 0.</summary>
        public static float PlaqueOffset(float elapsedSec)
        {
            float p = SpotlightProgress(elapsedSec);
            return PlaqueRisePx * (1f - p);
        }

        /// <summary>Зсув руки вниз-вгору, px (0..<see cref="HandTravelPx"/>); 0 у «менше анімацій».</summary>
        public static float HandOffset(float timeSec)
        {
            return UiMotion.Reduced ? 0f : HandTravelPx * Wave(timeSec);
        }

        /// <summary>Масштаб руки 1 -> <see cref="HandScaleMin"/> у фазі з тапом; 1 у «менше анімацій».</summary>
        public static float HandScale(float timeSec)
        {
            return UiMotion.Reduced ? 1f : 1f - ((1f - HandScaleMin) * Wave(timeSec));
        }

        /// <summary>Спокійний пульс рамки прожектора 1,00..1,03.</summary>
        public static float FramePulse(float timeSec)
        {
            return UiMotion.Reduced ? 1f : 1f + (FramePulseAmplitude * Mathf.Abs(Mathf.Sin(HalfTurn * FramePulseHz * timeSec)));
        }

        /// <summary>М'який відгук на тап поза вирізом: рамка 1 -> 1,08 -> 1 за 200 мс; 1 поза проміжком.</summary>
        public static float BlockedPulse(float elapsedSec)
        {
            if (UiMotion.Reduced || elapsedSec < 0f || elapsedSec >= BlockedPulseSec)
            {
                return 1f;
            }

            return 1f + ((BlockedPulsePeak - 1f) * Mathf.Sin(HalfTurn * (elapsedSec / BlockedPulseSec)));
        }

        /// <summary>
        /// Виріз прожектора: ціль із запасом <see cref="CutoutMarginPx"/>; під час відкриття ширший на
        /// <see cref="CutoutExpandPx"/> * (1 - progress). Не виходить за <paramref name="bounds"/>.
        /// </summary>
        public static Rect Cutout(Rect target, Rect bounds, float progress)
        {
            float grow = CutoutMarginPx + (CutoutExpandPx * (1f - Mathf.Clamp01(progress)));
            float xMin = Mathf.Max(bounds.xMin, target.xMin - grow);
            float yMin = Mathf.Max(bounds.yMin, target.yMin - grow);
            float xMax = Mathf.Min(bounds.xMax, target.xMax + grow);
            float yMax = Mathf.Min(bounds.yMax, target.yMax + grow);
            return Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
        }

        /// <summary>
        /// Чотири темні прямокутники навколо вирізу (зверху, знизу, ліворуч, праворуч): разом вони покривають
        /// <paramref name="bounds"/> без вирізу і без перекриття. <paramref name="result"/> має містити 4 елементи.
        /// </summary>
        public static void DimRects(Rect bounds, Rect cutout, Rect[] result)
        {
            result[0] = Rect.MinMaxRect(bounds.xMin, bounds.yMin, bounds.xMax, cutout.yMin);
            result[1] = Rect.MinMaxRect(bounds.xMin, cutout.yMax, bounds.xMax, bounds.yMax);
            result[2] = Rect.MinMaxRect(bounds.xMin, cutout.yMin, cutout.xMin, cutout.yMax);
            result[3] = Rect.MinMaxRect(cutout.xMax, cutout.yMin, bounds.xMax, cutout.yMax);
        }

        private static float Progress(float elapsedSec, float durationSec)
        {
            if (UiMotion.Reduced || elapsedSec >= durationSec)
            {
                return 1f;
            }

            return elapsedSec <= 0f ? 0f : elapsedSec / durationSec;
        }

        // 0..1..0 за один цикл руки, найбільше наприкінці першої половини.
        private static float Wave(float timeSec)
        {
            float phase = Mathf.Repeat(timeSec, HandCycleSec) / HandCycleSec;
            return 0.5f - (0.5f * Mathf.Cos(FullTurn * phase));
        }
    }
}
