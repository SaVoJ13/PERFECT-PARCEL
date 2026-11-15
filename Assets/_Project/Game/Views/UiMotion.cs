using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Рух екрана зміни (Q-05c, QB-SHIFT-02/03/04/09/10/13): чиста математика анімацій (без стану, без
    /// виділення пам'яті) і єдиний перемикач «менше анімацій» (<see cref="Reduced"/>): коли він увімкнений,
    /// усі функції повертають стан спокою. Змінюються лише transform і opacity (QB-ALL-16), розмітка
    /// не перераховується. Правил гри тут немає: час береться з <see cref="Clock"/>, його підміняють тести.
    /// </summary>
    public static class UiMotion
    {
        // QB-SHIFT-09: дихання scale-y 1,00..1,03, період 1,5-2,5 с.
        public const float BreathAmplitude = 0.03f;
        public const float BreathPeriodSec = 2f;

        // QB-SHIFT-02: останні 10 с число пульсує 1,00..1,12 раз на секунду.
        public const long TimerWarningSec = 10;
        public const float TimerPulsePeak = 1.12f;
        public const float TimerPulseSec = 1f;

        // QB-SHIFT-03: підскок цілі 1 -> 1,25 -> 1 за 200-250 мс.
        public const float GoalPopPeak = 1.25f;
        public const float GoalPopSec = 0.225f;

        // QB-SHIFT-04: монети набігають 300-500 мс, значок пульсує.
        public const float CoinCountSec = 0.4f;
        public const float CoinPopPeak = 1.15f;
        public const float CoinPopSec = 0.25f;

        // QB-SHIFT-10: тремтіння на останньому сердечку: +-3 px, <= 3 Гц (QB-ALL-13).
        public const float ShakeAmplitudePx = 3f;
        public const float ShakeHz = 2.5f;
        public const float MaxFlashHz = 3f;

        // QB-SHIFT-13: підскок -12 px за 150 мс; QB-SHIFT-08: виїзд збоку 400 мс; відхід 400 мс.
        public const float HopHeightPx = 12f;
        public const float HopSec = 0.15f;
        public const float ArriveSec = 0.4f;
        public const float ArriveOffsetPx = 80f;
        public const float LeaveSec = 0.4f;
        public const float LeaveOffsetPx = 120f;

        // Підпис події (оплата / пішов / не те): короткий підскок.
        public const float EventPopPeak = 1.3f;
        public const float EventPopSec = 0.25f;

        private const float OvershootC1 = 1.70158f;
        private const float OvershootC3 = OvershootC1 + 1f;
        private const float HalfTurn = (float)Math.PI;
        private const float FullTurn = 2f * (float)Math.PI;
        private const float Half = 0.5f;

        // Q-05d. QB-SHIFT-19: «вистриб» готового 1 -> 1,15 -> 1 за 200 мс, далі пульс 1,00..1,06 не частіше 1 Гц.
        public const float DonePopPeak = 1.15f;
        public const float DonePopSec = 0.2f;
        public const float DonePulseAmplitude = 0.06f;
        public const float DonePulseHz = 0.8f;

        // QB-SHIFT-20: майже пригоріле - пульс індикатора 2 Гц (не більше 3 Гц, QB-ALL-13) і тремтіння +-2,5 px.
        public const float WarnPulseAmplitude = 0.08f;
        public const float WarnPulseHz = 2f;
        public const float WarnShakePx = 2.5f;

        // QB-SHIFT-22: шматок м'яса падає на слот з -30 px за 150 мс, з відскоком.
        public const float DropHeightPx = 30f;
        public const float DropSec = 0.15f;

        private static Func<float> _clock = DefaultClock;
        private static float _pausedAt = -1f;
        private static float _pausedTotal;

        /// <summary>
        /// Джерело часу (с). У грі — реальний час; тести підставляють власне. Підміна скидає паузу.
        /// </summary>
        public static Func<float> Clock
        {
            get => _clock;
            set
            {
                _clock = value ?? DefaultClock;
                ResetPause();
            }
        }

        /// <summary>Чи рух зупинено паузою гри (<see cref="Now"/> стоїть).</summary>
        public static bool Paused => _pausedAt >= 0f;

        /// <summary>Режим «менше анімацій»: рух вимкнено, усе показується відразу.</summary>
        public static bool Reduced { get; set; }

        /// <summary>Ігровий час руху (с): реальний час без пауз. На паузі стоїть, тож дихання, пара, політ завмирають.</summary>
        public static float Now => (Paused ? _pausedAt : _clock()) - _pausedTotal;

        /// <summary>Пауза гри (Q-05d): зупиняє або відновлює <see cref="Now"/>; повторний виклик зіstanом нічого не міняє.</summary>
        public static void SetPaused(bool paused)
        {
            if (paused == Paused)
            {
                return;
            }

            float real = _clock();
            if (paused)
            {
                _pausedAt = real;
            }
            else
            {
                _pausedTotal += real - _pausedAt;
                _pausedAt = -1f;
            }
        }

        /// <summary>Скидає облік пауз (нова зміна, підміна годинника у тестах).</summary>
        public static void ResetPause()
        {
            _pausedAt = -1f;
            _pausedTotal = 0f;
        }

        /// <summary>Дихання: scale-y; <paramref name="phase"/> (0..1) зсуває фазу, щоб клієнти дихали не синхронно.</summary>
        public static float Breath(float timeSec, float phase)
        {
            if (Reduced)
            {
                return 1f;
            }

            float wave = Half + (Half * Mathf.Sin(FullTurn * ((timeSec / BreathPeriodSec) + phase)));
            return 1f + (BreathAmplitude * wave);
        }

        /// <summary>Чи таймер у зоні попередження (останні 10 с, зміна ще йде).</summary>
        public static bool IsTimerWarning(long remainingSec)
        {
            return remainingSec > 0 && remainingSec <= TimerWarningSec;
        }

        /// <summary>Пульс числа таймера: один імпульс на секунду, від моменту, коли число змінилось.</summary>
        public static float TimerPulse(float sinceTickSec, long remainingSec)
        {
            if (Reduced || !IsTimerWarning(remainingSec))
            {
                return 1f;
            }

            return Hump(sinceTickSec, TimerPulseSec, TimerPulsePeak);
        }

        /// <summary>Імпульс 1 -> пік -> 1 за <paramref name="durationSec"/>; поза проміжком (і в «менше анімацій») - 1.</summary>
        public static float Pop(float elapsedSec, float durationSec, float peak)
        {
            return Reduced ? 1f : Hump(elapsedSec, durationSec, peak);
        }

        /// <summary>Підскок угору в px (від'ємне значення = вгору); 0 поза проміжком.</summary>
        public static float Hop(float elapsedSec)
        {
            if (Reduced || elapsedSec < 0f || elapsedSec >= HopSec)
            {
                return 0f;
            }

            return -HopHeightPx * Mathf.Sin(HalfTurn * (elapsedSec / HopSec));
        }

        /// <summary>Тремтіння по горизонталі в px; частота обмежена <see cref="MaxFlashHz"/>.</summary>
        public static float Shake(float timeSec)
        {
            if (Reduced)
            {
                return 0f;
            }

            return ShakeAmplitudePx * Mathf.Sin(FullTurn * Mathf.Min(ShakeHz, MaxFlashHz) * timeSec);
        }

        /// <summary>Скільки показати на лічильнику монет: плавно від <paramref name="from"/> до <paramref name="to"/>.</summary>
        public static long CountUp(long from, long to, float elapsedSec, float durationSec)
        {
            if (Reduced || durationSec <= 0f || elapsedSec >= durationSec)
            {
                return to;
            }

            if (elapsedSec <= 0f)
            {
                return from;
            }

            float k = 1f - Mathf.Pow(1f - (elapsedSec / durationSec), 3f);
            return from + (long)Math.Round((to - from) * (double)k);
        }

        /// <summary>Зсув виїзду збоку: від -offset до 0 з невеликим відскоком; 0 поза проміжком.</summary>
        public static float SlideIn(float elapsedSec)
        {
            if (Reduced || elapsedSec < 0f || elapsedSec >= ArriveSec)
            {
                return 0f;
            }

            float x = elapsedSec / ArriveSec;
            float back = 1f + (OvershootC3 * Mathf.Pow(x - 1f, 3f)) + (OvershootC1 * Mathf.Pow(x - 1f, 2f));
            return -ArriveOffsetPx * (1f - back);
        }

        /// <summary>Прозорість при появі 0..1 за час виїзду; 1 поза проміжком.</summary>
        public static float ArriveOpacity(float elapsedSec)
        {
            return Reduced || elapsedSec < 0f || elapsedSec >= ArriveSec ? 1f : elapsedSec / ArriveSec;
        }

        /// <summary>Зсув відходу вбік 0..offset; 0 поза проміжком.</summary>
        public static float LeaveOffset(float elapsedSec)
        {
            if (Reduced || elapsedSec < 0f || elapsedSec >= LeaveSec)
            {
                return 0f;
            }

            float x = elapsedSec / LeaveSec;
            return LeaveOffsetPx * x * x;
        }

        /// <summary>Прозорість відходу 1..0.</summary>
        public static float LeaveOpacity(float elapsedSec)
        {
            return Reduced || elapsedSec < 0f || elapsedSec >= LeaveSec ? 1f : 1f - (elapsedSec / LeaveSec);
        }

        /// <summary>«Вистриб» готового м'яса (1 -> 1,15 -> 1 за 200 мс); 1 поза проміжком.</summary>
        public static float DonePop(float elapsedSec)
        {
            return Pop(elapsedSec, DonePopSec, DonePopPeak);
        }

        /// <summary>Спокійний пульс готового м'яса 1,00..1,06, не частіше 1 Гц.</summary>
        public static float DonePulse(float timeSec)
        {
            return Reduced ? 1f : 1f + (DonePulseAmplitude * Wave(timeSec, DonePulseHz));
        }

        /// <summary>Тривожний пульс майже пригорілого 1,00..1,08 з частотою 2 Гц (не більше 3 Гц).</summary>
        public static float WarnPulse(float timeSec)
        {
            return Reduced ? 1f : 1f + (WarnPulseAmplitude * Wave(timeSec, Mathf.Min(WarnPulseHz, MaxFlashHz)));
        }

        /// <summary>Тремтіння майже пригорілого м'яса по горизонталі, px.</summary>
        public static float WarnShake(float timeSec)
        {
            return Reduced ? 0f : WarnShakePx * Mathf.Sin(FullTurn * Mathf.Min(ShakeHz, MaxFlashHz) * timeSec);
        }

        /// <summary>Падіння шматка на слот: зсув по вертикалі від -30 px до 0 з відскоком; 0 поза проміжком.</summary>
        public static float Drop(float elapsedSec)
        {
            if (Reduced || elapsedSec < 0f || elapsedSec >= DropSec)
            {
                return 0f;
            }

            return -DropHeightPx * (1f - BounceOut(elapsedSec / DropSec));
        }

        /// <summary>Ease-out-bounce: 0..1.</summary>
        public static float BounceOut(float x)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (x < 1f / d1)
            {
                return n1 * x * x;
            }

            if (x < 2f / d1)
            {
                x -= 1.5f / d1;
                return (n1 * x * x) + 0.75f;
            }

            if (x < 2.5f / d1)
            {
                x -= 2.25f / d1;
                return (n1 * x * x) + 0.9375f;
            }

            x -= 2.625f / d1;
            return (n1 * x * x) + 0.984375f;
        }

        /// <summary>Чи триває проміжок [0, duration).</summary>
        public static bool Active(float elapsedSec, float durationSec)
        {
            return !Reduced && elapsedSec >= 0f && elapsedSec < durationSec;
        }

        private static float Wave(float timeSec, float hz)
        {
            return Half + (Half * Mathf.Sin(FullTurn * hz * timeSec));
        }

        private static float Hump(float elapsedSec, float durationSec, float peak)
        {
            if (elapsedSec < 0f || elapsedSec >= durationSec)
            {
                return 1f;
            }

            return 1f + ((peak - 1f) * Mathf.Sin(HalfTurn * (elapsedSec / durationSec)));
        }

        private static float DefaultClock()
        {
            return Time.realtimeSinceStartup;
        }
    }

    /// <summary>
    /// Керує transform і opacity одного елемента: пише у стиль, лише коли значення справді змінилось
    /// (без зайвого перемальовування), і запам'ятовує застосоване - тести читають його без панелі.
    /// </summary>
    public sealed class MotionTarget
    {
        private const float Epsilon = 0.001f;
        private readonly VisualElement _element;

        public MotionTarget(VisualElement element)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            ScaleX = 1f;
            ScaleY = 1f;
            Opacity = 1f;
        }

        public float ScaleX { get; private set; }

        public float ScaleY { get; private set; }

        public float TranslateX { get; private set; }

        public float TranslateY { get; private set; }

        public float Opacity { get; private set; }

        /// <summary>Скільки разів стиль справді записано (для тесту «без зайвих записів»).</summary>
        public int Writes { get; private set; }

        public void Set(float scaleX, float scaleY, float translateX, float translateY, float opacity)
        {
            bool scale = Differs(scaleX, ScaleX) || Differs(scaleY, ScaleY);
            bool move = Differs(translateX, TranslateX) || Differs(translateY, TranslateY);
            bool fade = Differs(opacity, Opacity);
            if (!scale && !move && !fade)
            {
                return;
            }

            Writes++;
            if (scale)
            {
                ScaleX = scaleX;
                ScaleY = scaleY;
                _element.style.scale = new Scale(new Vector3(scaleX, scaleY, 1f));
            }

            if (move)
            {
                TranslateX = translateX;
                TranslateY = translateY;
                _element.style.translate = new Translate(translateX, translateY);
            }

            if (fade)
            {
                Opacity = opacity;
                _element.style.opacity = opacity;
            }
        }

        private static bool Differs(float a, float b)
        {
            return Mathf.Abs(a - b) > Epsilon;
        }
    }
}
