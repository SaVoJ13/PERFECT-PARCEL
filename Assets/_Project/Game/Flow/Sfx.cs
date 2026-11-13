using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>Тимчасові звуки (S1-13): що саме сталося в грі. Без UnityEngine.</summary>
    public enum SfxCue
    {
        Sizzle = 1,
        Cash = 2,
        Happy = 3,
        Sad = 4,
        Burnt = 5,
        Tap = 6,
    }

    /// <summary>Куди йдуть звуки: у грі — AudioSource, у тестах — заглушка.</summary>
    public interface ISfxSink
    {
        void Play(SfxCue cue);
    }

    /// <summary>Імена ресурсів звуків у <c>Resources/Audio</c> (без розширення); тест звіряє їх із файлами й реєстром.</summary>
    public static class SfxCatalog
    {
        /// <summary>Тека звуків відносно Resources.</summary>
        public const string AudioFolder = "Audio/";

        public static readonly string[] AllNames =
        {
            "sfx_sizzle", "sfx_cash", "sfx_happy", "sfx_sad", "sfx_burnt", "sfx_tap",
        };

        /// <summary>Ім'я ресурсу звуку або null для невідомого значення.</summary>
        public static string NameOf(SfxCue cue)
        {
            switch (cue)
            {
                case SfxCue.Sizzle: return AllNames[0];
                case SfxCue.Cash: return AllNames[1];
                case SfxCue.Happy: return AllNames[2];
                case SfxCue.Sad: return AllNames[3];
                case SfxCue.Burnt: return AllNames[4];
                case SfxCue.Tap: return AllNames[5];
                default: return null;
            }
        }
    }

    /// <summary>
    /// Єдина точка запиту звуків: вимкнено, на паузі або без виходу — звук не запитується (false).
    /// Гучність/повзунок — не в цій картці: за замовчуванням звук увімкнено.
    /// </summary>
    public sealed class SfxRouter
    {
        private readonly ISfxSink _sink;

        public SfxRouter(ISfxSink sink)
        {
            _sink = sink;
        }

        /// <summary>Звук увімкнено (за замовчуванням так).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Гра на паузі: звуки не запитуються.</summary>
        public bool Paused { get; set; }

        /// <summary>true — звук передано виходу.</summary>
        public bool Play(SfxCue cue)
        {
            if (_sink == null || !Enabled || Paused)
            {
                return false;
            }

            _sink.Play(cue);
            return true;
        }
    }

    /// <summary>
    /// Глобальний доступ для кнопок UI, у яких немає посилання на контекст. Станом керує лише
    /// <see cref="Install"/>; без встановленого виходу всі запити тихо ігноруються.
    /// </summary>
    public static class SfxHub
    {
        private static SfxRouter _router = new SfxRouter(null);

        public static SfxRouter Router => _router;

        /// <summary>Підставляє вихід звуку (гра: AudioSource; тест: заглушка). null — тиша.</summary>
        public static SfxRouter Install(ISfxSink sink)
        {
            _router = new SfxRouter(sink);
            return _router;
        }

        public static bool Play(SfxCue cue)
        {
            return _router.Play(cue);
        }

        /// <summary>Обгортає дію кнопки: спершу звук тапа, потім сама дія.</summary>
        public static Action WithTapSound(Action action)
        {
            return () =>
            {
                Play(SfxCue.Tap);
                if (action != null)
                {
                    action();
                }
            };
        }
    }

    /// <summary>Лічильники подій зміни, за якими видно звуки.</summary>
    public struct SfxCounters : IEquatable<SfxCounters>
    {
        public long Placed;
        public long Served;
        public long Tips;
        public long Left;
        public long Burnt;

        public bool Equals(SfxCounters other)
        {
            return Placed == other.Placed && Served == other.Served && Tips == other.Tips
                && Left == other.Left && Burnt == other.Burnt;
        }

        public override bool Equals(object obj)
        {
            return obj is SfxCounters && Equals((SfxCounters)obj);
        }

        public override int GetHashCode()
        {
            return (int)(Placed * 31 + Served * 17 + Tips * 7 + Left * 3 + Burnt);
        }
    }

    /// <summary>
    /// Перетворює зміни лічильників симуляції на звуки (S1-13): м'ясо покладено — шипіння, страву
    /// оплачено — каса, чайові — радість, клієнт пішов — сумний, м'ясо пригоріло — сигнал. За один
    /// виклик кожен звук запитується не більше разу. Правил гри тут немає; пауза звуків не грає.
    /// </summary>
    public sealed class SfxDirector
    {
        private readonly Func<SfxCounters> _read;
        private readonly SfxRouter _router;
        private SfxCounters _last;

        public SfxDirector(Func<SfxCounters> read, SfxRouter router)
        {
            _read = read ?? throw new ArgumentNullException(nameof(read));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _last = _read();
        }

        /// <summary>Читає лічильники з драйвера зміни.</summary>
        public static SfxDirector ForDriver(ShiftDriver driver, SfxRouter router)
        {
            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            return new SfxDirector(() => Read(driver), router);
        }

        public static SfxCounters Read(ShiftDriver driver)
        {
            var counters = new SfxCounters();
            ShiftSimulation simulation = driver.Simulation;
            if (simulation.Grill != null)
            {
                counters.Placed = simulation.Grill.PlacedCount;
                counters.Burnt = simulation.Grill.BurntCount;
            }

            if (simulation.Counter != null)
            {
                counters.Served = simulation.Counter.ServedCount;
                counters.Tips = simulation.Counter.TipCoins;
                counters.Left = simulation.Counter.LeftCount;
            }

            return counters;
        }

        /// <summary>Викликається щокадру після кроку симуляції. <paramref name="paused"/> — гра на паузі.</summary>
        public void Update(bool paused)
        {
            SfxCounters now = _read();
            _router.Paused = paused;
            if (!paused)
            {
                if (now.Placed > _last.Placed)
                {
                    _router.Play(SfxCue.Sizzle);
                }

                if (now.Served > _last.Served)
                {
                    _router.Play(SfxCue.Cash);
                }

                if (now.Tips > _last.Tips)
                {
                    _router.Play(SfxCue.Happy);
                }

                if (now.Left > _last.Left)
                {
                    _router.Play(SfxCue.Sad);
                }

                if (now.Burnt > _last.Burnt)
                {
                    _router.Play(SfxCue.Burnt);
                }
            }

            _last = now;
        }
    }
}
