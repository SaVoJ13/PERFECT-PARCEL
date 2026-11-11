using System;
using System.IO;
using KebabStreet.Core;

namespace KebabStreet.Services
{
    /// <summary>
    /// Безпечне збереження і завантаження прогресу поверх ISaveStorage.
    /// Запис: тимчасовий файл → перевірка читанням → основний стає резервним → тимчасовий стає основним.
    /// У будь-якій точці збою на носії лишається придатний основний або резервний файл.
    /// Завантаження: основний → резервний → чистий профіль з явною ознакою скидання.
    /// Пошкоджені файли не видаляються, а відкладаються з суфіксом .corrupt.
    /// Файли новішої версії гри не читаються і не перезаписуються.
    /// </summary>
    public sealed class SaveService
    {
        public const string PrimaryName = "save.json";
        public const string BackupName = "save.json.bak";
        public const string TempName = "save.json.tmp";
        public const string CorruptSuffix = ".corrupt";

        private readonly ISaveStorage _storage;
        private readonly IClock _clock;

        public SaveService(ISaveStorage storage, IClock clock)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            if (clock == null)
            {
                throw new ArgumentNullException(nameof(clock));
            }

            _storage = storage;
            _clock = clock;
        }

        private enum FileKind
        {
            Missing,
            Valid,
            Corrupt,
            Newer,
        }

        /// <summary>Завантажує прогрес. Нічого не записує; пошкоджені файли лише перейменовує.</summary>
        public SaveLoadResult Load()
        {
            SaveData data;
            bool migrated;
            int corrupt = 0;

            FileKind primary = Inspect(PrimaryName, out data, out migrated);
            if (primary == FileKind.Valid)
            {
                return new SaveLoadResult(data, SaveLoadSource.Primary, migrated, false, corrupt);
            }

            if (primary == FileKind.Newer)
            {
                return new SaveLoadResult(new SaveData(), SaveLoadSource.NewerVersion, false, false, corrupt);
            }

            if (primary == FileKind.Corrupt)
            {
                corrupt++;
                TryQuarantine(PrimaryName);
            }

            FileKind backup = Inspect(BackupName, out data, out migrated);
            if (backup == FileKind.Valid)
            {
                return new SaveLoadResult(data, SaveLoadSource.Backup, migrated, false, corrupt);
            }

            if (backup == FileKind.Newer)
            {
                return new SaveLoadResult(new SaveData(), SaveLoadSource.NewerVersion, false, false, corrupt);
            }

            if (backup == FileKind.Corrupt)
            {
                corrupt++;
                TryQuarantine(BackupName);
            }

            return new SaveLoadResult(new SaveData(), SaveLoadSource.NewProfile, false, corrupt > 0, corrupt);
        }

        /// <summary>
        /// Зберігає прогрес. Перед записом ставить позначки часу (LastSavedUnixSec — показ годинника,
        /// LastKnownUnixSec — не зменшується). Не кидає винятків вводу-виводу: повертає Failed.
        /// </summary>
        public SaveWriteResult Save(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                SaveData unused;
                bool unusedFlag;
                FileKind primary = Inspect(PrimaryName, out unused, out unusedFlag);
                if (primary == FileKind.Newer
                    || Inspect(BackupName, out unused, out unusedFlag) == FileKind.Newer)
                {
                    return SaveWriteResult.RejectedNewerSave;
                }

                long clockNow = _clock.UtcNowUnixSec;
                TimeGuard.Observe(data, clockNow);
                data.LastSavedUnixSec = clockNow;
                string text = SaveSerializer.Serialize(data);

                // 1. Нові дані — у тимчасовий файл; основний і резервний ще не зачеплено.
                _storage.WriteText(TempName, text);

                // 2. Перевірка: записане читається з носія, розбирається і дає той самий текст.
                SaveData written;
                if (Inspect(TempName, out written, out unusedFlag) != FileKind.Valid
                    || !string.Equals(SaveSerializer.Serialize(written), text, StringComparison.Ordinal))
                {
                    return SaveWriteResult.Failed;
                }

                // 3. Придатний основний файл стає резервною копією; пошкоджений відкладається,
                //    щоб не затерти ним справну резервну копію.
                if (primary == FileKind.Valid)
                {
                    DeleteIfExists(BackupName);
                    _storage.Move(PrimaryName, BackupName);
                }
                else if (primary == FileKind.Corrupt)
                {
                    Quarantine(PrimaryName);
                }

                // 4. Тимчасовий файл стає основним (основного в цю мить немає — перейменування без заміни).
                _storage.Move(TempName, PrimaryName);
                return SaveWriteResult.Saved;
            }
            catch (IOException)
            {
                return SaveWriteResult.Failed;
            }
            catch (UnauthorizedAccessException)
            {
                return SaveWriteResult.Failed;
            }
        }

        /// <summary>Визначає стан файла. Будь-яка помилка читання чи розбору означає «непридатний».</summary>
        private FileKind Inspect(string name, out SaveData data, out bool migrated)
        {
            data = null;
            migrated = false;
            try
            {
                if (!_storage.Exists(name))
                {
                    return FileKind.Missing;
                }

                data = SaveSerializer.Deserialize(_storage.ReadText(name), out migrated);
                return FileKind.Valid;
            }
            catch (SaveVersionTooNewException)
            {
                return FileKind.Newer;
            }
            catch (Exception)
            {
                data = null;
                migrated = false;
                return FileKind.Corrupt;
            }
        }

        private void Quarantine(string name)
        {
            DeleteIfExists(name + CorruptSuffix);
            _storage.Move(name, name + CorruptSuffix);
        }

        private void TryQuarantine(string name)
        {
            try
            {
                Quarantine(name);
            }
            catch (IOException)
            {
                // Відкласти не вдалося: файл лишається на місці, наступний запис спробує ще раз.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void DeleteIfExists(string name)
        {
            if (_storage.Exists(name))
            {
                _storage.Delete(name);
            }
        }
    }
}
