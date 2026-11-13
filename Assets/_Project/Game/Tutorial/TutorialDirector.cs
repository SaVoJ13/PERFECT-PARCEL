using System;
using System.Collections.Generic;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>
    /// З'єднує зміну і навчання (S1-11): щокадру питає <see cref="TutorialEventTracker"/> про нові події,
    /// передає їх <see cref="TutorialModel"/> і рухає її час. Час — час зміни (такти симуляції), тож пауза
    /// гри зупиняє й підказки. Навчання лише читає симуляцію і ніколи не пише в неї. Без UnityEngine.
    /// </summary>
    public sealed class TutorialDirector
    {
        private const long MillisPerSecond = 1000L;

        private readonly ShiftDriver _driver;
        private readonly TutorialEventTracker _tracker;
        private readonly List<TutorialEvent> _events = new List<TutorialEvent>(8);

        public TutorialDirector(ShiftDriver driver, TutorialModel model)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _tracker = new TutorialEventTracker(driver.Simulation);
        }

        public TutorialModel Model { get; }

        /// <summary>Поточний крок або null.</summary>
        public TutorialStep Current => Model.Current;

        /// <summary>Час зміни, мс (за тактами симуляції).</summary>
        public long NowMs => _driver.Simulation.ElapsedTicks * MillisPerSecond / _driver.Simulation.Config.TicksPerSecond;

        /// <summary>Створює режисера для зміни; навчання на цьому рівні береться з конфігу (немає кроків — модель порожня).</summary>
        public static TutorialDirector Create(ShiftDriver driver, TutorialConfig config, SaveData profile)
        {
            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            return new TutorialDirector(driver, new TutorialModel(config, driver.Level.Id, profile));
        }

        /// <summary>Щокадру: нові події → модель, потім час.</summary>
        public void Update()
        {
            long now = NowMs;
            _tracker.Observe(_events);
            for (int i = 0; i < _events.Count; i++)
            {
                Model.Handle(_events[i], now);
            }

            _events.Clear();
            Model.Update(now);
        }

        public bool SkipStep()
        {
            return Model.SkipStep(NowMs);
        }

        public bool SkipAll()
        {
            return Model.SkipAll(NowMs);
        }
    }
}
