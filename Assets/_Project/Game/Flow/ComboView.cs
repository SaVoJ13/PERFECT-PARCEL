using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>Чому комбо скинулось (Q-11). Числові значення — номери в таблицях показу.</summary>
    public enum ComboLostReason
    {
        None = 0,

        /// <summary>Вікно комбо спливло: не встигли видати наступну страву.</summary>
        Expired = 1,

        /// <summary>Клієнтові запропонували хибну страву.</summary>
        WrongDish = 2,

        /// <summary>Клієнт пішов, не дочекавшись.</summary>
        CustomerLeft = 3,
    }

    /// <summary>Що показати про комбо в цей кадр: смужка вікна, «наступний бонус». Лише значення, без правил.</summary>
    public readonly struct ComboHudView : IEquatable<ComboHudView>
    {
        public ComboHudView(long combo, bool barShown, int barPercent, bool warning, long nextBonusPercent, bool atMax)
        {
            Combo = combo;
            BarShown = barShown;
            BarPercent = barPercent;
            Warning = warning;
            NextBonusPercent = nextBonusPercent;
            AtMax = atMax;
        }

        /// <summary>Поточне комбо Core.</summary>
        public long Combo { get; }

        /// <summary>Показати смужку вікна: ланцюг уже почався і комбо доступне за конфігом.</summary>
        public bool BarShown { get; }

        /// <summary>Скільки вікна лишилось, 0..100 %.</summary>
        public int BarPercent { get; }

        /// <summary>Вікно спливає (останні <see cref="ComboView.WarnMs"/>): смужка мерехтить.</summary>
        public bool Warning { get; }

        /// <summary>Бонус до плати за наступну видачу, % (як його рахує Core: крок × число кроків, до межі).</summary>
        public long NextBonusPercent { get; }

        /// <summary>Бонус уже на межі (comboMaxSteps): далі не росте.</summary>
        public bool AtMax { get; }

        public bool Equals(ComboHudView other)
        {
            return Combo == other.Combo && BarShown == other.BarShown && BarPercent == other.BarPercent
                && Warning == other.Warning && NextBonusPercent == other.NextBonusPercent && AtMax == other.AtMax;
        }

        public override bool Equals(object obj)
        {
            return obj is ComboHudView other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Combo.GetHashCode();
                hash = (hash * 397) ^ (BarShown ? 1 : 0);
                hash = (hash * 397) ^ BarPercent;
                hash = (hash * 397) ^ (Warning ? 1 : 0);
                hash = (hash * 397) ^ NextBonusPercent.GetHashCode();
                hash = (hash * 397) ^ (AtMax ? 1 : 0);
                return hash;
            }
        }
    }

    /// <summary>Константи й чисті функції показу комбо (Q-11). Вигляд, не баланс: числа комбо беруться з конфігу Core.</summary>
    public static class ComboView
    {
        /// <summary>Смужка вікна показується від першої вдалої видачі (ланцюг почався).</summary>
        public const long BarShownFrom = 1L;

        /// <summary>Іскра над клієнтом при комбо від ×3.</summary>
        public const long SparksFrom = 3L;

        /// <summary>Мерехтіння смужки в останні мс вікна (P3_SPEC 3.1).</summary>
        public const int WarnMs = 2000;

        /// <summary>Скільки показувати причину скидання, мс часу зміни.</summary>
        public const int LostShowMs = 2000;

        /// <summary>Комбо доступне за конфігом (вікно, крок і межа задані); інакше нічого про нього не показуємо.</summary>
        public static bool IsAvailable(CounterConfig config)
        {
            return config != null && config.ComboWindowMs > 0 && config.ComboStepPercent > 0 && config.ComboMaxSteps > 0;
        }

        /// <summary>Бонус за видачу при комбо <paramref name="combo"/> (як <c>Counter.Pay</c>), %.</summary>
        public static long BonusPercentAt(long combo, CounterConfig config)
        {
            long steps = Math.Min(Math.Max(combo - 1L, 0L), config.ComboMaxSteps);
            return steps * config.ComboStepPercent;
        }

        /// <summary>Бонус наступної видачі (комбо стане на 1 більше), %.</summary>
        public static long NextBonusPercent(long combo, CounterConfig config)
        {
            return BonusPercentAt(combo + 1L, config);
        }

        /// <summary>Бонус уже на межі: наступна видача дасть стільки ж.</summary>
        public static bool IsAtMax(long combo, CounterConfig config)
        {
            return combo >= config.ComboMaxSteps + 1L;
        }

        /// <summary>Скільки вікна лишилось, 0..100 %: <paramref name="sinceServeTicks"/> — тактів від останньої видачі.</summary>
        public static int BarPercent(long sinceServeTicks, long windowTicks)
        {
            if (windowTicks <= 0L)
            {
                return 0;
            }

            long left = windowTicks - sinceServeTicks;
            if (left <= 0L)
            {
                return 0;
            }

            if (left >= windowTicks)
            {
                return 100;
            }

            return (int)((left * 100L + (windowTicks / 2L)) / windowTicks);
        }

        /// <summary>Ключ тексту причини скидання; null — не показувати.</summary>
        public static string LostTextKey(ComboLostReason reason)
        {
            switch (reason)
            {
                case ComboLostReason.Expired:
                    return TextKeys.ComboLostExpired;
                case ComboLostReason.WrongDish:
                    return TextKeys.ComboLostWrongDish;
                case ComboLostReason.CustomerLeft:
                    return TextKeys.ComboLostLeft;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Стежить за комбо між кадрами (Q-11): знімок для смужки вікна і причина скидання (спливло / хибна страва /
    /// клієнт пішов). Лише читає публічні поля Core (<c>Combo</c>, <c>LastServeTick</c>, <c>WrongDishes</c>,
    /// <c>LeftCount</c>), нічого не рахує замість Core. Без UnityEngine; <see cref="Update"/> не виділяє пам'яті.
    /// </summary>
    public sealed class ComboTracker
    {
        private readonly CounterConfig _config;
        private readonly bool _available;
        private readonly long _windowTicks;
        private readonly long _warnTicks;
        private readonly long _lostTicks;
        private long _prevCombo;
        private long _prevWrong;
        private long _prevLeft;
        private long _lostUntilTick;

        public ComboTracker(ShiftSimulation simulation)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            Counter counter = simulation.Counter;
            _config = counter == null ? null : counter.Config;
            _available = ComboView.IsAvailable(_config);
            int tps = simulation.Config.TicksPerSecond;
            if (_available)
            {
                _windowTicks = TickTime.FromMs(_config.ComboWindowMs, tps);
            }

            _warnTicks = TickTime.FromMs(ComboView.WarnMs, tps);
            _lostTicks = TickTime.FromMs(ComboView.LostShowMs, tps);
            if (counter != null)
            {
                _prevCombo = counter.Combo;
                _prevWrong = counter.WrongDishes;
                _prevLeft = counter.LeftCount;
            }
        }

        /// <summary>Комбо доступне на цьому рівні за конфігом.</summary>
        public bool Available => _available;

        /// <summary>Останній знімок смужки.</summary>
        public ComboHudView Hud { get; private set; }

        /// <summary>Причина скидання, яку зараз показувати (None — нічого).</summary>
        public ComboLostReason Lost { get; private set; }

        /// <summary>Скільки разів комбо скидалося (для одноразових ефектів показу).</summary>
        public int LostCount { get; private set; }

        /// <summary>Звіряє комбо з попереднім кадром. Викликається щокадру.</summary>
        public void Update(ShiftSimulation simulation)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            Counter counter = simulation.Counter;
            if (!_available || counter == null)
            {
                Hud = default(ComboHudView);
                Lost = ComboLostReason.None;
                return;
            }

            long now = simulation.ElapsedTicks;
            long combo = counter.Combo;
            if (_prevCombo > 0L && combo == 0L)
            {
                if (counter.WrongDishes != _prevWrong)
                {
                    Lost = ComboLostReason.WrongDish;
                }
                else if (counter.LeftCount != _prevLeft)
                {
                    Lost = ComboLostReason.CustomerLeft;
                }
                else
                {
                    Lost = ComboLostReason.Expired;
                }

                _lostUntilTick = now + _lostTicks;
                LostCount++;
            }
            else if (combo > 0L || now >= _lostUntilTick)
            {
                Lost = ComboLostReason.None;
            }

            _prevCombo = combo;
            _prevWrong = counter.WrongDishes;
            _prevLeft = counter.LeftCount;

            bool bar = combo >= ComboView.BarShownFrom;
            long since = bar && counter.LastServeTick >= 0L ? now - counter.LastServeTick : 0L;
            int percent = bar ? ComboView.BarPercent(since, _windowTicks) : 0;
            bool warning = bar && _windowTicks - since <= _warnTicks;
            Hud = new ComboHudView(
                combo,
                bar,
                percent,
                warning,
                bar ? ComboView.NextBonusPercent(combo, _config) : 0L,
                bar && ComboView.IsAtMax(combo, _config));
        }
    }
}
