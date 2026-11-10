using System;

namespace KebabStreet.Core
{
    /// <summary>
    /// Симуляція однієї зміни: стани, таймер і генератор випадковості.
    /// Гарантії детермінізму: час зберігається в цілих тактах; однаковий seed, однакова
    /// конфігурація та однакова послідовність команд і викликів Tick/Advance дають
    /// однакові знімки стану на кожному кроці. Клас не читає системного часу і не має
    /// статичного стану. Команда в неправильному стані повертає false і нічого не змінює.
    /// </summary>
    public sealed class ShiftSimulation
    {
        // Роздільна здатність накопичувача Advance: мільйонна частка такту.
        private const long SubTicksPerTick = 1000000L;

        private readonly IShiftGoal _goal;
        private readonly SplitMix64Random _random;
        private long _endTick;
        private long _pendingSubTicks;

        /// <summary>
        /// Створює зміну в стані Intro. Непридатна конфігурація — ArgumentException
        /// з переліком помилок (див. <see cref="ShiftConfig.GetErrors"/>).
        /// </summary>
        public ShiftSimulation(ShiftConfig config, ulong seed, IShiftGoal goal)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _goal = goal ?? throw new ArgumentNullException(nameof(goal));

            var errors = config.GetErrors();
            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    "Непридатна конфігурація зміни: " + string.Join("; ", errors), nameof(config));
            }

            Seed = seed;
            _random = new SplitMix64Random(seed);
            _endTick = config.DurationTicks;
            State = ShiftState.Intro;
        }

        /// <summary>Конфігурація, з якою створено зміну.</summary>
        public ShiftConfig Config { get; }

        /// <summary>Seed генератора цієї зміни.</summary>
        public ulong Seed { get; }

        /// <summary>Поточний стан зміни.</summary>
        public ShiftState State { get; private set; }

        /// <summary>Скільки тактів зміна відпрацювала (росте лише в Running).</summary>
        public long ElapsedTicks { get; private set; }

        /// <summary>Скільки тактів лишилось до кінця таймера; ніколи не від'ємне.</summary>
        public long RemainingTicks => _endTick - ElapsedTicks;

        /// <summary>Чи використано продовження в цій спробі.</summary>
        public bool ContinueUsed { get; private set; }

        /// <summary>Єдиний генератор випадковості зміни; підсистеми беруть випадковість лише з нього.</summary>
        public IRandom Random => _random;

        /// <summary>Intro → Running.</summary>
        public bool Start()
        {
            return Transition(ShiftState.Intro, ShiftState.Running);
        }

        /// <summary>Running → Paused. Накопичений залишок часу зберігається.</summary>
        public bool Pause()
        {
            return Transition(ShiftState.Running, ShiftState.Paused);
        }

        /// <summary>Paused → Running.</summary>
        public bool Resume()
        {
            return Transition(ShiftState.Paused, ShiftState.Running);
        }

        /// <summary>
        /// Продовження після поразки: Lost → Running з додатковим часом із конфігурації.
        /// Лише раз за спробу; якщо додатковий час дорівнює 0 — недоступне.
        /// </summary>
        public bool Continue()
        {
            if (State != ShiftState.Lost || ContinueUsed || Config.ContinueBonusTicks <= 0)
            {
                return false;
            }

            ContinueUsed = true;
            _endTick += Config.ContinueBonusTicks;
            State = ShiftState.Running;
            return true;
        }

        /// <summary>
        /// Виконує рівно один такт. Поза Running нічого не робить і повертає false.
        /// На такті, де час вийшов, зміна проходить Resolving і стає Won або Lost.
        /// </summary>
        public bool Tick()
        {
            if (State != ShiftState.Running)
            {
                return false;
            }

            ElapsedTicks++;
            if (ElapsedTicks >= _endTick)
            {
                _pendingSubTicks = 0;
                State = ShiftState.Resolving;
                State = _goal.IsAchieved(this) ? ShiftState.Won : ShiftState.Lost;
            }

            return true;
        }

        /// <summary>
        /// Приймає реальний час кадру і виконує такти, що назбиралися; повертає їх кількість.
        /// Час округлюється до мільйонної частки такту і накопичується цілим числом, тому
        /// для кадрів, кратних цій частці, результат не залежить від нарізки на кадри.
        /// За один виклик виконується щонайбільше MaxCatchUpTicks тактів, надлишок відкидається.
        /// Поза Running час ігнорується. NaN або від'ємне значення — ArgumentOutOfRangeException.
        /// </summary>
        public int Advance(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || deltaSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaSeconds), "Час кадру має бути невід'ємним числом.");
            }

            if (State != ShiftState.Running)
            {
                return 0;
            }

            int maxTicks = Config.MaxCatchUpTicks;
            double exactTicks = deltaSeconds * Config.TicksPerSecond;
            long dueTicks = maxTicks + 1L;

            // Час, явно більший за межу, у ціле не переводимо (захист від переповнення);
            // точне порівняння з межею нижче — вже в цілих числах.
            if (exactTicks < dueTicks)
            {
                _pendingSubTicks += (long)Math.Round(exactTicks * SubTicksPerTick);
                dueTicks = _pendingSubTicks / SubTicksPerTick;
                _pendingSubTicks %= SubTicksPerTick;
            }

            if (dueTicks > maxTicks)
            {
                dueTicks = maxTicks;
                _pendingSubTicks = 0;
            }

            int executed = 0;
            while (executed < dueTicks && Tick())
            {
                executed++;
            }

            return executed;
        }

        /// <summary>Знімок повного стану для перевірки детермінізму.</summary>
        public ShiftSnapshot CaptureSnapshot()
        {
            return new ShiftSnapshot(
                State, ElapsedTicks, RemainingTicks, _pendingSubTicks, ContinueUsed, _random.State);
        }

        private bool Transition(ShiftState from, ShiftState to)
        {
            if (State != from)
            {
                return false;
            }

            State = to;
            return true;
        }
    }
}
