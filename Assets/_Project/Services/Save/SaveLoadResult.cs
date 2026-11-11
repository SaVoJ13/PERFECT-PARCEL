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

        /// <summary>
        /// Файл збереження існує, але сховище не дало його прочитати (помилка вводу-виводу, немає доступу).
        /// Це не втрата прогресу: файли не зачеплено, запис заборонено; завантаження варто повторити пізніше.
        /// </summary>
        Unavailable,
    }

    /// <summary>Підсумок запису.</summary>
    public enum SaveWriteResult
    {
        /// <summary>Нові дані записано й перевірено.</summary>
        Saved,

        /// <summary>На носії збереження новішої версії гри: нічого не змінено.</summary>
        RejectedNewerSave,

        /// <summary>
        /// Запис не вдався через сховище (помилка вводу-виводу або наявний файл не читається);
        /// попереднє збереження не зачеплено, спробу можна повторити.
        /// </summary>
        Failed,

        /// <summary>
        /// Самі дані не можна зберегти: після запису вони не прочиталися б назад (значення поза схемою,
        /// рядок, який не кодується в UTF-8). Повторювати без зміни даних марно; на носії нічого не змінено.
        /// </summary>
        RejectedInvalidData,

        /// <summary>
        /// Останній Load дав Unavailable: гра тримає чистий профіль, а не справжній прогрес, тому запис заборонено
        /// (навіть якщо доступ уже відновився). Потрібен новий успішний Load; файли не зачеплено.
        /// </summary>
        RejectedAfterUnavailableLoad,
    }

    /// <summary>Явний підсумок завантаження: прогрес ніколи не скидається мовчки.</summary>
    public sealed class SaveLoadResult
    {
        public SaveLoadResult(
            SaveData data,
            SaveLoadSource source,
            bool migrated,
            bool progressReset,
            int corruptFilesFound,
            bool canSave)
        {
            CanSave = canSave;
            Data = data;
            Source = source;
            Migrated = migrated;
            ProgressReset = progressReset;
            CorruptFilesFound = corruptFilesFound;
        }

        /// <summary>Дані для гри (ніколи не null; для NewProfile, NewerVersion і Unavailable — чистий профіль).</summary>
        public SaveData Data { get; }

        /// <summary>Звідки взято дані.</summary>
        public SaveLoadSource Source { get; }

        /// <summary>Дані були старої версії схеми і пройшли міграцію.</summary>
        public bool Migrated { get; }

        /// <summary>Прогрес утрачено: збереження існувало, але жоден файл не придатний. Гра має повідомити гравця.</summary>
        public bool ProgressReset { get; }

        /// <summary>Скільки пошкоджених файлів знайдено й відкладено вбік (0–2).</summary>
        public int CorruptFilesFound { get; }

        /// <summary>
        /// Чи прийняв би Save запис у стані, який побачило завантаження. false — якщо основний або резервний
        /// файл від новішої версії гри чи не читається (навіть коли дані взято з основного файла).
        /// Це знімок на мить завантаження: Save щоразу перевіряє файли заново.
        /// </summary>
        public bool CanSave { get; }
    }
}
