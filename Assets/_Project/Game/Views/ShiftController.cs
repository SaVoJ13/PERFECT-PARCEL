using KebabStreet.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Порожня зміна: рухає ShiftSimulation кадрами і показує таймер. Текст оновлюється лише
    /// коли змінилась секунда, щоб не створювати сміття щокадру.
    /// </summary>
    public sealed class ShiftController : ScreenController
    {
        private ShiftSimulation _shift;
        private Label _timer;
        private long _shownSeconds = -1;
        private ShiftState _shownState = ShiftState.Intro;
        private string _timeFormat;

        protected override void Build()
        {
            ILocalizer text = Context.Text;
            _timeFormat = text.Get(TextKeys.ShiftTimeLeft);
            _timer = UiFactory.Title(Container, string.Empty);
            UiFactory.AddButton(Container, text.Get(TextKeys.ShiftExit), OnExit);

            _shift = Context.NewShift();
            _shift.Start();
            Refresh();
        }

        private void Update()
        {
            if (_shift == null)
            {
                return;
            }

            if (_shift.State == ShiftState.Running)
            {
                _shift.Advance(Time.unscaledDeltaTime);
            }

            Refresh();
        }

        private void OnApplicationPause(bool paused)
        {
            if (_shift == null)
            {
                return;
            }

            if (paused)
            {
                _shift.Pause();
            }
            else
            {
                _shift.Resume();
            }
        }

        private void Refresh()
        {
            long seconds = EmptyShift.RemainingSeconds(_shift);
            if (seconds == _shownSeconds && _shift.State == _shownState)
            {
                return;
            }

            _shownSeconds = seconds;
            _shownState = _shift.State;
            bool finished = _shift.State == ShiftState.Won || _shift.State == ShiftState.Lost
                || _shift.State == ShiftState.Resolving;
            _timer.text = finished
                ? Context.Text.Get(TextKeys.ShiftFinished)
                : string.Format(_timeFormat, EmptyShift.FormatClock(seconds));
        }

        private void OnExit()
        {
            Context.Profile.Save();
            if (Context.Flow.ExitToMenu())
            {
                Open(GameFlow.SceneOf(Context.Flow.Current));
            }
        }
    }
}
