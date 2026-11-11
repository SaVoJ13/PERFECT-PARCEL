using System;
using KebabStreet.Core;
using KebabStreet.Services;

namespace KebabStreet.Game
{
    /// <summary>
    /// Профіль гравця на час роботи гри. Гарантує порядок Load до першого Save; чистий профіль після
    /// Unavailable/NewerVersion не пишеться (це додатково гарантує сам SaveService, BUG-025).
    /// </summary>
    public sealed class ProfileSession
    {
        private readonly SaveService _service;
        private SaveLoadResult _loaded;

        public ProfileSession(SaveService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public bool IsLoaded => _loaded != null;

        /// <summary>Дані профілю; доступні лише після Load.</summary>
        public SaveData Data
        {
            get
            {
                RequireLoaded();
                return _loaded.Data;
            }
        }

        /// <summary>Останнє повідомлення для гравця (завантаження або запис).</summary>
        public SaveNotice LastNotice { get; private set; }

        public SaveLoadResult Load()
        {
            _loaded = _service.Load();
            LastNotice = SaveNoticePolicy.FromLoad(_loaded);
            return _loaded;
        }

        /// <summary>Зберігає профіль. До Load - InvalidOperationException. Повертає повідомлення (None = збережено).</summary>
        public SaveNotice Save()
        {
            RequireLoaded();
            LastNotice = SaveNoticePolicy.FromWrite(_service.Save(_loaded.Data));
            return LastNotice;
        }

        private void RequireLoaded()
        {
            if (_loaded == null)
            {
                throw new InvalidOperationException("Профіль не завантажено: Load має бути викликано до Save.");
            }
        }
    }
}
