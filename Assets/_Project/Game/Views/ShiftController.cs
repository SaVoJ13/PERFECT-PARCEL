using KebabStreet.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Зміна на реальному часі (S1-08): рівень, обраний у меню, запускається через ShiftDriver.
    /// Контролер лише передає час кадру й паузу та показує таймер і мінімальний текстовий результат.
    /// Текст оновлюється, лише коли змінився (секунда, стан), щоб не створювати сміття щокадру.
    /// Станції й клієнти без графіки: це S1-09.
    /// </summary>
    public sealed class ShiftController : ScreenController
    {
        private ShiftDriver _driver;
        private ShiftCompletion _completion;
        private Label _timer;
        private Label _hint;
        private Label _resultTitle;
        private Label _resultStars;
        private Label _resultCoins;
        private long _shownSeconds = -1;
        private bool _shownPaused;
        private bool _shownHint;
        private bool _appPaused;
        private bool _hasFocus = true;
        private bool _resultShown;
        private string _timeFormat;
        private string _pausedText;

        protected override void Build()
        {
            ILocalizer text = Context.Text;
            if (Context.SelectedLevelNumber < 1 && !Context.SelectLevel(1))
            {
                GameLog.Info("Shift: немає жодного рівня");
                return;
            }

            LevelDefinition level = Context.Levels.FindByNumber(Context.SelectedLevelNumber);
            ulong seed = Context.NextShiftSeed();
            GameLog.Info("Shift: " + level.Id + " seed=" + seed + " спроба=" + Context.AttemptCount);

            _completion = new ShiftCompletion(Context.Profile, level);
            _driver = new ShiftDriver(level, seed, OnShiftFinished);
            _timeFormat = text.Get(TextKeys.ShiftTimeLeft);
            _pausedText = text.Get(TextKeys.ShiftPaused);

            UiFactory.Title(Container, string.Format(text.Get(TextKeys.ShiftLevelTitle), level.Number));
            _timer = UiFactory.Body(Container, string.Empty);
            _hint = UiFactory.Notice(Container, string.Empty);
            _resultTitle = UiFactory.Title(Container, string.Empty);
            _resultStars = UiFactory.Body(Container, string.Empty);
            _resultCoins = UiFactory.Body(Container, string.Empty);
            UiFactory.AddButton(Container, text.Get(TextKeys.ShiftExit), OnExit);

            _driver.Begin();
            Refresh();
        }

        private void Update()
        {
            if (_driver == null)
            {
                return;
            }

            _driver.Frame(Time.unscaledDeltaTime);
            Refresh();
        }

        private void OnApplicationPause(bool paused)
        {
            _appPaused = paused;
            ApplyPause();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            ApplyPause();
        }

        private void ApplyPause()
        {
            if (_driver != null)
            {
                _driver.SetPaused(_appPaused || !_hasFocus);
            }
        }

        private void Refresh()
        {
            ShiftSimulation sim = _driver.Simulation;
            long seconds = EmptyShift.RemainingSeconds(sim);
            if (seconds != _shownSeconds || _driver.IsPaused != _shownPaused)
            {
                _shownSeconds = seconds;
                _shownPaused = _driver.IsPaused;
                string clock = string.Format(_timeFormat, EmptyShift.FormatClock(seconds));
                _timer.text = _driver.IsPaused ? _pausedText + " - " + clock : clock;
            }

            if (_driver.GoalDoneStarHint != _shownHint)
            {
                _shownHint = _driver.GoalDoneStarHint;
                _hint.text = _shownHint ? Context.Text.Get(TextKeys.ShiftGoalDoneStar2) : string.Empty;
            }

            if (_driver.IsFinished && !_resultShown)
            {
                _resultShown = true;
                ShowResult(_driver.Result);
            }
        }

        private void OnShiftFinished(ShiftResult result)
        {
            _completion.Apply(result);
        }

        private void ShowResult(ShiftResult result)
        {
            ILocalizer text = Context.Text;
            _resultTitle.text = text.Get(result.Won ? TextKeys.ShiftResultWon : TextKeys.ShiftResultLost);
            _resultStars.text = string.Format(text.Get(TextKeys.ShiftResultStars), result.Stars);
            _resultCoins.text = string.Format(text.Get(TextKeys.ShiftResultCoins), result.RewardCoins);
        }

        private void OnExit()
        {
            if (Context.Flow.ExitToMenu())
            {
                Open(GameFlow.SceneOf(Context.Flow.Current));
            }
        }
    }
}
