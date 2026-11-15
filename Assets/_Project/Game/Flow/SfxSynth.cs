using System;

namespace KebabStreet.Game
{
    /// <summary>
    /// Синтез звуків кодом (Q-05g): детерміновані форми хвилі (синус/шум/огинаюча), без файлів і без UnityEngine.
    /// Результат — моно float-семпли в межах ±<see cref="PeakOf"/>; клипи з них створює <c>UnitySfxSink</c> і кешує.
    /// </summary>
    public static class SfxSynth
    {
        /// <summary>Частота дискретизації синтезованих звуків.</summary>
        public const int SampleRate = 22050;

        /// <summary>Довжина музичної петлі, с (8 нот по 0,5 с).</summary>
        public const double MusicSec = 4.0;

        /// <summary>Довжина петлі шипіння, с.</summary>
        public const double GrillLoopSec = 1.0;

        /// <summary>Спільна стеля піка (захист слуху, як для файлів).</summary>
        public const float PeakCeiling = 0.5f;

        private const double TwoPi = 6.283185307179586;
        private const uint NoiseSeed = 0x1234ABCDu;

        /// <summary>Тривалість разового звуку, с.</summary>
        public static double DurationSec(SfxCue cue)
        {
            switch (cue)
            {
                case SfxCue.TimerTick: return 0.06;
                case SfxCue.TimerTickUrgent: return 0.10;
                case SfxCue.StarChime: return 0.50;
                case SfxCue.CoinTick: return 0.04;
                case SfxCue.Locked: return 0.18;
                case SfxCue.BurntAlarm: return 0.45;
                case SfxCue.Bell: return 0.60;
                default: return 0.0;
            }
        }

        /// <summary>Піковий рівень звуку (0..<see cref="PeakCeiling"/>): тихі тіки, ледь чутне шипіння й музика.</summary>
        public static float PeakOf(SfxCue cue)
        {
            switch (cue)
            {
                case SfxCue.TimerTick: return 0.16f;
                case SfxCue.TimerTickUrgent: return 0.30f;
                case SfxCue.StarChime: return 0.36f;
                case SfxCue.CoinTick: return 0.22f;
                case SfxCue.Locked: return 0.34f;
                case SfxCue.BurntAlarm: return 0.30f;
                case SfxCue.Bell: return 0.30f;
                default: return 0f;
            }
        }

        /// <summary>Піковий рівень петлі: шипіння тихе, музика ще тихіша.</summary>
        public static float PeakOf(SfxLoop loop)
        {
            return loop == SfxLoop.Grill ? 0.12f : 0.10f;
        }

        /// <summary>Семпли разового синтезованого звуку; для файлового/невідомого — порожній масив.</summary>
        public static float[] Generate(SfxCue cue)
        {
            int length = (int)Math.Round(DurationSec(cue) * SampleRate);
            if (length <= 0)
            {
                return new float[0];
            }

            var data = new float[length];
            var noise = new Noise(NoiseSeed + (uint)cue);
            for (int i = 0; i < length; i++)
            {
                double t = (double)i / SampleRate;
                double p = (double)i / length;
                data[i] = (float)Sample(cue, t, p, ref noise);
            }

            Normalize(data, PeakOf(cue));
            return data;
        }

        /// <summary>Семпли безшовної петлі (початок продовжує кінець).</summary>
        public static float[] GenerateLoop(SfxLoop loop)
        {
            return loop == SfxLoop.Grill ? GrillLoop() : MusicLoop();
        }

