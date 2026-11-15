using System;
using System.Diagnostics;

namespace KebabStreet.Game
{
    /// <summary>Годинник звуку: реальний час у секундах (не залежить від паузи гри). Без UnityEngine.</summary>
    public static class SfxClock
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();

        public static double Now()
        {
            return Watch.Elapsed.TotalSeconds;
        }
    }

    /// <summary>
    /// Ліміт запитів звуку (QB-ALL-03, QB-ALL-15): один і той самий звук не частіше за заданий інтервал
    /// і не більше N одночасних копій. Без алокацій після створення.
    /// </summary>
    public sealed class SfxLimiter
    {
        private const int CueSlots = 32;
        private const int MaxCopies = 8;

        private readonly double[] _last = new double[CueSlots];
        private readonly double[] _ends = new double[CueSlots * MaxCopies];
        private readonly bool[] _seen = new bool[CueSlots];

        /// <summary>true — звук можна грати, запит зараховано; false — відхилено.</summary>
        public bool TryAcquire(SfxCue cue, double now, double minIntervalSec, double durationSec, int maxConcurrent)
        {
            int id = (int)cue;
            if (id < 0 || id >= CueSlots)
            {
                return false;
            }

            if (_seen[id] && now - _last[id] < minIntervalSec)
            {
                return false;
            }

            int cap = maxConcurrent < 1 ? 1 : (maxConcurrent > MaxCopies ? MaxCopies : maxConcurrent);
            int busy = 0;
            int free = -1;
            int baseIndex = id * MaxCopies;
            for (int i = 0; i < cap; i++)
            {
                if (_ends[baseIndex + i] > now)
                {
                    busy++;
                }
                else if (free < 0)
                {
                    free = i;
                }
            }

            if (busy >= cap || free < 0)
            {
                return false;
            }

            _ends[baseIndex + free] = now + durationSec;
            _last[id] = now;
            _seen[id] = true;
            return true;
        }
    }

    /// <summary>Висота звуку: кроки «дзинь» і детермінована варіація ±3-6% (QB-ALL-15).</summary>
    public static class SfxPitch
    {
        public const float JitterMin = 0.03f;
        public const float JitterMax = 0.06f;

        /// <summary>Максимум кроків зростання (QB-RES-02/SHIFT-30: до 5 кроків).</summary>
        public const int MaxSteps = 5;

        /// <summary>Множник висоти за <paramref name="step"/> кроків по два півтони (0 — без змін).</summary>
        public static float Steps(int step)
        {
            if (step <= 0)
            {
                return 1f;
            }

            int s = step > MaxSteps ? MaxSteps : step;
            return (float)Math.Pow(2.0, 2.0 * s / 12.0);
        }

        /// <summary>
        /// Множник 1 ± (3..6)% для запиту з номером <paramref name="counter"/>: знак і величина змінюються
        /// детерміновано (без випадковості), поспіль два однакових не йдуть.
        /// </summary>
        public static float Jitter(int counter)
        {
            uint h = unchecked((uint)counter * 2654435761u);
            h ^= h >> 15;
            float t = (h & 0xFFFFu) / 65535f;
            float amount = JitterMin + ((JitterMax - JitterMin) * t);
            bool up = ((counter & 1) == 0);
            return up ? 1f + amount : 1f - amount;
        }
    }
}
