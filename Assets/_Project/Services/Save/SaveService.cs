using System;
using System.Globalization;
using System.IO;
using System.Text;
using KebabStreet.Core;

namespace KebabStreet.Services
{
    /// <summary>
    /// Безпечне збереження і завантаження прогресу поверх ISaveStorage.
    /// Запис: тимчасовий файл → перевірка читанням → основний стає резервним → тимчасовий стає основним.
    /// У будь-якій точці збою на носії лишається придатний основний або резервний файл.
    /// Завантаження: основний → резервний → чистий профіль з явною ознакою скидання.
    /// Файл із пошкодженим ВМІСТОМ не видаляється, а відкладається з суфіксом .corrupt.
    /// Файл, який не вдалося ПРОЧИТАТИ (помилка сховища), пошкодженим не вважається: його не чіпають,
    /// завантаження повідомляє Unavailable, запис відмовляє — доки доступ не відновиться.
    /// Файли новішої версії гри не читаються і не перезаписуються.
    /// </summary>
    public sealed class SaveService
    {
        public const string PrimaryName = "save.json";
        public const string BackupName = "save.json.bak";
        public const string TempName = "save.json.tmp";
        public const string CorruptSuffix = ".corrupt";

        /// <summary>
        /// Скільки відкладених копій одного файла зберігається: «.corrupt», «.corrupt.2», … Перші копії
        /// не затираються ніколи; коли всі місця зайняті, новий пошкоджений файл замінює лише останнє.
        /// </summary>
        public const int MaxCorruptCopies = 3;

        private readonly ISaveStorage _storage;
        private readonly IClock _clock;

        // Результат останнього Load: Unavailable або NewerVersion. Поки він не змінився успішним Load,
        // Save нічого не пише, бо гра тримає чистий профіль, а не справжній прогрес (BUG-025).
        private SaveLoadSource? _blockedBy;

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
            Unavailable,
        }