        private static double Sample(SfxCue cue, double t, double p, ref Noise noise)
        {
            switch (cue)
            {
                case SfxCue.TimerTick:
                    return Math.Sin(TwoPi * 1200.0 * t) * Math.Exp(-t * 70.0);
                case SfxCue.TimerTickUrgent:
                    return (Math.Sin(TwoPi * 1600.0 * t) + (0.5 * Math.Sin(TwoPi * 2400.0 * t))) * Math.Exp(-t * 40.0);
                case SfxCue.StarChime:
                    return (Math.Sin(TwoPi * 880.0 * t) + (0.4 * Math.Sin(TwoPi * 1760.0 * t)) + (0.15 * Math.Sin(TwoPi * 2640.0 * t)))
                        * Math.Exp(-t * 7.0) * Attack(t, 0.004);
                case SfxCue.CoinTick:
                    return Math.Sin(TwoPi * 2400.0 * t) * Math.Exp(-t * 130.0);
                case SfxCue.Locked:
                    return (Math.Sin(TwoPi * 110.0 * t) + (0.35 * noise.Next())) * Math.Exp(-t * 22.0) * Attack(t, 0.002);
                case SfxCue.BurntAlarm:
                    return noise.NextHighPassed() * Math.Sin(Math.PI * p) * (0.8 + (0.2 * Math.Sin(TwoPi * 14.0 * t)));
                case SfxCue.Bell:
                    return (Math.Sin(TwoPi * 1318.5 * t) + (0.5 * Math.Sin(TwoPi * 1975.5 * t)) + (0.2 * Math.Sin(TwoPi * 3951.0 * t)))
                        * Math.Exp(-t * 6.0) * Attack(t, 0.003);
                default:
                    return 0.0;
            }
        }

        private static double Attack(double t, double sec)
        {
            return t >= sec ? 1.0 : t / sec;
        }

        /// <summary>Тихий шум із повільним тремтінням гучності; кінець плавно переходить у початок.</summary>
        private static float[] GrillLoop()
        {
            int n = (int)Math.Round(GrillLoopSec * SampleRate);
            int x = SampleRate / 10;
            var raw = new float[n + x];
            var noise = new Noise(NoiseSeed ^ 0x51F1u);
            float low = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                low += 0.35f * ((float)noise.Next() - low);
                raw[i] = (float)noise.Next() * 0.7f + (low * 0.6f);
            }

            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                data[i] = i < x
                    ? (raw[i] * ((float)i / x)) + (raw[n + i] * (1f - ((float)i / x)))
                    : raw[i];
            }

            Normalize(data, PeakOf(SfxLoop.Grill));
            return data;
        }

        /// <summary>Спокійна пентатонічна арпеджіо-петля: 8 м'яких нот із затуханням до нуля.</summary>
        private static float[] MusicLoop()
        {
            double[] notes = { 261.63, 329.63, 392.00, 440.00, 392.00, 329.63, 293.66, 329.63 };
            int n = (int)Math.Round(MusicSec * SampleRate);
            int noteLen = n / notes.Length;
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                for (int i = 0; i < noteLen; i++)
                {
                    double t = (double)i / SampleRate;
                    double env = Attack(t, 0.02) * Math.Exp(-t * 3.5) * Math.Min(1.0, (noteLen - i) / (SampleRate * 0.03));
                    double s = Math.Sin(TwoPi * notes[k] * t) + (0.25 * Math.Sin(TwoPi * notes[k] * 2.0 * t));
                    data[(k * noteLen) + i] = (float)(s * env);
                }
            }

            Normalize(data, PeakOf(SfxLoop.Music));
            return data;
        }

        /// <summary>Масштабує так, щоб пік дорівнював <paramref name="peak"/> (не вище стелі).</summary>
        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float a = Math.Abs(data[i]);
                if (a > max)
                {
                    max = a;
                }
            }

            if (max <= 0f)
            {
                return;
            }

            float target = Math.Min(peak, PeakCeiling);
            float k = target / max;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= k;
            }
        }

        /// <summary>Детермінований шум (xorshift32), -1..1.</summary>
        private struct Noise
        {
            private uint _state;
            private double _prev;

            public Noise(uint seed)
            {
                _state = seed == 0 ? 1u : seed;
                _prev = 0.0;
            }

            public double Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return ((_state / (double)uint.MaxValue) * 2.0) - 1.0;
            }

            /// <summary>Різниця сусідніх відліків — світліший «пшш».</summary>
            public double NextHighPassed()
            {
                double v = Next();
                double d = v - _prev;
                _prev = v;
                return d * 0.5;
            }
        }
    }
}
