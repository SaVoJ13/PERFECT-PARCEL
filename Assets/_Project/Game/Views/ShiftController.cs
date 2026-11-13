using KebabStreet.Core;
using UnityEngine;

namespace KebabStreet.Game
{
    /// <summary>
    /// Зміна на реальному часі (S1-08, екран S1-09a): рівень, обраний у меню, запускається через
    /// ShiftDriver. Контролер лише передає час кадру й паузу; показ — <see cref="ShiftScreen"/>,
    /// тапи по грилю йдуть у <see cref="ShiftDriver.Tap"/>. Правил гри тут немає.
    /// Пауза гравця (кнопка) і пауза системи (згортання, втрата фокуса) складаються: гра йде, лише
    /// коли жодної немає.
    /// </summary>
    public sealed class ShiftController : ScreenController
    {
        private ShiftDriver _driver;
        private ShiftEnding _ending;
        private ShiftScreen _screen;
        private TutorialDirector _tutorial;
        private bool _userPaused;
        private bool _appPaused;
        private bool _hasFocus = true;

        protected override void Build()
        {
            if (Context.SelectedLevelNumber < 1 && !Context.SelectLevel(1))
            {
                GameLog.Info("Shift: немає жодного рівня");
                return;
            }

            LevelDefinition level = Context.Levels.FindByNumber(Context.SelectedLevelNumber);
            ulong seed = Context.NextShiftSeed();
            GameLog.Info("Shift: " + level.Id + " seed=" + seed + " спроба=" + Context.AttemptCount);

            var completion = new ShiftCompletion(Context.Profile, level);
            _driver = new ShiftDriver(level, seed, OnShiftFinished);
            _tutorial = TutorialDirector.Create(_driver, Context.Tutorial, Context.Profile.Data);
            _ending = new ShiftEnding(completion, Context.Profile, _tutorial.Model);
            _screen = new ShiftScreen(
                Container, _driver, Context.Text, OnPauseTap, OnResumeTap, OnExit, OnRetry, OnNext, Context.NextLevelNumber != 0, _tutorial, SaveTutorial);

            _driver.Begin();
            ApplyPause();
            _screen.Refresh();
        }

        private void Update()
        {
            if (_driver == null)
            {
                return;
            }

            _driver.Frame(Time.unscaledDeltaTime);
            _screen.Refresh();
        }

        private void OnApplicationPause(bool paused)
        {
            _appPaused = paused;
            if (paused)
            {
                SaveTutorial();
            }

            ApplyPause();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            ApplyPause();
        }

        private void OnPauseTap()
        {
            _userPaused = true;
            ApplyPause();
        }

        private void OnResumeTap()
        {
            _userPaused = false;
            ApplyPause();
        }

        private void ApplyPause()
        {
            if (_driver == null)
            {
                return;
            }

            _driver.SetPaused(_userPaused || _appPaused || !_hasFocus);
            _screen.Refresh();
        }

        private void OnShiftFinished(ShiftResult result)
        {
            SaveNotice notice = _ending.OnFinished(result);
            if (_screen != null)
            {
                _screen.ShowSaveNotice(notice);
            }
        }

        /// <summary>Зберігає профіль, якщо навчання щось у ньому змінило (крок побачено чи пропущено).</summary>
        private void SaveTutorial()
        {
            if (_ending != null)
            {
                _ending.SaveTutorialIfDirty();
            }
        }

        private void OnRetry()
        {
            if (_driver != null && _driver.IsFinished && Context.RetryShift())
            {
                Open(GameFlow.SceneOf(Context.Flow.Current));
            }
        }

        private void OnNext()
        {
            if (_driver != null && _driver.IsFinished && Context.AdvanceToNextLevel(_driver.Result.Won))
            {
                Open(GameFlow.SceneOf(Context.Flow.Current));
            }
        }

        private void OnExit()
        {
            SaveTutorial();
            if (Context.Flow.ExitToMenu())
            {
                Open(GameFlow.SceneOf(Context.Flow.Current));
            }
        }
    }
}