        /// <summary>Ім'я відкладеної копії: номер 1 — «ім'я.corrupt», далі «ім'я.corrupt.2» і так далі.</summary>
        public static string CorruptName(string name, int copyNumber)
        {
            string first = name + CorruptSuffix;
            return copyNumber <= 1 ? first : first + "." + copyNumber.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Завантажує прогрес. Нічого не записує; файли з пошкодженим вмістом лише перейменовує.
        /// Якщо хоч один потрібний файл не читається, нічого не перейменовується.
        /// </summary>
        public SaveLoadResult Load()
        {
            SaveLoadResult result = LoadCore();
            bool blocked = result.Source == SaveLoadSource.Unavailable || result.Source == SaveLoadSource.NewerVersion;
            _blockedBy = blocked ? (SaveLoadSource?)result.Source : null;
            return result;
        }

        private SaveLoadResult LoadCore()
        {
            SaveData primaryData;
            SaveData backupData;
            bool primaryMigrated;
            bool backupMigrated;

            FileKind primary = Inspect(PrimaryName, out primaryData, out primaryMigrated);
            if (primary == FileKind.Unavailable)
            {
                // Резервну копію не беремо: це був би мовчазний відкат, а наступний запис затер би новіший основний файл.
                return new SaveLoadResult(new SaveData(), SaveLoadSource.Unavailable, false, false, 0, false);
            }

            if (primary == FileKind.Newer)
            {
                return new SaveLoadResult(new SaveData(), SaveLoadSource.NewerVersion, false, false, 0, false);
            }

            FileKind backup = Inspect(BackupName, out backupData, out backupMigrated);
            if (primary == FileKind.Valid)
            {
                bool canSave = backup != FileKind.Newer && backup != FileKind.Unavailable;
                int corruptBackup = 0;
                if (backup == FileKind.Corrupt)
                {
                    // BUG-026: пошкоджену резервну копію не видаляємо мовчки, а відкладаємо й повідомляємо.
                    corruptBackup = 1;
                    TryQuarantine(BackupName);
                }

                return new SaveLoadResult(primaryData, SaveLoadSource.Primary, primaryMigrated, false, corruptBackup, canSave);
            }

            if (backup == FileKind.Unavailable)
            {
                // Основного немає або він пошкоджений, а резервний не читається: це не «прогрес утрачено».
                return new SaveLoadResult(new SaveData(), SaveLoadSource.Unavailable, false, false, 0, false);
            }

            int corrupt = 0;
            if (primary == FileKind.Corrupt)
            {
                corrupt++;
                TryQuarantine(PrimaryName);
            }

            if (backup == FileKind.Valid)
            {
                return new SaveLoadResult(backupData, SaveLoadSource.Backup, backupMigrated, false, corrupt, true);
            }

            if (backup == FileKind.Newer)
            {
                return new SaveLoadResult(new SaveData(), SaveLoadSource.NewerVersion, false, false, corrupt, false);
            }

            if (backup == FileKind.Corrupt)
            {
                corrupt++;
                TryQuarantine(BackupName);
            }

            return new SaveLoadResult(new SaveData(), SaveLoadSource.NewProfile, false, corrupt > 0, corrupt, true);
        }

        /// <summary>
        /// Зберігає прогрес. Перед записом ставить позначки часу (LastSavedUnixSec — показ годинника, не менше 0;
        /// LastKnownUnixSec — не зменшується). Не кидає винятків вводу-виводу: повертає Failed.
        /// </summary>
        public SaveWriteResult Save(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (_blockedBy == SaveLoadSource.NewerVersion)
            {
                return SaveWriteResult.RejectedNewerSave;
            }

            if (_blockedBy == SaveLoadSource.Unavailable)
            {
                return SaveWriteResult.RejectedAfterUnavailableLoad;
            }

            try
            {
                SaveData unused;
                bool unusedFlag;
                FileKind primary = Inspect(PrimaryName, out unused, out unusedFlag);
                FileKind backup = Inspect(BackupName, out unused, out unusedFlag);
                if (primary == FileKind.Newer || backup == FileKind.Newer)
                {
                    return SaveWriteResult.RejectedNewerSave;
                }

                // Файл, який не вдалося прочитати, може бути справним: його не можна ні ротувати, ні видаляти.
                if (primary == FileKind.Unavailable || backup == FileKind.Unavailable)
                {
                    return SaveWriteResult.Failed;
                }

                long clockNow = _clock.UtcNowUnixSec;
                TimeGuard.Observe(data, clockNow);
                data.LastSavedUnixSec = Math.Max(0, clockNow);
                string text = SaveSerializer.Serialize(data);
                if (!CanBeLoadedBack(text))
                {
                    return SaveWriteResult.RejectedInvalidData;
                }

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
                    if (backup == FileKind.Corrupt)
                    {
                        Quarantine(BackupName);
                    }
                    else
                    {
                        DeleteIfExists(BackupName);
                    }

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

        /// <summary>
        /// Визначає стан файла. Помилка сховища (існування, читання) — Unavailable; пошкодженим (Corrupt)
        /// файл вважається лише тоді, коли його текст прочитано, але він не розбирається.
        /// </summary>
        private FileKind Inspect(string name, out SaveData data, out bool migrated)
        {
            data = null;
            migrated = false;
            string text;
            try
            {
                if (!_storage.Exists(name))
                {
                    return FileKind.Missing;
                }

                text = _storage.ReadText(name);
            }
            catch (Exception)
            {
                return FileKind.Unavailable;
            }

            try
            {
                data = SaveSerializer.Deserialize(text, out migrated);
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

        /// <summary>Чи прочитається цей текст назад: відповідає схемі й без втрат кодується в UTF-8.</summary>
        private static bool CanBeLoadedBack(string text)
        {
            try
            {
                bool migrated;
                SaveData check = SaveSerializer.Deserialize(text, out migrated);
                new UTF8Encoding(false, true).GetByteCount(text);
                return string.Equals(SaveSerializer.Serialize(check), text, StringComparison.Ordinal);
            }
            catch (SaveFormatException)
            {
                return false;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        /// <summary>Відкладає файл у перше вільне місце; раніше відкладені копії (крім останнього місця) не затираються.</summary>
        private void Quarantine(string name)
        {
            int copyNumber = 1;
            while (copyNumber < MaxCorruptCopies && _storage.Exists(CorruptName(name, copyNumber)))
            {
                copyNumber++;
            }

            string target = CorruptName(name, copyNumber);
            DeleteIfExists(target);
            _storage.Move(name, target);
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
