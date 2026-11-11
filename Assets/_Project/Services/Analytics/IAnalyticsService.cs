using System.Collections.Generic;

namespace KebabStreet.Services
{
    /// <summary>
    /// Контракт аналітики без прив'язки до SDK. Персональних даних у контракті немає й бути не повинно:
    /// назви подій і параметри — лише ігрові (рівень, місце реклами тощо), див. PRIVACY_CHECKLIST.md.
    /// </summary>
    public interface IAnalyticsService
    {
        /// <summary>Чи існує справжній сервіс (false для заглушки Null).</summary>
        bool IsAvailable { get; }

        /// <summary>Записує подію. <paramref name="parameters"/> може бути null.</summary>
        void LogEvent(string name, IReadOnlyDictionary<string, string> parameters);

        void SetUserProperty(string name, string value);
    }
}
