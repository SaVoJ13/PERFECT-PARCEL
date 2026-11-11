using KebabStreet.Core;

namespace KebabStreet.Services
{
    /// <summary>Звідки взято дані під час завантаження.</summary>
    public enum SaveLoadSource
    {
        /// <summary>Основний файл.</summary>
        Primary,

        /// <summary>Резервна копія (основний файл відсутній або пошкоджений).</summary>
        Backup,

        /// <summary>Чистий профіль: придатного збереження немає (перший запуск або скидання — див. ProgressReset).</summary>
        NewProfile,

        /// <summary>Збереження створене новішою версією гри: дані не прочитано, запис заборонено.</summary>
        NewerVersion,
    }

    /// <summary>Підсумок запису.</summary>
    public enum SaveWriteResult
    {
        /// <summary>Нові дані записано й перевірено.</summary>
        Saved,

        /// <summary>На носії збереження новішої версії гри: нічого не змінено.</summary>
        RejectedNewerSave,

        /// <summary>Запис не вдався; попереднє збереження лишилося придатним.</summary>
        Failed,
    }

    /// <summary>Явний підсумок завантаження: прогрес ніколи не скидається мовчки.</summary>
    public sealed class SaveLoadResult
    {
        public SaveLoadResult(
            SaveData data, SaveLoadSource source, bool migrated, bool progressReset, int corruptFilesFound)
        {
            Data = data;
            Source = source;
            Migrated = migrated;
            ProgressReset = progressReset;
            CorruptFilesFound = corruptFilesFound;
        }

        /// <summary>Дані для гри (ніколи не null; для NewProfile і NewerVersion — чистий профіль).</summary>
        public SaveData Data { get; }

        /// <summary>Звідки взято дані.</summary>
        public SaveLoadSource Source { get; }

        /// <summary>Дані були старої версії схеми і пройшли міграцію.</summary>
        public bool Migrated { get; }

        /// <summary>Прогрес утрачено: збереження існувало, але жоден файл не придатний. Гра має повідомити гравця.</summary>
        public bool ProgressReset { get; }

        /// <summary>Скільки пошкоджених файлів знайдено й відкладено вбік (0–2).</summary>
        public int CorruptFilesFound { get; }

        /// <summary>Чи можна зберігати поверх наявних файлів (ні — якщо вони від новішої версії гри).</summary>
        public bool CanSave
        {
            get { return Source != SaveLoadSource.NewerVersion; }
        }
    }
}
