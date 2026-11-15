using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>
    /// Звуки гри: що саме сталося. Перші шість — файли з <c>Resources/Audio</c> (S1-13), решта синтезується кодом
    /// (Q-05g, <see cref="SfxSynth"/>) без нових файлів. Без UnityEngine.
    /// </summary>
    public enum SfxCue
    {
        Sizzle = 1,
        Cash = 2,
        Happy = 3,
        Sad = 4,
        Burnt = 5,
        Tap = 6,
        TimerTick = 7,
        TimerTickUrgent = 8,
        StarChime = 9,
        CoinTick = 10,
        Locked = 11,
        BurntAlarm = 12,
        Bell = 13,
    }

    /// <summary>Петлі: шипіння гриля (шина «Ефекти») і музика зміни (шина «Музика»).</summary>
    public enum SfxLoop
    {
        Grill = 1,
        Music = 2,
    }

    /// <summary>Куди йдуть звуки: у грі — AudioSource, у тестах — заглушка.</summary>
    public interface ISfxSink
    {
        /// <summary>Одноразовий звук; <paramref name="pitch"/> — множник висоти (1 = без змін).</summary>
        void Play(SfxCue cue, float pitch);

        /// <summary>Вмикає/вимикає петлю.</summary>
        void SetLoop(SfxLoop loop, bool on);

        /// <summary>Гучності шин (0..1): «Ефекти» і «Музика» (музика вже з урахуванням ducking).</summary>
        void SetVolumes(float effects, float music);
    }

    /// <summary>Імена ресурсів звуків у <c>Resources/Audio</c> (без розширення); тест звіряє їх із файлами й реєстром.</summary>
    public static class SfxCatalog
    {
        /// <summary>Тека звуків відносно Resources.</summary>
        public const string AudioFolder = "Audio/";

        /// <summary>QB-ALL-03: той самий звук не частіше 1 разу на 50 мс.</summary>
        public const double MinIntervalSec = 0.05;

        /// <summary>QB-RES-03: «тік-тік» монет не частіше 15 разів на секунду.</summary>
        public const double CoinTickIntervalSec = 1.0 / 15.0;

        /// <summary>QB-ALL-15: не більше 3 одночасних копій одного звуку.</summary>
        public const int MaxConcurrent = 3;

        /// <summary>Орієнтовна тривалість файлових звуків для обліку одночасних копій (файли ≤ 1 с, тест).</summary>
        public const double FileDurationSec = 0.4;

        /// <summary>Тап — найкоротший звук: ~0,1 с, щоб швидкі натискання не відсікалися лімітом копій.</summary>
        public const double TapDurationSec = 0.1;

        /// <summary>QB-SHIFT-02: тік таймера в останні 10 с зміни (збігається з <c>UiMotion.TimerWarningSec</c>, тест).</summary>
        public const long TimerTickFromSec = 10;

        /// <summary>QB-SHIFT-02: за 3 с тік гучніший.</summary>
        public const long TimerUrgentFromSec = 3;

        public static readonly string[] AllNames =
        {
            "sfx_sizzle", "sfx_cash", "sfx_happy", "sfx_sad", "sfx_burnt", "sfx_tap",
        };

        /// <summary>Ім'я файлового ресурсу звуку; null — для невідомого значення і для синтезованих звуків.</summary>
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

        /// <summary>Звук синтезується кодом (немає файлу).</summary>
        public static bool IsSynth(SfxCue cue)
        {
            return cue >= SfxCue.TimerTick && cue <= SfxCue.Bell;
        }

        /// <summary>Звук відомий грі (файловий або синтезований).</summary>
        public static bool IsKnown(SfxCue cue)
        {
            return NameOf(cue) != null || IsSynth(cue);
        }

        /// <summary>Мінімальний інтервал між двома запитами цього звуку.</summary>
        public static double IntervalOf(SfxCue cue)
        {
            return cue == SfxCue.CoinTick ? CoinTickIntervalSec : MinIntervalSec;
        }

        /// <summary>Тривалість звуку, с (для обліку одночасних копій).</summary>
        public static double DurationOf(SfxCue cue)
        {
            if (IsSynth(cue))
            {
                return SfxSynth.DurationSec(cue);
            }

            return cue == SfxCue.Tap ? TapDurationSec : FileDurationSec;
        }

        /// <summary>Чи варіювати висоту повторюваного звуку на ±3-6% (QB-ALL-15): лише «подієві» звуки, не тони з сенсом висоти.</summary>
        public static bool JitterPitch(SfxCue cue)
        {
            switch (cue)
            {
                case SfxCue.Sizzle:
                case SfxCue.Cash:
                case SfxCue.Happy:
                case SfxCue.Sad:
                case SfxCue.Tap:
                case SfxCue.CoinTick:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Важливі звуки, під які музика приглушується (ducking).</summary>
        public static bool DucksMusic(SfxCue cue)
        {
            switch (cue)
            {
                case SfxCue.StarChime:
                case SfxCue.Burnt:
                case SfxCue.BurntAlarm:
                case SfxCue.Locked:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Єдина точка запиту звуків і мікс: шини «Ефекти»/«Музика», ducking музики, ліміт частоти й одночасних копій,
    /// варіація висоти. Вимкнено, на паузі або без виходу — звук не запитується (false).
    /// </summary>
    public sealed class SfxRouter
    {
        /// <summary>QB-ALL-15: приглушення музики 6-10 дБ; береться середина (8 дБ = x0,40).</summary>
        public const float DuckGain = 0.40f;

        /// <summary>Тривалість ducking після важливого звуку, с.</summary>
        public const float DuckHoldSec = 0.8f;

        /// <summary>Швидкість зміни коефіцієнта ducking, 1/с (плавно, без клацань).</summary>
        public const float DuckRate = 6f;

        private readonly ISfxSink _sink;
        private readonly Func<double> _clock;
        private readonly SfxLimiter _limiter = new SfxLimiter();
        private readonly bool[] _wanted = new bool[3];
        private readonly bool[] _applied = new bool[3];
        private bool _enabled = true;
        private bool _paused;
        private float _effects = 1f;
        private float _music = 1f;
        private float _duckGain = 1f;
        private double _duckUntil = -1.0;
        private int _jitterCounter;
        private float _sentEffects = -1f;
        private float _sentMusic = -1f;

        public SfxRouter(ISfxSink sink, Func<double> clock = null)
        {
            _sink = sink;
            _clock = clock ?? SfxClock.Now;
        }

        /// <summary>Звук увімкнено (за замовчуванням так).</summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; Refresh(); }
        }

        /// <summary>Гра на паузі: звуки не запитуються, петлі стоять.</summary>
        public bool Paused
        {
            get { return _paused; }
            set { _paused = value; Refresh(); }
        }

        /// <summary>Гучність шини «Ефекти», 0..1.</summary>
        public float EffectsVolume
        {
            get { return _effects; }
            set { _effects = Clamp01(value); Refresh(); }
        }

        /// <summary>Гучність шини «Музика», 0..1.</summary>
        public float MusicVolume
        {
            get { return _music; }
            set { _music = Clamp01(value); Refresh(); }
        }

        /// <summary>Поточний коефіцієнт ducking музики (1 — без приглушення).</summary>
        public float DuckFactor => _duckGain;

        /// <summary>Чи має музика зараз бути приглушена.</summary>
        public bool IsDucking => _clock() < _duckUntil;

        /// <summary>true — звук передано виходу. <paramref name="step"/> — кроки по два півтони вгору (зростаючий «дзинь»).</summary>
        public bool Play(SfxCue cue, int step = 0)
        {
            if (_sink == null || !_enabled || _paused || !SfxCatalog.IsKnown(cue) || _effects <= 0f)
            {
                return false;
            }

            double now = _clock();
            if (!_limiter.TryAcquire(cue, now, SfxCatalog.IntervalOf(cue), SfxCatalog.DurationOf(cue), SfxCatalog.MaxConcurrent))
            {
                return false;
            }

            float pitch = SfxPitch.Steps(step);
            if (SfxCatalog.JitterPitch(cue))
            {
                pitch *= SfxPitch.Jitter(_jitterCounter++);
            }

            _sink.Play(cue, pitch);
            if (SfxCatalog.DucksMusic(cue))
            {
                Duck(DuckHoldSec);
            }

            return true;
        }

        /// <summary>Приглушує музику на <paramref name="seconds"/> (спливне вікно, навчальна репліка).</summary>
        public void Duck(float seconds)
        {
            double until = _clock() + seconds;
            if (until > _duckUntil)
            {
                _duckUntil = until;
            }
        }

        /// <summary>Побажання петлі: фактично вона грає, лише коли звук увімкнено, гра не на паузі й гучність шини більша за 0.</summary>
        public void SetLoop(SfxLoop loop, bool wanted)
        {
            _wanted[(int)loop] = wanted;
            Refresh();
        }

        /// <summary>Чи побажана петля (не те саме, що чутна).</summary>
        public bool IsLoopWanted(SfxLoop loop)
        {
            return _wanted[(int)loop];
        }

        /// <summary>Чи петля передана виходу як увімкнена.</summary>
        public bool IsLoopPlaying(SfxLoop loop)
        {
            return _applied[(int)loop];
        }

        /// <summary>Знімає всі побажання петель (вихід зі зміни).</summary>
        public void StopLoops()
        {
            _wanted[(int)SfxLoop.Grill] = false;
            _wanted[(int)SfxLoop.Music] = false;
            Refresh();
        }

        /// <summary>Щокадру: плавно рухає коефіцієнт ducking до цілі.</summary>
        public void Tick(float dtSec)
        {
            float target = IsDucking ? DuckGain : 1f;
            float step = DuckRate * dtSec;
            if (_duckGain > target)
            {
                _duckGain = Math.Max(target, _duckGain - step);
            }
            else if (_duckGain < target)
            {
                _duckGain = Math.Min(target, _duckGain + step);
            }

            Refresh();
        }

        private void Refresh()
        {
            if (_sink == null)
            {
                return;
            }

            float effects = _enabled ? _effects : 0f;
            float music = _enabled ? _music * _duckGain : 0f;
            if (effects != _sentEffects || music != _sentMusic)
            {
                _sentEffects = effects;
                _sentMusic = music;
                _sink.SetVolumes(effects, music);
            }

            Apply(SfxLoop.Grill, _wanted[(int)SfxLoop.Grill] && _enabled && !_paused && _effects > 0f);
            Apply(SfxLoop.Music, _wanted[(int)SfxLoop.Music] && _enabled && !_paused && _music > 0f);
        }

        private void Apply(SfxLoop loop, bool on)
        {
            if (_applied[(int)loop] != on)
            {
                _applied[(int)loop] = on;
                _sink.SetLoop(loop, on);
            }
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
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
        public static SfxRouter Install(ISfxSink sink, Func<double> clock = null)
        {
            _router = new SfxRouter(sink, clock);
            return _router;
        }

        public static bool Play(SfxCue cue)
        {
            return _router.Play(cue);
        }

        /// <summary>Звук зі зсувом висоти на <paramref name="step"/> кроків (зростаючий «дзинь» зірок).</summary>
        public static bool Play(SfxCue cue, int step)
        {
            return _router.Play(cue, step);
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

        /// <summary>Скільки клієнтів уже прийшло (дзвіночок нового клієнта).</summary>
        public long Arrived;

        /// <summary>Секунд до кінця зміни, з округленням угору; 0 — час вийшов.</summary>
        public long RemainingSec;

        /// <summary>Скільки шматків зараз смажиться (сире м'ясо) — для петлі шипіння.</summary>
        public int Cooking;

        /// <summary>Біти слотів гриля, де м'ясо «майже пригоріле» (вікно попередження).</summary>
        public int WarnMask;

        public bool Equals(SfxCounters other)
        {
            return Placed == other.Placed && Served == other.Served && Tips == other.Tips
                && Left == other.Left && Burnt == other.Burnt && Arrived == other.Arrived
                && RemainingSec == other.RemainingSec && Cooking == other.Cooking && WarnMask == other.WarnMask;
        }

        public override bool Equals(object obj)
        {
            return obj is SfxCounters && Equals((SfxCounters)obj);
        }

        public override int GetHashCode()
        {
            return (int)(Placed * 31 + Served * 17 + Tips * 7 + Left * 3 + Burnt + Arrived * 13 + RemainingSec * 5 + Cooking * 11 + WarnMask);
        }
    }

    /// <summary>
    /// Перетворює зміни лічильників симуляції на звуки (S1-13, Q-05g): м'ясо покладено — шипіння, страву
    /// оплачено — каса, чайові — радість, клієнт пішов — сумний, м'ясо пригоріло — сигнал, новий клієнт — дзвіночок,
    /// м'ясо «майже пригоріле» — «пшш», останні 10 с — тік. Петлі: шипіння (лише поки щось смажиться) і музика зміни.
    /// За один виклик кожен звук запитується не більше разу. Правил гри тут немає; пауза звуків не грає.
    /// </summary>
    public sealed class SfxDirector
    {
        private readonly Func<SfxCounters> _read;
        private readonly SfxRouter _router;
        private SfxCounters _last;
        private bool _stopped;

        public SfxDirector(Func<SfxCounters> read, SfxRouter router)
        {
            _read = read ?? throw new ArgumentNullException(nameof(read));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _last = _read();
        }

        /// <summary>Чи грає музика зміни (за замовчуванням так; гучність — шина «Музика»).</summary>
        public bool MusicOn { get; set; } = true;

        /// <summary>Зміна закінчилась: петлі (шипіння, музика) вимкнено, разові звуки ще можуть грати.</summary>
        public void Stop()
        {
            MusicOn = false;
            _stopped = true;
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
            counters.RemainingSec = EmptyShift.RemainingSeconds(simulation);
            if (simulation.Grill != null)
            {
                counters.Placed = simulation.Grill.PlacedCount;
                counters.Burnt = simulation.Grill.BurntCount;
                int slots = simulation.Grill.SlotCount;
                for (int i = 0; i < slots; i++)
                {
                    GrillSlot slot = simulation.Grill.GetSlot(i);
                    if (slot.State == MeatState.Raw)
                    {
                        counters.Cooking++;
                    }

                    if (slot.IsBurnWarning && i < 31)
                    {
                        counters.WarnMask |= 1 << i;
                    }
                }
            }

            if (simulation.Counter != null)
            {
                counters.Served = simulation.Counter.ServedCount;
                counters.Tips = simulation.Counter.TipCoins;
                counters.Left = simulation.Counter.LeftCount;
                counters.Arrived = simulation.Counter.ArrivedCount;
            }

            return counters;
        }

        /// <summary>Викликається щокадру після кроку симуляції. <paramref name="paused"/> — гра на паузі.</summary>
        public void Update(bool paused)
        {
            Update(paused, 0f);
        }

        /// <summary>Те саме, з часом кадру для плавного ducking музики.</summary>
        public void Update(bool paused, float dtSec)
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

                if (now.Arrived > _last.Arrived)
                {
                    _router.Play(SfxCue.Bell);
                }

                if ((now.WarnMask & ~_last.WarnMask) != 0)
                {
                    _router.Play(SfxCue.BurntAlarm);
                }

                if (now.RemainingSec < _last.RemainingSec && now.RemainingSec >= 1 && now.RemainingSec <= SfxCatalog.TimerTickFromSec)
                {
                    _router.Play(now.RemainingSec <= SfxCatalog.TimerUrgentFromSec ? SfxCue.TimerTickUrgent : SfxCue.TimerTick);
                }
            }

            _router.SetLoop(SfxLoop.Grill, !_stopped && now.Cooking > 0);
            _router.SetLoop(SfxLoop.Music, MusicOn);
            _router.Tick(dtSec);
            _last = now;
        }
    }
}
