namespace KebabStreet.Services
{
    /// <summary>
    /// Сховище іменованих текстових файлів збереження (основний, резервний, тимчасовий).
    /// Лише прості операції: порядок безпечного запису і відновлення визначає SaveService.
    /// Помилки вводу-виводу — винятки System.IO.IOException або UnauthorizedAccessException.
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>Чи існує файл.</summary>
        bool Exists(string name);

        /// <summary>Увесь текст файла; файла немає — виняток.</summary>
        string ReadText(string name);

        /// <summary>Створює або повністю замінює файл і скидає дані на носій.</summary>
        void WriteText(string name, string text);

        /// <summary>Перейменовує файл. Файла призначення не повинно існувати.</summary>
        void Move(string fromName, string toName);

        /// <summary>Видаляє файл.</summary>
        void Delete(string name);
    }
}
