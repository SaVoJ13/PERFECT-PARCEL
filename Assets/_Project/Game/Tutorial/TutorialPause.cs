using System;

namespace KebabStreet.Game
{
    /// <summary>
    /// Q-05k (ADR-031): поки показано підказку навчання, зміна стоїть (<see cref="ShiftDriver.SetTutorialPause"/>), щоб
    /// гравець встиг прочитати. Пауза знімається, коли підказку закрито («Далі», «Пропустити все», виконано дію), а для
    /// кроку з дією - ще й першим тапом по грі (<see cref="ShiftDriver.Tap"/> знімає паузу підказки), інакше дію
    /// не виконати. Для одного кроку пауза після зняття вдруге не вмикається. Пауза гравця окрема й не зачіпається.
    /// Без UnityEngine; <see cref="Update"/> не виділяє пам'яті.
    /// </summary>
    public sealed class TutorialPause
    {
        private readonly ShiftDriver _driver;
        private readonly TutorialDirector _director;
        private TutorialStep _step;
        private bool _released;
        private bool _applied;

        public TutorialPause(ShiftDriver driver, TutorialDirector director)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _director = director ?? throw new ArgumentNullException(nameof(director));
        }

        /// <summary>Щокадру перед кроком симуляції.</summary>
        public void Update()
        {
            TutorialStep current = _director.Current;
            if (!ReferenceEquals(current, _step))
            {
                _step = current;
                _released = false;
                _applied = false;
            }

            if (_applied && !_driver.IsTutorialPaused)
            {
                _released = true; // паузу зняв тап гравця
                _applied = false;
            }

            bool want = current != null && !_released;
            if (want != _applied)
            {
                _driver.SetTutorialPause(want);
                _applied = want && _driver.IsTutorialPaused;
            }
        }
    }
}
