using System;

namespace KebabStreet.Game
{
    /// <summary>Сила тактильного відгуку (QB-ALL-14): легкий «клік» або сильніший для важливих подій.</summary>
    public enum HapticKind
    {
        None = 0,
        Light = 1,
        Strong = 2,
    }

    /// <summary>
    /// Вихід вібрації (Q-05i): на Android - системні константи <c>HapticFeedbackConstants</c>, в редакторі й тестах - заглушка.
    /// Реалізація не знає про налаштування і ліміти: їх тримає <see cref="HapticsRouter"/>.
    /// </summary>
    public interface IHapticsService
    {
        /// <summary>Виконує один короткий відгук.</summary>
        void Perform(HapticKind kind);
    }

    /// <summary>Заглушка: нічого не робить (редактор, тести, збірки не під Android). Успіх не імітує.</summary>
    public sealed class NullHapticsService : IHapticsService
    {
        public void Perform(HapticKind kind)
        {
        }
    }

    /// <summary>Яким подіям (звуковим сигналам) відповідає який відгук і як часто їх можна повторювати.</summary>
    public static class HapticsCatalog
    {
        /// <summary>QB-ALL-14: події в реальному часі - не частіше 2 разів на секунду.</summary>
        public const double RealtimeIntervalSec = 0.5;

        /// <summary>Тапи гравця й оформлення результату: короткий захист від «дробу».</summary>
        public const double UserIntervalSec = 0.08;

        /// <summary>Різні події в межах цього вікна зливаються в один відгук (сильніший витісняє слабший).</summary>
        public const double GlobalGapSec = 0.05;

        /// <summary>Відгук для події; <see cref="HapticKind.None"/> - без вібрації (тіки, дзвіночок, монети, радість).</summary>
        public static HapticKind KindOf(SfxCue cue)
        {
            switch (cue)
            {
                case SfxCue.Tap:
                case SfxCue.Cash:
                case SfxCue.Sizzle:
                case SfxCue.Locked:
                    return HapticKind.Light;
                case SfxCue.Burnt:
                case SfxCue.BurntAlarm:
                case SfxCue.Sad:
                case SfxCue.StarChime:
                    return HapticKind.Strong;
                default:
                    return HapticKind.None;
            }
        }

        /// <summary>Подія симуляції в реальному часі (може йти частіше, ніж гравець встигає тапати).</summary>
        public static bool IsRealtime(SfxCue cue)
        {
            switch (cue)
            {
                case SfxCue.Cash:
                case SfxCue.Sizzle:
                case SfxCue.Burnt:
                case SfxCue.BurntAlarm:
                case SfxCue.Sad:
                    return true;
                default:
                    return false;
            }
        }

        public static double IntervalOf(SfxCue cue)
        {
            return IsRealtime(cue) ? RealtimeIntervalSec : UserIntervalSec;
        }
    }

    /// <summary>
    /// Єдина точка запиту вібрації: поважає перемикач «Вібрація», ліміти частоти (QB-ALL-14) і не вібрує без сервісу.
    /// Пауза відсікається вище - <see cref="SfxRouter.Play"/> не передає сюди події на паузі.
    /// </summary>
    public sealed class HapticsRouter
    {
        private readonly IHapticsService _service;
        private readonly Func<bool> _enabled;
        private readonly Func<double> _clock;
        private readonly double[] _lastCue = new double[16];
        private double _lastAt = -1000.0;
        private HapticKind _lastKind;

        public HapticsRouter(IHapticsService service, Func<bool> enabled, Func<double> clock = null)
        {
            _service = service;
            _enabled = enabled;
            _clock = clock ?? SfxClock.Now;
            for (int i = 0; i < _lastCue.Length; i++)
            {
                _lastCue[i] = -1000.0;
            }
        }

        /// <summary>Скільки відгуків реально передано сервісу.</summary>
        public int Performed { get; private set; }

        /// <summary>true - сервіс викликано.</summary>
        public bool Cue(SfxCue cue)
        {
            HapticKind kind = HapticsCatalog.KindOf(cue);
            if (kind == HapticKind.None || !Allowed())
            {
                return false;
            }

            int slot = (int)cue;
            double now = _clock();
            if (slot < 0 || slot >= _lastCue.Length || now - _lastCue[slot] < HapticsCatalog.IntervalOf(cue))
            {
                return false;
            }

            if (now - _lastAt < HapticsCatalog.GlobalGapSec && kind <= _lastKind)
            {
                return false;
            }

            _lastCue[slot] = now;
            return Fire(kind, now);
        }

        /// <summary>Підтвердження перемикача «Вібрація» (одразу після вмикання): легкий відгук без лімітів подій.</summary>
        public bool Confirm()
        {
            return Allowed() && Fire(HapticKind.Light, _clock());
        }

        private bool Allowed()
        {
            return _service != null && _enabled != null && _enabled();
        }

        private bool Fire(HapticKind kind, double now)
        {
            _lastAt = now;
            _lastKind = kind;
            Performed++;
            _service.Perform(kind);
            return true;
        }
    }

    /// <summary>Глобальний доступ до вібрації; без <see cref="Install"/> усе тихо ігнорується.</summary>
    public static class HapticsHub
    {
        private static HapticsRouter _router = new HapticsRouter(null, null);

        public static HapticsRouter Router => _router;

        /// <summary>Підставляє сервіс вібрації (гра: Android; тест: заглушка); null - без вібрації.</summary>
        public static HapticsRouter Install(IHapticsService service, Func<bool> enabled = null, Func<double> clock = null)
        {
            _router = new HapticsRouter(service, enabled ?? (() => GameSettings.Shared.HapticsEnabled), clock);
            return _router;
        }
    }
}
