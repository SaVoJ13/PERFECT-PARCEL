namespace KebabStreet.Services
{
    /// <summary>Результат покупки. Нараховувати товар можна лише при <see cref="Success"/>, один раз на транзакцію.</summary>
    public enum PurchaseResult
    {
        /// <summary>Сервіс покупок або продукт недоступний. Нічого не нараховувати.</summary>
        Unavailable = 0,

        /// <summary>Гравець скасував.</summary>
        Cancelled = 1,

        /// <summary>Платіж відкладено (очікує підтвердження). Не нараховувати, доки не прийде Success.</summary>
        Pending = 2,

        /// <summary>Разовий продукт уже куплено.</summary>
        AlreadyOwned = 3,

        /// <summary>Помилка.</summary>
        Failed = 4,

        /// <summary>Куплено й підтверджено.</summary>
        Success = 5,
    }

    /// <summary>Результат відновлення разових покупок.</summary>
    public enum RestoreResult
    {
        Unavailable = 0,
        Failed = 1,

        /// <summary>Запит виконано; відновлені покупки надійшли як <see cref="PurchaseResult.Success"/> по продуктах.</summary>
        Completed = 2,
    }
}
