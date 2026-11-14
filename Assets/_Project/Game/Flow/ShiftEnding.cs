using System;
using KebabStreet.Core;

namespace KebabStreet.Game
{
    /// <summary>
    /// Запис у профіль наприкінці зміни та під час виходу (S1-08, S1-11): підсумок зміни (<see cref="ShiftCompletion"/>)
    /// і стан навчання (<see cref="TutorialModel.Dirty"/>) зберігаються одним файлом профілю. Без UnityEngine, тож це
    /// перевіряє тест, а контролер лише передає сюди події. Повідомлення про невдале збереження повертається
    /// для показу на екрані результату (BUG-043).
    /// </summary>
    public sealed class ShiftEnding
    {
        private readonly ShiftCompletion _completion;
        private readonly ProfileSession _profile;
        private readonly TutorialModel _tutorial;

        /// <param name="tutorial">Модель навчання або null, якщо навчання не підключено.</param>
        public ShiftEnding(ShiftCompletion completion, ProfileSession profile, TutorialModel tutorial)
        {
            _completion = completion ?? throw new ArgumentNullException(nameof(completion));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _tutorial = tutorial;
        }

        /// <summary>
        /// Зміна завершилась: записує підсумок і, якщо треба, стан навчання. Перемога вже зберігає профіль разом зі
        /// станом навчання; після поразки зберігається лише він. Повертає повідомлення для гравця (None — усе гаразд).
        /// </summary>
        public SaveNotice OnFinished(ShiftResult result)
        {
            _completion.Apply(result);
            SaveNotice notice = _completion.Notice;
            if (_tutorial != null && _tutorial.Dirty)
            {
                if (!result.Won)
                {
                    notice = SaveTutorialIfDirty();
                }
                else if (notice == SaveNotice.None)
                {
                    _tutorial.ClearDirty();
                }
            }

            return notice;
        }

        /// <summary>Зберігає профіль, якщо навчання щось у ньому змінило (крок побачено чи пропущено). None, якщо зберігати не було чого.</summary>
        public SaveNotice SaveTutorialIfDirty()
        {
            if (_tutorial == null || !_tutorial.Dirty)
            {
                return SaveNotice.None;
            }

            SaveNotice notice = _profile.Save();
            if (notice == SaveNotice.None)
            {
                _tutorial.ClearDirty();
            }

            return notice;
        }
    }
}
