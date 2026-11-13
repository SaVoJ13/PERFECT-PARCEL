using System;
using System.Collections.Generic;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>Стан кроку навчання в цій зміні.</summary>
    public enum TutorialStepState
    {
        /// <summary>Умова показу ще не настала.</summary>
        Pending = 0,

        /// <summary>Подія показу була, крок чекає паузи між підказками (minGapSec).</summary>
        Armed = 1,

        /// <summary>Крок показано гравцю зараз.</summary>
        Active = 2,

        /// <summary>Крок завершено: виконано, пропущено, зник за часом, або вже був у профілі.</summary>
        Done = 3,
    }

    /// <summary>
    /// Модель навчання одного рівня (S1-11), без UnityEngine. Веде кроки з tutorial.json за подіями зміни:
    /// по одному кроку одночасно, пауза <c>minGapSec</c> між кроками, <c>oncePerProfile</c>, «пропустити крок»
    /// і «пропустити все». Час — лише мілісекунди часу зміни, які передає викликач (пауза гри зупиняє й навчання).
    /// Навчання не втручається в симуляцію: воно тільки читає події й пам'ятає, що гравець уже бачив, у
    /// <see cref="SaveData"/> (<see cref="SaveData.TutorialSeen"/>, <see cref="SaveData.TutorialSkipAll"/>).
    /// Збереження файла — справа викликача (див. <see cref="Dirty"/>).
    /// </summary>
    public sealed class TutorialModel
    {
        private const long NoTime = -1L;

        private readonly TutorialConfig _config;
        private readonly SaveData _profile;
        private readonly TutorialStep[] _steps;
        private readonly TutorialStepState[] _states;
        private int _active = -1;
        private long _shownAtMs;
        private long _lastEndMs = NoTime;

        public TutorialModel(TutorialConfig config, string levelId, SaveData profile)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            LevelId = levelId ?? throw new ArgumentNullException(nameof(levelId));
            _steps = config.StepsFor(levelId).ToArray();
            _states = new TutorialStepState[_steps.Length];
            IsSkippedAll = profile.TutorialSkipAll;
            for (int i = 0; i < _steps.Length; i++)
            {
                if (IsSkippedAll || IsRemembered(_steps[i]))
                {
                    _states[i] = TutorialStepState.Done;
                }
            }
        }

        public string LevelId { get; }

        /// <summary>Кількість кроків, що діють на цьому рівні.</summary>
        public int StepCount => _steps.Length;

        /// <summary>Крок, який зараз показано; null — підказки немає.</summary>
        public TutorialStep Current => _active < 0 ? null : _steps[_active];

        /// <summary>Час показу поточного кроку, мс часу зміни.</summary>
        public long CurrentShownAtMs => _shownAtMs;

        /// <summary>Усе навчання вимкнено («пропустити все» зараз або раніше).</summary>
        public bool IsSkippedAll { get; private set; }

        /// <summary>Профіль змінився (крок завершено чи пропущено) і потребує збереження. Скидається <see cref="ClearDirty"/>.</summary>
        public bool Dirty { get; private set; }

        /// <summary>Поточний крок можна пропустити кнопкою.</summary>
        public bool CanSkipStep => _active >= 0 && _steps[_active].Skippable && _config.AllowSkipStep;

        /// <summary>«Пропустити все» доступне, поки показано крок.</summary>
        public bool CanSkipAll => _active >= 0 && _config.AllowSkipAll;

        public TutorialStep GetStep(int index)
        {
            return _steps[index];
        }

        public TutorialStepState GetState(string stepId)
        {
            for (int i = 0; i < _steps.Length; i++)
            {
                if (string.Equals(_steps[i].Id, stepId, StringComparison.Ordinal))
                {
                    return _states[i];
                }
            }

            throw new ArgumentException("Немає кроку " + stepId + " на рівні " + LevelId, nameof(stepId));
        }

        public void ClearDirty()
        {
            Dirty = false;
        }

        /// <summary>Подія зміни: може завершити поточний крок, позначити кроки до показу й показати наступний.</summary>
        public void Handle(TutorialEvent e, long nowMs)
        {
            if (IsSkippedAll || e.Kind == TutorialEventKind.None)
            {
                return;
            }

            for (int i = 0; i < _steps.Length; i++)
            {
                TutorialStepState state = _states[i];
                TutorialTrigger done = _steps[i].CompleteWhen;
                if ((state == TutorialStepState.Active || state == TutorialStepState.Armed) && done != null && done.Matches(e))
                {
                    Finish(i, nowMs);
                }
            }

            for (int i = 0; i < _steps.Length; i++)
            {
                if (_states[i] == TutorialStepState.Pending && _steps[i].ShowWhen.Matches(e) && PrerequisiteDone(_steps[i]))
                {
                    _states[i] = TutorialStepState.Armed;
                }
            }

            TryShow(nowMs);
        }

        /// <summary>Хід часу: автоприховування поточного кроку й показ наступного, коли мине пауза між підказками.</summary>
        public void Update(long nowMs)
        {
            if (IsSkippedAll)
            {
                return;
            }

            if (_active >= 0)
            {
                int hideMs = _steps[_active].AutoHideMs;
                if (hideMs > 0 && nowMs - _shownAtMs >= hideMs)
                {
                    Finish(_active, nowMs);
                }
            }

            TryShow(nowMs);
        }

        /// <summary>«Пропустити крок» (кнопка). false, якщо кроку немає або його не можна пропустити.</summary>
        public bool SkipStep(long nowMs)
        {
            if (!CanSkipStep)
            {
                return false;
            }

            Finish(_active, nowMs);
            return true;
        }

        /// <summary>
        /// «Пропустити все». Усі кроки цього рівня стають завершеними й записуються як побачені; якщо
        /// rememberSkipAll — у профілі лишається прапорець для всіх наступних рівнів.
        /// </summary>
        public bool SkipAll(long nowMs)
        {
            if (!_config.AllowSkipAll || IsSkippedAll)
            {
                return false;
            }

            for (int i = 0; i < _steps.Length; i++)
            {
                _states[i] = TutorialStepState.Done;
                Remember(_steps[i]);
            }

            _active = -1;
            _lastEndMs = nowMs;
            IsSkippedAll = true;
            if (_config.RememberSkipAll)
            {
                _profile.TutorialSkipAll = true;
            }

            Dirty = true;
            return true;
        }

        private bool IsRemembered(TutorialStep step)
        {
            bool seen = _profile.TutorialSeen.Contains(step.Id);
            return seen && (_config.OncePerProfile || step.ShowWhen.FirstTimeInProfile);
        }

        private void Remember(TutorialStep step)
        {
            if (_profile.TutorialSeen.Add(step.Id))
            {
                Dirty = true;
            }
        }

        private bool PrerequisiteDone(TutorialStep step)
        {
            string after = step.ShowWhen.AfterStep;
            if (after == null)
            {
                return true;
            }

            for (int i = 0; i < _steps.Length; i++)
            {
                if (string.Equals(_steps[i].Id, after, StringComparison.Ordinal))
                {
                    return _states[i] == TutorialStepState.Done;
                }
            }

            // Крок, на який є посилання, діє на іншому рівні: тут його немає, тож умова вважається виконаною.
            return true;
        }

        private void Finish(int index, long nowMs)
        {
            bool wasActive = _states[index] == TutorialStepState.Active;
            _states[index] = TutorialStepState.Done;
            Remember(_steps[index]);
            if (wasActive)
            {
                _active = -1;
                _lastEndMs = nowMs;
            }
        }

        private void TryShow(long nowMs)
        {
            if (_active >= 0 || IsSkippedAll)
            {
                return;
            }

            if (_lastEndMs != NoTime && nowMs - _lastEndMs < _config.MinGapMs)
            {
                return;
            }

            for (int i = 0; i < _steps.Length; i++)
            {
                if (_states[i] == TutorialStepState.Armed)
                {
                    _states[i] = TutorialStepState.Active;
                    _active = i;
                    _shownAtMs = nowMs;
                    return;
                }
            }
        }
    }
}
